using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;
using UnityEditor.U2D;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 텍스처 포맷 가드 동작 모드. AITEditorScriptObject.textureFormatGuardMode 에 int 로 저장된다.
    /// 기본값 0(AutoConvert)이라 필드가 없던 기존 프로젝트도 자동 변환 모드로 열린다.
    /// </summary>
    public enum AITTextureFormatGuardMode
    {
        /// <summary>WebGL 텍스처 서브타깃을 ASTC로 강제하고, DXT/BC 오버라이드를 모바일 호환 포맷으로 변환한다.</summary>
        AutoConvert = 0,
        /// <summary>아무것도 바꾸지 않고 경고만 낸다.</summary>
        WarnOnly = 1,
        /// <summary>가드를 끈다. 로그 한 줄만 남긴다.</summary>
        Off = 2,
    }

    /// <summary>가드가 개별 에셋에 취할 조치.</summary>
    internal enum AITTextureGuardAction
    {
        Skip,
        Warn,
        Convert,
    }

    internal enum AITTextureAssetKind
    {
        Texture,
        SpriteAtlasV1,
        SpriteAtlasV2,
    }

    /// <summary>스캔 결과 한 건 (에셋 하나).</summary>
    internal class AITTextureFinding
    {
        public string path;
        public AITTextureAssetKind kind;
        public TextureImporterFormat format;
        public int maxSize;
        /// <summary>WebGL 오버라이드가 모바일 비호환(DXT/BC) 포맷인가.</summary>
        public bool incompatibleFormat;
        /// <summary>WebGL max texture size 가 대형(>= 4096)인가.</summary>
        public bool large;
        /// <summary>옵트아웃 레이블이 붙어 있는가.</summary>
        public bool keepLabel;
        /// <summary>변환 목표 포맷. null이면 안전한 대체 포맷이 없어 변환하지 않는다.</summary>
        public TextureImporterFormat? target;
    }

    internal struct AITTextureGuardResult
    {
        public int converted;
        public int warnedIncompatible;
        public int keptByLabel;
        public int largeWarned;
        public List<string> convertedPaths;
    }

    /// <summary>
    /// 모바일 WebView가 지원하지 않는 텍스처 포맷(DXT/BC)이 WebGL 빌드에 섞여 들어가는 것을 막는 빌드 가드.
    ///
    /// 배경: iOS Safari와 대부분의 Android GPU는 S3TC(DXT/BC)를 지원하지 않아서 Unity가 런타임에
    /// RGBA32로 풀어 올린다. 4096x4096 아틀라스 하나가 64MB가 되므로 큰 DXT5 스프라이트 아틀라스가 여러 장이면
    /// 모바일에서 메모리 부족으로 죽는다. 기존 스캐너는 비압축 포맷만 잡았고 SpriteAtlas는 보지 않았다.
    ///
    /// 구성: 순수 헬퍼(포맷 분류·변환 매핑·조치 결정·모드 해석)와 에셋 I/O 래퍼를 분리했다.
    /// 진입점 <see cref="RunBuildGuard"/> 는 어떤 예외도 밖으로 던지지 않는다.
    /// </summary>
    internal static class AITTextureFormatGuard
    {
        /// <summary>
        /// 옵트아웃 에셋 레이블. 붙이면 가드가 그 에셋을 변환하지 않고 경고만 낸다.
        /// 주의: 명시적 WebGL 오버라이드가 있는 에셋만 보호한다. 포맷이 Automatic인 에셋은
        /// 항상 WebGL 텍스처 서브타깃을 따르므로 레이블이 효과가 없다. 비호환 포맷이 꼭 필요한 에셋은
        /// WebGL 오버라이드로 포맷을 명시하고 이 레이블을 함께 붙여야 한다.
        /// </summary>
        public const string KeepFormatLabel = "AIT-KeepTextureFormat";

        /// <summary>CI에서 모드를 덮어쓰는 환경 변수 (auto|warn|off).</summary>
        public const string ModeEnvVar = "AIT_TEXTURE_FORMAT_GUARD";

        /// <summary>이 크기(픽셀) 이상이면 대형 텍스처 경고를 낸다.</summary>
        public const int LargeTextureSize = 4096;

        private const string WebGLPlatform = "WebGL";

        #region Pure helpers

        /// <summary>모바일 WebView가 하드웨어로 지원하지 않는 DXT/BC 계열(크런치 포함)인가.</summary>
        internal static bool IsMobileIncompatibleFormat(TextureImporterFormat format)
        {
            switch (format)
            {
                case TextureImporterFormat.DXT1:
                case TextureImporterFormat.DXT5:
                case TextureImporterFormat.DXT1Crunched:
                case TextureImporterFormat.DXT5Crunched:
                case TextureImporterFormat.BC4:
                case TextureImporterFormat.BC5:
                case TextureImporterFormat.BC6H:
                case TextureImporterFormat.BC7:
                    return true;
                default:
                    return false;
            }
        }

        internal static bool IsCrunchedFormat(TextureImporterFormat format)
        {
            return format == TextureImporterFormat.DXT1Crunched ||
                   format == TextureImporterFormat.DXT5Crunched ||
                   format == TextureImporterFormat.ETC_RGB4Crunched ||
                   format == TextureImporterFormat.ETC2_RGBA8Crunched;
        }

        /// <summary>스캐너·가드 공통 권장 포맷. Unity 6은 ASTC 6x6, 그 이전은 ETC2.</summary>
        internal static TextureImporterFormat GetRecommendedTextureFormat()
        {
#if UNITY_6000_0_OR_NEWER
            return TextureImporterFormat.ASTC_6x6;
#else
            return TextureImporterFormat.ETC2_RGBA8;
#endif
        }

        internal static string GetRecommendedTextureFormatName()
        {
#if UNITY_6000_0_OR_NEWER
            return "ASTC 6x6";
#else
            return "ETC2";
#endif
        }

        /// <summary>
        /// 비호환 포맷의 변환 목표를 돌려준다. 안전한 대체가 없으면 null(변환하지 않고 경고만).
        ///
        /// 매핑 (Unity 6 / 그 이전):
        /// - DXT1, DXT5, BC4, BC5, BC7       -> ASTC 6x6 / ETC2 RGBA8 (GetRecommendedTextureFormat)
        /// - DXT1Crunched, DXT5Crunched      -> ASTC 6x6 / ETC_RGB4Crunched(DXT1) 또는 ETC2_RGBA8Crunched(DXT5)
        ///   (ASTC는 크런치 변형이 없으므로 Unity 6에서는 크런치를 버리고 ASTC로 간다. 용량은 ASTC 6x6이
        ///    DXT5 대비 작고 로딩 시 크런치 해제 비용도 없다. ETC2는 크런치 변형이 있어 유지한다.)
        /// - BC6H(HDR)                       -> Unity 6: ASTC_HDR_6x6 / 그 이전: null
        ///   (LDR 포맷으로 바꾸면 HDR 범위가 깨지므로 HDR 대체가 있을 때만 변환한다)
        /// 비호환이 아닌 포맷은 null.
        /// </summary>
        internal static TextureImporterFormat? GetConversionTarget(TextureImporterFormat source)
        {
            if (!IsMobileIncompatibleFormat(source)) return null;

            switch (source)
            {
                case TextureImporterFormat.BC6H:
#if UNITY_6000_0_OR_NEWER
                    return TextureImporterFormat.ASTC_HDR_6x6;
#else
                    return null;
#endif
                case TextureImporterFormat.DXT1Crunched:
#if UNITY_6000_0_OR_NEWER
                    return TextureImporterFormat.ASTC_6x6;
#else
                    return TextureImporterFormat.ETC_RGB4Crunched;
#endif
                case TextureImporterFormat.DXT5Crunched:
#if UNITY_6000_0_OR_NEWER
                    return TextureImporterFormat.ASTC_6x6;
#else
                    return TextureImporterFormat.ETC2_RGBA8Crunched;
#endif
                default:
                    return GetRecommendedTextureFormat();
            }
        }

        /// <summary>WebGL 텍스처 서브타깃이 모바일 호환(ETC2/ASTC)인가. Generic·DXT는 비호환.</summary>
        internal static bool IsMobileCompatibleSubtarget(WebGLTextureSubtarget subtarget)
        {
            return subtarget == WebGLTextureSubtarget.ASTC || subtarget == WebGLTextureSubtarget.ETC2;
        }

        /// <summary>모드와 옵트아웃 레이블 유무로 에셋별 조치를 결정한다.</summary>
        internal static AITTextureGuardAction DecideAction(AITTextureFormatGuardMode mode, bool hasKeepLabel)
        {
            switch (mode)
            {
                case AITTextureFormatGuardMode.Off:
                    return AITTextureGuardAction.Skip;
                case AITTextureFormatGuardMode.WarnOnly:
                    return AITTextureGuardAction.Warn;
                default:
                    return hasKeepLabel ? AITTextureGuardAction.Warn : AITTextureGuardAction.Convert;
            }
        }

        /// <summary>환경 변수 문자열(auto|warn|off)을 모드로 파싱한다.</summary>
        internal static bool TryParseMode(string value, out AITTextureFormatGuardMode mode)
        {
            mode = AITTextureFormatGuardMode.AutoConvert;
            if (string.IsNullOrWhiteSpace(value)) return false;
            switch (value.Trim().ToLowerInvariant())
            {
                case "auto": mode = AITTextureFormatGuardMode.AutoConvert; return true;
                case "warn": mode = AITTextureFormatGuardMode.WarnOnly; return true;
                case "off": mode = AITTextureFormatGuardMode.Off; return true;
                default: return false;
            }
        }

        /// <summary>
        /// 환경 변수 > 프로젝트 설정 순으로 모드를 정한다. source 는 로그용 출처 문자열.
        /// 환경 변수 값이 잘못되면 무시하고 프로젝트 설정을 쓴다. 범위 밖 설정값은 AutoConvert.
        /// </summary>
        internal static AITTextureFormatGuardMode ResolveMode(int configValue, string envValue, out string source)
        {
            if (!string.IsNullOrEmpty(envValue))
            {
                if (TryParseMode(envValue, out var envMode))
                {
                    source = $"환경 변수 {ModeEnvVar}";
                    return envMode;
                }
                Debug.LogWarning($"[AIT] {ModeEnvVar} 환경 변수 값이 올바르지 않습니다: '{envValue}' (auto/warn/off 필요). 프로젝트 설정을 사용합니다.");
            }

            source = "프로젝트 설정";
            if (configValue < (int)AITTextureFormatGuardMode.AutoConvert || configValue > (int)AITTextureFormatGuardMode.Off)
                return AITTextureFormatGuardMode.AutoConvert;
            return (AITTextureFormatGuardMode)configValue;
        }

        /// <summary>RGBA32로 풀렸을 때의 메모리(바이트). width*height*4.</summary>
        internal static long Rgba32Bytes(int width, int height)
        {
            return (long)Math.Max(0, width) * Math.Max(0, height) * 4L;
        }

        private static string FormatMb(long bytes)
        {
            return $"{bytes / (1024.0 * 1024.0):0.#}MB";
        }

        #endregion

        #region Mode resolution

        internal static AITTextureFormatGuardMode ResolveCurrentMode(AITEditorScriptObject config, out string source)
        {
            int configValue = config != null ? config.textureFormatGuardMode : 0;
            return ResolveMode(configValue, Environment.GetEnvironmentVariable(ModeEnvVar), out source);
        }

        #endregion

        #region Asset inspection (Unity asset I/O)

        private static readonly string[] DefaultSearchFolders = { "Assets" };

        /// <summary>검색 폴더 아래의 Texture2D 와 SpriteAtlas(v1/v2) 중 가드 대상(비호환 포맷 또는 대형)을 모은다.</summary>
        internal static List<AITTextureFinding> Scan(string[] searchFolders = null)
        {
            var folders = searchFolders ?? DefaultSearchFolders;
            var findings = new List<AITTextureFinding>();
            var seen = new HashSet<string>();

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!seen.Add(path)) continue;
                var finding = InspectTexture(path);
                if (finding != null) findings.Add(finding);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:SpriteAtlas", folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!seen.Add(path)) continue;
                var finding = InspectAtlas(path);
                if (finding != null) findings.Add(finding);
            }

            return findings;
        }

        private static AITTextureFinding InspectTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            var ps = importer.GetPlatformTextureSettings(WebGLPlatform);
            if (!ps.overridden) return null; // 오버라이드 없는 에셋은 서브타깃을 따른다

            return BuildFinding(path, AITTextureAssetKind.Texture, ps);
        }

        private static AITTextureFinding InspectAtlas(string path)
        {
            TextureImporterPlatformSettings ps;
            AITTextureAssetKind kind;

#if UNITY_2022_1_OR_NEWER
            if (path.EndsWith(".spriteatlasv2", StringComparison.OrdinalIgnoreCase))
            {
                var importer = AssetImporter.GetAtPath(path) as SpriteAtlasImporter;
                if (importer == null) return null;
                kind = AITTextureAssetKind.SpriteAtlasV2;
                ps = importer.GetPlatformSettings(WebGLPlatform);
                if (!ps.overridden)
                {
                    var def = importer.GetPlatformSettings("DefaultTexturePlatform");
                    return BuildFinding(path, kind, ps, def.maxTextureSize);
                }
                return BuildFinding(path, kind, ps);
            }
#else
            // SpriteAtlasImporter(v2) 처리는 2022.1+ 에서만 한다. 그 이전 버전에서는 v1 API 로 v2 에셋을 건드리지 않는다.
            if (path.EndsWith(".spriteatlasv2", StringComparison.OrdinalIgnoreCase)) return null;
#endif
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
            if (atlas == null) return null;
            kind = AITTextureAssetKind.SpriteAtlasV1;
            ps = SpriteAtlasExtensions.GetPlatformSettings(atlas, WebGLPlatform);
            if (!ps.overridden)
            {
                var def = SpriteAtlasExtensions.GetPlatformSettings(atlas, "DefaultTexturePlatform");
                return BuildFinding(path, kind, ps, def.maxTextureSize);
            }
            return BuildFinding(path, kind, ps);
        }

        /// <summary>
        /// 오버라이드가 없는 아틀라스는 포맷이 Automatic(서브타깃 따름)이므로 비호환 포맷 판정은 하지 않고
        /// 기본 플랫폼 설정의 max size 로 대형 여부만 본다.
        /// </summary>
        private static AITTextureFinding BuildFinding(string path, AITTextureAssetKind kind,
            TextureImporterPlatformSettings ps, int sizeOverride = -1)
        {
            bool overridden = ps.overridden;
            int maxSize = sizeOverride > 0 ? sizeOverride : ps.maxTextureSize;
            var finding = new AITTextureFinding
            {
                path = path,
                kind = kind,
                format = ps.format,
                maxSize = maxSize,
                incompatibleFormat = overridden && IsMobileIncompatibleFormat(ps.format),
                large = maxSize >= LargeTextureSize,
            };
            if (!finding.incompatibleFormat && !finding.large) return null;

            if (finding.incompatibleFormat)
            {
                finding.target = GetConversionTarget(ps.format);
                finding.keepLabel = HasKeepLabel(path);
            }
            return finding;
        }

        internal static bool HasKeepLabel(string path)
        {
            try
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(path);
                if (obj == null) return false;
                return Array.IndexOf(AssetDatabase.GetLabels(obj), KeepFormatLabel) >= 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>스캐너용: 지정 경로의 에셋이 비호환 포맷 오버라이드(옵트아웃 제외)면 finding 을 돌려준다.</summary>
        internal static AITTextureFinding InspectIncompatible(string path)
        {
            AITTextureFinding finding = path.EndsWith(".spriteatlas", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".spriteatlasv2", StringComparison.OrdinalIgnoreCase)
                ? InspectAtlas(path)
                : InspectTexture(path);
            if (finding == null || !finding.incompatibleFormat || finding.keepLabel) return null;
            return finding;
        }

        #endregion

        #region Conversion (writes asset settings)

        /// <summary>
        /// 비호환 WebGL 오버라이드를 목표 포맷으로 다시 쓴다 (에셋 설정은 영구 변경되어 git diff 로 보인다).
        /// StartAssetEditing 안에서 호출한다. 실제 reimport 는 호출자가 ImportAsset 으로 한다.
        /// </summary>
        private static bool ApplyConversion(AITTextureFinding f)
        {
            if (!f.target.HasValue) return false;
            var target = f.target.Value;

            switch (f.kind)
            {
                case AITTextureAssetKind.Texture:
                {
                    var importer = AssetImporter.GetAtPath(f.path) as TextureImporter;
                    if (importer == null) return false;
                    var ps = importer.GetPlatformTextureSettings(WebGLPlatform);
                    ps.overridden = true;
                    ps.format = target;
                    ps.crunchedCompression = IsCrunchedFormat(target);
                    importer.SetPlatformTextureSettings(ps);
                    EditorUtility.SetDirty(importer);
                    return true;
                }
                case AITTextureAssetKind.SpriteAtlasV1:
                {
                    var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(f.path);
                    if (atlas == null) return false;
                    var ps = SpriteAtlasExtensions.GetPlatformSettings(atlas, WebGLPlatform);
                    ps.overridden = true;
                    ps.format = target;
                    ps.crunchedCompression = IsCrunchedFormat(target);
                    SpriteAtlasExtensions.SetPlatformSettings(atlas, ps);
                    EditorUtility.SetDirty(atlas);
                    return true;
                }
#if UNITY_2022_1_OR_NEWER
                case AITTextureAssetKind.SpriteAtlasV2:
                {
                    var importer = AssetImporter.GetAtPath(f.path) as SpriteAtlasImporter;
                    if (importer == null) return false;
                    var ps = importer.GetPlatformSettings(WebGLPlatform);
                    ps.overridden = true;
                    ps.format = target;
                    ps.crunchedCompression = IsCrunchedFormat(target);
                    importer.SetPlatformSettings(ps);
                    EditorUtility.SetDirty(importer);
                    return true;
                }
#endif
                default:
                    return false;
            }
        }

        /// <summary>스캐너 수정 흐름용: 단일 에셋을 변환한다 (reimport 제외).</summary>
        internal static bool ConvertAsset(string path)
        {
            var finding = InspectIncompatible(path);
            return finding != null && ApplyConversion(finding);
        }

        #endregion

        #region Guard runner

        /// <summary>
        /// 빌드 진입점. WebGL 빌드 직전에 호출한다. 배치 모드·빠른 빌드와 상관없이 항상 실행되며
        /// 대화형 최적화 창과 독립이다. 어떤 예외도 밖으로 던지지 않는다 (빌드를 막지 않는다).
        /// </summary>
        internal static void RunBuildGuard(AITEditorScriptObject config)
        {
            try
            {
                var mode = ResolveCurrentMode(config, out string source);
                if (mode == AITTextureFormatGuardMode.Off)
                {
                    Debug.Log($"[AIT] 텍스처 포맷 가드가 꺼져 있습니다 ({source}). " +
                              "다시 켜려면 Apps in Toss 설정 > 고급 설정 > '텍스처 포맷 가드'를 변경하거나 " +
                              $"환경 변수 {ModeEnvVar}=auto|warn 을 지정하세요.");
                    return;
                }

                Debug.Log($"[AIT] 텍스처 포맷 가드 모드: {mode} ({source})");
                ApplyWebGLTextureSubtarget(mode);
                RunAssetGuard(mode);
            }
            catch (Exception e)
            {
                AITLog.Warning($"[AIT] 텍스처 포맷 가드 실행 중 오류가 발생해 건너뜁니다 (빌드는 계속 진행됨): {e.Message}", sentryCapture: false);
            }
        }

        /// <summary>
        /// WebGL 텍스처 서브타깃(EditorUserBuildSettings.webGLBuildSubtarget)을 처리한다.
        /// - AutoConvert: 모바일 비호환(Generic/DXT)이면 ASTC로 강제하고 전/후 값을 로그에 남긴다.
        /// - WarnOnly: 바꾸지 않고, 비호환이면 한 번 경고한다.
        ///
        /// 복원하지 않는다: 이 값은 PlayerSettings가 아니라 Library/EditorUserBuildSettings.asset(로컬, 버전 관리 밖)에
        /// 저장되고, 바꾸면 Automatic 포맷 WebGL 텍스처가 reimport 된다. 빌드마다 원복하면 매번 전체 reimport가
        /// 두 번 일어난다. 그래서 PlayerSettingsSnapshot 에는 넣지 않고 바뀐 값을 유지한다.
        ///
        /// Unity 6 빌드 프로필: 활성 프로필이 있으면 프로필 쪽 값이 우선할 수 있으므로 쓴 뒤 다시 읽어
        /// 값이 반영되지 않았으면 경고한다.
        /// </summary>
        internal static void ApplyWebGLTextureSubtarget(AITTextureFormatGuardMode mode)
        {
            if (mode == AITTextureFormatGuardMode.Off) return;

            var before = EditorUserBuildSettings.webGLBuildSubtarget;
            if (before == WebGLTextureSubtarget.ASTC)
            {
                Debug.Log($"[AIT] WebGL 텍스처 서브타깃: {before} (변경 없음)");
                return;
            }

            if (mode == AITTextureFormatGuardMode.WarnOnly)
            {
                if (!IsMobileCompatibleSubtarget(before))
                {
                    AITLog.Warning($"[AIT] WebGL 텍스처 서브타깃이 {before} 입니다. iOS Safari와 대부분의 Android GPU는 DXT/BC를 지원하지 않아 " +
                                   "Automatic 포맷 텍스처가 런타임에 RGBA32로 풀립니다 (메모리 가로x세로x4바이트). " +
                                   "Build Settings > WebGL > Texture Compression 을 ASTC로 바꾸거나 텍스처 포맷 가드를 '자동 변환'으로 두세요.",
                        sentryCapture: false);
                }
                return;
            }

            EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.ASTC;
            var after = EditorUserBuildSettings.webGLBuildSubtarget;
            Debug.Log($"[AIT] WebGL 텍스처 서브타깃: {before} -> {after}");
            if (after != WebGLTextureSubtarget.ASTC)
            {
                AITLog.Warning($"[AIT] WebGL 텍스처 서브타깃을 ASTC로 바꾸지 못했습니다 (현재 {after}). " +
                               "Unity 6 빌드 프로필이 값을 따로 들고 있을 수 있습니다. 활성 빌드 프로필의 Texture Compression 을 ASTC로 직접 지정하세요.",
                    sentryCapture: false);
            }
        }

        /// <summary>에셋 단계: 스캔 -> 모드별 변환/경고. 테스트에서 폴더를 좁혀 호출할 수 있다.</summary>
        internal static AITTextureGuardResult RunAssetGuard(AITTextureFormatGuardMode mode, string[] searchFolders = null)
        {
            var result = new AITTextureGuardResult { convertedPaths = new List<string>() };
            if (mode == AITTextureFormatGuardMode.Off) return result;

            var findings = Scan(searchFolders);
            var toConvert = new List<AITTextureFinding>();

            foreach (var f in findings)
            {
                if (f.incompatibleFormat)
                {
                    var action = DecideAction(mode, f.keepLabel);
                    if (action == AITTextureGuardAction.Convert && !f.target.HasValue)
                        action = AITTextureGuardAction.Warn; // 안전한 대체 포맷 없음

                    if (action == AITTextureGuardAction.Convert)
                    {
                        toConvert.Add(f);
                    }
                    else
                    {
                        if (f.keepLabel) result.keptByLabel++;
                        result.warnedIncompatible++;
                        AITLog.Warning(BuildIncompatibleWarning(f, action), sentryCapture: false);
                    }
                }
                if (f.large)
                {
                    result.largeWarned++;
                    AITLog.Warning(BuildLargeWarning(f), sentryCapture: false);
                }
            }

            if (toConvert.Count > 0)
            {
                var changed = new List<AITTextureFinding>();
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var f in toConvert)
                    {
                        if (ApplyConversion(f)) changed.Add(f);
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                AssetDatabase.SaveAssets();
                foreach (var f in changed)
                {
                    // importer 설정(.meta)을 먼저 디스크에 쓴다. 안 쓰면 reimport 가 이전 설정으로 돌아갈 수 있다.
                    AssetDatabase.WriteImportSettingsIfDirty(f.path);
                    AssetDatabase.ImportAsset(f.path, ImportAssetOptions.ForceUpdate);
                    result.convertedPaths.Add(f.path);
                    Debug.Log($"[AIT] 텍스처 포맷 변환: {f.path} ({f.kind}) {f.format} -> {f.target.Value}");
                }
                result.converted = changed.Count;
            }

            if (result.converted > 0)
            {
                AITLog.Warning($"[AIT] 모바일 WebView가 지원하지 않는 DXT/BC WebGL 텍스처 {result.converted}개를 {GetRecommendedTextureFormatName()} 계열로 변환했습니다 " +
                               "(에셋 설정이 변경되었으니 git diff를 확인하세요). 비호환 포맷이 꼭 필요한 에셋은 WebGL 오버라이드로 포맷을 명시하고 " +
                               $"에셋 레이블 '{KeepFormatLabel}' 을 함께 붙이세요 (포맷이 Automatic인 에셋은 레이블과 무관하게 WebGL 텍스처 서브타깃을 따릅니다). " +
                               "가드 끄기/경고만: 고급 설정 > 텍스처 포맷 가드.",
                    sentryCapture: false);
            }

            return result;
        }

        private static string BuildIncompatibleWarning(AITTextureFinding f, AITTextureGuardAction action)
        {
            long bytes = Rgba32Bytes(f.maxSize, f.maxSize);
            var sb = new StringBuilder();
            sb.Append($"[AIT] {f.path}: WebGL 포맷이 {f.format} 입니다. iOS Safari와 대부분의 Android GPU는 DXT/BC를 지원하지 않아 ");
            sb.Append($"런타임에 RGBA32(가로x세로x4바이트)로 풀립니다. 최대 크기 {f.maxSize}x{f.maxSize} 기준 최대 {FormatMb(bytes)}의 메모리를 씁니다.");
            if (f.keepLabel) sb.Append($" '{KeepFormatLabel}' 레이블 때문에 변환하지 않았습니다.");
            else if (action == AITTextureGuardAction.Warn && !f.target.HasValue) sb.Append(" 안전한 대체 포맷이 없어 변환하지 않았습니다.");
            else sb.Append(" 경고만 모드라 변환하지 않았습니다.");
            return sb.ToString();
        }

        private static string BuildLargeWarning(AITTextureFinding f)
        {
            long bytes = Rgba32Bytes(f.maxSize, f.maxSize);
            return $"[AIT] {f.path}: WebGL 최대 텍스처 크기가 {f.maxSize} 입니다. 모바일에서 RGBA32로 풀리면 {FormatMb(bytes)}(가로x세로x4바이트)이며, " +
                   $"압축 포맷이어도 여러 장이 겹치면 메모리 부족으로 앱이 종료될 수 있습니다. {LargeTextureSize} 미만으로 줄이는 것을 권장합니다.";
        }

        #endregion
    }
}
