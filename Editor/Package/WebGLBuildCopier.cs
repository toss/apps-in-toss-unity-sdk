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
    /// - 추가 사용자 BuildConfig 파일 복사 (재귀)
    /// - ait-build 폴더의 이전 결과물 정리 (node_modules/설정 파일, public/의 미러 대상은 유지)
    /// - Early fetch 스크립트 생성 (WebGLBuildCopier.EarlyFetch.cs)
    /// internal 멤버는 Editor/AssemblyInfo.cs 의 InternalsVisibleTo 를 통해 테스트 어셈블리에서 접근됩니다.
    /// </summary>
    internal static partial class WebGLBuildCopier
    {
        /// <summary>
        /// public/ 하위에서 <see cref="CopyWebGLToPublic"/>가 미러로 소유(생성·갱신·잔여물 정리)하는
        /// 디렉토리 목록. <see cref="PrepareAitBuildFolder"/>는 이 목록만 보존해 변경분 미러 복사가
        /// 실제로 스킵될 수 있게 하고, 나머지 public/ 항목은 이전과 동일하게 매 빌드 정리한다.
        /// </summary>
        internal static readonly string[] MirroredPublicDirectories =
        {
            "Build",
            "TemplateData",
            "Runtime",
            "StreamingAssets"
        };

        /// <summary>
        /// 패키징 산출물(미니앱 아카이브) 확장자. web-framework 2.x/3.x의 `ait build`는 이 파일을
        /// ait-build 루트에 emit한다(3.x의 dist/에는 vite 산출물인 dist/web만 생성됨).
        /// AITBuildValidator.ValidateDistOutput이 "루트 → dist/" 순으로 탐색하는 위치 규약과 동일.
        /// </summary>
        internal const string AitArchiveExtension = ".ait";

        /// <summary>
        /// Unity WebGL 빌드를 public 폴더로 복사합니다.
        /// <see cref="MirroredPublicDirectories"/>는 이 함수가 미러로 소유합니다 — 변경분만 복사하고,
        /// 소스에 없는 파일·디렉토리와 소스 자체가 사라진 미러 대상은 제거해 전체 삭제+재복사와
        /// 동일한 최종 상태를 보장합니다.
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

            // 필수 파일만 선별 복사 (변경분만 — 크기/내용이 같으면 스킵해 초 단위 I/O를 줄인다)
            var filesToCopy = new List<string> { loaderFile, dataFile, frameworkFile, wasmFile };
            if (!string.IsNullOrEmpty(symbolsFile))
            {
                filesToCopy.Add(symbolsFile);
            }

            Directory.CreateDirectory(buildDest);

            long totalBytes = 0;
            try
            {
                int copiedCount = 0, skippedCount = 0, staleCount = 0;
                foreach (var fileName in filesToCopy)
                {
                    string src = Path.Combine(buildSrc, fileName);
                    string dest = Path.Combine(buildDest, fileName);
                    if (CopyFileIfChanged(src, dest)) copiedCount++; else skippedCount++;
                    totalBytes += new FileInfo(src).Length;
                }

                // 미러 의미론 유지: 압축 포맷 전환(.br ↔ .unityweb 등)이나 symbols 파일 유무 변경으로
                // 이전 선택 집합에만 있던 잔존 파일이 남지 않도록 제거한다.
                // (public/이 빌드마다 통째로 삭제되지 않으므로 이 정리가 유일한 잔여물 방어선이다.)
                var desiredNames = new HashSet<string>(filesToCopy, System.StringComparer.OrdinalIgnoreCase);
                foreach (var existing in Directory.GetFiles(buildDest))
                {
                    if (!desiredNames.Contains(Path.GetFileName(existing)))
                    {
                        File.Delete(existing);
                        staleCount++;
                    }
                }

                // Unity WebGL의 Build/ 산출물은 평면 구조라 하위 디렉토리는 모두 잔여물이다.
                foreach (var existingDir in Directory.GetDirectories(buildDest))
                {
                    Directory.Delete(existingDir, true);
                    staleCount++;
                }

                Debug.Log($"[AIT] ✓ Build 파일 {filesToCopy.Count}개 선별 복사 완료 (복사 {copiedCount}개, 스킵 {skippedCount}개, 잔여물 정리 {staleCount}개, {totalBytes / 1024.0 / 1024.0:0.#}MB)");
            }
            catch (System.Exception ex)
            {
                // 기능 정확성이 속도보다 우선 — 변경분 복사 경로에서 실패하면 기존 전체 삭제+재복사로 폴백.
                Debug.LogWarning($"[AIT] Build 폴더 변경분 복사 실패, 전체 재복사로 폴백: {ex.GetType().Name}: {ex.Message}");

                if (!AITFileUtils.DeleteDirectory(buildDest))
                {
                    Debug.LogWarning($"[AIT] 이전 빌드 잔여물 정리 실패: {buildDest} — 새 빌드에 오래된 파일이 섞일 수 있습니다");
                }
                Directory.CreateDirectory(buildDest);

                totalBytes = 0;
                foreach (var fileName in filesToCopy)
                {
                    string src = Path.Combine(buildSrc, fileName);
                    string dest = Path.Combine(buildDest, fileName);
                    File.Copy(src, dest, true);
                    UnityUtil.EnsureFileReadable(dest);
                    totalBytes += new FileInfo(src).Length;
                }

                Debug.Log($"[AIT] ✓ Build 파일 {filesToCopy.Count}개 전체 재복사 완료 ({totalBytes / 1024.0 / 1024.0:0.#}MB)");
            }

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
                MirrorDirectorySafe(templateDataSrc, templateDataDest, "TemplateData");
            }
            else
            {
                RemoveStalePublicDirectory(templateDataDest, "TemplateData");
            }

            // Runtime 폴더 → public/Runtime
            // 1순위: webgl/ 폴더에 Runtime이 있으면 사용 (AITTemplate 빌드)
            // 2순위: webgl/ 폴더에 Runtime이 없으면 SDK 템플릿에서 복사
            string runtimeSrc = Path.Combine(webglPath, "Runtime");
            string runtimeDest = Path.Combine(publicPath, "Runtime");
            if (Directory.Exists(runtimeSrc))
            {
                MirrorDirectorySafe(runtimeSrc, runtimeDest, "Runtime");
            }
            else
            {
                // SDK 템플릿에서 Runtime 폴더 복사 (수동 WebGL 빌드 시 AITTemplate 미사용 대응).
                // 이 분기는 SDK가 자가복구를 수행하는 정상 폴백 경로이므로 Log로 출력한다
                // (LogWarning으로 두면 ErrorTracker가 Sentry로 송신해 노이즈가 됨 — Sentry R8).
                Debug.Log("[AIT] WebGL 빌드에 Runtime 폴더가 없어 SDK 템플릿에서 복사합니다 (AITTemplate이 아닌 다른 템플릿으로 빌드되었을 수 있음).");
                Debug.Log("[AIT] ⚠ 커스텀(비AITTemplate) 템플릿에서는 PlayerPrefs 영속화가 적용되지 않습니다 (index.html의 %AIT_PLAYERPREFS_PERSISTENCE% 치환/스크립트 삽입이 AITTemplate 전용).");
                string sdkRuntimePath = SdkPathResolver.FindSdkRuntimePath();
                if (!string.IsNullOrEmpty(sdkRuntimePath) && Directory.Exists(sdkRuntimePath))
                {
                    MirrorDirectorySafe(sdkRuntimePath, runtimeDest, "Runtime(SDK 템플릿)");
                    Debug.Log("[AIT] ✓ Runtime 폴더: SDK 템플릿에서 복사 완료");
                }
                else
                {
                    Debug.LogError("[AIT] Runtime 폴더를 찾을 수 없습니다. 'Build And Package'를 사용하세요.");
                    // 소스를 어디서도 찾지 못한 경우, 이전 빌드의 Runtime을 그대로 서빙하면
                    // 원인 파악이 더 어려워지므로 잔여물을 제거한다(public 전체 삭제 시절과 동일한 최종 상태).
                    RemoveStalePublicDirectory(runtimeDest, "Runtime");
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
                MirrorDirectorySafe(streamingAssetsSrc, streamingAssetsDest, "StreamingAssets");
            }
            else
            {
                RemoveStalePublicDirectory(streamingAssetsDest, "StreamingAssets");
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

            // 프로필 기반 설정값
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
                // ── 템플릿에서 작은따옴표 문자열 리터럴 자리에 놓이는 토큰 ──
                // 값에 작은따옴표·개행·"</script>"가 들어가면 그 인라인 <script> 블록 전체가
                // SyntaxError 로 죽는다(appInfo = { iconUrl: '%AIT_ICON_URL%', ... }).
                // "따옴표 자리면 예외 없이 이스케이프" 규칙을 유지한다 — 아래 셋처럼 값이 상수라
                // 실질 no-op 인 경우에도 그대로 통과시켜, 호출부마다 안전 여부를 판단하는 일이
                // 없도록 한다(판단이 개입하는 순간 다음 토큰에서 빠뜨리게 된다).
                // AITJsStringEscaperWiringTests 가 템플릿에서 따옴표 자리 토큰을 유도해 대조한다.
                .Replace("%AIT_ENABLE_DEBUG_CONSOLE%", AITJsStringEscaper.EscapeSingleQuoted(enableDebugConsole))
                .Replace("%AIT_FIRST_INTERACTIVE_LOG%", AITJsStringEscaper.EscapeSingleQuoted(EffectiveFirstInteractiveLog(config) ? "true" : "false"))
                .Replace("%AIT_PLAYERPREFS_PERSISTENCE%", AITJsStringEscaper.EscapeSingleQuoted(EffectivePlayerPrefsPersistence(config) ? "true" : "false"))
                .Replace("%AIT_ICON_URL%", AITJsStringEscaper.EscapeSingleQuoted(config.iconUrl ?? ""))
                .Replace("%AIT_DISPLAY_NAME%", AITJsStringEscaper.EscapeSingleQuoted(config.displayName ?? ""))
                .Replace("%AIT_PRIMARY_COLOR%", AITJsStringEscaper.EscapeSingleQuoted(config.primaryColor ?? "#3182f6"))
                // ── 코드 문맥(값이 그대로 JS 로 전개) — 이스케이프하면 안 된다 ──
                .Replace("%AIT_DEVICE_PIXEL_RATIO%", config.devicePixelRatio.ToString())
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

            Debug.Log("[AIT] Unity WebGL 빌드 복사 완료");
            Debug.Log("[AIT]   - index.html → 프로젝트 루트");
            Debug.Log("[AIT]   - Build, TemplateData, Runtime → public/");

            return AITConvertCore.AITExportError.SUCCEED;
        }

        /// <summary>
        /// 파일이 이미 동일한 내용인지 판정합니다 (크기 비교 → 동일하면 청크 단위 바이트 비교).
        /// mtime은 Unity가 매 빌드 산출물을 다시 쓰므로 판정 기준에서 제외한다.
        /// </summary>
        private static bool FilesAreIdentical(string srcPath, string destPath)
        {
            var srcInfo = new FileInfo(srcPath);
            var destInfo = new FileInfo(destPath);
            if (!destInfo.Exists || srcInfo.Length != destInfo.Length)
            {
                return false;
            }

            const int bufferSize = 1024 * 1024;
            var bufferA = new byte[bufferSize];
            var bufferB = new byte[bufferSize];

            using (var fsA = new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize))
            using (var fsB = new FileStream(destPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize))
            {
                int readA;
                while ((readA = fsA.Read(bufferA, 0, bufferSize)) > 0)
                {
                    int readB = fsB.Read(bufferB, 0, readA);
                    if (readA != readB)
                    {
                        return false;
                    }
                    for (int i = 0; i < readA; i++)
                    {
                        if (bufferA[i] != bufferB[i])
                        {
                            return false;
                        }
                    }
                }
                return true;
            }
        }

        /// <summary>
        /// 소스 파일을 대상 경로로 복사하되, 이미 동일한 파일이 있으면 복사를 스킵합니다.
        /// internal 승격: EditMode 테스트(AppsInTossEditModeTests, InternalsVisibleTo)에서 헬퍼 단위로 검증하기 위함.
        /// </summary>
        /// <returns>실제로 복사했으면 true, 동일 파일이라 스킵했으면 false</returns>
        internal static bool CopyFileIfChanged(string srcPath, string destPath)
        {
            if (File.Exists(destPath) && FilesAreIdentical(srcPath, destPath))
            {
                return false;
            }

            File.Copy(srcPath, destPath, true);
            UnityUtil.EnsureFileReadable(destPath);
            return true;
        }

        /// <summary>
        /// srcDir → destDir 재귀 미러 복사: 변경된 파일만 복사하고, destDir에서 srcDir에 없는
        /// 파일/디렉토리를 제거해 stale 산출물이 남지 않게 한다 (Unity 버전 전환으로 파일명 세트가
        /// 바뀌는 경우 포함). .meta 파일은 UnityUtil.CopyDirectory와 동일하게 복사·정리 대상에서
        /// 제외한다 (Unity가 대상 위치에 새로 생성 — GUID 충돌 방지).
        /// internal 승격: EditMode 테스트(AppsInTossEditModeTests, InternalsVisibleTo)에서 미러 의미론을 검증하기 위함.
        /// </summary>
        internal static void MirrorCopyDirectory(string srcDir, string destDir, ref int copiedCount, ref int skippedCount, ref int staleCount)
        {
            Directory.CreateDirectory(destDir);

            var srcFileNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(srcDir))
            {
                if (file.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase)) continue;

                string fileName = Path.GetFileName(file);
                srcFileNames.Add(fileName);

                string destFile = Path.Combine(destDir, fileName);
                if (CopyFileIfChanged(file, destFile)) copiedCount++; else skippedCount++;
            }

            var srcDirNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var dir in Directory.GetDirectories(srcDir))
            {
                string dirName = Path.GetFileName(dir);
                srcDirNames.Add(dirName);
                MirrorCopyDirectory(dir, Path.Combine(destDir, dirName), ref copiedCount, ref skippedCount, ref staleCount);
            }

            // stale 정리: 소스에 더 이상 없는 파일/디렉토리는 dest에서 제거 (.meta는 위와 동일하게 건드리지 않음)
            foreach (var existingFile in Directory.GetFiles(destDir))
            {
                string fileName = Path.GetFileName(existingFile);
                if (fileName.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (!srcFileNames.Contains(fileName))
                {
                    File.Delete(existingFile);
                    staleCount++;
                }
            }

            foreach (var existingDir in Directory.GetDirectories(destDir))
            {
                string dirName = Path.GetFileName(existingDir);
                if (!srcDirNames.Contains(dirName))
                {
                    Directory.Delete(existingDir, true);
                    staleCount++;
                }
            }
        }

        /// <summary>
        /// 이번 빌드에서 소스가 사라진 미러 대상 디렉토리를 public/에서 제거한다.
        /// PrepareAitBuildFolder가 public/을 통째로 지우지 않게 되면서(변경분 미러 복사 보존),
        /// "소스가 사라진 디렉토리"의 정리 책임이 이쪽으로 넘어왔다.
        /// </summary>
        private static void RemoveStalePublicDirectory(string destDir, string label)
        {
            if (!Directory.Exists(destDir)) return;

            if (AITFileUtils.DeleteDirectory(destDir))
            {
                Debug.Log($"[AIT] ✓ {label} 소스가 없어 public 잔여물 제거");
            }
            else
            {
                Debug.LogWarning($"[AIT] {label} 잔여물 정리 실패: {destDir} — 이전 빌드 파일이 서빙될 수 있습니다");
            }
        }

        /// <summary>
        /// MirrorCopyDirectory를 실패 시 기존 전체 삭제+재복사(UnityUtil.CopyDirectory)로 폴백하는
        /// 안전 래퍼. 기능 정확성이 속도보다 우선이므로 예외가 나면 변경분 복사를 포기하고 통째로 다시 복사한다.
        /// </summary>
        private static void MirrorDirectorySafe(string srcDir, string destDir, string label)
        {
            try
            {
                int copiedCount = 0, skippedCount = 0, staleCount = 0;
                MirrorCopyDirectory(srcDir, destDir, ref copiedCount, ref skippedCount, ref staleCount);
                Debug.Log($"[AIT] ✓ {label} 미러 복사 완료 (복사 {copiedCount}개, 스킵 {skippedCount}개, 잔여물 정리 {staleCount}개)");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AIT] {label} 변경분 복사 실패, 전체 재복사로 폴백: {ex.GetType().Name}: {ex.Message}");
                if (Directory.Exists(destDir))
                {
                    AITFileUtils.DeleteDirectory(destDir);
                }
                UnityUtil.CopyDirectory(srcDir, destDir);
            }
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

                // 무조건 덮어쓰기(File.Copy overwrite)가 아니라 IfChanged 경로를 타야 한다:
                // excludeFolders 에 "public" 이 없어서 사용자가 BuildConfig~/public/ 에 정적 파일을
                // 두면 그대로 ait-build/public/ 로 복사되는데, PackageBuildStateMarker 는 public/ 트리를
                // (경로, 길이, mtimeTicks)로 해시하며 "mtime 불변 == 내용 불변"을 전제한다.
                // 내용이 같은데도 매번 다시 쓰면 mtime 이 전진해 패키징 스킵이 영원히 발동하지 않는다.
                string destFile = Path.Combine(destDir, fileName);
                CopyFileIfChanged(file, destFile);

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
        /// PlayerPrefs 영속화(앱인토스 Storage) 실효 활성 여부를 반환한다(tri-state 해석).
        /// 설정 로드 실패 시에도 기본 보호를 제공하기 위해 null → true(fail-open).
        /// playerPrefsPersistence >= 0 이면 ==1, &lt;0 이면 GetDefaultPlayerPrefsPersistence().
        /// </summary>
        internal static bool EffectivePlayerPrefsPersistence(AITEditorScriptObject config)
        {
            if (config == null) return true; // fail-open: 설정 로드 실패 시 기본 보호 유지
            return config.playerPrefsPersistence >= 0
                ? config.playerPrefsPersistence == 1
                : AITDefaultSettings.GetDefaultPlayerPrefsPersistence();
        }

    }
}
