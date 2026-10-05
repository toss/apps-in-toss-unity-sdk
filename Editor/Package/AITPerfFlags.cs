// -----------------------------------------------------------------------
// AITPerfFlags.cs - 모바일 런타임 최적화 플래그의 빌드타임 해석과 JSON 직렬화
//
// 설정(AITEditorScriptObject 의 tri-state 필드)을 빌드 시점에 실효값(bool/수치)으로 풀어
// index.html 의 window.__AIT_PERF 로 주입한다. 런타임 스크립트(Runtime/ait-*.js)는 tri-state 를
// 해석하지 않고 이 JSON 만 읽는다. 키가 없거나 JSON 파싱이 실패해도(`{}`) 각 스크립트는
// 아래 "자동 기본값"으로 동작해야 한다(fail-open).
//
// === window.__AIT_PERF 키 계약 (v1) ===
//   v                  number   스키마 버전(현재 1)
//   patchSet           number   AITPatchedFileNaming.PatchSetVersion (framework/loader 패치 세트 버전)
//   glHook             bool     ait-gl.js getContext 훅 설치 여부           (webglAntialiasOpt != 0)        기본 true
//   glDropAntialias    bool     모바일 조건부 antialias 끄기                (webglAntialiasOpt == 1)        기본 false
//   glContextRecovery  bool     webglcontextlost 시 reload + 루프 가드                                      기본 true
//   frameRateCap       number   프레임 상한 fps. 0 이면 상한 없음. 100Hz 이상 패널에서만 의미      기본 60
//   adaptiveFrameRate  bool     배터리·발열 힌트로 30fps 하향                                                기본 false
//   mobileLifecycle    bool     hidden/pagehide/freeze 시 루프·오디오 정지 게이트                            기본 true
//   memoryTelemetry    bool     Memory.grow 기록 + crashCount 추적                                          기본 true
//   exactDataBody      bool     data Response 를 정확한 Content-Length 로 재포장                             기본 true
//   dataRawSize        number   .data 압축 해제 크기(바이트). 측정 실패/비활성이면 -1
//   releaseConsumedData bool    소비한 data 구간(metadata 등) 해제                                           기본 false
//   audioForceCompressed bool   긴 클립 강제 압축 재생(framework 패치로 적용; 런타임은 로그용)               기본 false
//   audioForceCompressedMinSeconds number  강제 대상 최소 길이(초)                                           기본 10
//   unityweb           bool     Decompression Fallback(.unityweb) 빌드 여부 — true 면 data/framework 훅 비활성
//   audioPatched       bool     framework 패치의 compressed-clip-meta 가 적용됐는지. false 면 런타임이 압축 재생 경로를 쓰지 않는다   기본 false
//   lowMemoryTier      bool     저사양 기기 티어 판별 사용(ait-mem.js 가 AITMemory.lowMemTier 를 계산)          기본 true
//   pageCacheDeferredPut number tri-state 그대로(-1 자동=WebKit 만 / 0 끔 / 1 모든 엔진). 런타임이 UA 로 자동을 푼다  기본 -1
//   textureStreamingMemoryBudgetMB number 스트림 텍스처 동시 RGBA32 메모리 예산(MB). 0 이하면 제한 없음               기본 16
//   audioStreamLoopTranscode bool 루프 클립도 스트림 사본 재인코딩(빌드타임 게이트; 런타임은 로그용)                기본 false
//   raw                object   설정의 원본 tri-state 값(-1/0/1). 디버깅·로그 전용
// -----------------------------------------------------------------------

using System.Globalization;
using System.Text;

namespace AppsInToss.Editor.Package
{
    internal static class AITPerfFlags
    {
        /// <summary>__AIT_PERF 스키마 버전. 키의 의미가 바뀌면 올린다.</summary>
        internal const int SchemaVersion = 1;

        /// <summary>frameRateCap 이 켜졌을 때의 상한 fps.</summary>
        internal const int FrameRateCapFps = 60;

        /// <summary>tri-state 해석: 명시값(0/1)이 있으면 그 값, 자동(-1)이면 autoValue.</summary>
        internal static bool Resolve(int stored, bool autoValue)
        {
            return stored >= 0 ? stored == 1 : autoValue;
        }

        internal static bool EffectiveGlHook(AITEditorScriptObject c)
        {
            return c == null || c.webglAntialiasOpt != 0;
        }

