using UnityEditor;
using UnityEngine;

namespace AppsInToss
{
    /// <summary>
    /// Unity 버전별 빌드 프리셋
    /// </summary>
    public static class AITBuildPresets
    {
        /// <summary>
        /// 현재 Unity 버전에 맞는 최적화 설정 자동 적용
        /// </summary>
        public static void ApplyOptimalSettings()
        {
            string unityVersion = Application.unityVersion;

            Debug.Log($"[AIT] Unity 버전 감지: {unityVersion}");

#if UNITY_2023_3_OR_NEWER
            ApplyUnity2023_3Settings();
#elif UNITY_2022_3_OR_NEWER
            ApplyUnity2022_3Settings();
#elif UNITY_2021_2_OR_NEWER
            ApplyUnity2021_3Settings();
#elif UNITY_2020_3_OR_NEWER
            ApplyUnity2020_3Settings();
#else
            ApplyUnity2019_4Settings();
#endif
        }

        /// <summary>
        /// Unity 2023.3 LTS (Unity 6) - 최우선 권장
        /// </summary>
        private static void ApplyUnity2023_3Settings()
        {
            Debug.Log("[AIT] Unity 2023.3 (Unity 6) 최적화 설정 적용 중...");

            // Unity 6 기반 최고 성능 최적화 (문서 기준)
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.memorySize = 1024; // 더 큰 메모리 풀 지원
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.threadsSupport = true; // Unity 6에서 향상된 멀티스레딩
            PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;

#if UNITY_2023_1_OR_NEWER
            PlayerSettings.WebGL.wasm2023 = true; // 스트리밍 최적화
#endif

            // Unity 6 전용 고급 설정
            PlayerSettings.WebGL.dataCaching = true;

#if UNITY_2021_2_OR_NEWER
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.External;
#endif

            // 엔진 코드 스트리핑
            PlayerSettings.stripEngineCode = true;

            Debug.Log("[AIT] ✓ Unity 2023.3 최적화 완료");
            Debug.Log("[AIT]   - Brotli 압축, 1024MB 메모리, High Performance 모드");
        }

        /// <summary>
        /// Unity 2022.3 LTS - 안정적 검증된 선택
        /// </summary>
        private static void ApplyUnity2022_3Settings()
        {
            Debug.Log("[AIT] Unity 2022.3 LTS 최적화 설정 적용 중...");

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.memorySize = 512;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.threadsSupport = false; // 브라우저 호환성

            // 공통 설정
            PlayerSettings.WebGL.dataCaching = false;
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.External;
#endif
            PlayerSettings.stripEngineCode = true;

            Debug.Log("[AIT] ✓ Unity 2022.3 최적화 완료");
            Debug.Log("[AIT]   - Brotli 압축, 512MB 메모리");
        }

        /// <summary>
        /// Unity 2021.3 LTS - 안정적 선택
        /// </summary>
        private static void ApplyUnity2021_3Settings()
        {
            Debug.Log("[AIT] Unity 2021.3 LTS 최적화 설정 적용 중...");

            // 안정적 설정
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.memorySize = 256;
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;

#if UNITY_2021_2_OR_NEWER
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.External;
#endif

            PlayerSettings.WebGL.dataCaching = false;
            PlayerSettings.stripEngineCode = true;

#pragma warning disable CS0618
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.High);
#pragma warning restore CS0618

            Debug.Log("[AIT] ✓ Unity 2021.3 최적화 완료");
            Debug.Log("[AIT]   - Gzip 압축, 256MB 메모리");
        }

        /// <summary>
        /// Unity 2020.3 LTS - 호환성 위주
        /// </summary>
        private static void ApplyUnity2020_3Settings()
        {
            Debug.Log("[AIT] Unity 2020.3 LTS 최적화 설정 적용 중...");

            // 보수적 설정
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.memorySize = 256;
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
            PlayerSettings.WebGL.dataCaching = false;

#pragma warning disable CS0618
            PlayerSettings.WebGL.debugSymbols = true;
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.Medium);
#pragma warning restore CS0618

