using System;
using System.Reflection;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// WebGL "code optimization" 레버(Coatsink/Meta 로드타임 스택의 "Disk Size with LTO")를
    /// Unity 버전 차이를 흡수해 reflection으로 읽고 쓴다.
    ///
    /// 왜 IL2CPP 컴파일러 config(Master)가 아니라 이쪽인가:
    /// IL2CPP 컴파일러 config(Master)는 WebGL의 emscripten 최적화/LTO에 영향을 주지 않는다.
    /// 실측상 Master 빌드 산출물은 Release와 바이트 단위로 동일(no-op)했다. 실제로 LTO를
    /// 켜고 wasm 코드 크기를 줄이는 레버는 emscripten code optimization = "Disk Size with LTO"다.
    ///
    /// 왜 reflection인가:
    /// - Unity 2022.3/6(6000.x): UnityEditor.WebGL.UserBuildSettings.codeOptimization
    ///   (enum UnityEditor.WebGL.WasmCodeOptimization). WebGL 모듈 어셈블리에 있어 Editor
    ///   asmdef가 이를 참조한다는 보장이 없다 → 직접 참조 시 CS0103 위험.
    /// - 구버전(존재 시): PlayerSettings.WebGL.codeOptimization (enum WebGLCodeOptimization).
    ///   Unity 6에서 제거됨 → 직접 참조 시 컴파일 실패.
    /// 두 API의 enum 멤버명(DiskSizeLTO 등)이 동일하므로 멤버 "이름"으로 다룬다.
    ///
    /// codeOptimization API는 있지만 enum에 DiskSizeLTO/DiskSize/Size가 모두 없는 버전에서만
    /// fail-safe로 동작한다 — 설정을 건너뛰고 경고만 남기며 빌드는 계속된다(해당 버전에서 LTO 이득만 없음).
    /// 2021.3의 레거시 WebGLCodeOptimization={Speed,Size}은 DiskSizeLTO/DiskSize는 없지만
    /// Size는 있어 3순위 폴백으로 커버된다(아래 SizeFallback 참고).
    /// </summary>
    internal static class AITWebGLCodeOptimization
    {
        /// <summary>Coatsink/Meta 로드타임 스택의 권장값(enum 멤버 이름).</summary>
        public const string DiskSizeLTO = "DiskSizeLTO";

        /// <summary>
        /// DiskSizeLTO 미지원 버전에서 사용하는 1차 폴백 멤버 이름.
        /// DiskSize는 LTO 없는 disk-size 최적화로, DiskSizeLTO와 동일한 방향의 최선 근사다.
        /// </summary>
        internal const string DiskSizeFallback = "DiskSize";

        /// <summary>
        /// DiskSizeLTO/DiskSize 둘 다 미지원인 버전(2021.3 레거시)용 2차 폴백 멤버 이름.
        /// 근거: 2021.3의 PlayerSettings.WebGL.codeOptimization은 별도 레거시 enum
        /// WebGLCodeOptimization={Speed,Size}를 쓴다(2feeb437에서 실측 확인, 테스트 파일 참고).
        /// 이 enum의 "Size"는 "코드 크기를 우선해 최적화"라는 동일한 방향성을 가지므로
        /// 6000.x의 "DiskSize"(LTO 없는 disk-size 최적화)와 의미상 동치로 본다.
        /// 2022.3+는 UserBuildSettings.codeOptimization(WasmCodeOptimization) enum에
        /// DiskSizeLTO가 이미 정의되어 있어 1순위에서 매칭되고 이 3순위까지 내려오지 않는다.
        /// </summary>
        internal const string SizeFallback = "Size";

        private static bool _resolved;
        private static PropertyInfo _prop;

        /// <summary>
        /// 현재 Unity 버전에서 codeOptimization 정적 프로퍼티를 찾아 캐시한다.
        /// 신규 API(UserBuildSettings)를 먼저, 구버전 API(PlayerSettings.WebGL)를 폴백으로 본다.
        /// API가 없으면 null을 캐시.
        /// </summary>
        private static PropertyInfo ResolveProperty()
        {
            if (_resolved) return _prop;
            _resolved = true;

            // 신규 API: UnityEditor.WebGL.UserBuildSettings.codeOptimization (static)
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t;
                try { t = asm.GetType("UnityEditor.WebGL.UserBuildSettings"); }
                catch { continue; }
                if (t == null) continue;

                var p = t.GetProperty("codeOptimization",
                    BindingFlags.Public | BindingFlags.Static);
                if (p != null && p.PropertyType.IsEnum && p.CanRead && p.CanWrite)
                {
                    _prop = p;
                    return _prop;
                }
            }

            // 구버전 API: PlayerSettings.WebGL.codeOptimization (중첩 static 타입)
            var webglNested = typeof(UnityEditor.PlayerSettings)
                .GetNestedType("WebGL", BindingFlags.Public | BindingFlags.Static);
            var legacy = webglNested?.GetProperty("codeOptimization",
                BindingFlags.Public | BindingFlags.Static);
            if (legacy != null && legacy.PropertyType.IsEnum && legacy.CanRead && legacy.CanWrite)
            {
                _prop = legacy;
                return _prop;
            }

            return _prop; // null — 이 버전엔 API 없음
        }

        /// <summary>이 Unity 버전에서 codeOptimization 설정이 가능한지.</summary>
        public static bool IsSupported => ResolveProperty() != null;

        /// <summary>
        /// 지정한 멤버 이름이 현재 resolved enum에 정의되어 있는지.
        /// 버전별 폴백 우선순위(DiskSizeLTO/DiskSize/Size)를 구분해야 하는 테스트가 사용한다.
        /// </summary>
        internal static bool IsMemberDefined(string name)
        {
            var p = ResolveProperty();
            if (p == null || string.IsNullOrEmpty(name)) return false;
            return Enum.IsDefined(p.PropertyType, name);
        }

        /// <summary>
        /// resolved된 codeOptimization enum이 DiskSizeLTO/DiskSize/Size 중 하나라도 정의하는지.
        /// 이 셋이 모두 없는 버전에서만 TrySetDiskSizeLTO()가 설계상 false(fail-safe skip)를
        /// 반환한다. 이 경우를 구분해야 하는 호출자/테스트가 사용한다.
        /// </summary>
        internal static bool SupportsDiskSizeMember =>
            IsMemberDefined(DiskSizeLTO) || IsMemberDefined(DiskSizeFallback) || IsMemberDefined(SizeFallback);

        /// <summary>현재 값의 enum 멤버 이름. API 부재 시 null.</summary>
        public static string GetCurrentName()
        {
            var p = ResolveProperty();
            if (p == null) return null;
            try { return p.GetValue(null)?.ToString(); }
            catch { return null; }
        }

        /// <summary>
        /// 멤버 이름으로 값을 설정한다. name이 null/빈 문자열이거나 API 부재/멤버 미정의 시 false.
        /// fail-safe: 실패해도 예외를 던지지 않고 경고만 남긴다(빌드 계속).
        /// </summary>
        public static bool TrySetByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            var p = ResolveProperty();
            if (p == null) return false;

            try
            {
                if (!Enum.IsDefined(p.PropertyType, name))
                {
                    Debug.LogWarning(
                        $"[AIT] WebGL codeOptimization 멤버 미정의: '{name}' (enum={p.PropertyType.Name}) — 건너뜀");
                    return false;
                }

                var val = Enum.Parse(p.PropertyType, name);
                p.SetValue(null, val);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] WebGL codeOptimization 설정 실패('{name}'): {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// DiskSizeLTO(disk-size 최적화 + cross-module LTO)를 적용한다.
        /// DiskSizeLTO 멤버가 이 Unity 버전의 enum에 없으면 DiskSize(LTO 없는 disk-size)로,
        /// 그마저 없으면(2021.3 레거시) Size로 폴백한다.
        /// 세 멤버 모두 없거나 API 자체가 없으면 false를 반환하고 호출자가 경고를 남긴다.
        ///
        /// Sentry APPS-IN-TOSS-UNITY-SDK-10W: DiskSizeLTO 미정의 버전에서 경고만 남기고
        /// 설정을 완전 건너뛰던 동작을 DiskSize 폴백으로 개선.
        /// 2021.3(레거시 WebGLCodeOptimization={Speed,Size})은 DiskSize도 없어 이 개선의
        /// 사각지대로 남아 있었다 — Size 3순위 폴백으로 추가 커버.
        ///
        /// 기존 공개 시그니처는 유지한다 — 사다리 1순위(DiskSizeLTO)부터 시도하는
        /// <see cref="TrySetBestAvailable"/>(allowLto: true)로 위임.
        /// </summary>
        public static bool TrySetDiskSizeLTO() => TrySetBestAvailable(allowLto: true);

        /// <summary>
        /// codeOptimization 사다리를 적용한다. allowLto가 false면 1순위(DiskSizeLTO)를 건너뛰고
        /// 2순위(DiskSize, LTO 없는 disk-size 최적화)부터 3순위(Size, 2021.3 레거시)로만 폴백한다.
        ///
        /// Unity 6000.0.x의 emscripten 툴체인은 대형 프로젝트 whole-program LTO 링크에서
        /// wasm-ld가 빌드 머신 메모리를 초과해 OOM(SIGKILL)을 낼 수 있다(OOM 원인은 LTO 링크이지
        /// disk-size 최적화(-Os) 자체가 아니다) — 이 경로("LTO 제외 모드")가 그 회피 레버다.
        /// 버전 판정은 <see cref="ResolveDecision"/>이 담당하며 이 메서드는 버전을 모른다(순수 레버).
        /// </summary>
        internal static bool TrySetBestAvailable(bool allowLto)
        {
            var p = ResolveProperty();
            if (p == null) return false;

            // 1순위: DiskSizeLTO (cross-module LTO 포함, 권장) — allowLto=false면 건너뛴다.
            if (allowLto && Enum.IsDefined(p.PropertyType, DiskSizeLTO))
                return TrySetByName(DiskSizeLTO);

            // 2순위: DiskSize (LTO 없는 disk-size 최적화, 동일 방향의 폴백)
            if (Enum.IsDefined(p.PropertyType, DiskSizeFallback))
            {
                Debug.Log(allowLto
                    ? $"[AIT] WebGL codeOptimization: '{DiskSizeLTO}' 미지원 버전 — '{DiskSizeFallback}'(폴백) 적용"
                    : $"[AIT] WebGL codeOptimization: LTO 제외 모드 — '{DiskSizeFallback}'(비-LTO) 적용");
                return TrySetByName(DiskSizeFallback);
            }

            // 3순위: Size (2021.3 레거시 enum 전용, DiskSize와 의미상 동치인 크기 우선 최적화)
            if (Enum.IsDefined(p.PropertyType, SizeFallback))
            {
                Debug.Log(
                    $"[AIT] WebGL codeOptimization: '{DiskSizeLTO}'/'{DiskSizeFallback}' 미지원 버전 — '{SizeFallback}'(폴백) 적용");
                return TrySetByName(SizeFallback);
            }

            // 셋 다 없는 경우: 호출자가 별도 경고를 남기므로 여기서는 false만 반환
            return false;
        }

        /// <summary>
        /// CI/로컬에서 code optimization 정책을 덮어쓰는 환경변수 키.
        /// 'off'/'none'/'false'(대소문자 무관) = 모든 Unity 버전에서 적용 자체를 끔(킬스위치),
        /// 'auto'/빈값 = 기본 정책(editorConfig + 버전 게이트), 그 외 = 해당 enum 멤버를 강제
        /// (예: 6000.0에서 DiskSizeLTO를 재현/반증). 관용구 출처: AITBuildInitializer의
        /// AIT_IL2CPP_CONFIGURATION 오버라이드.
        /// </summary>
        internal const string EnvOverrideKey = "AIT_WEBGL_CODE_OPTIMIZATION";

        /// <summary>
        /// Unity 6000.0.x emscripten 툴체인은 대형 프로젝트 whole-program LTO 링크에서
        /// wasm-ld가 빌드 머신 메모리를 초과해 OOM(SIGKILL "Killed: 9")으로 빌드를 깨뜨린다.
        /// OOM 원인은 LTO 링크이지 disk-size 최적화(-Os) 자체가 아니므로 사다리 1순위만 제외한다.
        /// 6000.3의 신형 툴체인은 동일한 DiskSizeLTO를 정상 링크함을 실측 확인했다(6000.0 한정 위험).
        /// </summary>
        internal static bool IsLtoRiskyVersion(string unityVersion) =>
            !string.IsNullOrEmpty(unityVersion) && unityVersion.StartsWith("6000.0.", StringComparison.Ordinal);

        /// <summary>
        /// <see cref="ResolveDecision"/>의 반환값. code optimization 적용 여부/LTO 허용 여부/
        /// env로 강제된 멤버 이름을 담는다(순수 데이터, 부수효과 없음).
        /// </summary>
        internal readonly struct Decision
        {
            /// <summary>code optimization을 적용할지.</summary>
            public readonly bool Apply;

            /// <summary>사다리 1순위(DiskSizeLTO) 허용 여부. false면 DiskSize→Size로만 폴백.</summary>
            public readonly bool AllowLto;

            /// <summary>env로 강제된 enum 멤버 이름(없으면 null). Apply=true일 때만 의미가 있다.</summary>
            public readonly string ForcedMember;

            public Decision(bool apply, bool allowLto, string forcedMember)
            {
                Apply = apply;
                AllowLto = allowLto;
                ForcedMember = forcedMember;
            }
        }

        /// <summary>
        /// editorConfig(-1 자동/0 미적용/1 적용의 의미상 "적용 여부") + Unity 버전 + 환경변수로
        /// code optimization 적용 정책을 결정하는 순수 함수. PlayerSettings/Application 등 실제 API를
        /// 건드리지 않으므로 실행 중인 Unity 버전과 무관하게 EditMode에서 데이터 주도로 검증할 수 있다.
        /// </summary>
        /// <param name="configApply">editorConfig.webGLCodeOptimization != 0 (GUI가 적용을 원하는지).</param>
        /// <param name="unityVersion">Application.unityVersion.</param>
        /// <param name="envValue">EnvOverrideKey 환경변수 원본 값(null 허용).</param>
        internal static Decision ResolveDecision(bool configApply, string unityVersion, string envValue)
        {
            string env = (envValue ?? string.Empty).Trim();

            // 킬스위치: off/none/false는 GUI 설정(configApply)이나 Unity 버전과 무관하게
            // *모든* Unity 버전에서 code optimization 적용 자체를 끈다. 6000.0 한정으로는
            // 과거(완전 skip) 동작을 정확히 복원하지만, 2021.3/2022.3/6000.2/6000.3 등 다른
            // 버전에서는 오늘 적용되던 DiskSizeLTO/폴백이 이 값으로 "새로" 꺼진다는 점에 주의.
            if (env.Equals("off", StringComparison.OrdinalIgnoreCase) ||
                env.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                env.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return new Decision(apply: false, allowLto: false, forcedMember: null);
            }

            // 버전 게이트: 6000.0.x만 LTO 위험(OOM) — 그 외 버전은 기존과 동일하게 LTO 허용.
            bool allowLto = !IsLtoRiskyVersion(unityVersion);

            if (env.Length == 0 || env.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                // 기본 정책: GUI(editorConfig) 값 그대로 + 버전 게이트만 적용.
                return new Decision(apply: configApply, allowLto: allowLto, forcedMember: null);
            }

            // 명시적 멤버 강제(운영자 오버라이드, 예: 6000.0에서 DiskSizeLTO를 다시 켜 OOM 재현/반증):
            // GUI '미적용'(configApply == false)도 무시하고 적용한다 — 의도된 동작이다.
            //
            // AllowLto는 여기서도 버전 게이트 값을 그대로 유지해야 한다(무조건 true로 고정하면 안 됨).
            // 이유: 강제 멤버명이 오타이거나 이 버전 enum에 없으면 TrySetByName이 false를 반환하고,
            // 호출자(AITBuildInitializer)는 일반 사다리 TrySetBestAvailable(decision.AllowLto)로
            // 폴백한다. 이때 AllowLto가 true로 고정돼 있으면 6000.0에서 강제가 실패한 뒤 폴백
            // 사다리가 1순위 DiskSizeLTO(OOM 레버)부터 다시 타 조용히 재활성화된다 — 6000.0에서는
            // 강제 실패 후에도 폴백 사다리가 여전히 DiskSize(2순위)부터 타야 한다.
            return new Decision(apply: true, allowLto: allowLto, forcedMember: env);
        }
    }
}