        internal static bool EffectiveGlDropAntialias(AITEditorScriptObject c)
        {
            return c != null && Resolve(c.webglAntialiasOpt, AITDefaultSettings.GetDefaultWebglAntialiasOpt());
        }

        internal static bool EffectiveGlContextRecovery(AITEditorScriptObject c)
        {
            return c == null || Resolve(c.webglContextRecovery, AITDefaultSettings.GetDefaultWebglContextRecovery());
        }

        internal static int EffectiveFrameRateCap(AITEditorScriptObject c)
        {
            bool on = c == null || Resolve(c.frameRateCap, AITDefaultSettings.GetDefaultFrameRateCap());
            return on ? FrameRateCapFps : 0;
        }

        internal static bool EffectiveAdaptiveFrameRate(AITEditorScriptObject c)
        {
            return c != null && Resolve(c.adaptiveFrameRate, AITDefaultSettings.GetDefaultAdaptiveFrameRate());
        }

        internal static bool EffectiveMobileLifecycle(AITEditorScriptObject c)
        {
            return c == null || Resolve(c.mobileLifecycle, AITDefaultSettings.GetDefaultMobileLifecycle());
        }

        internal static bool EffectiveMemoryTelemetry(AITEditorScriptObject c)
        {
            return c == null || Resolve(c.memoryTelemetry, AITDefaultSettings.GetDefaultMemoryTelemetry());
        }

        internal static bool EffectiveExactDataBody(AITEditorScriptObject c)
        {
            return c == null || Resolve(c.exactDataBody, AITDefaultSettings.GetDefaultExactDataBody());
        }

        internal static bool EffectiveReleaseConsumedData(AITEditorScriptObject c)
        {
            return c != null && Resolve(c.releaseConsumedData, AITDefaultSettings.GetDefaultReleaseConsumedData());
        }

        internal static bool EffectiveLowMemoryTier(AITEditorScriptObject c)
        {
            return c == null || Resolve(c.lowMemoryTier, AITDefaultSettings.GetDefaultLowMemoryTier());
        }

        /// <summary>tri-state 원본(-1/0/1). 자동의 엔진별 해석은 런타임(ait-mem.js/index.html)이 한다.</summary>
        internal static int EffectivePageCacheDeferredPut(AITEditorScriptObject c)
        {
            if (c == null) return -1;
            return c.pageCacheDeferredPut < 0 ? -1 : (c.pageCacheDeferredPut == 1 ? 1 : 0);
        }

        /// <summary>텍스처 스트리밍 동시 메모리 예산(MB). 0 이하는 0(제한 없음)으로 정규화한다.</summary>
        internal static int EffectiveTextureStreamingMemoryBudgetMB(AITEditorScriptObject c)
        {
            if (c == null) return AITDefaultSettings.DefaultTextureStreamingMemoryBudgetMB;
            return c.textureStreamingMemoryBudgetMB > 0 ? c.textureStreamingMemoryBudgetMB : 0;
        }

        internal static bool EffectiveAudioStreamLoopTranscode(AITEditorScriptObject c)
        {
            return c != null && Resolve(c.audioStreamLoopTranscode, AITDefaultSettings.GetDefaultAudioStreamLoopTranscode());
        }

        internal static bool EffectiveAudioForceCompressed(AITEditorScriptObject c)
        {
            return c != null && Resolve(c.audioForceCompressedPlayback, AITDefaultSettings.GetDefaultAudioForceCompressedPlayback());
        }

        /// <summary>강제 압축 재생 최소 길이(초). 0 이하/NaN 이면 기본 10초.</summary>
        internal static float EffectiveAudioForceCompressedMinSeconds(AITEditorScriptObject c)
        {
            if (c == null) return AITDefaultSettings.DefaultAudioForceCompressedMinSeconds;
            float v = c.audioForceCompressedMinSeconds;
            return v > 0f && !float.IsNaN(v) && !float.IsInfinity(v)
                ? v
                : AITDefaultSettings.DefaultAudioForceCompressedMinSeconds;
        }

