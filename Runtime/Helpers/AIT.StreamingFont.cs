// -----------------------------------------------------------------------
// <copyright file="AIT.StreamingFont.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Streaming Font (runtime rehydrator)
// </copyright>
// -----------------------------------------------------------------------
//
// 빌드 단계(AITFontExternalizer)에서 초기 .data 밖(StreamingAssets)의 WebGL AssetBundle 로
// 외부화된 비-부팅 대형 폰트를, 게임이 interactive 된(=첫 프레임 그려진) 이후 비동기로 로드하여
// 그 안의 TMP_FontAsset 을 TMP 글로벌 fallback 체인에 주입한다. 텍스처/오디오 스트리밍의 폰트 버전.
//
// 왜 in-place 복원이 아니라 fallback 주입인가:
//   텍스처는 Texture2D.LoadImage, 오디오는 AudioSource.clip 재할당으로 살아있는 객체에 제자리
//   복원이 되지만, 폰트는 런타임에 .ttf 바이트→Font 생성이 WebGL 에서 구조적으로 막혀 있어
//   "원본 Font 제자리 복원"이 불가능하다. 대신 풀 폰트를 담은 AssetBundle 을 로드하여 그 안의
//   TMP_FontAsset 을 TMP 글로벌 fallback 목록에 추가한다 — 빌드의 소스-비운 폰트(primary)는
//   CJK □ 를 그리지만 fallback 의 풀 폰트가 누락 글리프를 채운다.
//
// TMP 비의존(reflection): SDK 는 TMPro(com.unity.textmeshpro) 에 컴파일 의존을 갖지 않는다
//   (모든 소비자 프로젝트가 TMP 를 쓰는 것은 아님). 따라서 TMP_Settings.fallbackFontAssets 주입은
//   전적으로 reflection 으로 수행하며, TMP 가 없는 프로젝트에서는 조용히 no-op 한다.
//
// 런타임 경로(§3-U-gate 결정): stripping High 로 잘리는 UnityWebRequestAssetBundle/DownloadHandlerAssetBundle
//   (WebGL 가상 FS 캐시 의존) 대신 UnityWebRequest.Get → DownloadHandlerBuffer(잔존) →
//   AssetBundle.LoadFromMemoryAsync(byte[]) 경로를 쓴다. SDK 가 이 API 를 참조하므로 IL2CPP
//   스트리퍼가 managed 래퍼를 reachable 로 보존한다(네이티브 AssetBundle 모듈은 빌드에 잔존 확인).
//
// TTFF 영향: 주입은 interactive(=TTFF 측정 시점) 이후, 그리고 추가로 몇 프레임 지연 후에 일어나므로
//   TTFF 에 영향 없다. 초기 .data 에서 비-부팅 폰트 바이트가 빠진 만큼 초기 다운로드/TTFF 가 줄어드는
//   것이 본질 효과다(§3-U: @100Mbps −1,040ms / @200Mbps −617ms).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Scripting;

