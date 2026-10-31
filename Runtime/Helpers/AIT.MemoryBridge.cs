// -----------------------------------------------------------------------
// <copyright file="AIT.MemoryBridge.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Memory Telemetry Bridge
// </copyright>
// -----------------------------------------------------------------------
//
// 템플릿의 ait-mem.js 가 모은 메모리 텔레메트리(WebAssembly.Memory.grow 횟수·소요 시간·크기, 압력 단계,
// 이전 세션 크래시 횟수)를 C# 이벤트로 노출한다. 노출만 한다 — 이 브릿지는 게임 동작을 바꾸지 않는다.
//
// 흐름:
//   1) 부팅 시 __AITMemoryBridge_Register 로 JS 쪽에 "받을 준비가 됐다"고 알린다. 텔레메트리가 꺼진 빌드
//      (window.AITMemory 없음 / memoryTelemetry=false)는 0 을 돌려주므로 GameObject 도 만들지 않는다.
//   2) ait-mem.js 가 setTimeout 으로 미룬 시점(wasm 호출 스택 밖)에 SendMessage("AITMemoryBridge", "OnMemoryEvent", json) 를 보낸다.
//   3) 수신 컴포넌트가 JSON 을 파싱해 AITMemoryBridge.OnMemoryEvent 를 발행한다.
//
// 자동 UnloadUnusedAssets 는 기본 꺼짐이다. 임계값(256/384MB)이 실기기 검증 전 추정치라서, 켜려면
// AITMemoryBridge.AutoUnloadUnusedAssets = true 로 명시해야 한다(켜도 30초에 한 번, critical 단계에서만).