        /// <summary>
        /// 설정을 __AIT_PERF JSON 한 줄로 직렬화한다. 키·의미는 파일 머리 주석의 계약을 따른다.
        /// 값은 bool/숫자뿐이라 별도 JSON 라이브러리 없이 조립한다(문화권 영향 없도록 InvariantCulture).
        /// 호출부(WebGLBuildCopier)가 이 결과를 AITJsStringEscaper.EscapeSingleQuoted 로 감싸 템플릿의
        /// 작은따옴표 리터럴에 넣는다.
        /// </summary>
        /// <param name="config">null 이면 전부 자동 기본값으로 직렬화한다.</param>
        /// <param name="dataRawSize">AITDataRawSize.Measure 결과. 측정 안 했거나 실패면 -1.</param>
        /// <param name="unityweb">Decompression Fallback(.unityweb) 빌드 여부.</param>
        /// <param name="audioPatched">framework 패치(compressed-clip-meta)가 실제로 적용됐는지. 키가 없거나 false 면 런타임은 압축 재생 경로를 피한다.</param>
        internal static string ToJson(AITEditorScriptObject config, long dataRawSize, bool unityweb, bool audioPatched = false)
        {
            var sb = new StringBuilder(512);
            sb.Append('{');
            AppendInt(sb, "v", SchemaVersion, first: true);
            AppendInt(sb, "patchSet", AITPatchedFileNaming.PatchSetVersion);
            AppendBool(sb, "glHook", EffectiveGlHook(config));
            AppendBool(sb, "glDropAntialias", EffectiveGlDropAntialias(config));
            AppendBool(sb, "glContextRecovery", EffectiveGlContextRecovery(config));
            AppendInt(sb, "frameRateCap", EffectiveFrameRateCap(config));
            AppendBool(sb, "adaptiveFrameRate", EffectiveAdaptiveFrameRate(config));
            AppendBool(sb, "mobileLifecycle", EffectiveMobileLifecycle(config));
            AppendBool(sb, "memoryTelemetry", EffectiveMemoryTelemetry(config));
            AppendBool(sb, "exactDataBody", EffectiveExactDataBody(config));
            AppendLong(sb, "dataRawSize", dataRawSize);
            AppendBool(sb, "releaseConsumedData", EffectiveReleaseConsumedData(config));
            AppendBool(sb, "audioForceCompressed", EffectiveAudioForceCompressed(config));
            sb.Append(",\"audioForceCompressedMinSeconds\":")
              .Append(EffectiveAudioForceCompressedMinSeconds(config).ToString("0.###", CultureInfo.InvariantCulture));
            AppendBool(sb, "unityweb", unityweb);
            AppendBool(sb, "audioPatched", audioPatched);
            AppendBool(sb, "lowMemoryTier", EffectiveLowMemoryTier(config));
            AppendInt(sb, "pageCacheDeferredPut", EffectivePageCacheDeferredPut(config));
            AppendInt(sb, "textureStreamingMemoryBudgetMB", EffectiveTextureStreamingMemoryBudgetMB(config));
            AppendBool(sb, "audioStreamLoopTranscode", EffectiveAudioStreamLoopTranscode(config));

            sb.Append(",\"raw\":{");
            AppendInt(sb, "webglAntialiasOpt", config != null ? config.webglAntialiasOpt : -1, first: true);
            AppendInt(sb, "webglContextRecovery", config != null ? config.webglContextRecovery : -1);
            AppendInt(sb, "frameRateCap", config != null ? config.frameRateCap : -1);
            AppendInt(sb, "adaptiveFrameRate", config != null ? config.adaptiveFrameRate : -1);
            AppendInt(sb, "mobileLifecycle", config != null ? config.mobileLifecycle : -1);
            AppendInt(sb, "memoryTelemetry", config != null ? config.memoryTelemetry : -1);
            AppendInt(sb, "exactDataBody", config != null ? config.exactDataBody : -1);
            AppendInt(sb, "releaseConsumedData", config != null ? config.releaseConsumedData : -1);
            AppendInt(sb, "audioForceCompressedPlayback", config != null ? config.audioForceCompressedPlayback : -1);
            AppendInt(sb, "lowMemoryTier", config != null ? config.lowMemoryTier : -1);
            AppendInt(sb, "pageCacheDeferredPut", config != null ? config.pageCacheDeferredPut : -1);
            AppendInt(sb, "audioStreamLoopTranscode", config != null ? config.audioStreamLoopTranscode : -1);
            sb.Append('}');

            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendBool(StringBuilder sb, string key, bool value)
        {
            sb.Append(",\"").Append(key).Append("\":").Append(value ? "true" : "false");
        }

        private static void AppendInt(StringBuilder sb, string key, int value, bool first = false)
        {
            if (!first) sb.Append(',');
            sb.Append('"').Append(key).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendLong(StringBuilder sb, string key, long value)
        {
            sb.Append(",\"").Append(key).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));
        }
    }
}
