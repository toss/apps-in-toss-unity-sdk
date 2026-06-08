// -----------------------------------------------------------------------
// <copyright file="AIT.StreamingAudio.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Streaming Audio (runtime rehydrator)
// </copyright>
// -----------------------------------------------------------------------
//
// 빌드 단계에서 초기 .data 밖(StreamingAssets)으로 외부화된 대용량 AudioClip을,
// 게임이 interactive 된 이후 비동기로 스트리밍 로드하여 원래 재생을 복원한다.
// 씬/직렬화 배열을 변형하지 않으므로 코드 구동 재생(예: source.clip = clips[i]; Play())
// 패턴에도 강건하다.
//
// 동작:
//   1) [RuntimeInitializeOnLoadMethod] 로 자동 부팅 (게임 코드 수정 불필요).
//   2) StreamingAssets/ait-stream-audio/manifest.json 로드 → name→entry 맵.
//   3) 주기적으로(throttle) 살아있는 AudioSource 스캔. clip이 "무음 스텁"
//      (manifest에 이름 존재 && clip.length < STUB_MAX_SEC)이면 실 오디오를
//      UnityWebRequestMultimedia로 async 로드 → clip 핫스왑(loop/재생위치/isPlaying 보존).
//   4) 로드된 실 클립은 캐시 → 트랙 전환/재요청 시 재다운로드 없음.
//
// TTI 영향: 스왑은 interactive(=TTI 측정 시점) 이후에 일어나므로 TTI에 영향 없음.
// 초기 .data 에서 오디오 바이트가 빠진 만큼 초기 다운로드/TTI가 줄어드는 것이 본질 효과.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Scripting;

namespace AppsInToss
{
    /// <summary>
    /// 외부화된 오디오를 런타임에 스트리밍으로 복원하는 SDK 컴포넌트.
    /// 빌드 단계(<c>AITAudioStreamingProcessor</c>)가 매니페스트와 StreamingAssets 사본을 만들어 두면,
    /// 이 컴포넌트가 자동 부팅되어 무음 스텁 AudioSource를 실 오디오로 핫스왑한다.
    /// 매니페스트가 없는 빌드(기능 미사용)에서는 조용히 no-op 후 자체 종료한다.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class AITStreamingAudio : MonoBehaviour
    {
        /// <summary>스텁 무음 클립 길이(초)보다 큰 클립은 "실 클립"으로 간주 → 재스왑 안 함.</summary>
        private const float StubMaxSeconds = 0.5f;

        /// <summary>AudioSource 스캔 주기(초). 너무 잦으면 GC/CPU, 너무 느리면 BGM 시작 지연.</summary>
        private const float ScanIntervalSeconds = 0.2f;

        private const string ManifestRelativePath = "ait-stream-audio/manifest.json";
        private const string StreamDirRelativePath = "ait-stream-audio/";

        [System.Serializable]
        private struct Entry
        {
            public string guid;
            public string name;
            public string file;
            public float length;
        }

        [System.Serializable]
        private struct Manifest
        {
            public Entry[] entries;
        }

        private readonly Dictionary<string, Entry> byName = new Dictionary<string, Entry>();
        private readonly Dictionary<string, AudioClip> loaded = new Dictionary<string, AudioClip>();
        private readonly HashSet<string> loading = new HashSet<string>();
        private bool ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        [Preserve]
        private static void Bootstrap()
        {
            // SDK가 오디오 외부화를 수행한 빌드에서만 매니페스트가 존재한다.
            // 부팅 후 매니페스트가 없으면 Run() 코루틴이 스스로 종료한다.
            var go = new GameObject("[AIT] StreamingAudio");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<AITStreamingAudio>();
        }

        private void Start() => StartCoroutine(Run());

        private IEnumerator Run()
        {
            yield return LoadManifest();
            if (!ready || byName.Count == 0)
            {
                // 외부화된 오디오가 없는 빌드 → 워처 종료(자원 회수).
                Destroy(gameObject);
                yield break;
            }

            var wait = new WaitForSeconds(ScanIntervalSeconds);
            while (true)
            {
                ScanAndSwap();
                yield return wait;
            }
        }

        private IEnumerator LoadManifest()
        {
            string url = ResolveStreamingUrl(ManifestRelativePath);
            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                if (!IsSuccess(req))
                {
                    // 매니페스트 없음 = 이 빌드는 오디오 외부화를 안 함. 정상 경로(no-op).
                    yield break;
                }

                try
                {
                    var m = JsonUtility.FromJson<Manifest>(req.downloadHandler.text);
                    if (m.entries != null)
                    {
                        foreach (var e in m.entries)
                        {
                            if (!string.IsNullOrEmpty(e.name))
                            {
                                byName[e.name] = e;
                            }
                        }
                    }

                    ready = true;
                    Debug.Log($"[AIT-StreamingAudio] 매니페스트 로드: {byName.Count}개 외부화 오디오");
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[AIT-StreamingAudio] 매니페스트 파싱 실패: {ex.Message}");
                }
            }
        }

