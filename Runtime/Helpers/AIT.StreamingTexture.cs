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
#if AIT_HAS_UNITYWEBREQUEST
using UnityEngine.Networking;
#endif
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

        /// <summary>
        /// 동시 스트리밍 다운로드/디코드 기본 상한(매니페스트에 값이 없을 때). LoadImage 가 메인스레드 디코드라 hitch 를 제한하고,
        /// 디코드 버퍼(2048² 하나에 RGBA32 16MB)가 겹쳐 힙 high-water 를 올리지 않도록 1 로 둔다. 복원은 TTFF 뒤라 체감 비용이 작다.
        /// </summary>
        private const int DefaultMaxConcurrent = 1;

        // --- 진단 카운터(P0-1): 복원에 성공한 텍스처의 CPU 사본 추정 바이트(readable 로 남긴 것만: LoadImage 는 w*h*4, raw 는 블록 바이트). non-readable 로 복원하면 0. AITUnityMemReporter 가 읽는다.
        internal static long HeldBytes;
        internal static int HeldCount;

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

            /// <summary>페이로드 인코딩("br" = brotli). 빈 값이면 무압축(구 매니페스트 호환).</summary>
            public string encoding;

            /// <summary>
            /// 스트림 사본이 의도적으로 다운스케일된 경우의 '스트림 이미지 실제 차원'(sw=width, sh=height).
            /// 0 이면 다운스케일 없음(width/height 와 동일 = 구 매니페스트 호환). width/height 는 스텁 매칭용
            /// '원본 차원'으로 유지되고(FindStub 계약), 여기 sw/sh 는 LoadImage 후 기대되는 실제 텍스처 차원이다.
            /// </summary>
            public int sw;
            public int sh;

            /// <summary>
            /// 1 = 원본이 non-readable 이었다(복원 후에도 non-readable 로 되돌려 CPU 사본을 남기지 않는다).
            /// 0 = 원본이 readable 이었거나 구 매니페스트(필드 없음) — 기존처럼 readable 로 복원.
            /// </summary>
            public int nonReadable;

            /// <summary>
            /// GPU 포맷 보존(raw) 사본: 원본 ASTC 블록 전체 mip 체인(필요하면 brotli). 비어 있으면 PNG/JPG(file)만 쓴다.
            /// file 은 raw 를 쓸 수 없는 환경(ASTC 미지원 등)의 폴백으로 항상 함께 실린다.
            /// </summary>
            public string rawFile;

            /// <summary>raw 페이로드 인코딩("br" = brotli). 비어 있으면 무압축.</summary>
            public string rawEncoding;

            /// <summary>raw 사본의 TextureFormat 정수값(스텁 포맷과 일치해야 한다).</summary>
            public int rawFormat;

            /// <summary>raw 사본의 mip 수(스텁 mipmapCount 와 일치해야 한다).</summary>
            public int rawMips;

            /// <summary>raw 사본의 해제 후 바이트(LoadRawTextureData 가 요구하는 정확한 크기).</summary>
            public int rawSize;
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

        /// <summary>엔트리별 다운로드 실패 횟수(guid 키). 상한 초과 시 포기해 무한 재다운로드를 차단.</summary>
        private readonly Dictionary<string, int> downloadFailCounts = new Dictionary<string, int>();

        /// <summary>엔트리별 디코드/적용 실패 횟수(guid 키). 같은 바이트는 재시도해도 같게 실패하므로 상한이 작다.</summary>
        private readonly Dictionary<string, int> applyFailCounts = new Dictionary<string, int>();

        /// <summary>일시적일 수 있는 다운로드 실패의 시도 상한(초과 시 포기 — 스텁 유지, 기능 저하일 뿐 안전).</summary>
        private const int MaxDownloadAttempts = 8;

        /// <summary>결정적(같은 페이로드 → 같은 결과) 디코드/적용 실패의 시도 상한.</summary>
        private const int MaxApplyAttempts = 2;

        /// <summary>raw 복원에 실패한(포맷·mip 불일치, 크기 불일치, 예외) 엔트리 guid. 이후 시도는 PNG/JPG 폴백으로 간다.</summary>
        private readonly HashSet<string> rawFailed = new HashSet<string>();

        private int maxConcurrent = DefaultMaxConcurrent;
        private int loadingCount;
        private bool ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        [Preserve]
        private static void Bootstrap()
        {
            using var _hookTimer = AITHookTimer.Begin("StreamingTexture");

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
#if !AIT_HAS_UNITYWEBREQUEST || !AIT_HAS_IMAGECONVERSION
#if !AIT_HAS_UNITYWEBREQUEST
            Debug.LogWarning("[AIT] unitywebrequest 모듈이 비활성화되어 텍스처 스트리밍 복원을 건너뜁니다");
#else
            Debug.LogWarning("[AIT] imageconversion 모듈이 비활성화되어 텍스처 스트리밍 복원을 건너뜁니다");
#endif
            Destroy(gameObject);
            yield break;
#endif
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
#if AIT_HAS_UNITYWEBREQUEST
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
                    int tier = 0;
                    try
                    {
                        tier = AITMemoryBridge.LowMemTier;
                    }
                    catch (System.Exception)
                    {
                        // 티어를 못 읽으면 정상 기기로 본다.
                    }

                    maxConcurrent = ResolveMaxConcurrent(m.maxConcurrent, tier);

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
                    Debug.Log($"[AIT-StreamingTexture] 매니페스트 로드: {pending.Count}개 외부화 텍스처 (동시 {maxConcurrent}, lowMemTier={tier})");
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[AIT-StreamingTexture] 매니페스트 파싱 실패: {ex.Message}");
                }
            }