using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Scripting;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace AppsInToss
{
    /// <summary>메모리 압력 단계. 임계값은 ait-mem.js 가 정한다(기본 256MB 이상 High, 384MB 이상 또는 grow 실패 시 Critical).</summary>
    public enum AITMemoryLevel
    {
        Ok = 0,
        High = 1,
        Critical = 2,
    }

    /// <summary>
    /// ait-mem.js 가 보내는 메모리 텔레메트리 요약. 필드명은 JSON 키와 같아야 한다(JsonUtility 매핑).
    /// </summary>
    [Serializable]
    public sealed class AITMemoryInfo
    {
        /// <summary>이벤트 종류: "grow" | "grow-failed" | "pressure" | "crash" | "snapshot".</summary>
        public string type;

        /// <summary>압력 단계 문자열: "ok" | "high" | "critical". 코드에서는 <see cref="Level"/> 을 쓴다.</summary>
        public string level;

        /// <summary>마지막으로 성공한 grow 직후의 wasm heap 크기(바이트). grow 가 한 번도 없었으면 0.</summary>
        public long heapBytes;

        /// <summary>관측한 heap 최대 크기(바이트).</summary>
        public long peakBytes;

        public int growCount;
        public int growFailures;

        /// <summary>grow 호출에 쓴 누적 시간(ms).</summary>
        public float growTotalMs;

        /// <summary>가장 오래 걸린 grow 한 번(ms).</summary>
        public float growMaxMs;

        /// <summary>이전 세션이 포그라운드에서 정상 종료 신호 없이 끝난 연속 횟수.</summary>
        public int crashCount;

        /// <summary>이전 세션이 백그라운드(hidden)에서 끝난 횟수. OS 가 숨은 탭을 정리한 경우일 수 있어 crashCount 와 따로 센다.</summary>
        public int bgKillCount;

        /// <summary>최근 10분 안에 부팅 중(first-frame 후 60초 안정 전) 사망한 부팅 횟수. ait-mem.js 의 localStorage 마커 기준(P0-1).</summary>
        public int bootFailCount;

        /// <summary>저사양 티어 0/1/2(0 = 정상). 부팅 때 한 번 정해지며 세션 중 바뀌지 않는다. <see cref="AITMemoryBridge.LowMemTier"/> 와 같은 값.</summary>
        public int lowMemTier;

        /// <summary><see cref="level"/> 문자열을 enum 으로 푼 값. 모르는 값은 Ok.</summary>
        public AITMemoryLevel Level
        {
            get { return AITMemoryBridge.ParseLevel(level); }
        }

        /// <summary>heapBytes 를 MB 로 환산한 값.</summary>
        public double HeapMegabytes
        {
            get { return heapBytes / 1048576.0; }
        }
    }

    /// <summary>
    /// 메모리 텔레메트리 브릿지. WebGL 빌드에서만 이벤트가 오며, 에디터와 다른 플랫폼에서는 아무 일도 하지 않는다.
    /// </summary>
    /// <remarks>
    /// 사용 예시:
    /// <code>
    /// AITMemoryBridge.OnMemoryEvent += info => {
    ///     if (info.Level == AITMemoryLevel.Critical) ReleaseOptionalCaches();
    /// };
    /// </code>
    /// 이벤트는 메인 스레드(SendMessage)에서 온다. 핸들러가 던진 예외는 삼키고 경고만 남긴다.
    /// </remarks>
    [Preserve]
    public static class AITMemoryBridge
    {
        /// <summary>JS 가 보내는 GameObject 이름. ait-mem.js 의 SendMessage 대상과 같아야 한다.</summary>
        internal const string ReceiverObjectName = "AITMemoryBridge";

        /// <summary>자동 UnloadUnusedAssets 의 최소 간격(초).</summary>
        public const float AutoUnloadMinIntervalSeconds = 30f;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int __AITMemoryBridge_Register();

        [DllImport("__Internal")]
        private static extern string __AITMemoryBridge_GetSnapshot();

        [DllImport("__Internal")]
        private static extern int __AITMemoryBridge_GetLowMemTier();
#endif

        /// <summary>
        /// 저사양 티어(0 = 정상, 1, 2). ait-mem.js 가 부팅 때 정한 window.AITMemory.lowMemTier 를 그대로 읽는다.
        /// WebGL 빌드가 아니거나, 텔레메트리/lowMemoryTier 설정이 꺼졌거나, 읽기에 실패하면 0 이다.
        /// 값은 세션 중 바뀌지 않으므로 필요한 쪽이 한 번 읽어 캐시해도 된다(후속 저메모리 최적화는 이 값으로 정책을 정한다).
        /// </summary>
        public static int LowMemTier
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                try
                {
                    return Mathf.Clamp(__AITMemoryBridge_GetLowMemTier(), 0, 2);
                }
                catch (Exception)
                {
                    return 0;
                }
#else
                return 0;
#endif
            }
        }

        /// <summary>메모리 이벤트. grow 는 1초 이상 간격으로 합쳐서, pressure/grow-failed/crash 는 발생 즉시(다음 태스크) 온다.</summary>
        public static event Action<AITMemoryInfo> OnMemoryEvent;

        /// <summary>마지막으로 받은 이벤트. 아직 받은 적이 없으면 null.</summary>
        public static AITMemoryInfo Latest { get; private set; }

        /// <summary>
        /// true 면 Critical 단계 이벤트에서 Resources.UnloadUnusedAssets() 를 부른다(30초에 한 번). 기본 false.
        /// 임계값이 실기기 검증 전 추정치라 기본으로 켜지 않는다.
        /// </summary>
        public static bool AutoUnloadUnusedAssets { get; set; }

        private static float _lastAutoUnloadTime = float.NegativeInfinity;

        /// <summary>level 문자열("ok"/"high"/"critical", 대소문자 무시)을 enum 으로 푼다. 모르는 값과 null 은 Ok.</summary>
        public static AITMemoryLevel ParseLevel(string level)
        {
            if (string.IsNullOrEmpty(level)) return AITMemoryLevel.Ok;
            if (string.Equals(level, "critical", StringComparison.OrdinalIgnoreCase)) return AITMemoryLevel.Critical;
            if (string.Equals(level, "high", StringComparison.OrdinalIgnoreCase)) return AITMemoryLevel.High;
            return AITMemoryLevel.Ok;
        }

        /// <summary>
        /// JS 가 보낸 JSON 을 파싱한다. null/빈 문자열/JSON 이 아닌 값/type 이 없는 객체는 false 를 돌려주며 예외를 던지지 않는다.
        /// 모르는 키(JS 쪽 진단 필드)는 무시한다.
        /// </summary>
        public static bool TryParse(string json, out AITMemoryInfo info)
        {
            info = null;
            if (string.IsNullOrWhiteSpace(json)) return false;

            try
            {
                var parsed = JsonUtility.FromJson<AITMemoryInfo>(json);
                if (parsed == null || string.IsNullOrEmpty(parsed.type)) return false;
                info = parsed;
                return true;
            }
            catch (Exception)
            {
                // JsonUtility 는 잘못된 JSON 에 ArgumentException 을 던진다. 텔레메트리는 부가 정보이므로 조용히 버린다.
                return false;
            }
        }

        /// <summary>
        /// JS 에서 받은 메시지 한 건을 처리한다: 파싱 → Latest 갱신 → 이벤트 발행(→ 옵션: UnloadUnusedAssets).
        /// 수신 컴포넌트가 부르는 진입점이고, 도구/테스트가 직접 넣어 볼 수도 있다. 파싱에 실패하면 false.
        /// </summary>
        public static bool Dispatch(string json)
        {
            if (!TryParse(json, out var info)) return false;

            Latest = info;

            var handlers = OnMemoryEvent;
            if (handlers != null)
            {
                foreach (var d in handlers.GetInvocationList())
                {
                    try
                    {
                        ((Action<AITMemoryInfo>)d)(info);
                    }
                    catch (Exception e)
                    {
                        // 구독자 하나의 예외가 다른 구독자와 JS 쪽 이벤트 흐름을 막지 않게 한다.
                        Debug.LogWarning("[AITMemoryBridge] 이벤트 핸들러 예외: " + e);
                    }
                }
            }

            MaybeUnloadUnusedAssets(info);
            return true;
        }

        /// <summary>
        /// 지금 JS 쪽 상태를 한 번 읽는다(이벤트를 기다리지 않는 폴링용). WebGL 빌드가 아니거나 텔레메트리가 꺼져 있으면 false.
        /// </summary>
        public static bool TryGetSnapshot(out AITMemoryInfo info)
        {
            info = null;
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                return TryParse(__AITMemoryBridge_GetSnapshot(), out info);
            }
            catch (Exception)
            {
                return false;
            }
