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
// 런타임 경로(게이트 결정): stripping High 로 잘리는 UnityWebRequestAssetBundle/DownloadHandlerAssetBundle
//   (WebGL 가상 FS 캐시 의존) 대신 UnityWebRequest.Get → DownloadHandlerBuffer(잔존) →
//   AssetBundle.LoadFromMemoryAsync(byte[]) 경로를 쓴다. SDK 가 이 API 를 참조하므로 IL2CPP
//   스트리퍼가 managed 래퍼를 reachable 로 보존한다(네이티브 AssetBundle 모듈은 빌드에 잔존 확인).
//
// TTFF 영향: 주입은 interactive(=TTFF 측정 시점) 이후, 그리고 추가로 몇 프레임 지연 후에 일어나므로
//   TTFF 에 영향 없다. 초기 .data 에서 비-부팅 폰트 바이트가 빠진 만큼 초기 다운로드/TTFF 가 줄어드는
//   것이 본질 효과다(초기 .data 에서 비-부팅 폰트 바이트가 빠진 만큼 TTFF 감소).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
#if AIT_HAS_UNITYWEBREQUEST
using UnityEngine.Networking;
#endif
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

            /// <summary>페이로드 인코딩("br" = brotli). 빈 값이면 무압축(구 매니페스트 호환).</summary>
            public string encoding;

            /// <summary>lazy 확장 언어 태그(예: "ja"). 빈 값/부재(구 매니페스트) = 기존 eager entry.</summary>
            public string lazyTag;

            /// <summary>lazyTag 의 전체 유니코드 범위("U+XXXX-YYYY,U+ZZZZ" 콤마 구분, 언어 테이블 값 그대로).</summary>
            public string lazyRanges;
        }

        /// <summary>파싱된 유니코드 코드포인트 구간(양끝 포함). 순수 값 타입 — 테스트 가능.</summary>
        internal readonly struct CodepointRange
        {
            public readonly int Start;
            public readonly int End;

            public CodepointRange(int start, int end)
            {
                Start = start;
                End = end;
            }

            public bool Contains(int codepoint) => codepoint >= Start && codepoint <= End;
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

        // ─────────────── lazy 확장 언어 상태(pending 소진 전까지 GameObject 를 살려둔다) ───────────────
        private readonly Dictionary<string, Entry> lazyPending = new Dictionary<string, Entry>();
        private readonly Dictionary<string, CodepointRange[]> lazyPendingRanges = new Dictionary<string, CodepointRange[]>();
        private int lazyInflight;
        private bool lazySubscribed;
        private EventInfo lazyEventInfo;
        private Delegate lazyEventHandler;
        private Coroutine lazyPollCoroutine;

        // TMP_Text 리플렉션 캐시(lazy 초기 스윕/이벤트 스캔 전용 — RefreshVisibleText 와 별개 캐시).
        private Type lazyTmpTextType;
        private PropertyInfo lazyTmpTextTextProperty;

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
#if !AIT_HAS_UNITYWEBREQUEST || !AIT_HAS_ASSETBUNDLE
#if !AIT_HAS_UNITYWEBREQUEST
            Debug.LogWarning("[AIT] unitywebrequest 모듈이 비활성화되어 폰트 스트리밍 재수화를 건너뜁니다");
#else
            Debug.LogWarning("[AIT] assetbundle 모듈이 비활성화되어 폰트 스트리밍 재수화를 건너뜁니다");
#endif
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

            // eager/lazy 분리: lazyTag 가 있는 entry 는 즉시 로드하지 않고 pending 맵에만 등록한다.
            var eagerQueue = new Queue<Entry>();
            foreach (var e in pending)
            {
                if (string.IsNullOrEmpty(e.lazyTag))
                {
                    eagerQueue.Enqueue(e);
                    continue;
                }

                if (lazyPending.ContainsKey(e.lazyTag))
                {
                    Debug.LogWarning($"[AIT-StreamingFont] 중복 lazyTag 무시: {e.lazyTag} ({e.bundle})");
                    continue;
                }

                var ranges = ParseRanges(e.lazyRanges);
                if (ranges.Length == 0)
                {
                    Debug.LogWarning($"[AIT-StreamingFont] lazyRanges 파싱 결과가 비어 있어 이 태그는 감지되지 않습니다: {e.lazyTag}");
                }

                lazyPending[e.lazyTag] = e;
                lazyPendingRanges[e.lazyTag] = ranges;
            }

            int injected = 0;
            int inflight = 0;
            int doneCount = 0;
            int total = eagerQueue.Count;

            // 단순 동시성 게이트: maxConcurrent 만큼 동시에 로드/주입.
            while (doneCount < total)
            {
                while (inflight < maxConcurrent && eagerQueue.Count > 0)
                {
                    var e = eagerQueue.Dequeue();
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

            Debug.Log($"[AIT-StreamingFont] 폰트 재수화 완료(eager): {injected}/{total} 주입.");

            if (lazyPending.Count == 0)
            {
                Destroy(gameObject);
                yield break;
            }

            // lazy pending 이 남아있는 동안은 GameObject 를 유지한다 — 소진 시 FinishLazyDetection 에서 파괴.
            SetupLazyDetection();
        }

        private IEnumerator LoadManifest()
        {
#if AIT_HAS_UNITYWEBREQUEST
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
#else
            // AIT_HAS_UNITYWEBREQUEST 미정의 시: Run() 진입부에서 이미 종료하므로 여기에 도달하지 않음.
            yield return null;
#endif
        }

        private IEnumerator LoadAndInject(Entry e, Action<bool> done)
        {
#if AIT_HAS_ASSETBUNDLE && AIT_HAS_UNITYWEBREQUEST
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

            // .br 외부화 페이로드 정규화: 서버가 Content-Encoding 으로 이미 해제했으면 그대로,
            // raw brotli 면 여기서 해제(UnityFS 매직으로 판별). 무압축 엔트리는 no-op.
            data = AITStreamingCodec.DecodePayload(e.encoding, data, AITStreamingCodec.LooksLikeUnityFs, e.bundle);

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
            // AIT_HAS_ASSETBUNDLE/AIT_HAS_UNITYWEBREQUEST 미정의 시: Run() 진입부에서 이미 종료하므로 여기에 도달하지 않음.
            // C# 컴파일러의 "yield return 없는 IEnumerator" 경고를 방지하기 위해 yield 유지.
            yield return null;
            done(false);
#endif
        }

        // ─────────────────────────── lazy 확장 언어 감지/로드 ───────────────────────────

        /// <summary>
        /// "U+XXXX-YYYY,U+ZZZZ" 형식의 lazyRanges 문자열을 (start,end) 구간 배열로 파싱한다(양끝 포함).
        /// 단일 코드포인트 토큰("U+ZZZZ", 대시 없음)은 start==end 구간으로 취급. 형식이 어긋난 토큰은
        /// 조용히 무시하고 나머지 유효 토큰은 그대로 반영한다(전체 실패로 번지지 않음). 순수 정적
        /// 함수 — 부수 효과 없음, 테스트 대상.
        /// </summary>
        internal static CodepointRange[] ParseRanges(string ranges)
        {
            if (string.IsNullOrEmpty(ranges))
            {
                return Array.Empty<CodepointRange>();
            }

            var result = new List<CodepointRange>();
            foreach (var rawToken in ranges.Split(','))
            {
                string token = rawToken.Trim();
                if (token.Length < 3 || !token.StartsWith("U+", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // 빈 토큰/형식 불일치 — 무시.
                }

                string body = token.Substring(2);
                int dashIdx = body.IndexOf('-');
                string startHex = dashIdx >= 0 ? body.Substring(0, dashIdx) : body;
                string endHex = dashIdx >= 0 ? body.Substring(dashIdx + 1) : body;

                if (int.TryParse(startHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int start)
                    && int.TryParse(endHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int end)
                    && start <= end)
                {
                    result.Add(new CodepointRange(start, end));
                }

                // else: 파싱 실패(비16진/역전 구간 등) — 이 토큰만 무시하고 계속.
            }

            return result.ToArray();
        }

        /// <summary>
        /// text 안의 각 문자를 pending 언어 태그들의 유니코드 범위와 대조해, 매치되는 태그 전부를
        /// 반환한다. 서로게이트 쌍은 UTF-32 코드포인트로 합성해 검사(BMP 밖 이모지 등 지원). 한
        /// 문자가 여러 태그 범위에 걸치면(예: 한자가 ja/zh-Hans 양쪽에 속함) 매치되는 태그 전부를
        /// 반환한다 — 겹치는 태그 모두 로드하는 것이 의도된 동작. 순수 정적 함수 — 테스트 대상.
        /// </summary>
        internal static List<string> MatchPendingTags(string text, IDictionary<string, CodepointRange[]> pendingTagRanges)
        {
            var matched = new List<string>();
            if (string.IsNullOrEmpty(text) || pendingTagRanges == null || pendingTagRanges.Count == 0)
            {
                return matched;
            }

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                int codepoint;
                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    codepoint = char.ConvertToUtf32(c, text[i + 1]);
                    i++; // 서로게이트 쌍 소비.
                }
                else if (char.IsSurrogate(c))
                {
                    continue; // 짝없는 서로게이트 — 무시.
                }
                else
                {
                    codepoint = c;
                }

                foreach (var kv in pendingTagRanges)
                {
                    if (matched.Contains(kv.Key))
                    {
                        continue;
                    }

                    var tagRanges = kv.Value;
                    if (tagRanges == null)
                    {
                        continue;
                    }

                    for (int r = 0; r < tagRanges.Length; r++)
                    {
                        if (tagRanges[r].Contains(codepoint))
                        {
                            matched.Add(kv.Key);
                            break;
                        }
                    }
                }
            }

            return matched;
        }

        /// <summary>lazy pending 등록 직후 1회: 감지 소스(이벤트 or 폴링)를 세팅하기 전에 이미 떠 있는 TMP 텍스트를 스캔.</summary>
        private void SetupLazyDetection()
        {
            ScanAllTmpText();

            if (TrySubscribeTextChanged())
            {
                Debug.Log("[AIT-StreamingFont] TMPro_EventManager.TEXT_CHANGED_EVENT 구독 성공 — 이벤트 기반 lazy 감지.");
            }
            else
            {
                Debug.LogWarning("[AIT-StreamingFont] TEXT_CHANGED_EVENT 구독 실패 — 1초 간격 폴링으로 폴백합니다.");
                lazyPollCoroutine = StartCoroutine(PollLazyDetection());
            }

            // 스윕 도중 이미 전 태그가 트리거되어 로드/완료까지 끝났을 수 있음(동기 완료 극단값) — 정리.
            MaybeFinishLazy();
        }

        private bool CacheLazyTmpTextIntrospection()
        {
            if (lazyTmpTextType != null)
            {
                return true;
            }

            lazyTmpTextType = FindType("TMPro.TMP_Text");
            if (lazyTmpTextType == null)
            {
                return false;
            }

            lazyTmpTextTextProperty = lazyTmpTextType.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
            return lazyTmpTextTextProperty != null;
        }

        private void ScanAllTmpText()
        {
            if (!CacheLazyTmpTextIntrospection())
            {
                return;
            }

            try
            {
                var objs = Resources.FindObjectsOfTypeAll(lazyTmpTextType);
                foreach (var o in objs)
                {
                    ScanOneTmpText(o);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-StreamingFont] 초기 TMP 텍스트 스윕 예외: {e.Message}");
            }
        }

        /// <summary>단일 TMP_Text 오브젝트의 현재 text 를 스캔해 매치되는 lazy 태그가 있으면 로드를 트리거.</summary>
        private void ScanOneTmpText(UnityEngine.Object tmpTextObj)
        {
            if (tmpTextObj == null || lazyTmpTextTextProperty == null || lazyPending.Count == 0)
            {
                return;
            }

            string text;
            try
            {
                text = lazyTmpTextTextProperty.GetValue(tmpTextObj) as string;
            }
            catch
            {
                return; // 개별 오브젝트 리플렉션 실패 — 무시하고 계속.
            }

            var matched = MatchPendingTags(text, lazyPendingRanges);
            if (matched.Count > 0)
            {
                TriggerLazyLoad(matched);
            }
        }

        /// <summary>매치된 태그들을 pending 에서 제거하고 각각 온디맨드 로드 코루틴을 시작.</summary>
        private void TriggerLazyLoad(List<string> tags)
        {
            foreach (var tag in tags)
            {
                if (!lazyPending.TryGetValue(tag, out var e))
                {
                    continue; // 이미 트리거됨(경합 방지) — 세션 내 1회 시도 정책.
                }

                lazyPending.Remove(tag);
                lazyPendingRanges.Remove(tag);
                StartCoroutine(LoadLazyEntry(e));
            }
        }

        /// <summary>lazy entry 1개를 기존 maxConcurrent 게이트를 지켜 로드/주입. 완료 시 소진 여부를 확인해 정리.</summary>
        private IEnumerator LoadLazyEntry(Entry e)
        {
            while (lazyInflight >= maxConcurrent)
            {
                yield return null;
            }

            lazyInflight++;
            bool ok = false;
            yield return LoadAndInject(e, r => ok = r);
            lazyInflight--;

            if (ok)
            {
                RefreshVisibleText();
                Debug.Log($"[AIT-StreamingFont] lazy 폰트 로드 완료: {e.lazyTag} ({e.bundle})");
            }
            else
            {
                Debug.LogWarning($"[AIT-StreamingFont] lazy 폰트 로드 실패(세션 내 재시도 없음): {e.lazyTag} ({e.bundle})");
            }

            MaybeFinishLazy();
        }

        /// <summary>pending 도 inflight 도 없으면(전 태그 소진) 감지 리소스를 정리하고 GameObject 를 파괴.</summary>
        private void MaybeFinishLazy()
        {
            if (lazyPending.Count == 0 && lazyInflight == 0)
            {
                FinishLazyDetection();
            }
        }

        private void FinishLazyDetection()
        {
            UnsubscribeTextChanged();
            if (lazyPollCoroutine != null)
            {
                StopCoroutine(lazyPollCoroutine);
                lazyPollCoroutine = null;
            }

            if (this != null && gameObject != null)
            {
                Debug.Log("[AIT-StreamingFont] lazy 폰트 전 태그 소진 — 재수화 컴포넌트를 종료합니다.");
                Destroy(gameObject);
            }
        }

        /// <summary>pending 이 남아있는 동안 1초 간격으로 전체 TMP_Text 를 스윕(이벤트 구독 실패 시 폴백).</summary>
        private IEnumerator PollLazyDetection()
        {
            var wait = new WaitForSeconds(1f);
            while (lazyPending.Count > 0)
            {
                yield return wait;
                ScanAllTmpText();
            }
        }

        /// <summary>
        /// TMPro_EventManager.TEXT_CHANGED_EVENT(static event, UnityEngine.Object 1개 인자)에 리플렉션으로
        /// 구독한다. 이벤트/타입 부재 또는 구독 중 예외 시 false(호출부가 폴링 폴백으로 전환).
        /// </summary>
        private bool TrySubscribeTextChanged()
        {
            try
            {
                var mgrType = FindType("TMPro.TMPro_EventManager");
                if (mgrType == null)
                {
                    return false;
                }

                var eventInfo = mgrType.GetEvent("TEXT_CHANGED_EVENT", BindingFlags.Public | BindingFlags.Static);
                var addMethod = eventInfo?.GetAddMethod();
                var handlerMethod = GetType().GetMethod(nameof(OnTmpTextChangedHandler), BindingFlags.NonPublic | BindingFlags.Instance);
                if (addMethod == null || handlerMethod == null)
                {
                    return false;
                }

                var handler = Delegate.CreateDelegate(eventInfo.EventHandlerType, this, handlerMethod);
                addMethod.Invoke(null, new object[] { handler });

                lazyEventInfo = eventInfo;
                lazyEventHandler = handler;
                lazySubscribed = true;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-StreamingFont] TEXT_CHANGED_EVENT 구독 예외: {e.Message}");
                lazyEventInfo = null;
                lazyEventHandler = null;
                lazySubscribed = false;
                return false;
            }
        }

        private void UnsubscribeTextChanged()
        {
            if (!lazySubscribed || lazyEventInfo == null || lazyEventHandler == null)
            {
                return;
            }

            try
            {
                lazyEventInfo.GetRemoveMethod()?.Invoke(null, new object[] { lazyEventHandler });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-StreamingFont] TEXT_CHANGED_EVENT 구독 해제 예외: {e.Message}");
            }
            finally
            {
                lazySubscribed = false;
                lazyEventHandler = null;
                lazyEventInfo = null;
            }
        }

        /// <summary>TEXT_CHANGED_EVENT 핸들러 — 변경된 오브젝트 1개만 스캔(전체 재스윕 대비 저비용).</summary>
        private void OnTmpTextChangedHandler(UnityEngine.Object changedObj)
        {
            ScanOneTmpText(changedObj);
        }

        private void OnDestroy()
        {
            // static 이벤트 구독을 남긴 채 파괴되면 델리게이트가 죽은 this 를 참조해 누수/예외 위험 —
            // 정상 종료 경로(FinishLazyDetection) 밖에서 파괴되는 경우(도메인 리로드 등)를 대비한 안전망.
            UnsubscribeTextChanged();
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

#if AIT_HAS_UNITYWEBREQUEST
        private static bool IsSuccess(UnityWebRequest req)
        {
#if UNITY_2020_2_OR_NEWER
            return req.result == UnityWebRequest.Result.Success;
#else
            return !req.isHttpError && !req.isNetworkError;
#endif
        }
#endif

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