namespace AppsInToss
{
    /// <summary>
    /// 외부화된 폰트를 런타임에 스트리밍으로 재수화하는 SDK 컴포넌트.
    /// 빌드 단계(<c>AITFontExternalizer</c>)가 매니페스트와 StreamingAssets 번들을 만들어 두면,
    /// 이 컴포넌트가 자동 부팅되어 번들 속 TMP_FontAsset 을 TMP fallback 체인에 주입한다.
    /// 매니페스트가 없거나 TMP 가 없는 빌드에서는 조용히 no-op 후 자체 종료한다.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class AITStreamingFont : MonoBehaviour
    {
        /// <summary>fallback 주입 전 대기 프레임 수. 첫 프레임(TTFF) 이후로 확실히 미루기 위함.</summary>
        private const int WarmupFrames = 3;

        /// <summary>동시 번들 다운로드/로드 기본 상한(매니페스트에 값이 없을 때).</summary>
        private const int DefaultMaxConcurrent = 2;

        private const string ManifestRelativePath = "ait-stream-font/manifest.json";
        private const string StreamDirRelativePath = "ait-stream-font/";

        [Serializable]
        private struct Entry
        {
            public string guid;
            public string bundle;
            public string[] fonts;
        }

        [Serializable]
        private struct Manifest
        {
            public int maxConcurrent;
            public Entry[] entries;
        }

        private readonly List<Entry> pending = new List<Entry>();
        private int maxConcurrent = DefaultMaxConcurrent;
        private bool ready;

        // TMP reflection 캐시(주입 1회 해석 후 재사용).
        private Type tmpSettingsType;
        private Type tmpFontAssetType;
        private IList fallbackList;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        [Preserve]
        private static void Bootstrap()
        {
            // SDK 가 폰트 외부화를 수행한 빌드에서만 매니페스트가 존재한다.
            // 부팅 후 매니페스트가 없거나 TMP 가 없으면 Run() 코루틴이 스스로 종료한다.
            var go = new GameObject("[AIT] StreamingFont");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<AITStreamingFont>();
        }

        private void Start() => StartCoroutine(Run());

        private IEnumerator Run()
        {
#if !AIT_HAS_ASSETBUNDLE
            Debug.LogWarning("[AIT] assetbundle 모듈이 비활성화되어 폰트 스트리밍 재수화를 건너뜁니다");
            Destroy(gameObject);
            yield break;
#endif
            // 첫 프레임(TTFF) 이후로 확실히 미룬다 — 재수화가 부팅 임계 경로에 끼지 않도록.
            for (int i = 0; i < WarmupFrames; i++)
            {
                yield return null;
            }

            yield return LoadManifest();
            if (!ready || pending.Count == 0)
            {
                Destroy(gameObject);
                yield break;
            }

            // TMP 가 없는 프로젝트면 주입 대상이 없으므로 조용히 종료(번들 다운로드도 생략).
            if (!ResolveTmpFallback())
            {
                Debug.Log("[AIT-StreamingFont] TMP(TMPro)가 없어 폰트 fallback 주입을 건너뜁니다(no-op).");
                Destroy(gameObject);
                yield break;
            }

            int injected = 0;
            int inflight = 0;
            var queue = new Queue<Entry>(pending);
            int doneCount = 0;
            int total = pending.Count;

            // 단순 동시성 게이트: maxConcurrent 만큼 동시에 로드/주입.
            while (doneCount < total)
            {
                while (inflight < maxConcurrent && queue.Count > 0)
                {
                    var e = queue.Dequeue();
                    inflight++;
                    StartCoroutine(LoadAndInject(e, ok =>
                    {
                        inflight--;
                        doneCount++;
                        if (ok)
                        {
                            injected++;
                        }
                    }));
                }

                yield return null;
            }

            if (injected > 0)
            {
                RefreshVisibleText();
            }

            Debug.Log($"[AIT-StreamingFont] 폰트 재수화 완료: {injected}/{total} 주입.");
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
                    // 매니페스트 없음 = 이 빌드는 폰트 외부화를 안 함. 정상 경로(no-op).
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
                            if (!string.IsNullOrEmpty(e.bundle))
                            {
                                pending.Add(e);
                            }
                        }
                    }