            Debug.Log("[AIT] ✓ Unity 2020.3 최적화 완료");
            Debug.Log("[AIT]   - Gzip 압축, 256MB 메모리 (호환성 모드)");
        }

        /// <summary>
        /// Unity 2019.4 LTS - 제한적 지원
        /// </summary>
        private static void ApplyUnity2019_4Settings()
        {
            Debug.Log("[AIT] Unity 2019.4 최적화 설정 적용 중...");
            Debug.LogWarning("[AIT] ⚠️  Unity 2019.4는 제한적 지원입니다. Unity 2022.3 이상으로 업그레이드를 권장합니다.");

            // 최소 설정
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.memorySize = 256;
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
            PlayerSettings.WebGL.dataCaching = false;

#pragma warning disable CS0618
            PlayerSettings.WebGL.debugSymbols = true;
#pragma warning restore CS0618

            Debug.Log("[AIT] ✓ Unity 2019.4 최적화 완료 (최소 설정)");
        }

        /// <summary>
        /// 현재 Unity 버전 정보 출력
        /// </summary>
        public static string GetUnityVersionInfo()
        {
#if UNITY_2023_3_OR_NEWER
            return "Unity 2023.3+ (Unity 6) - 최우선 권장 ⭐⭐⭐⭐⭐";
#elif UNITY_2022_3_OR_NEWER
            return "Unity 2022.3 LTS - 안정적 검증됨 ⭐⭐⭐⭐";
#elif UNITY_2021_2_OR_NEWER
            return "Unity 2021.3 LTS - 안정적 ⭐⭐⭐";
#elif UNITY_2020_3_OR_NEWER
            return "Unity 2020.3 LTS - 호환성 위주 ⭐⭐";
#else
            return "Unity 2019.4 이하 - 제한적 지원 ⚠️";
#endif
        }

        /// <summary>
        /// 사용자 정의 프리셋 적용
        /// </summary>
        public static void ApplyCustomPreset(BuildPreset preset)
        {
            switch (preset)
            {
                case BuildPreset.Production:
                    ApplyProductionPreset();
                    break;
                case BuildPreset.Development:
                    ApplyDevelopmentPreset();
                    break;
            }
        }

        /// <summary>
        /// Production 프리셋 - 프로덕션 최적화
        /// </summary>
        private static void ApplyProductionPreset()
        {
            Debug.Log("[AIT] Production 프리셋 적용 중...");

#if UNITY_2023_3_OR_NEWER
            // Unity 2023.3+ (Unity 6): 문서 기준 최고 성능 설정
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.memorySize = 1024; // 더 큰 메모리 풀 지원
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.threadsSupport = true; // Unity 6에서 향상된 멀티스레딩
            PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;

#if UNITY_2023_1_OR_NEWER
            PlayerSettings.WebGL.wasm2023 = true;
#endif

            PlayerSettings.WebGL.dataCaching = true;

#if UNITY_2021_2_OR_NEWER
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.External;
#endif

            Debug.Log("[AIT] ✓ Production 설정: Brotli 압축, 1024MB 메모리, High Performance");

#elif UNITY_2022_3_OR_NEWER
            // Unity 2022.3 LTS: 문서 기준 안정적 설정
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.memorySize = 512;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.threadsSupport = false;

            PlayerSettings.WebGL.dataCaching = false;

#if UNITY_2021_2_OR_NEWER
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.External;
#endif

            Debug.Log("[AIT] ✓ Production 설정: Brotli 압축, 512MB 메모리");

#elif UNITY_2021_2_OR_NEWER
            // Unity 2021.3 LTS: 문서 기준 호환성 설정
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.memorySize = 256;
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;

            PlayerSettings.WebGL.dataCaching = false;

#if UNITY_2021_2_OR_NEWER
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.External;
#endif

            Debug.Log("[AIT] ✓ Production 설정: Gzip 압축, 256MB 메모리");

#else
            // Unity 2020.3 이하
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.memorySize = 256;
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;

            PlayerSettings.WebGL.dataCaching = false;

            Debug.Log("[AIT] ✓ Production 설정: Gzip 압축, 256MB 메모리");
#endif

            // 공통 최적화 설정
            PlayerSettings.stripEngineCode = true;
        }

        /// <summary>
        /// Development 프리셋 - 디버깅 최적화
        /// </summary>
        private static void ApplyDevelopmentPreset()
        {
            Debug.Log("[AIT] Development 프리셋 적용 중...");

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled; // 빠른 빌드
            PlayerSettings.WebGL.memorySize = 512; // 개발 중 여유있는 메모리
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithStacktrace;
            PlayerSettings.stripEngineCode = false; // 디버깅 용이
            PlayerSettings.WebGL.dataCaching = false;

            Debug.Log("[AIT] ✓ Development 프리셋 적용 완료 (디버깅 최적화)");
            Debug.Log("[AIT] 💡 빠른 빌드와 상세한 에러 로그를 위한 설정");
        }
    }

    /// <summary>
    /// 빌드 프리셋 타입
    /// </summary>
    public enum BuildPreset
    {
        Production,   // 프로덕션 (모바일 최적화)
        Development   // 개발 모드
    }
}
