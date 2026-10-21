using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor.ErrorTracker
{
    /// <summary>
    /// SDK 자체 패키지 경로(im.toss/com.toss.apps-in-toss)에서 발생하는 C# 컴파일 에러(error CS####)를
    /// "일시적 임포트 아티팩트"와 "실제 SDK 자체 컴파일 회귀"로 판별하는 defer+confirm 게이트.
    ///
    /// <para><b>배경 (#42)</b></para>
    /// UPM 임포트/재컴파일 도중, 심볼을 참조하는 어셈블리(예: ErrorTracker)가 그 심볼을 정의하는
    /// 어셈블리보다 먼저 컴파일되는 일시적 순서 문제로 "error CS0103 The name 'AITVersion' does not
    /// exist" 같은 컴파일 에러가 잠깐 발생할 수 있다. 이 에러들은 임포트가 정상 마무리되며 올바른 순서로
    /// 재컴파일되면 사라지는 transient 아티팩트로, 0-user 노이즈로 Sentry에 새어 들어간다
    /// (SDK-133/130/131/12Z/12P/12W/12V/12T/12S, 2026-06 확정). 반대로 우리가 실제로 깨진 SDK 소스를
    /// 배포하면(실 회귀) 컴파일이 끝내 성공하지 못한다.
    ///
    /// <para><b>판별 원리</b></para>
    /// "성공적인 도메인 리로드가 일어났다" == "SDK가 깨끗하게 컴파일됐다"는 신호다. 따라서:
    /// <list type="bullet">
    ///   <item>에러를 즉시 캡처하지 않고 <see cref="Defer"/>로 SessionState에 보류 기록한다(관측 시각 +
    ///   당시 reload 세대 + Unity/SDK 버전).</item>
    ///   <item><see cref="InitializeOnLoadAttribute"/> 정적 생성자는 도메인이 성공적으로 (재)로드될
    ///   때마다 실행되므로, 이를 "성공 리로드 카운터"로 사용한다. 보류 기록보다 카운터가 앞서 있으면
    ///   (= 그 사이 성공 리로드 발생) transient로 보고 <b>드롭</b>한다.</item>
    ///   <item>실 회귀는 컴파일이 끝내 실패해 도메인 리로드가 일어나지 않으므로 카운터가 전진하지 않는다.
    ///   에디터가 settle(컴파일/임포트 종료)된 뒤 짧은 grace를 넘기면 보류 기록을 <b>캡처</b>한다.</item>
    /// </list>
    ///
    /// <para><b>수용한 트레이드오프 (Option 2)</b></para>
    /// 실 SDK 자체 컴파일 회귀는 오늘보다 한 reload/settle 사이클 늦게(수 초) Sentry에 보고된다. 그 대가로
    /// transient 임포트 노이즈를 near-zero false-negative로 차단한다. SessionState 보류면은 버전 불일치 +
    /// wall-clock staleness(30분)로 항상 경계되며, 에디터 종료 시 SessionState가 비워져 누수가 없다.
    /// </summary>
    [InitializeOnLoad]
    internal static class AITSdkSelfCompileGuard
    {
        // SessionState 키 (도메인 리로드 생존, 에디터 종료 시 소거). v1 접미사로 포맷 변경 시 자연 무효화.
        private const string PendingKey = "AIT_SdkSelfCompileGuard_Pending_v1";
        private const string ReloadGenKey = "AIT_SdkSelfCompileGuard_ReloadGen_v1";

        // 보류 기록이 이 시간을 넘도록 확정(캡처/드롭)되지 못하면 stale로 보고 드롭한다(무한 보류 방지).
        // AITBuildDiagnostics와 동일한 30분 신선도 창.
        internal static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(30);

        // settle 후 캡처까지의 디바운스. 임포트/컴파일이 끝났음(settled)을 이미 게이트하므로 짧게 둔다.
        internal static readonly TimeSpan MinGrace = TimeSpan.FromSeconds(5);

        // 보류면 폭주 방지 상한.
        private const int MaxPending = 50;

        // 메시지/스택 보관 길이 상한(CaptureError의 1000자 절단과 정합).
        private const int MaxMessageChars = 1000;
        private const int MaxStackChars = 2000;

        // update 폴링 throttle(초). 도메인-로컬 타이머이므로 SessionState 불필요.
        private const double SweepIntervalSeconds = 2.0;
        private static double _lastSweepAt = -1.0;

        // 신선도 판정 시계. 프로덕션은 UtcNow, 테스트는 가짜 시각 주입.
        internal static Func<DateTime> UtcNowProvider = () => DateTime.UtcNow;

        // 에디터 settle 여부 프로브. 컴파일/임포트가 진행 중이면 판정을 보류한다.
        internal static Func<bool> IsEditorSettled = () =>
            !EditorApplication.isCompiling && !EditorApplication.isUpdating;

        /// <summary>
        /// 게이트의 순수 판정 결과.
        /// </summary>
        internal enum GateDecision
        {
            /// <summary>아직 확정 불가 — 보류 유지.</summary>
            Wait,

            /// <summary>일시적 아티팩트로 확정 — 드롭(캡처하지 않음).</summary>
            Drop,

            /// <summary>실 회귀로 확정 — 캡처.</summary>
            Capture
        }

        static AITSdkSelfCompileGuard()
        {
            // 정적 생성자는 도메인이 성공적으로 (재)로드될 때마다 실행된다 == "성공 리로드" 신호.
            // 카운터를 전진시켜, 직전 도메인에서 보류된 transient 기록이 다음 Sweep에서 드롭되게 한다.
            SetReloadGen(GetReloadGen() + 1);

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;

            // 리로드 직후에도 한 번 즉시 정리해, 직전 도메인의 transient 기록을 빠르게 드롭한다.
            EditorApplication.delayCall += () =>
            {
                try { Sweep(); }
                catch { /* 텔레메트리 게이트는 에디터를 깨뜨리면 안 됨 */ }
            };
        }

        /// <summary>
        /// 메시지가 "SDK 자체 패키지 경로의 C# 컴파일 에러"인지 판정한다. SDK 패키지 경로
        /// (im.toss/com.toss.apps-in-toss)를 포함하는 "error CS####"만 매칭한다. 사용자 프로젝트 컴파일
        /// 에러(SDK 브레이킹 체인지)는 SDK 패키지 경로를 포함하지 않으므로 여기에 잡히지 않는다
        /// (그쪽은 <c>AITEditorErrorTracker.IsSdkBreakingChangeCompileError</c>가 담당).
        /// </summary>
        internal static bool IsSdkSelfCompileError(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            // C# 컴파일러 '에러'(error CS####)만. 경고(warning CS####)는 컴파일을 막지 않으므로 제외.
            if (message.IndexOf("error CS", StringComparison.Ordinal) < 0)
                return false;

            // SDK 자체 패키지 경로에서 발생한 컴파일 에러여야 한다.
            return message.IndexOf("im.toss.apps-in-toss", StringComparison.Ordinal) >= 0
                || message.IndexOf("com.toss.apps-in-toss", StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// SDK 자체 컴파일 에러를 즉시 캡처하지 않고 보류 기록으로 적재한다. 동일 fingerprint가 이미
        /// 보류 중이면 무시(최초 관측 시각/reload 세대를 보존해야 판정이 정확하므로 갱신하지 않음).
        /// </summary>
        internal static void Defer(string message, string stackTrace)
        {
            try
            {
                string fp = Fingerprint(message);
                var store = Load();

                foreach (var r in store.records)
                {
                    if (r.fp == fp)
                        return; // 이미 보류 중
                }

                store.records.Add(new PendingRecord
                {
                    fp = fp,
                    firstSeenUnixMs = ToUnixMs(UtcNowProvider()),
                    reloadGen = GetReloadGen(),
                    unityVer = Application.unityVersion,
                    sdkVer = SafeSdkVersion(),
                    message = Truncate(message, MaxMessageChars),
                    stackTrace = Truncate(stackTrace, MaxStackChars)
                });

                // 폭주 방지: 가장 오래된 항목부터 버린다.
                while (store.records.Count > MaxPending)
                    store.records.RemoveAt(0);

                Save(store);
            }
            catch
            {
                // 게이트 실패가 로그 콜백을 깨뜨리면 안 됨 — best-effort.
            }
        }

        /// <summary>
        /// 보류 기록 전체를 훑어 확정(캡처) / 드롭 / 보류 유지로 분류한다. 캡처 대상은
        /// <c>AITEditorErrorTracker.CaptureConfirmedSdkSelfCompileError</c>로 전달한다.
        /// </summary>
        internal static void Sweep()
        {
            var store = Load();
            if (store.records.Count == 0)
                return;

            int reloadGen = GetReloadGen();
            bool settled = SafeIsSettled();
            DateTime now = UtcNowProvider();
            string unityVer = Application.unityVersion;
            string sdkVer = SafeSdkVersion();

            var survivors = new List<PendingRecord>(store.records.Count);
            var toCapture = new List<PendingRecord>();

            foreach (var r in store.records)
            {
                GateDecision decision = Decide(
                    r.firstSeenUnixMs, r.reloadGen, r.unityVer, r.sdkVer,
                    now, reloadGen, settled, unityVer, sdkVer,
                    StaleThreshold, MinGrace);

                if (decision == GateDecision.Capture)
                    toCapture.Add(r);
                else if (decision == GateDecision.Wait)
                    survivors.Add(r);
                // Drop → 어느 목록에도 넣지 않음
            }

            if (survivors.Count != store.records.Count)
                Save(new PendingStore { records = survivors });

            // 영속 상태를 먼저 정리한 뒤 캡처(캡처 중 재진입에도 보류면이 일관되도록).
            foreach (var r in toCapture)
                AITEditorErrorTracker.CaptureConfirmedSdkSelfCompileError(r.message, r.stackTrace);
        }

        /// <summary>
        /// 클럭/에디터 의존성을 모두 인자로 받는 순수 판정 함수(EditMode 단위 테스트 대상).
        /// </summary>
        /// <param name="firstSeenUnixMs">보류 기록의 최초 관측 시각(Unix ms, UTC).</param>
        /// <param name="recordReloadGen">최초 관측 당시의 reload 세대.</param>
        /// <param name="recordUnityVer">최초 관측 당시 Unity 버전.</param>
        /// <param name="recordSdkVer">최초 관측 당시 SDK 버전.</param>
        /// <param name="nowUtc">현재 시각(UTC).</param>
        /// <param name="currentReloadGen">현재 reload 세대.</param>
        /// <param name="isSettled">에디터가 컴파일/임포트를 마쳤는지.</param>
        /// <param name="currentUnityVer">현재 Unity 버전.</param>
        /// <param name="currentSdkVer">현재 SDK 버전.</param>
        /// <param name="staleThreshold">미확정 보류의 최대 수명.</param>
        /// <param name="minGrace">settle 후 캡처까지의 디바운스.</param>
        internal static GateDecision Decide(
            long firstSeenUnixMs, int recordReloadGen, string recordUnityVer, string recordSdkVer,
            DateTime nowUtc, int currentReloadGen, bool isSettled, string currentUnityVer, string currentSdkVer,
            TimeSpan staleThreshold, TimeSpan minGrace)
        {
            // 버전 전환 자체가 일시적 컴파일 에러의 원인일 수 있고, 더는 동일 맥락이 아니므로 드롭.
            if (!string.Equals(recordUnityVer, currentUnityVer, StringComparison.Ordinal)
                || !string.Equals(recordSdkVer, currentSdkVer, StringComparison.Ordinal))
                return GateDecision.Drop;

            DateTime firstSeen = FromUnixMs(firstSeenUnixMs);
            TimeSpan age = nowUtc - firstSeen;

            // 너무 오래된 미확정 기록은 드롭(무한 보류 방지).
            if (age > staleThreshold)
                return GateDecision.Drop;

            // 성공적인 도메인 리로드가 일어났다 == SDK가 깨끗하게 컴파일됐다 == transient. 드롭.
            if (currentReloadGen > recordReloadGen)
                return GateDecision.Drop;

            // 컴파일/임포트가 아직 진행 중이면 판정 보류.
            if (!isSettled)
                return GateDecision.Wait;

            // settle됐고 성공 리로드 없이 grace를 넘겼다 == 컴파일이 끝내 실패한 실 회귀. 캡처.
            if (age >= minGrace)
                return GateDecision.Capture;

            return GateDecision.Wait;
        }

        // ── 런타임 배선 ──────────────────────────────────────────────────────

        private static void OnEditorUpdate()
        {
            double t = EditorApplication.timeSinceStartup;
            if (_lastSweepAt >= 0 && (t - _lastSweepAt) < SweepIntervalSeconds)
                return;
            _lastSweepAt = t;

            try { Sweep(); }
            catch { /* 텔레메트리 게이트는 에디터를 깨뜨리면 안 됨 */ }
        }

        // ── SessionState 영속 ────────────────────────────────────────────────

        [Serializable]
        private class PendingRecord
        {
            public string fp;
            public long firstSeenUnixMs;
            public int reloadGen;
            public string unityVer;
            public string sdkVer;
            public string message;
            public string stackTrace;
        }

        [Serializable]
        private class PendingStore
        {
            public List<PendingRecord> records = new List<PendingRecord>();
        }

        private static PendingStore Load()
        {
            string json = SessionState.GetString(PendingKey, null);
            if (string.IsNullOrEmpty(json))
                return new PendingStore();
            try
            {
                var store = JsonUtility.FromJson<PendingStore>(json);
                if (store == null)
                    return new PendingStore();
                if (store.records == null)
                    store.records = new List<PendingRecord>();
                return store;
            }
            catch
            {
                return new PendingStore();
            }
        }

        private static void Save(PendingStore store)
        {
            if (store.records.Count == 0)
            {
                SessionState.EraseString(PendingKey);
                return;
            }
            SessionState.SetString(PendingKey, JsonUtility.ToJson(store));
        }

        private static int GetReloadGen() => SessionState.GetInt(ReloadGenKey, 0);

        private static void SetReloadGen(int v) => SessionState.SetInt(ReloadGenKey, v);

        // ── 헬퍼 ────────────────────────────────────────────────────────────

        private static bool SafeIsSettled()
        {
            try { return IsEditorSettled(); }
            catch { return false; } // 프로브 실패 시 보수적으로 미settle 취급(캡처 보류).
        }

        private static string SafeSdkVersion()
        {
            try { return AITVersion.Version; }
            catch { return string.Empty; }
        }

        // 세션 내 보류면 dedup 키. 한 에디터 세션에서 동일 컴파일 에러는 동일한 텍스트(같은 PackageCache
        // 해시·라인·CS코드)로 반복되므로 메시지 원문을 그대로 해싱한다 — 숫자 정규화를 하면 CS0103과 CS1003
        // 같은 서로 다른 에러 코드가 한 키로 충돌해(두 번째 실 회귀가 dedup으로 누락) false-negative가 난다.
        // AppDomain 시드에 흔들리는 string.GetHashCode 대신 도메인 리로드를 넘어 안정적인 결정적 FNV-1a를
        // 16진 문자열로 쓴다. (이 키는 dedup 전용이며 Sentry 그룹화와 무관 — 실제 캡처 fingerprint는
        // AITEditorErrorTracker.BuildNormalizedFingerprint가 별도로 부여한다.)
        private static string Fingerprint(string message)
        {
            if (string.IsNullOrEmpty(message))
                return "0";

            const ulong fnvOffset = 14695981039346656037UL;
            const ulong fnvPrime = 1099511628211UL;
            ulong hash = fnvOffset;
            foreach (char c in message)
                hash = (hash ^ c) * fnvPrime;
            return hash.ToString("x");
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max)
                return s;
            return s.Substring(0, max);
        }

        private static long ToUnixMs(DateTime utc)
        {
            return new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        }

        private static DateTime FromUnixMs(long ms)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
        }
    }
}