                    ready = true;
                    Debug.Log($"[AIT-StreamingFont] 매니페스트 로드: {pending.Count}개 외부화 폰트 (동시 {maxConcurrent})");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AIT-StreamingFont] 매니페스트 파싱 실패: {ex.Message}");
                }
            }
        }

        private IEnumerator LoadAndInject(Entry e, Action<bool> done)
        {
#if AIT_HAS_ASSETBUNDLE
            byte[] data = null;
            string url = ResolveStreamingUrl(StreamDirRelativePath + e.bundle);
            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                if (!IsSuccess(req))
                {
                    Debug.LogWarning($"[AIT-StreamingFont] 번들 다운로드 실패 {e.bundle}: {req.error}");
                    done(false);
                    yield break;
                }

                data = req.downloadHandler.data;
            }

            // stripping High 가 잘라낸 GetAssetBundle/DownloadHandlerAssetBundle 대신
            // DownloadHandlerBuffer 로 받은 바이트를 LoadFromMemoryAsync 로 적재(가상 FS 캐시 비의존).
            var createReq = AssetBundle.LoadFromMemoryAsync(data);
            yield return createReq;

            var bundle = createReq.assetBundle;
            if (bundle == null)
            {
                Debug.LogWarning($"[AIT-StreamingFont] 번들 적재 실패(LoadFromMemoryAsync null): {e.bundle}");
                done(false);
                yield break;
            }

            bool any = false;
            var loadReq = bundle.LoadAllAssetsAsync();
            yield return loadReq;

            try
            {
                var assets = loadReq.allAssets;
                if (assets != null)
                {
                    foreach (var a in assets)
                    {
                        if (a == null)
                        {
                            continue;
                        }

                        // TMP 컴파일 의존 없이 타입명으로 TMP_FontAsset 식별 → fallback 목록에 추가.
                        if (IsTmpFontAsset(a) && InjectFallback(a))
                        {
                            any = true;
                            Debug.Log($"[AIT-StreamingFont]   fallback 주입: {a.name} ({e.bundle})");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AIT-StreamingFont] 주입 예외 {e.bundle}: {ex.Message}");
            }

            // 번들은 언로드하지 않는다(unload(true) 는 주입한 폰트를 파괴, unload(false) 도 동적
            // 래스터화가 번들 자원을 늦게 참조할 위험이 있어 세션 동안 유지). 메모리 비용은 폰트 1~2개분.
            done(any);
#else
            // AIT_HAS_ASSETBUNDLE 미정의 시: Run() 진입부에서 이미 종료하므로 여기에 도달하지 않음.
            // C# 컴파일러의 "yield return 없는 IEnumerator" 경고를 방지하기 위해 yield 유지.
            yield return null;
            done(false);
#endif
        }

        // ─────────────────────────── TMP reflection ───────────────────────────

        /// <summary>TMP_Settings.fallbackFontAssets(글로벌 fallback 목록)를 reflection 으로 해석/캐시. TMP 부재 시 false.</summary>
        private bool ResolveTmpFallback()
        {
            try
            {
                tmpSettingsType = FindType("TMPro.TMP_Settings");
                tmpFontAssetType = FindType("TMPro.TMP_FontAsset");
                if (tmpSettingsType == null || tmpFontAssetType == null)
                {
                    return false; // TMP 미사용 프로젝트 → 조용히 no-op.
                }

                // 1순위: public static 프로퍼티 fallbackFontAssets (instance.m_fallbackFontAssets 위임).
                //   instance 가 null 이면 getter 내부에서 NRE → SafeGet 으로 흡수하고 2순위로 진행.
                var prop = tmpSettingsType.GetProperty("fallbackFontAssets", BindingFlags.Public | BindingFlags.Static);
                if (prop != null)
                {
                    fallbackList = SafeGet(() => prop.GetValue(null)) as IList;
                }

                // 2순위: instance.m_fallbackFontAssets 필드 직접. 설정 자산에 fallback 이 하나도 없으면 backing
                //   필드가 null 일 수 있는데, 그대로 두면 빌드는 폰트를 비웠는데 런타임 재수화는 통째로 skip 되는
                //   silent-fail(CJK 영구 □) 이 된다 → 새 List<TMP_FontAsset> 를 만들어 직접 주입한다.
                if (fallbackList == null)
                {
                    var instProp = tmpSettingsType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
                    object settings = instProp != null ? SafeGet(() => instProp.GetValue(null)) : null;
                    if (settings == null)
                    {
                        Debug.LogWarning("[AIT-StreamingFont] TMP_Settings.instance 가 null — 'TMP Settings' 리소스가 빌드에 없어 fallback 주입 불가.");
                        return false;
                    }

                    var field = tmpSettingsType.GetField("m_fallbackFontAssets", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    if (field == null)
                    {
                        Debug.LogWarning("[AIT-StreamingFont] TMP_Settings.m_fallbackFontAssets 필드를 찾지 못함(TMP 버전 불일치).");
                        return false;
                    }

                    fallbackList = field.GetValue(settings) as IList;
                    if (fallbackList == null)
                    {
                        // 글로벌 fallback 이 비어 있어 backing 필드가 null → 빈 List 를 만들어 set 후 그 인스턴스를 주입 대상으로.
                        var listType = typeof(List<>).MakeGenericType(tmpFontAssetType);
                        fallbackList = (IList)Activator.CreateInstance(listType);
                        field.SetValue(settings, fallbackList);
                        Debug.Log("[AIT-StreamingFont] TMP 글로벌 fallback 목록이 비어 있어 새로 생성했습니다.");
                    }
                }

                return fallbackList != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-StreamingFont] TMP fallback 해석 예외: {e.Message}");
                return false;
            }
        }

        /// <summary>reflection getter 호출을 예외 흡수로 감싸는 헬퍼(instance getter 내부 NRE 등을 null 로 변환).</summary>
        private static object SafeGet(Func<object> getter)
        {
            try
            {
                return getter();
            }
            catch
            {
                return null;
            }
        }

        private bool IsTmpFontAsset(UnityEngine.Object a)
        {
            if (tmpFontAssetType == null)
            {
                return false;
            }

            return tmpFontAssetType.IsInstanceOfType(a);
        }

        /// <summary>해석된 글로벌 fallback 목록에 폰트 에셋을 중복 없이 추가. 추가 시 true.</summary>
        private bool InjectFallback(UnityEngine.Object fontAsset)
        {
            if (fallbackList == null)
            {
                return false;
            }

            try
            {
                if (fallbackList.Contains(fontAsset))
                {
                    return false; // 이미 등록됨
                }

                fallbackList.Add(fontAsset);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-StreamingFont] fallback Add 예외: {e.Message}");
                return false;
            }
        }

        /// <summary>주입 직후 화면에 떠 있는 TMP 텍스트가 새 fallback 으로 다시 그려지도록 ForceMeshUpdate(best-effort).</summary>
        private void RefreshVisibleText()
        {
            try
            {
                var tmpTextType = FindType("TMPro.TMP_Text");
                if (tmpTextType == null)
                {
                    return;
                }

                // TMP 버전별 오버로드 차이 흡수: (bool,bool) → (bool) → () 순으로 탐색.
                var force = tmpTextType.GetMethod("ForceMeshUpdate", new[] { typeof(bool), typeof(bool) })
                            ?? tmpTextType.GetMethod("ForceMeshUpdate", new[] { typeof(bool) })
                            ?? tmpTextType.GetMethod("ForceMeshUpdate", Type.EmptyTypes);
                if (force == null)
                {
                    return;
                }

                var objs = Resources.FindObjectsOfTypeAll(tmpTextType);
                int paramCount = force.GetParameters().Length;
                object[] args = paramCount == 2 ? new object[] { true, false }
                              : paramCount == 1 ? new object[] { true }
                              : Array.Empty<object>();
                foreach (var o in objs)
                {
                    try
                    {
                        force.Invoke(o, args);
                    }
                    catch
                    {
                        // 개별 실패 무시 — 다음 텍스트 변경/렌더 때 자연 반영됨.
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-StreamingFont] 텍스트 갱신 예외(무시): {e.Message}");
            }
        }

        private static Type FindType(string fullName)
        {
            // 어셈블리 한정 없이도 찾도록 로드된 어셈블리를 스캔(TMP 어셈블리명 버전 차이 흡수).
            var t = Type.GetType(fullName);
            if (t != null)
            {
                return t;
            }

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    t = asm.GetType(fullName);
                    if (t != null)
                    {
                        return t;
                    }
                }
                catch
                {
                    // 일부 어셈블리는 GetType 에서 예외 — 무시하고 계속.
                }
            }

            return null;
        }

        // ─────────────────────────── 공통 유틸 ───────────────────────────

        private static bool IsSuccess(UnityWebRequest req)
        {
#if UNITY_2020_2_OR_NEWER
            return req.result == UnityWebRequest.Result.Success;
#else
            return !req.isHttpError && !req.isNetworkError;
#endif
        }

        // WebGL: streamingAssetsPath 는 상대/절대 URL. UnityWebRequest 는 file:// 또는 http(s):// 모두 처리.
        private static string ResolveStreamingUrl(string rel)
        {
            return JoinUrl(Application.streamingAssetsPath, rel);
        }

        /// <summary>basePath 와 상대 경로를 슬래시 중복 없이 결합. (테스트 가능한 순수 함수)</summary>
        internal static string JoinUrl(string basePath, string rel)
        {
            return basePath.EndsWith("/") ? basePath + rel : basePath + "/" + rel;
        }
    }
}