        private void ScanAndSwap()
        {
            // 활성 AudioSource 전수 스캔. (BGM은 보통 소수 — 비용 작음.)
#if UNITY_2023_1_OR_NEWER
            var sources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
#else
            var sources = Object.FindObjectsOfType<AudioSource>();
#endif
            foreach (var src in sources)
            {
                var clip = src.clip;
                if (clip == null)
                {
                    continue;
                }

                if (!IsStubLength(clip.length))
                {
                    continue; // 이미 실 클립
                }

                if (!byName.TryGetValue(clip.name, out var entry))
                {
                    continue; // 외부화 대상 아님
                }

                if (loaded.TryGetValue(entry.name, out var real) && real != null)
                {
                    HotSwap(src, real);
                    continue;
                }

                if (loading.Add(entry.name))
                {
                    StartCoroutine(LoadClip(entry, src));
                }
            }
        }

        private IEnumerator LoadClip(Entry entry, AudioSource firstRequester)
        {
            string url = ResolveStreamingUrl(StreamDirRelativePath + entry.file);
            var type = GuessAudioType(entry.file);
            using (var req = UnityWebRequestMultimedia.GetAudioClip(url, type))
            {
                // 스트리밍 디코드(메모리 절약). 길이가 길어도 점진 재생.
                ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio = true;
                yield return req.SendWebRequest();
                if (!IsSuccess(req))
                {
                    Debug.LogWarning($"[AIT-StreamingAudio] 로드 실패 {entry.file}: {req.error}");
                    loading.Remove(entry.name);
                    yield break;
                }

                var real = DownloadHandlerAudioClip.GetContent(req);
                if (real != null)
                {
                    real.name = entry.name; // 이름 보존(이후 동일 클립 식별)
                    loaded[entry.name] = real;
                    if (firstRequester != null && firstRequester.clip != null
                        && firstRequester.clip.name == entry.name && IsStubLength(firstRequester.clip.length))
                    {
                        HotSwap(firstRequester, real);
                    }
                }

                loading.Remove(entry.name);
            }
        }

        /// <summary>클립 길이가 무음 스텁 임계값보다 짧으면 재수화 대상(=스텁)으로 판정. (테스트 가능한 순수 함수)</summary>
        internal static bool IsStubLength(float clipLength) => clipLength < StubMaxSeconds;

        /// <summary>무음 스텁이 재생 중이던 AudioSource를 실 클립으로 교체하고 재생 상태를 보존.</summary>
        internal static void HotSwap(AudioSource src, AudioClip real)
        {
            if (src.clip == real)
            {
                return;
            }

            bool wasPlaying = src.isPlaying;
            float t = src.time; // 스텁(짧은 loop)에서의 위치 — 보통 0 근처
            src.clip = real;
            if (wasPlaying)
            {
                src.time = (t < real.length) ? t : 0f;
                src.Play();
            }
        }

        private static bool IsSuccess(UnityWebRequest req)
        {
#if UNITY_2020_2_OR_NEWER
            return req.result == UnityWebRequest.Result.Success;
#else
            return !req.isHttpError && !req.isNetworkError;
#endif
        }

        internal static AudioType GuessAudioType(string file)
        {
            string f = file.ToLowerInvariant();
            if (f.EndsWith(".mp3"))
            {
                return AudioType.MPEG;
            }

            if (f.EndsWith(".ogg"))
            {
                return AudioType.OGGVORBIS;
            }

            if (f.EndsWith(".wav"))
            {
                return AudioType.WAV;
            }

            if (f.EndsWith(".aiff") || f.EndsWith(".aif"))
            {
                return AudioType.AIFF;
            }

            return AudioType.UNKNOWN;
        }

        // WebGL: streamingAssetsPath는 상대/절대 URL. UnityWebRequest는 file:// 또는 http(s):// 모두 처리.
        private static string ResolveStreamingUrl(string rel)
        {
            return JoinUrl(Application.streamingAssetsPath, rel);
        }

        /// <summary>basePath와 상대 경로를 슬래시 중복 없이 결합. (테스트 가능한 순수 함수)</summary>
        internal static string JoinUrl(string basePath, string rel)
        {
            return basePath.EndsWith("/") ? basePath + rel : basePath + "/" + rel;
        }
    }
}
