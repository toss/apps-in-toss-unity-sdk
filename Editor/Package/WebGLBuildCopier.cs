using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor.Package
{
    /// <summary>
    /// Unity WebGL 빌드 결과물을 Vite 기반 ait-build 프로젝트 구조로 복사/가공.
    /// - index.html은 프로젝트 루트로 (Vite 요구); Unity/AIT 플레이스홀더 치환, 사용자 커스텀 섹션 머지, 로딩 화면 삽입 포함
    /// - Build/TemplateData/Runtime은 public/ 하위로 (필수 파일 선별 복사)
    /// - Runtime/appsintoss-unity-bridge.js의 Mock 브릿지 플래그 치환
    /// - 추가 사용자 BuildConfig 파일 복사 (재귀)
    /// - ait-build 폴더의 이전 결과물 정리 (node_modules/설정 파일은 유지)
    /// - Early fetch 스크립트 생성
    /// internal 멤버는 Editor/AssemblyInfo.cs 의 InternalsVisibleTo 를 통해 테스트 어셈블리에서 접근됩니다.
    /// </summary>
    internal static class WebGLBuildCopier
    {
        /// <summary>
        /// Unity WebGL 빌드를 public 폴더로 복사합니다.
        /// </summary>
        /// <returns>성공 시 SUCCEED, 실패 시 해당 에러 코드</returns>
        internal static AITConvertCore.AITExportError CopyWebGLToPublic(string webglPath, string buildProjectPath, AITBuildProfile profile = null)
        {
            // 프로필이 없으면 기본 프로필 사용
            if (profile == null)
            {
                profile = AITBuildProfile.CreateProductionProfile();
            }

            var config = UnityUtil.GetEditorConf();

            // Unity WebGL 빌드를 Vite 프로젝트에 복사
            // - index.html: 프로젝트 루트 (Vite 요구사항)
            // - Build, TemplateData, Runtime: public 폴더 (정적 자산)
            string publicPath = Path.Combine(buildProjectPath, "public");

            // public 폴더 생성
            if (!Directory.Exists(publicPath))
            {
                Directory.CreateDirectory(publicPath);
            }

            // Build 폴더 → public/Build (필수 파일만 선별 복사)
            string buildSrc = Path.Combine(webglPath, "Build");
            string buildDest = Path.Combine(publicPath, "Build");

            if (!Directory.Exists(buildSrc))
            {
                // Build 폴더 부재는 사용자가 WebGL 빌드를 실행하지 않은 워크플로우 문제이며
                // SDK 자체 버그가 아니므로 Sentry 캡처를 억제한다 (APPS-IN-TOSS-UNITY-SDK-12E).
                // 오류 메시지는 Unity Console에 표시되어 사용자에게 안내된다.
                AITLog.Error(
                    "[AIT] ✗ 치명적: Build 폴더를 찾을 수 없습니다!\n"
                    + $"검색 경로: {buildSrc}",
                    sentryCapture: false
                );
                return AITConvertCore.AITExportError.BUILD_FOLDER_MISSING;
            }

            // Build 폴더에서 실제 파일 이름 찾기
            // 빌드 마커에서 압축 포맷 정보를 읽어 정확한 확장자로 탐지
            Debug.Log("[AIT] WebGL 빌드 파일 검색 중...");

            var buildInfo = AITConvertCore.ReadBuildMarker(webglPath);
            int compressionFormat = buildInfo?.compressionFormat ?? -1;
            bool decompressionFallback = buildInfo?.decompressionFallback ?? false;
            var patterns = AITBuildValidator.GetFilePatterns(compressionFormat, decompressionFallback);

            // 폴백 경로 존재 여부: 정확한 패턴(압축 포맷 또는 .unityweb)이 있을 때만 와일드카드 폴백 가능
            bool hasFallbackPath = compressionFormat >= 0 || decompressionFallback;

            if (buildInfo != null)
            {
                string[] formatNames = { "Disabled", "Gzip", "Brotli" };
                string formatName = compressionFormat >= 0 && compressionFormat < formatNames.Length ? formatNames[compressionFormat] : "Unknown";
                Debug.Log($"[AIT] 빌드 마커 감지: 압축 포맷 = {formatName} ({compressionFormat}), Decompression Fallback = {decompressionFallback}");
            }

            // 정확한 패턴으로 시도
            // 폴백 경로가 있으면 isRequired: false (와일드카드에서 에러 보고)
            // 폴백 경로가 없으면 isRequired: true (여기서 바로 에러 보고)
            string loaderFile = AITBuildValidator.FindFileInBuild(buildSrc, patterns["loader"], isRequired: true);
            string dataFile = AITBuildValidator.FindFileInBuild(buildSrc, patterns["data"], isRequired: !hasFallbackPath);
            string frameworkFile = AITBuildValidator.FindFileInBuild(buildSrc, patterns["framework"], isRequired: !hasFallbackPath);
            string wasmFile = AITBuildValidator.FindFileInBuild(buildSrc, patterns["wasm"], isRequired: !hasFallbackPath);

            // 선택적 파일
            string symbolsFile = AITBuildValidator.FindFileInBuild(buildSrc, patterns["symbols"]);

            // 정확한 패턴으로 못 찾으면 와일드카드로 폴백 (loader는 압축 무관하므로 제외)
            if (hasFallbackPath)
            {
                var fallback = AITBuildValidator.GetFilePatterns(-1);
                if (string.IsNullOrEmpty(dataFile)) dataFile = AITBuildValidator.FindFileInBuild(buildSrc, fallback["data"], isRequired: true);
                if (string.IsNullOrEmpty(frameworkFile)) frameworkFile = AITBuildValidator.FindFileInBuild(buildSrc, fallback["framework"], isRequired: true);
                if (string.IsNullOrEmpty(wasmFile)) wasmFile = AITBuildValidator.FindFileInBuild(buildSrc, fallback["wasm"], isRequired: true);
                if (string.IsNullOrEmpty(symbolsFile)) symbolsFile = AITBuildValidator.FindFileInBuild(buildSrc, fallback["symbols"]);
            }

            // 필수 파일 검증
            var missingFiles = new List<string>();
            if (string.IsNullOrEmpty(loaderFile)) missingFiles.Add("*.loader.js");
            if (string.IsNullOrEmpty(dataFile)) missingFiles.Add("*.data");
            if (string.IsNullOrEmpty(frameworkFile)) missingFiles.Add("*.framework.js");
            if (string.IsNullOrEmpty(wasmFile)) missingFiles.Add("*.wasm");

            if (missingFiles.Count > 0)
            {
                // Sentry로는 단일 fingerprint(누락 파일 요약)만 보내고, 상세 가이드/원인은 콘솔에만 남긴다.
                // Unity Log Listener가 \n으로 분할된 라인을 각각 다른 이슈로 묶는 경우를 회피.
                AITLog.Error($"[AIT] ✗ 치명적: WebGL 빌드 필수 파일 누락! 누락된 필수 파일: {string.Join(", ", missingFiles)}");
                AITLog.Error(
                    "[AIT]   가능한 원인:\n"
                    + "  1. Unity WebGL 빌드가 완료되지 않았습니다.\n"
                    + "  2. WebGL 빌드가 실패했지만 부분 결과물만 남아있습니다.\n"
                    + "  3. 빌드 설정(압축 방식 등)이 예상과 다릅니다.\n"
                    + "해결 방법:\n"
                    + "  1. 'Clean Build' 옵션을 활성화하고 다시 빌드하세요.\n"
                    + "  2. Unity Console에서 빌드 에러를 확인하세요.",
                    sentryCapture: false
                );
                return AITConvertCore.AITExportError.REQUIRED_FILE_MISSING;
            }

            // Build 대상 폴더 정리 후 재생성
            if (Directory.Exists(buildDest))
            {
                // 실패 시 DeleteDirectory가 내부 경고를 남기지만, 잔존 파일이 이후 복사 단계에
                // 섞일 수 있으므로 상위 레벨에서도 한 번 더 사용자에게 알림.
                // 주의: 이 LogWarning은 단순 폴백이 아니라 실제 빌드 오염 위험 신호이므로 Sentry로
                // 캡처되도록 Warning 레벨을 유지한다. (File.Copy는 덮어쓰지만 복사 대상 목록
                // (filesToCopy)에 포함되지 않은 잔존 파일은 패키지에 섞여 런타임 오류를 유발할 수 있음)
                if (!AITFileUtils.DeleteDirectory(buildDest))
                {
                    Debug.LogWarning($"[AIT] 이전 빌드 잔여물 정리 실패: {buildDest} — 새 빌드에 오래된 파일이 섞일 수 있습니다");
                }
            }
            Directory.CreateDirectory(buildDest);

            // 필수 파일만 선별 복사
            var filesToCopy = new List<string> { loaderFile, dataFile, frameworkFile, wasmFile };
            if (!string.IsNullOrEmpty(symbolsFile))
            {
                filesToCopy.Add(symbolsFile);
            }

            long totalBytes = 0;
            foreach (var fileName in filesToCopy)
            {
                string src = Path.Combine(buildSrc, fileName);
                string dest = Path.Combine(buildDest, fileName);
                File.Copy(src, dest, true);
                UnityUtil.EnsureFileReadable(dest);
                totalBytes += new FileInfo(src).Length;
            }

            Debug.Log($"[AIT] ✓ Build 파일 {filesToCopy.Count}개 선별 복사 완료 ({totalBytes / 1024.0 / 1024.0:0.#}MB)");

            // 안전장치: Build/ 폴더에 인식되지 않은 파일이 있으면 로그 출력
            var allBuildFiles = Directory.GetFiles(buildSrc);
            var copiedFileNames = new HashSet<string>(filesToCopy);
            foreach (var file in allBuildFiles)
            {
                string name = Path.GetFileName(file);
                if (!copiedFileNames.Contains(name))
                {
                    Debug.Log($"[AIT] Build 폴더에 복사되지 않은 파일: {name}");
                }
            }

            // TemplateData 폴더 → public/TemplateData
            string templateDataSrc = Path.Combine(webglPath, "TemplateData");
            string templateDataDest = Path.Combine(publicPath, "TemplateData");
            if (Directory.Exists(templateDataSrc))
            {
                UnityUtil.CopyDirectory(templateDataSrc, templateDataDest);
            }

            // Runtime 폴더 → public/Runtime
            // 1순위: webgl/ 폴더에 Runtime이 있으면 사용 (AITTemplate 빌드)
            // 2순위: webgl/ 폴더에 Runtime이 없으면 SDK 템플릿에서 복사
            string runtimeSrc = Path.Combine(webglPath, "Runtime");
            string runtimeDest = Path.Combine(publicPath, "Runtime");
            if (Directory.Exists(runtimeSrc))
            {
                UnityUtil.CopyDirectory(runtimeSrc, runtimeDest);
            }
            else
            {
                // SDK 템플릿에서 Runtime 폴더 복사 (수동 WebGL 빌드 시 AITTemplate 미사용 대응).
                // 이 분기는 SDK가 자가복구를 수행하는 정상 폴백 경로이므로 Log로 출력한다
                // (LogWarning으로 두면 ErrorTracker가 Sentry로 송신해 노이즈가 됨 — Sentry R8).
                Debug.Log("[AIT] WebGL 빌드에 Runtime 폴더가 없어 SDK 템플릿에서 복사합니다 (AITTemplate이 아닌 다른 템플릿으로 빌드되었을 수 있음).");
                string sdkRuntimePath = SdkPathResolver.FindSdkRuntimePath();
                if (!string.IsNullOrEmpty(sdkRuntimePath) && Directory.Exists(sdkRuntimePath))
                {
                    UnityUtil.CopyDirectory(sdkRuntimePath, runtimeDest);
                    Debug.Log("[AIT] ✓ Runtime 폴더: SDK 템플릿에서 복사 완료");
                }
                else
                {
                    Debug.LogError("[AIT] Runtime 폴더를 찾을 수 없습니다. 'Build And Package'를 사용하세요.");
                }
            }

            // Dev 전용 디버그 콘솔(vConsole) 산출물 정리:
            // enableDebugConsole=false(프로덕션)면 index.html 부트스트랩이 조기 반환해
            // devconsole 스크립트를 로드하지 않지만, CopyDirectory는 플래그와 무관하게 복사한다.
            // public 저장소 산출 위생을 위해 프로덕션 빌드에서는 Runtime/devconsole/ 를 제거한다
            // (%AIT_ENABLE_DEBUG_CONSOLE% 치환과 동일하게 profile.enableDebugConsole을 소스로 사용).
            if (!profile.enableDebugConsole)
            {
                string devConsoleDest = Path.Combine(runtimeDest, "devconsole");
                if (Directory.Exists(devConsoleDest))
                {
                    Directory.Delete(devConsoleDest, true);
                    Debug.Log("[AIT] ✓ 프로덕션 빌드: Runtime/devconsole/ 제거 (디버그 콘솔 비활성화)");
                }
            }

            // StreamingAssets 폴더 → public/StreamingAssets (있는 경우)
            string streamingAssetsSrc = Path.Combine(webglPath, "StreamingAssets");
            string streamingAssetsDest = Path.Combine(publicPath, "StreamingAssets");
            if (Directory.Exists(streamingAssetsSrc))
            {
                UnityUtil.CopyDirectory(streamingAssetsSrc, streamingAssetsDest);
            }

            // index.html → 프로젝트 루트 (Vite가 루트에서 index.html을 찾음)
            string indexSrc = Path.Combine(webglPath, "index.html");
            string indexDest = Path.Combine(buildProjectPath, "index.html");

            // index.html 필수 검증
            if (!File.Exists(indexSrc))
            {
                Debug.LogError(
                    "[AIT] ✗ 치명적: index.html을 찾을 수 없습니다!\n"
                    + $"검색 경로: {indexSrc}\n"
                    + "가능한 원인:\n"
                    + "  1. Unity WebGL 빌드가 완료되지 않았습니다.\n"
                    + "  2. WebGL 템플릿이 올바르게 설정되지 않았습니다.\n"
                    + "  3. 이전 빌드가 손상되었습니다.\n"
                    + "해결 방법:\n"
                    + "  1. 'Clean Build' 옵션을 활성화하고 다시 빌드하세요.\n"
                    + "  2. AIT > Clean 메뉴로 빌드 폴더를 삭제 후 재빌드하세요."
                );
                return AITConvertCore.AITExportError.INDEX_HTML_MISSING;
            }

            string indexContent = File.ReadAllText(indexSrc);

            // 프로필 기반 설정값 (Mock 브릿지가 비활성화되면 프로덕션 모드로 간주)
            string isProduction = profile.enableMockBridge ? "false" : "true";
            string enableDebugConsole = profile.enableDebugConsole ? "true" : "false";

            // 프로젝트의 index.html에서 사용자 커스텀 섹션 추출 (있는 경우)
            string projectIndexPath = Path.Combine(Application.dataPath, "WebGLTemplates", "AITTemplate", "index.html");
            if (File.Exists(projectIndexPath))
            {
                string projectIndexContent = File.ReadAllText(projectIndexPath);

                // USER_HEAD 섹션 추출 및 교체
                string userHeadSection = AITTemplateManager.ExtractHtmlUserSection(projectIndexContent, AITTemplateManager.HTML_USER_HEAD_START, AITTemplateManager.HTML_USER_HEAD_END);
                if (userHeadSection != null)
                {
                    indexContent = AITTemplateManager.ReplaceHtmlUserSection(indexContent, AITTemplateManager.HTML_USER_HEAD_START, AITTemplateManager.HTML_USER_HEAD_END, userHeadSection);
                    Debug.Log("[AIT] index.html USER_HEAD 섹션 머지됨");
                }

                // USER_BODY_END 섹션 추출 및 교체
                string userBodyEndSection = AITTemplateManager.ExtractHtmlUserSection(projectIndexContent, AITTemplateManager.HTML_USER_BODY_END_START, AITTemplateManager.HTML_USER_BODY_END_END);
                if (userBodyEndSection != null)
                {
                    indexContent = AITTemplateManager.ReplaceHtmlUserSection(indexContent, AITTemplateManager.HTML_USER_BODY_END_START, AITTemplateManager.HTML_USER_BODY_END_END, userBodyEndSection);
                    Debug.Log("[AIT] index.html USER_BODY_END 섹션 머지됨");
                }
            }

            // Unity 플레이스홀더 치환
            indexContent = indexContent
                .Replace("%UNITY_WEB_NAME%", PlayerSettings.productName)
                .Replace("%UNITY_WIDTH%", PlayerSettings.defaultWebScreenWidth.ToString())
                .Replace("%UNITY_HEIGHT%", PlayerSettings.defaultWebScreenHeight.ToString())
                .Replace("%UNITY_COMPANY_NAME%", PlayerSettings.companyName)
                .Replace("%UNITY_PRODUCT_NAME%", PlayerSettings.productName)
                .Replace("%UNITY_PRODUCT_VERSION%", PlayerSettings.bundleVersion)
                // Unity 표준 URL 형식 (Unity가 치환하지 않은 경우 SDK가 처리)
                .Replace("%UNITY_WEBGL_LOADER_URL%", $"Build/{loaderFile}")
                .Replace("%UNITY_WEBGL_DATA_URL%", $"Build/{dataFile}")
                .Replace("%UNITY_WEBGL_FRAMEWORK_URL%", $"Build/{frameworkFile}")
                .Replace("%UNITY_WEBGL_CODE_URL%", $"Build/{wasmFile}")
                .Replace("%UNITY_WEBGL_SYMBOLS_URL%", !string.IsNullOrEmpty(symbolsFile) ? $"Build/{symbolsFile}" : "")
                // 하위 호환성을 위한 FILENAME 형식 (레거시)
                .Replace("%UNITY_WEBGL_LOADER_FILENAME%", loaderFile)
                .Replace("%UNITY_WEBGL_DATA_FILENAME%", dataFile)
                .Replace("%UNITY_WEBGL_FRAMEWORK_FILENAME%", frameworkFile)
                .Replace("%UNITY_WEBGL_CODE_FILENAME%", wasmFile)
                .Replace("%UNITY_WEBGL_SYMBOLS_FILENAME%", symbolsFile)
                // AIT 커스텀 플레이스홀더
                .Replace("%AIT_IS_PRODUCTION%", isProduction)
                .Replace("%AIT_ENABLE_DEBUG_CONSOLE%", enableDebugConsole)
                .Replace("%AIT_FIRST_INTERACTIVE_LOG%", EffectiveFirstInteractiveLog(config) ? "true" : "false")
                .Replace("%AIT_DEVICE_PIXEL_RATIO%", config.devicePixelRatio.ToString())
                .Replace("%AIT_ICON_URL%", config.iconUrl ?? "")
                .Replace("%AIT_DISPLAY_NAME%", config.displayName ?? "")
                .Replace("%AIT_PRIMARY_COLOR%", config.primaryColor ?? "#3182f6")
                // Early Fetch 스크립트 (로딩 성능 개선 + 레거시 warm-reload Cache-Storage 워밍)
                .Replace("%AIT_EARLY_FETCH_SCRIPT%", GenerateEarlyFetchScript(dataFile, wasmFile, buildSrc, PlayerSettings.bundleVersion));

            // 로딩 화면 삽입 (%AIT_LOADING_SCREEN% 플레이스홀더)
            string loadingContent = "";
            string projectLoadingPath = AITPackageInitializer.GetProjectLoadingPath();

            // 프로젝트의 loading.html 사용 (SDK 초기화 시 자동 생성됨)
            if (File.Exists(projectLoadingPath))
            {
                loadingContent = File.ReadAllText(projectLoadingPath);
                Debug.Log("[AIT] ✓ 로딩 화면 적용: " + projectLoadingPath);
            }
            else
            {
                // 폴백: SDK 기본 템플릿 직접 사용 (초기화가 실행되지 않은 경우)
                string sdkTemplatePath = AITPackageInitializer.GetSDKLoadingTemplatePath();
                if (sdkTemplatePath != null)
                {
                    loadingContent = File.ReadAllText(sdkTemplatePath);
                    Debug.Log("[AIT] ✓ SDK 기본 로딩 화면 적용");
                }
                else
                {
                    Debug.LogWarning("[AIT] 로딩 화면 파일을 찾을 수 없습니다. 빈 로딩 화면이 사용됩니다.");
                }
            }

            // %AIT_LOADING_SCREEN% 플레이스홀더 치환
            indexContent = indexContent.Replace("%AIT_LOADING_SCREEN%", loadingContent);

            File.WriteAllText(indexDest, indexContent, System.Text.Encoding.UTF8);
            Debug.Log("[AIT] index.html → 프로젝트 루트에 생성");

            // 플레이스홀더 치환 결과 검증
            if (!AITBuildValidator.ValidatePlaceholderSubstitution(indexContent, indexDest))
            {
                return AITConvertCore.AITExportError.PLACEHOLDER_SUBSTITUTION_FAILED;
            }

            // Runtime/appsintoss-unity-bridge.js 파일도 치환
            string bridgeSrc = Path.Combine(publicPath, "Runtime", "appsintoss-unity-bridge.js");
            if (File.Exists(bridgeSrc))
            {
                string bridgeContent = File.ReadAllText(bridgeSrc);
                bridgeContent = bridgeContent.Replace("%AIT_IS_PRODUCTION%", isProduction);
                File.WriteAllText(bridgeSrc, bridgeContent, System.Text.Encoding.UTF8);
                Debug.Log($"[AIT] appsintoss-unity-bridge.js Mock 브릿지 모드: {(profile.enableMockBridge ? "활성화" : "비활성화")}");
            }

            Debug.Log("[AIT] Unity WebGL 빌드 복사 완료");
            Debug.Log("[AIT]   - index.html → 프로젝트 루트");
            Debug.Log("[AIT]   - Build, TemplateData, Runtime → public/");

            return AITConvertCore.AITExportError.SUCCEED;
        }

        /// <summary>
        /// 프로젝트 BuildConfig의 추가 파일들을 재귀적으로 복사합니다.
        /// internal 승격: facade(AITPackageBuilder.CopyBuildConfigFromTemplate)에서 호출하기 위함.
        /// </summary>
        internal static void CopyAdditionalUserFiles(string projectBuildConfigPath, string destPath)
        {
            if (!Directory.Exists(projectBuildConfigPath)) return;

            // 루트 레벨에서 제외할 파일들
            // (pnpm-workspace.yaml은 BuildConfigMerger.CopyPnpmWorkspaceWithFallback가 전담 복사하므로 제외)
            var excludeRootFiles = new HashSet<string>
            {
                "package.json", "pnpm-lock.yaml", "pnpm-workspace.yaml", "vite.config.ts",
                "tsconfig.json", "unity-bridge.ts", "granite.config.ts",
                "apps-in-toss.config.ts"
            };

            // 제외할 폴더들
            var excludeFolders = new HashSet<string>
            {
                "node_modules",
                ".npm-cache",
                "dist"
            };

            CopyUserFilesRecursive(projectBuildConfigPath, destPath, excludeRootFiles, excludeFolders, isRoot: true);
        }

        /// <summary>
        /// 재귀적으로 사용자 파일을 복사합니다.
        /// </summary>
        private static void CopyUserFilesRecursive(
            string sourceDir,
            string destDir,
            HashSet<string> excludeRootFiles,
            HashSet<string> excludeFolders,
            bool isRoot)
        {
            // 대상 폴더 생성
            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // 파일 복사
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);

                // 루트 레벨에서만 특정 파일 제외
                if (isRoot && excludeRootFiles.Contains(fileName))
                {
                    continue;
                }

                string destFile = Path.Combine(destDir, fileName);
                File.Copy(file, destFile, true);

                // 의미 있는 파일만 로그 출력
                if (fileName.EndsWith(".ts") || fileName.EndsWith(".tsx") ||
                    fileName.EndsWith(".js") || fileName.EndsWith(".jsx") ||
                    fileName.EndsWith(".css") || fileName.EndsWith(".scss"))
                {
                    Debug.Log($"[AIT]   ✓ {fileName} (사용자 추가 파일)");
                }
            }

            // 하위 폴더 재귀 복사
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(dir);

                // 제외 폴더 스킵
                if (excludeFolders.Contains(dirName))
                {
                    continue;
                }

                string destSubDir = Path.Combine(destDir, dirName);
                CopyUserFilesRecursive(dir, destSubDir, excludeRootFiles, excludeFolders, isRoot: false);

                // 폴더 복사 완료 로그
                Debug.Log($"[AIT]   ✓ {dirName}/ (사용자 추가 폴더)");
            }
        }

        /// <summary>
        /// Unity WebGL 리소스를 조기 fetch하고 Unity loader의 fetch를 인터셉트하는 스크립트를 생성합니다.
        ///
        /// 배경: &lt;link rel="preload"&gt;는 CDN의 cache-control: max-age=0 환경에서 동작하지 않습니다.
        /// preload로 받은 리소스가 즉시 stale 처리되어 Unity loader가 재사용하지 못하고 revalidation 요청을 보내며,
        /// 이는 이중 로딩과 "preload is found, but is not used" 경고를 유발합니다.
        ///
        /// 해결: head에서 JS fetch()를 즉시 시작하고, window.fetch를 일회성으로 인터셉트하여
        /// Unity loader가 같은 URL을 요청할 때 이미 받은 Response를 반환합니다.
        /// </summary>
        private static string GenerateEarlyFetchScript(string dataFile, string wasmFile, string buildSrc, string bundleVersion)
        {
            var urls = new List<string>();
            if (!string.IsNullOrEmpty(dataFile)) urls.Add($"Build/{dataFile}");
            if (!string.IsNullOrEmpty(wasmFile)) urls.Add($"Build/{wasmFile}");

            if (urls.Count == 0) return "";

            // JSON 배열로 URL 목록 생성
            var urlsJson = "[" + string.Join(",", urls.ConvertAll(u => $"\"{u}\"")) + "]";

            // Unity 6000.x 로더는 자체 IndexedDB(UnityCache)로 데이터를 검증 캐싱하므로 warm reload에서
            // 재다운로드가 없다 → 우리 Cache-Storage 오버라이드는 순이득이 없고 스테일/이중 저장 위험만 더한다.
            // 레거시(2021/2022) 로더는 데이터 캐시가 없어 warm reload마다 100MB를 재다운로드 → 로컬 preview/CDN
            // 순단에 노출된다. 따라서 Cache-Storage 워밍은 레거시에만 적용하고 6000.x는 기존 스크립트를 유지한다.
            if (!IsLegacyUnityLoader())
            {
                return GenerateEarlyFetchScriptModern(urlsJson);
            }

            string cacheName = BuildDataCacheName(dataFile, wasmFile, buildSrc, bundleVersion);
            return GenerateEarlyFetchScriptLegacyCaching(urlsJson, cacheName);
        }

        /// <summary>
        /// Application.unityVersion의 메이저가 6000 미만이면 레거시 로더(자체 데이터 캐시 없음)로 판정한다.
        /// WebGL 빌드는 에디터 버전의 로더를 임베드하므로 빌드 시 에디터 버전 == 런타임 로더 버전이다.
        /// 파싱 실패 시 보수적으로 false(기존 무캐시 동작 유지)를 반환한다.
        /// </summary>
        private static bool IsLegacyUnityLoader()
        {
            try
            {
                string v = Application.unityVersion;
                int dot = v.IndexOf('.');
                string majorStr = dot > 0 ? v.Substring(0, dot) : v;
                if (int.TryParse(majorStr, out int major))
                {
                    return major < 6000;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Cache-Storage 캐시명 생성. 콘텐츠 변경 버스팅을 위해 data/wasm 파일의 바이트 크기와 bundleVersion을
        /// 캐시명에 포함한다. 2021 고정 파일명(webgl.data)에서도 콘텐츠(에셋/코드)가 바뀌면 최소 한 파일의 크기가
        /// 달라져 새 캐시명이 되고, 이전 빌드 캐시는 콜드 로드 시 정리된다(스테일 데이터/wasm 서빙 방지).
        /// </summary>
        private static string BuildDataCacheName(string dataFile, string wasmFile, string buildSrc, string bundleVersion)
        {
            long dataSize = FileSizeSafe(Path.Combine(buildSrc, dataFile));
            long wasmSize = string.IsNullOrEmpty(wasmFile) ? 0L : FileSizeSafe(Path.Combine(buildSrc, wasmFile));
            string ver = string.IsNullOrEmpty(bundleVersion) ? "0" : bundleVersion;
            return $"ait-unity-{SanitizeCacheToken(dataFile)}-{dataSize}-{wasmSize}-{SanitizeCacheToken(ver)}";
        }

        private static long FileSizeSafe(string path)
        {
            try { return File.Exists(path) ? new FileInfo(path).Length : 0L; }
            catch { return 0L; }
        }

        private static string SanitizeCacheToken(string s)
        {
            if (string.IsNullOrEmpty(s)) return "0";
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
                if (!ok) chars[i] = '_';
            }
            return new string(chars);
        }

        /// <summary>
        /// 6000.x용 기존 Early Fetch 스크립트(무캐시): 콜드 로드에서 병렬 prefetch → 로더에 전달, 재로드에서는 미설치.
        /// </summary>
        private static string GenerateEarlyFetchScriptModern(string urlsJson)
        {
            return $@"<script>
    // Early Fetch: HTML 파싱과 동시에 리소스 다운로드를 시작하고,
    // Unity loader가 같은 URL을 요청할 때 이미 받은 Response를 반환합니다.
    (function() {{
        // 재로드 내비게이션에서는 early fetch를 건너뛴다.
        // 재로드 시점에는 이전 문서의 keep-alive 소켓이 정리되는 중이라 파싱 시점 fetch가
        // 그 해체와 경합해 ERR_CONNECTION_CLOSED로 죽을 수 있고, 레거시 Unity(2021/2022)
        // 로더는 데이터 다운로드 실패를 삼키고 undefined를 resolve해 영구 행으로 이어진다.
        // 재로드에서는 HTTP 캐시가 이미 따뜻하므로 early fetch의 이득도 거의 없다 —
        // 인터셉터를 아예 설치하지 않으면 로더가 자체 단일 fetch를 수행한다(이중 다운로드 없음).
        try {{
            var navEntries = performance.getEntriesByType && performance.getEntriesByType('navigation');
            var isReload = (navEntries && navEntries[0])
                ? navEntries[0].type === 'reload'
                : (performance.navigation && performance.navigation.type === 1);
            if (isReload) return;
        }} catch (e) {{}}
        var earlyFetchMap = {{}};
        var urls = {urlsJson};
        for (var i = 0; i < urls.length; i++) {{
            (function(href, fetchUrl) {{
                var p = fetch(fetchUrl).catch(function() {{ delete earlyFetchMap[href]; return null; }});
                earlyFetchMap[href] = p;
            }})(new URL(urls[i], location.href).href, urls[i]);
        }}
        var originalFetch = window.fetch;
        window.fetch = function(resource, init) {{
            var url = (typeof resource === 'string') ? new URL(resource, location.href).href : resource.url;
            var pending = earlyFetchMap[url];
            if (pending) {{
                delete earlyFetchMap[url];
                if (Object.keys(earlyFetchMap).length === 0) {{
                    window.fetch = originalFetch;
                }}
                // early fetch 실패(null)·비정상 응답(!ok)·body 소진 시 원본 fetch로 재시도
                var self = this, args = arguments;
                return pending.then(function(r) {{
                    return (r && r.ok && !r.bodyUsed) ? r : originalFetch.apply(self, args);
                }});
            }}
            return originalFetch.apply(this, arguments);
        }};
    }})();
    </script>";
        }

        /// <summary>
        /// 레거시(2021/2022) 로더용 데이터 캐싱 + 버퍼링 재시도 fetch 인터셉터.
        ///
        /// 문제: 레거시 로더는 데이터 캐시가 없어 warm reload마다 webgl.data/wasm(~100MB)를 재다운로드한다.
        /// 부하 상태의 로컬 vite preview/CDN이 본문 스트림을 중단(ERR_CONNECTION_CLOSED)하면 로더의
        /// downloadBinary().catch가 실패를 삼키고 undefined를 resolve → new DataView(undefined.buffer)
        /// throw(loader.js:620) → 영구 로딩 행.
        ///
        /// 대책(버퍼링-재시도 모델 — 캐시가 아니라 네트워크 경로 자체가 순단에서 복구):
        ///  - window.fetch 오버라이드가 data/wasm 요청을 가로챈다. 캐시 HIT면 네트워크 없이 서빙(warm reload
        ///    순단 원천 차단). MISS면 bufferedFetch로 진행.
        ///  - bufferedFetch: 본문을 arrayBuffer()로 끝까지 받고 Content-Length와 대조. 스트림 중단 →
        ///    arrayBuffer reject, 길이 불일치 → throw → 최대 MAX_TRIES회 재시도. 성공 시 완결 버퍼를
        ///    Cache Storage에 저장(버퍼가 이미 완전 → put 원자적, 부분/오염 엔트리 없음)하고 로더에는
        ///    완결 Response(body 스트림 + Content-Length)를 반환한다 → 로더는 undefined를 볼 수 없다.
        ///  - 콜드 로드에서 data/wasm 모두 결정적으로 캐싱되므로(이전 clone-tee가 CI에서 wasm을 못 담던 문제
        ///    해소) 이후 reload는 전량 HIT → 네트워크 접촉 0.
        ///  - 저메모리 기기(deviceMemory<4)는 ~80MB 버퍼링이 OOM 위험 → 버퍼링/캐싱을 생략하고 원본
        ///    스트리밍 fetch로 폴백(기존 동작 유지, 제품 워치독이 방어).
        ///  - 워치독 복구 reload는 SKIP_KEY로 캐시를 1회 우회 → 오염 캐시가 복구를 막지 않음(self-amplification 차단).
        ///  - 캐시명에 data/wasm 바이트 크기 포함 → 콘텐츠 변경 시 자동 버스팅(2021 고정 파일명 스테일 방지).
        /// </summary>
        private static string GenerateEarlyFetchScriptLegacyCaching(string urlsJson, string cacheName)
        {
            return $@"<script>
    (function() {{
        var urls = {urlsJson};
        if (!urls || !urls.length) return;
        var CACHE_NAME = '{cacheName}';
        var SKIP_KEY = '__ait_skip_data_cache__';
        var MAX_TRIES = 3;

        var knownSet = {{}};
        for (var i = 0; i < urls.length; i++) {{
            knownSet[new URL(urls[i], location.href).href] = urls[i];
        }}

        // Cache Storage 가용성(보안 컨텍스트 필요: https 또는 localhost — E2E/프로덕션 모두 충족).
        var hasCache = false;
        try {{ hasCache = !!(self.caches && self.caches.open); }} catch (e) {{ hasCache = false; }}

        // 저메모리 기기(모바일 WebView)는 큰 파일 전체 버퍼링(~80MB)이 OOM 위험 → deviceMemory<4면
        // 버퍼링/캐싱을 생략하고 원본 스트리밍 fetch로 폴백(기존 동작 유지 + 제품 워치독 방어).
        // deviceMemory 미지원(undefined)이면 허용(데스크톱/CI 등 메모리 충분 가정).
        var bufOK = true;
        try {{ if (typeof navigator.deviceMemory === 'number' && navigator.deviceMemory < 4) bufOK = false; }} catch (e) {{}}
        var cacheOK = hasCache && bufOK;

        var isReload = false;
        try {{
            var navEntries = performance.getEntriesByType && performance.getEntriesByType('navigation');
            isReload = (navEntries && navEntries[0])
                ? navEntries[0].type === 'reload'
                : (performance.navigation && performance.navigation.type === 1);
        }} catch (e) {{}}

        // 워치독 복구 reload는 캐시를 1회 우회하고 네트워크(버퍼링 재시도)로 직행(오염 캐시에 복구가 갇히지 않도록).
        var skipCacheOnce = false;
        try {{
            if (sessionStorage.getItem(SKIP_KEY)) {{ skipCacheOnce = true; sessionStorage.removeItem(SKIP_KEY); }}
        }} catch (e) {{}}

        try {{ console.log('[AIT] cache: legacy active isReload=' + isReload + ' cacheOK=' + cacheOK + ' skip=' + skipCacheOnce + ' devMem=' + (navigator.deviceMemory)); }} catch (e) {{}}

        var originalFetch = window.fetch;

        // 콜드 로드에서 이전(스테일) 빌드 캐시 정리(현재 캐시명은 data/wasm 바이트 크기로 버스팅됨).
        if (!isReload && cacheOK) {{
            try {{
                self.caches.keys().then(function(names) {{
                    names.forEach(function(n) {{
                        if (n.indexOf('ait-unity-') === 0 && n !== CACHE_NAME) self.caches.delete(n);
                    }});
                }}).catch(function() {{}});
            }} catch (e) {{}}
        }}

        // 완결 버퍼로 Cache Storage에 저장. 버퍼가 이미 완전하므로 라이브 소켓/스트림 중단과 무관하게
        // 저장이 원자적으로 성공한다(부분/오염 엔트리 없음). fire-and-forget: 이후 reload가 HIT로 서빙.
        function storeBuffer(url, buf, ct) {{
            try {{
                var h = {{ 'Content-Type': ct || 'application/octet-stream', 'Content-Length': String(buf.byteLength) }};
                self.caches.open(CACHE_NAME).then(function(c) {{
                    return c.put(url, new Response(buf, {{ status: 200, headers: h }}));
                }}).then(function() {{
                    try {{ console.log('[AIT] cache: stored ' + url); }} catch (e) {{}}
                }}).catch(function() {{
                    try {{ console.warn('[AIT] cache: put failed ' + url); }} catch (e) {{}}
                }});
            }} catch (e) {{}}
        }}

        // 버퍼링 다운로드(재시도): 본문을 끝까지 받고 Content-Length와 대조.
        // 스트림 중단(ERR_CONNECTION_CLOSED) → arrayBuffer reject, 길이 불일치 → 재시도.
        // 성공 시 (cacheOK면) 캐시에 저장하고 로더에는 완결 Response(body 스트림 + Content-Length 보유)를 반환한다.
        // 모두 소진 시 원본 fetch로 폴백(로더가 실패를 삼키면 제품 워치독 reload가 처리).
        function bufferedFetch(url, left) {{
            return originalFetch(url, {{ method: 'GET' }}).then(function(r) {{
                if (!r || !r.ok) throw new Error('bad status ' + (r && r.status));
                var ct = r.headers.get('Content-Type') || 'application/octet-stream';
                var expected = parseInt(r.headers.get('Content-Length') || '-1', 10);
                return r.arrayBuffer().then(function(ab) {{
                    var buf = new Uint8Array(ab);
                    if (expected >= 0 && buf.byteLength !== expected) {{
                        throw new Error('short read ' + buf.byteLength + '/' + expected);
                    }}
                    if (cacheOK) {{ storeBuffer(url, buf, ct); }}
                    return new Response(buf, {{ status: 200, headers: {{ 'Content-Type': ct, 'Content-Length': String(buf.byteLength) }} }});
                }});
            }}).catch(function(e) {{
                if (left > 1) {{
                    try {{ console.warn('[AIT] cache: retry ' + url + ' (' + (left - 1) + ' left): ' + (e && e.message)); }} catch (x) {{}}
                    return bufferedFetch(url, left - 1);
                }}
                try {{ console.error('[AIT] cache: giveup ' + url + ': ' + (e && e.message)); }} catch (x) {{}}
                return originalFetch(url, {{ method: 'GET' }});
            }});
        }}

        window.fetch = function(resource, init) {{
            var url = (typeof resource === 'string') ? new URL(resource, location.href).href : resource.url;
            if (!knownSet[url]) return originalFetch.apply(this, arguments);
            var self2 = this, args = arguments;

            // 저메모리/무캐시: 버퍼링 없이 원본 스트리밍 fetch(기존 동작, 제품 워치독 방어).
            if (!cacheOK) return originalFetch.apply(self2, args);

            // 캐시 우선(skip 플래그면 우회). HIT → 네트워크 없이 서빙(warm reload 순단 원천 차단).
            if (!skipCacheOnce) {{
                return self.caches.open(CACHE_NAME).then(function(c) {{
                    return c.match(url, {{ ignoreSearch: true }});
                }}).then(function(cached) {{
                    if (cached && cached.ok) {{
                        try {{ console.log('[AIT] cache: HIT ' + url); }} catch (e) {{}}
                        return cached;
                    }}
                    try {{ console.log('[AIT] cache: MISS ' + url); }} catch (e) {{}}
                    return bufferedFetch(url, MAX_TRIES);
                }}).catch(function() {{
                    return bufferedFetch(url, MAX_TRIES);
                }});
            }}
            return bufferedFetch(url, MAX_TRIES);
        }};
    }})();
    </script>";
        }

        /// <summary>
        /// first-interactive 계측 실효 활성 여부를 반환한다(tri-state 해석).
        /// 계측기는 픽셀 불변이며 설정 로드 실패가 계측을 침묵시키면 안 되므로 null → true(fail-open).
        /// (파괴적 변환 프로세서와 달리 null→false 안전 전략을 쓰지 않는다)
        /// firstInteractiveLog >= 0 이면 ==1, &lt;0 이면 GetDefaultFirstInteractiveLog().
        /// </summary>
        internal static bool EffectiveFirstInteractiveLog(AITEditorScriptObject config)
        {
            if (config == null) return true; // fail-open: 설정 로드 실패 시 계측 침묵 방지
            return config.firstInteractiveLog >= 0
                ? config.firstInteractiveLog == 1
                : AITDefaultSettings.GetDefaultFirstInteractiveLog();
        }

        /// <summary>
        /// ait-build 폴더 준비 (기존 결과물 정리)
        /// internal 승격: facade(AITPackageBuilder) 및 EditMode 테스트(리플렉션)에서 호출하기 위함.
        /// </summary>
        internal static void PrepareAitBuildFolder(string buildProjectPath)
        {
            if (!Directory.Exists(buildProjectPath))
            {
                Directory.CreateDirectory(buildProjectPath);
                Debug.Log("[AIT] ait-build 폴더 생성");
            }
            else
            {
                Debug.Log("[AIT] 기존 빌드 결과물 정리 중... (node_modules와 설정 파일은 유지)");

                string[] itemsToKeep = new string[]
                {
                    "node_modules",
                    "package.json",
                    "package-lock.json",
                    "pnpm-lock.yaml",
                    "pnpm-workspace.yaml",
                    "granite.config.ts",
                    "apps-in-toss.config.ts",
                    "vite.config.ts",
                    "tsconfig.json"
                };

                foreach (string item in Directory.GetFileSystemEntries(buildProjectPath))
                {
                    string itemName = Path.GetFileName(item);

                    bool shouldKeep = false;
                    foreach (string keepItem in itemsToKeep)
                    {
                        if (itemName == keepItem)
                        {
                            shouldKeep = true;
                            break;
                        }
                    }

                    if (shouldKeep) continue;

                    // SafeDelete/DeleteDirectory는 예외를 던지지 않고 실패 시 내부에서 경고 로그를 남김
                    if (Directory.Exists(item))
                    {
                        AITFileUtils.DeleteDirectory(item);
                    }
                    else if (File.Exists(item))
                    {
                        AITFileSystemHelper.SafeDelete(item);
                    }
                }
            }
        }
    }
}
