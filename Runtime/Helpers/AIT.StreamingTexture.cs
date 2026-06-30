// -----------------------------------------------------------------------
// <copyright file="AIT.StreamingTexture.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Streaming Texture (runtime rehydrator)
// </copyright>
// -----------------------------------------------------------------------
//
// 빌드 단계에서 초기 .data 밖(StreamingAssets)으로 외부화된 비-부팅 대형 Texture2D를,
// 게임이 interactive 된(=첫 프레임 그려진) 이후 비동기로 스트리밍 로드하여 원래 픽셀을 복원한다.
// 오디오 스트리밍의 텍스처 버전이다.
//
// 오디오는 AudioSource.clip(가변 프로퍼티) 재할당으로 핫스왑하지만, 텍스처는 Sprite.texture 가
// read-only 라 참조 재할당이 불가하다. 대신 빌드 단계가 스텁을 "원본과 동일 차원"으로 만들어
// 두므로(Sprite rect/pivot/border 동일 bake), 런타임은 살아있는 공유 Texture2D 객체의 픽셀만
// LoadImage 로 제자리(in-place) 교체한다 — 이를 참조하는 모든 Sprite/Material 이 참조 재할당
// 없이 새 픽셀을 렌더한다.
//
// 동작:
//   1) [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] 로 자동 부팅 (게임 코드 수정 불필요).
//   2) StreamingAssets/ait-stream-texture/manifest.json 로드 → 외부화 엔트리 목록.
//   3) 주기적으로(throttle) 로드된 Texture2D 를 스캔. manifest 엔트리와 name+차원이 일치하는
//      "스텁" 텍스처를 찾으면 실 텍스처 바이트를 UnityWebRequest 로 async 로드 →
//      LoadImage 로 동일 객체에 in-place 복원. (maxConcurrent 로 동시 다운로드/디코드 제한.)
//   4) 모든 엔트리 복원 완료 시 워처를 종료(자원 회수). 매니페스트가 없는 빌드(기능 미사용)에서는
//      조용히 no-op 후 자체 종료한다.
//
// TTFF 영향: 복원은 interactive(=TTFF 측정 시점) 이후에 일어나므로 TTFF에 영향 없음.
// 초기 .data 에서 비-부팅 텍스처 바이트가 빠진 만큼 초기 다운로드/TTFF가 줄어드는 것이 본질 효과.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Scripting;

namespace AppsInToss
{
    /// <summary>
    /// 외부화된 텍스처를 런타임에 스트리밍으로 복원하는 SDK 컴포넌트.
    /// 빌드 단계(<c>AITLargeTextureExternalizer</c>)가 매니페스트와 StreamingAssets 사본을 만들어 두면,
    /// 이 컴포넌트가 자동 부팅되어 동일 차원 단색 스텁 텍스처의 픽셀을 실 텍스처로 in-place 복원한다.
    /// 매니페스트가 없는 빌드(기능 미사용)에서는 조용히 no-op 후 자체 종료한다.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class AITStreamingTexture : MonoBehaviour
    {
        /// <summary>스캔 주기(초). 너무 잦으면 FindObjectsOfTypeAll 비용, 너무 느리면 텍스처 복원 지연.</summary>
        private const float ScanIntervalSeconds = 0.25f;

        /// <summary>동시 스트리밍 다운로드/디코드 기본 상한(매니페스트에 값이 없을 때). LoadImage 가 메인스레드 디코드라 hitch 제한용.</summary>
        private const int DefaultMaxConcurrent = 3;

        private const string ManifestRelativePath = "ait-stream-texture/manifest.json";
        private const string StreamDirRelativePath = "ait-stream-texture/";

        [System.Serializable]
        private struct Entry
        {
            public string guid;
            public string name;
            public string file;
            public int width;
            public int height;
        }

        [System.Serializable]
        private struct Manifest
        {
            public int maxConcurrent;
            public Entry[] entries;
        }

        /// <summary>아직 복원되지 않은 엔트리(복원 성공 시 제거).</summary>
        private readonly List<Entry> pending = new List<Entry>();

        /// <summary>현재 다운로드/디코드 진행 중인 엔트리 guid (중복 로드 방지).</summary>
        private readonly HashSet<string> inflight = new HashSet<string>();

        /// <summary>이미 복원한 Texture2D 인스턴스 ID (동일 name+차원 중복 텍스처를 각기 다른 인스턴스에 매핑).</summary>
        private readonly HashSet<int> restoredInstanceIds = new HashSet<int>();

        private int maxConcurrent = DefaultMaxConcurrent;
        private int loadingCount;
        private bool ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        [Preserve]
        private static void Bootstrap()
        {
            // SDK가 텍스처 외부화를 수행한 빌드에서만 매니페스트가 존재한다.
            // 부팅 후 매니페스트가 없으면 Run() 코루틴이 스스로 종료한다.
            var go = new GameObject("[AIT] StreamingTexture");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<AITStreamingTexture>();
        }

        private void Start() => StartCoroutine(Run());

        private IEnumerator Run()
        {
            yield return LoadManifest();
            if (!ready || pending.Count == 0)
            {
                // 외부화된 텍스처가 없는 빌드 → 워처 종료(자원 회수).
                Destroy(gameObject);
                yield break;
            }

            var wait = new WaitForSeconds(ScanIntervalSeconds);
            while (pending.Count > 0)
            {
                ScanAndRestore();
                yield return wait;
            }

            // 모든 외부화 텍스처 복원 완료 → 더 스캔할 것이 없으므로 종료.
            Destroy(gameObject);
        }

        private IEnumerator LoadManifest()
        {
            string url = ResolveStreamingUrl(ManifestRelativePath);
            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                if (!IsSuccess(req))
                {
                    // 매니페스트 없음 = 이 빌드는 텍스처 외부화를 안 함. 정상 경로(no-op).
                    yield break;
                }

                try
                {
                    var m = JsonUtility.FromJson<Manifest>(req.downloadHandler.text);
                    if (m.maxConcurrent > 0)
                    {
                        maxConcurrent = m.maxConcurrent;
                    }

                    if (m.entries != null)
                    {
                        foreach (var e in m.entries)
                        {
                            if (!string.IsNullOrEmpty(e.name) && !string.IsNullOrEmpty(e.file) && e.width > 0 && e.height > 0)
                            {
                                pending.Add(e);
                            }
                        }
                    }

                    ready = true;
                    Debug.Log($"[AIT-StreamingTexture] 매니페스트 로드: {pending.Count}개 외부화 텍스처 (동시 {maxConcurrent})");
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[AIT-StreamingTexture] 매니페스트 파싱 실패: {ex.Message}");
                }
            }
        }