#else
            // AIT_HAS_UNITYWEBREQUEST 미정의 시: Run() 진입부에서 이미 종료하므로 여기에 도달하지 않음.
            yield return null;
#endif
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
#if AIT_HAS_UNITYWEBREQUEST && AIT_HAS_IMAGECONVERSION
            // raw(GPU 포맷 보존) 사본은 스텁 포맷·mip 이 매니페스트와 정확히 같고 기기가 그 포맷을 지원할 때만 쓴다.
            // 그렇지 않으면 PNG/JPG 사본(file)으로 폴백 — 사본 선택은 다운로드 전에 정해 불필요한 쪽을 받지 않는다.
            bool useRaw = tex != null && IsRawUsable(
                e.rawFile, rawFailed.Contains(e.guid), e.rawFormat, e.rawMips, e.rawSize,
                SupportsRawFormat(e.rawFormat), (int)tex.format, tex.mipmapCount);
            string url = ResolveStreamingUrl(StreamDirRelativePath + (useRaw ? e.rawFile : e.file));

            bool ok;
            string error;
            byte[] data = null;
            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                loadingCount--;
                inflight.Remove(e.guid);

                ok = IsSuccess(req);
                error = req.error;
                if (ok)
                {
                    // 응답 본문을 managed 로 한 번 옮긴 뒤 using 를 닫아 UWR 의 네이티브 버퍼를 바로 해제한다
                    // (아래 디코드와 네이티브 사본·managed 사본이 겹치지 않게).
                    data = req.downloadHandler.data;
                }
            }

            if (!ok)
            {
                // 실패 → 인스턴스 예약 해제 후 다음 스캔에서 재시도(다른 인스턴스 포함).
                // 일시적 네트워크 실패일 수 있어 재시도하되, 상한 초과 시 포기(스텁 유지)해
                // 250ms 간격 무한 재다운로드(배터리/네트워크 소모)를 차단한다.
                restoredInstanceIds.Remove(tex != null ? tex.GetInstanceID() : 0);
                int dlFails = IncrementFailure(downloadFailCounts, e.guid);
                string failedFile = useRaw ? e.rawFile : e.file;
                if (dlFails >= MaxDownloadAttempts)
                {
                    pending.RemoveAll(x => x.guid == e.guid);
                    Debug.LogWarning($"[AIT-StreamingTexture] 로드 실패 {failedFile}: {error} — {dlFails}회 누적, 포기(스텁 유지)");
                }
                else
                {
                    Debug.LogWarning($"[AIT-StreamingTexture] 로드 실패 {failedFile}: {error} (재시도 {dlFails}/{MaxDownloadAttempts})");
                }

                yield break;
            }

            if (tex == null)
            {
                // 대상 텍스처가 그 사이 언로드됨 — 복원 불필요로 간주하고 pending 에서 제거.
                pending.RemoveAll(x => x.guid == e.guid);
                yield break;
            }

            bool markNonReadable = ShouldMarkNonReadable(e.nonReadable);
            bool applied = false;
            long heldBytes = 0;
            try
            {
                if (useRaw)
                {
                    // .br 정규화: 서버가 Content-Encoding 으로 이미 풀었으면 길이가 rawSize 와 같다.
                    int expectedRaw = e.rawSize;
                    byte[] raw = AITStreamingCodec.DecodePayload(
                        e.rawEncoding, data, d => d != null && d.Length == expectedRaw, e.name);
                    data = null;
                    if (raw != null && raw.Length == expectedRaw)
                    {
                        // 같은 ASTC 포맷의 스텁에 원본 블록을 그대로 올린다 — RGBA32 로 팽창하지 않는다.
                        tex.LoadRawTextureData(raw);
                        tex.Apply(false, markNonReadable);
                        applied = true;
                        heldBytes = markNonReadable ? 0 : expectedRaw;
                    }
                    else
                    {
                        Debug.LogWarning($"[AIT-StreamingTexture] raw 크기 불일치 {e.name}: 기대 {expectedRaw}B, 수신 {(raw != null ? raw.Length : 0)}B → PNG/JPG 폴백");
                    }
                }
                else
                {
                    // .br 외부화 페이로드 정규화: 서버가 Content-Encoding 으로 이미 해제했으면
                    // 그대로, raw brotli 면 여기서 해제(PNG/JPG 매직으로 판별). 무압축 엔트리는 no-op.
                    byte[] payload = AITStreamingCodec.DecodePayload(
                        e.encoding, data, AITStreamingCodec.LooksLikeImage, e.name);
                    data = null;

                    // 동일 차원 스텁(readable)에 실 픽셀을 in-place 업로드.
                    // LoadImage 는 PNG/JPG 디코드 후 GPU 업로드까지 수행 → 참조하는 Sprite/Material 자동 갱신.
                    // 원본이 non-readable 이었으면 markNonReadable 로 CPU 사본(w*h*4)을 남기지 않는다.
                    applied = tex.LoadImage(payload, markNonReadable);
                    payload = null;
                    if (applied)
                    {
                        heldBytes = markNonReadable ? 0 : (long)tex.width * tex.height * 4;

                        // 기대 차원: 의도적 다운스케일(sw/sh>0)이면 스트림 이미지 차원, 아니면 스텁=원본 차원.
                        // 균일 배율 다운스케일은 Sprite 의 정규화 UV(비율)를 보존하므로 렌더는 정상(저해상도일 뿐).
                        int expW = e.sw > 0 ? e.sw : e.width;
                        int expH = e.sh > 0 ? e.sh : e.height;
                        if (tex.width != expW || tex.height != expH)
                        {
                            // 기대와 다른 실제 차원 = 진짜 불일치(예: crunch maxTextureSize 캡 + 스트리밍 동시 사용으로
                            // 스텁/스트림 차원이 예기치 않게 어긋남). Sprite rect UV 가 틀어질 수 있음 — 경고만(복원은 유지).
                            Debug.LogWarning($"[AIT-StreamingTexture] 차원 불일치 {e.name}: 기대 {expW}x{expH} → 실제 {tex.width}x{tex.height} (스텁 {e.width}x{e.height})");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AIT-StreamingTexture] 복원 예외 {e.name}: {ex.Message}");
            }

            data = null;

            if (applied)
            {
                HeldBytes += heldBytes;
                HeldCount++;
                pending.RemoveAll(x => x.guid == e.guid);
                Debug.Log($"[AIT-StreamingTexture] 복원 {e.name} {tex.width}x{tex.height} path={(useRaw ? "raw" : "image")} fmt={tex.format} readable={(markNonReadable ? 0 : 1)}");
            }
            else if (useRaw && !string.IsNullOrEmpty(e.file))
            {
                // raw 복원 실패 → 이 엔트리는 이후 PNG/JPG 폴백으로만 시도한다(다음 스캔). 스텁은 손대지 않은 상태라 안전하다.
                rawFailed.Add(e.guid);
                restoredInstanceIds.Remove(tex.GetInstanceID());
                Debug.LogWarning($"[AIT-StreamingTexture] raw 복원 실패 {e.name} → PNG/JPG 폴백으로 재시도");
            }
            else
            {
                // 적용 실패 → 인스턴스 예약 해제. 같은 페이로드는 재시도해도 같게 실패하므로
                // (예: 영구 해독 불가 브로틀리, 손상 이미지) 소수 시도 후 포기(스텁 유지)해
                // 250ms 간격 무한 재다운로드 루프를 차단한다.
                restoredInstanceIds.Remove(tex.GetInstanceID());
                int apFails = IncrementFailure(applyFailCounts, e.guid);
                if (apFails >= MaxApplyAttempts)
                {
                    pending.RemoveAll(x => x.guid == e.guid);
                    Debug.LogWarning($"[AIT-StreamingTexture] 복원 적용 실패 {e.name} — {apFails}회(디코드 불가/손상 페이로드), 포기(스텁 유지)");
                }
            }
#else
            // AIT_HAS_UNITYWEBREQUEST/AIT_HAS_IMAGECONVERSION 미정의 시: Run() 진입부에서 이미 종료하므로 여기에 도달하지 않음.
            yield return null;
#endif
        }

        // ─────────────────────── 순수 판정(EditMode 테스트 대상) ───────────────────────

        /// <summary>
        /// 실제 동시 상한. 저사양 티어(1 이상)에서는 매니페스트 값과 무관하게 1 로 강제한다
        /// (디코드 버퍼·페이로드가 겹치는 힙 high-water 를 막는다). 그 외는 매니페스트 값, 없으면 기본값.
        /// </summary>
        internal static int ResolveMaxConcurrent(int manifestValue, int lowMemTier)
        {
            if (lowMemTier >= 1)
            {
                return 1;
            }

            return manifestValue > 0 ? manifestValue : DefaultMaxConcurrent;
        }

        /// <summary>원본이 non-readable 이었으면(nonReadable=1) 복원 후에도 non-readable 로 되돌린다. 필드 없음(0)은 기존 동작(readable).</summary>
        internal static bool ShouldMarkNonReadable(int nonReadableFlag)
        {
            return nonReadableFlag != 0;
        }

        /// <summary>
        /// raw(GPU 포맷 보존) 사본을 쓸 수 있는지. 매니페스트에 raw 가 있고, 이전에 실패하지 않았고, 기기가 그 포맷을 지원하며,
        /// 스텁의 실제 포맷·mip 수가 raw 와 같을 때만 true — 하나라도 어긋나면 PNG/JPG 폴백.
        /// </summary>
        internal static bool IsRawUsable(
            string rawFile, bool failedBefore, int expectedFormat, int expectedMips, int expectedSize,
            bool supportsFormat, int actualFormat, int actualMips)
        {
            if (string.IsNullOrEmpty(rawFile) || failedBefore)
            {
                return false;
            }

            if (expectedFormat <= 0 || expectedMips <= 0 || expectedSize <= 0)
            {
                return false;
            }

            return supportsFormat && actualFormat == expectedFormat && actualMips == expectedMips;
        }

        private static bool SupportsRawFormat(int format)
        {
            if (format <= 0)
            {
                return false;
            }

            try
            {
                return SystemInfo.SupportsTextureFormat((TextureFormat)format);
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        /// <summary>guid 의 실패 횟수를 1 올리고 누적값을 반환한다.</summary>
        private static int IncrementFailure(Dictionary<string, int> counts, string guid)
        {
            counts.TryGetValue(guid, out int n);
            counts[guid] = ++n;
            return n;
        }

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