#else
            return false;
#endif
        }

        private const double Mb = 1048576.0;

        private static string MbText(long bytes)
        {
            return (bytes / Mb).ToString("0.0", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Unity 측 메모리 분해 한 줄(P0-1 진단). 태그는 <c>[AIT-UnityMem]</c>.
        /// alloc/reserved/monoHeap/monoUsed/gfx 는 UnityEngine.Profiling.Profiler 값(WebGL 비개발 빌드는 0 으로 올 수 있다 — 그 자체가 정보다),
        /// streamTex/streamAudio/streamFont 는 스트리밍 헬퍼가 보유한 바이트와 개수, brotli 는 managed 해제 누계.
        /// wasmHeap 은 마지막으로 받은 텔레메트리의 heapBytes(없으면 생략). 예외는 던지지 않는다.
        /// </summary>
        internal static string FormatUnityBreakdown(string label)
        {
            var sb = new StringBuilder(256);
            sb.Append("[AIT-UnityMem] ").Append(label);
            try
            {
                sb.Append(" alloc=").Append(MbText(Profiler.GetTotalAllocatedMemoryLong())).Append("MB");
                sb.Append(" reserved=").Append(MbText(Profiler.GetTotalReservedMemoryLong())).Append("MB");
                sb.Append(" monoHeap=").Append(MbText(Profiler.GetMonoHeapSizeLong())).Append("MB");
                sb.Append(" monoUsed=").Append(MbText(Profiler.GetMonoUsedSizeLong())).Append("MB");
                sb.Append(" gfx=").Append(MbText(Profiler.GetAllocatedMemoryForGraphicsDriver())).Append("MB");
            }
            catch (Exception e)
            {
                sb.Append(" profiler=unavailable(").Append(e.GetType().Name).Append(')');
            }

            sb.Append(" streamTex=").Append(MbText(AITStreamingTexture.HeldBytes)).Append("MB(").Append(AITStreamingTexture.HeldCount).Append(')');
            sb.Append(" streamAudio=").Append(MbText(AITStreamingAudio.HeldBytes)).Append("MB(").Append(AITStreamingAudio.HeldCount).Append(')');
            sb.Append(" streamFont=").Append(MbText(AITStreamingFont.HeldBytes)).Append("MB(").Append(AITStreamingFont.HeldCount).Append(')');
            sb.Append(" brotli=").Append(AITStreamingCodec.ManagedBrotliCount).Append('x');
            if (AITStreamingCodec.ManagedBrotliCount > 0)
            {
                sb.Append('(').Append(MbText(AITStreamingCodec.ManagedBrotliInBytes)).Append("MB→")
                  .Append(MbText(AITStreamingCodec.ManagedBrotliOutBytes)).Append("MB,")
                  .Append(AITStreamingCodec.ManagedBrotliMs.ToString("0", CultureInfo.InvariantCulture)).Append("ms)");
            }

            var latest = Latest;
            if (latest != null)
            {
                sb.Append(" wasmHeap=").Append(MbText(latest.heapBytes)).Append("MB");
                sb.Append(" bootFail=").Append(latest.bootFailCount);
            }
            sb.Append(" lowMemTier=").Append(LowMemTier);
            return sb.ToString();
        }

        private static void MaybeUnloadUnusedAssets(AITMemoryInfo info)
        {
            if (!AutoUnloadUnusedAssets) return;
            if (info.Level != AITMemoryLevel.Critical) return;

            float now = Time.realtimeSinceStartup;
            if (now - _lastAutoUnloadTime < AutoUnloadMinIntervalSeconds) return;
            _lastAutoUnloadTime = now;

            Debug.Log("[AITMemoryBridge] 메모리 압력 critical — UnloadUnusedAssets 실행 (heap " +
                      info.HeapMegabytes.ToString("F1") + "MB)");
            Resources.UnloadUnusedAssets();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        [Preserve]
        private static void Bootstrap()
        {
            using var _hookTimer = AITHookTimer.Begin("MemoryBridge");

#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                // 분해 로그 리포터는 텔레메트리 설정과 무관하게 항상 만든다(all0 같은 대조군 빌드에서도 같은 줄을 남겨야 A/B 가 된다).
                var reporterGo = new GameObject("AITUnityMemReporter");
                UnityEngine.Object.DontDestroyOnLoad(reporterGo);
                reporterGo.AddComponent<AITUnityMemReporter>();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AITMemoryBridge] 분해 리포터 초기화 실패(무시): " + e.Message);
            }

            try
            {
                // 텔레메트리가 꺼진 빌드는 0 을 돌려준다 — 그러면 오브젝트도 만들지 않는다.
                if (__AITMemoryBridge_Register() == 0) return;

                var go = new GameObject(ReceiverObjectName);
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<AITMemoryBridgeReceiver>();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AITMemoryBridge] 초기화 실패(무시): " + e.Message);
            }
#endif
        }
    }

    /// <summary>
    /// Unity 측 메모리 분해를 두 번 로그로 남긴다(P0-1 진단): 씬 로드 직후 2프레임째(첫 프레임 근사)와 그로부터 30초 뒤.
    /// 로그만 남기고 게임 동작은 바꾸지 않는다. 두 번 남긴 뒤 자신을 파괴한다.
    /// </summary>
    [Preserve]
    internal sealed class AITUnityMemReporter : MonoBehaviour
    {
        internal const float LateDelaySeconds = 30f;

        private int _frames;
        private float _startTime = -1f;
        private bool _firstLogged;

        private void Update()
        {
            if (_startTime < 0f) _startTime = Time.realtimeSinceStartup;

            if (!_firstLogged)
            {
                if (++_frames < 2) return;
                _firstLogged = true;
                Debug.Log(AITMemoryBridge.FormatUnityBreakdown("t=first-frame"));
                return;
            }

            if (Time.realtimeSinceStartup - _startTime >= LateDelaySeconds)
            {
                Debug.Log(AITMemoryBridge.FormatUnityBreakdown("t=first-frame+30s"));
                Destroy(gameObject);
            }
        }
    }

    /// <summary>JS SendMessage 의 수신자. GameObject 이름은 <c>AITMemoryBridge</c> 여야 한다.</summary>
    [Preserve]
    internal sealed class AITMemoryBridgeReceiver : MonoBehaviour
    {
        [Preserve]
        public void OnMemoryEvent(string json)
        {
            AITMemoryBridge.Dispatch(json);
        }
    }
}