        private void ScanAndRestore()
        {
            if (loadingCount >= maxConcurrent)
            {
                return; // 동시 상한 — 다음 스캔에서 재시도
            }

            // 로드된 모든 Texture2D(에셋 포함). 스텁은 씬 컴포넌트가 아니므로 FindObjectsOfTypeAll 필요.
            var all = Resources.FindObjectsOfTypeAll<Texture2D>();
            foreach (var e in pending)
            {
                if (loadingCount >= maxConcurrent)
                {
                    break;
                }

                if (inflight.Contains(e.guid))
                {
                    continue;
                }

                var tex = FindStub(all, e);
                if (tex == null)
                {
                    continue; // 아직 로드 안 됨(해당 씬/프리팹 미로드) → 다음 스캔
                }

                inflight.Add(e.guid);
                restoredInstanceIds.Add(tex.GetInstanceID());
                loadingCount++;
                StartCoroutine(LoadAndApply(e, tex));
            }
        }

        /// <summary>manifest 엔트리와 name+차원이 일치하고 아직 복원하지 않은 Texture2D 인스턴스를 찾는다.</summary>
        private Texture2D FindStub(Texture2D[] all, Entry e)
        {
            foreach (var t in all)
            {
                if (t == null || t.width != e.width || t.height != e.height)
                {
                    continue;
                }

                if (t.name != e.name)
                {
                    continue;
                }

                if (restoredInstanceIds.Contains(t.GetInstanceID()))
                {
                    continue; // 동일 name+차원 중복 텍스처 — 이미 다른 엔트리가 이 인스턴스 복원
                }

                return t;
            }

            return null;
        }

        private IEnumerator LoadAndApply(Entry e, Texture2D tex)
        {
            string url = ResolveStreamingUrl(StreamDirRelativePath + e.file);
            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                loadingCount--;
                inflight.Remove(e.guid);

                if (!IsSuccess(req))
                {
                    // 실패 → 인스턴스 예약 해제 후 다음 스캔에서 재시도(다른 인스턴스 포함).
                    restoredInstanceIds.Remove(tex != null ? tex.GetInstanceID() : 0);
                    Debug.LogWarning($"[AIT-StreamingTexture] 로드 실패 {e.file}: {req.error}");
                    yield break;
                }

                if (tex == null)
                {
                    // 대상 텍스처가 그 사이 언로드됨 — 복원 불필요로 간주하고 pending 에서 제거.
                    pending.RemoveAll(x => x.guid == e.guid);
                    yield break;
                }

                bool applied = false;
                try
                {
                    // 동일 차원 스텁(readable+uncompressed)에 실 픽셀을 in-place 업로드.
                    // LoadImage 는 PNG/JPG 디코드 후 GPU 업로드까지 수행 → 참조하는 Sprite/Material 자동 갱신.
                    applied = tex.LoadImage(req.downloadHandler.data, false);
                    if (applied && (tex.width != e.width || tex.height != e.height))
                    {
                        // 스텁 차원과 원본 차원 불일치(예: crunch maxTextureSize 캡 + D1 동시 사용).
                        // Sprite rect 가 스텁 차원으로 bake 됐다면 UV 가 어긋날 수 있음 — 경고만(복원은 유지).
                        Debug.LogWarning($"[AIT-StreamingTexture] 차원 불일치 {e.name}: 스텁 {e.width}x{e.height} → 원본 {tex.width}x{tex.height}");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[AIT-StreamingTexture] 복원 예외 {e.name}: {ex.Message}");
                }

                if (applied)
                {
                    pending.RemoveAll(x => x.guid == e.guid);
                }
                else
                {
                    // 적용 실패 → 인스턴스 예약 해제(다음 스캔 재시도).
                    restoredInstanceIds.Remove(tex.GetInstanceID());
                }
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
