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
    /// - Early fetch 스크립트 생성
    /// internal 멤버는 Editor/AssemblyInfo.cs 의 InternalsVisibleTo 를 통해 테스트 어셈블리에서 접근됩니다.
    /// </summary>
    internal static class WebGLBuildCopier
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
        internal static AITConvertCore.AITExportError CopyWebGLToPublic(string webglPath, string buildProjectPath, out string inlinePrefetchJson, AITBuildProfile profile = null)
        {
            // prefetch 인라인 카탈로그는 성공 경로(WriteManifest 산출)에서만 채워짐. 그 외 경로는 null 유지.
            inlinePrefetchJson = null;

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

            // Early Fetch 캐시명(BuildDataCacheName)의 콘텐츠 버스팅 기준 크기는 재인코딩 훅 실행 '전'에
            // 스냅숏한다. brotli q11 재인코딩은 콘텐츠가 그대로여도 산출 .br 바이트 크기를 바꾸므로, 훅
            // 온/오프 토글(동일 버전 재배포·카나리 등)만으로 캐시명이 바뀌면 레거시 캐싱 스크립트의
            // 스테일-스윕(ait-unity- 접두 삭제)이 콘텐츠 불변 빌드까지 구 캐시로 오판해 콜드 부트마다
            // 불필요한 전체 재다운로드를 유발한다. 재인코딩 전(Unity 자체 출력) 크기는 동일 콘텐츠에 대해
            // 결정적이므로 이 스냅숏이 캐시 버스팅 기준으로는 더 안정적이다.
            // (totalBytes 로그·AITWarmManifestEmitter·플레이스홀더 치환 등 다른 모든 소비자는 이 스냅숏과
            // 무관하게 buildSrc 를 직접 재읽어 항상 실제(재인코딩 후) 바이트를 사용하므로 정확성 영향 없음.)
            long cacheDataSize = FileSizeSafe(Path.Combine(buildSrc, dataFile));
            long cacheWasmSize = string.IsNullOrEmpty(wasmFile) ? 0L : FileSizeSafe(Path.Combine(buildSrc, wasmFile));

            // ── (스파이크, 기본 OFF) brotli .br q11 in-place 재인코딩 ──
            // Unity 내장 brotli(~q5)를 외부 q11 로 다시 눌러 data/wasm 을 더 줄인다(동일 파일명 유지).
            // 반드시 이 지점 — 필수 파일 검증 직후, 그리고 buildSrc→buildDest 복사·totalBytes 로그·
            // early-fetch kickUrls·플레이스홀더 치환·AITWarmManifestEmitter/ValidatePlaceholderSubstitution
            // (파일 크기 읽는 모든 단계)보다 앞 — 에서 buildSrc 를 in-place 로 갱신해야 이후 복사본과
            // 그 크기 읽기들이 실제 바이트와 일치한다(캐시명은 위 스냅숏을 쓰므로 예외).
            // 훅이 뒤로 가면 totalBytes 로그·warm manifest 가 재인코딩 전 크기로 계산돼 실 바이트와 어긋난다.
            // 대상은 buildSrc 최상위 .br 파일만이며 .unityweb(감지 마커)은 AITBrotliCompressor 가 제외한다.
            if (EffectiveBrotliRecompress(config))
            {
                Debug.Log("[AIT] brotli q11 재인코딩 활성 — buildSrc 의 .br 파일을 in-place 재인코딩합니다.");
                AITBrotliCompressor.RecompressBrFilesInPlace(buildSrc);
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
                // 번들 마킹 — 이 SDK 변형(perf 채널 등) 식별자를 in-page JS(window.AITLoading.buildVariant)에 주입
                .Replace("%AIT_BUILD_VARIANT%", AITBuildVariant.Value)
                .Replace("%AIT_ENABLE_DEBUG_CONSOLE%", enableDebugConsole)
                .Replace("%AIT_FIRST_INTERACTIVE_LOG%", EffectiveFirstInteractiveLog(config) ? "true" : "false")
                .Replace("%AIT_PLAYERPREFS_PERSISTENCE%", EffectivePlayerPrefsPersistence(config) ? "true" : "false")
                .Replace("%AIT_DEVICE_PIXEL_RATIO%", config.devicePixelRatio.ToString())
                .Replace("%AIT_ICON_URL%", config.iconUrl ?? "")
                .Replace("%AIT_DISPLAY_NAME%", config.displayName ?? "")
                .Replace("%AIT_PRIMARY_COLOR%", config.primaryColor ?? "#3182f6")
                // 페이지 캐시 인터셉터 (재방문 서빙, opt-in). index.html 에서 Early Fetch 보다 '앞'에 위치해야
                // priorFetch=native 캡처 → 캐시 히트가 Early Fetch 소진과 무관하게 단락됨.
                // 각 토큰은 독립 치환이므로 치환 순서는 출력 위치를 바꾸지 않음(물리 위치는 index.html 이 보장).
                .Replace("%AIT_PAGE_CACHE_SCRIPT%", AITPageCacheEmitter.GenerateInterceptorScript(config, dataFile, frameworkFile, wasmFile))
                // Early Fetch 스크립트 (로딩 성능 개선 + 레거시 warm-reload Cache-Storage 워밍).
                // framework/loader 도 함께 조기 요청해 HTTP 캐시를 워밍한다.
                .Replace("%AIT_EARLY_FETCH_SCRIPT%", GenerateEarlyFetchScript(dataFile, frameworkFile, wasmFile, loaderFile, PlayerSettings.bundleVersion, cacheDataSize, cacheWasmSize));

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

            // Build 파일 복사 및 index.html 치환 완료 후 warm manifest 를 산출합니다.
            // [destPath = publicPath] 명세 원문은 'index.html 이 놓이는 web 루트(buildProjectPath)' 라 기술하나,
            // Build/* 파일은 publicPath(buildProjectPath/public/)에 복사되므로 wireBytes 계산이
            // buildProjectPath 기준이면 FileInfo.Length 가 실패합니다. publicPath 를 전달해야
            // Path.Combine(destPath, "Build", file) 이 실제 파일 위치와 일치합니다.
            // Vite 가 public/ 을 정적 루트로 서빙하므로 호스트는 /ait-warm-manifest.json 으로 취득합니다.
            inlinePrefetchJson = AITWarmManifestEmitter.WriteManifest(config, publicPath, loaderFile, dataFile, frameworkFile, wasmFile, symbolsFile);
            AITWarmPageEmitter.WritePage(config, publicPath);

            Debug.Log("[AIT] Unity WebGL 빌드 복사 완료");
            Debug.Log("[AIT]   - index.html → 프로젝트 루트");
            Debug.Log("[AIT]   - Build, TemplateData, Runtime → public/");

            // 네이티브 에셋 소스 레버 실효값 빌드 요약 + 침묵 열화(silent degradation) 경고.
            // pageCache 가 ON 일 때만 인터셉터에 신호가 주입되므로 AND 게이트.
            bool pageCacheEffective = config.pageCache < 0
                ? AITDefaultSettings.GetDefaultPageCache()
                : config.pageCache == 1;
            bool nativeSourceEffective = config.nativeAssetSource < 0
                ? AITDefaultSettings.GetDefaultNativeAssetSource()
                : config.nativeAssetSource == 1;
            if (pageCacheEffective && nativeSourceEffective)
            {
                Debug.Log("[AIT]   - 네이티브 에셋 소스 우선: 활성 (호스트 window.__aitResolveAsset 주입 시 native→CacheStorage→network)");

                // 네이티브가 프리페치 대상 목록을 얻으려면 ait-warm-manifest.json 이 필요하다.
                // warmManifest 가 OFF 면 매니페스트가 없어 네이티브 우선 경로가 사실상 무력화(폴백)된다 → 경고.
                bool warmManifestEffective = config.warmManifest < 0
                    ? AITDefaultSettings.GetDefaultWarmManifest()
                    : config.warmManifest == 1;
                if (!warmManifestEffective)
                {
                    Debug.LogWarning(
                        "[AIT] 네이티브 에셋 소스가 활성이지만 Warm Manifest 가 비활성입니다. " +
                        "호스트 네이티브가 프리페치 대상 목록(ait-warm-manifest.json)을 얻을 수 없어 " +
                        "네이티브 우선 경로가 사실상 동작하지 않고 CacheStorage/network 로 폴백됩니다. " +
                        "Warm Manifest 를 활성화하세요."
                    );
                }
            }

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
        ///
        /// 워밍 대상 확장(data/wasm → +framework/+loader):
        ///  · framework.js, loader.js: 둘 다 &lt;script src&gt; 로 소비됩니다 — loader.js 는 index.html 이,
        ///    framework.js 는 로더 자신이 document.createElement('script') 로 로드합니다. 따라서 window.fetch 를
        ///    통해 재요청되지 않아 earlyFetchMap 엔트리가 소진되지 않습니다(브라우저 HTTP 캐시 워밍이 목적).
        ///    이로 인한 두 가지 양성(benign) 부작용: (1) earlyFetchMap 이 완전히 비지 않아 window.fetch 가
        ///    originalFetch 로 복원되지 않고 early-fetch 래퍼가 상주(fetch 호출당 프로퍼티 조회 1회 오버헤드,
        ///    무시 가능), (2) early-fetch 의 bare fetch(framework/loader)가 page-cache 래퍼를 경유해 일시적으로
        ///    page-cache 에 put 될 수 있으나 부팅 sweep 이 allowlist(data/wasm 만) 기준으로 즉시 삭제합니다.
        ///    둘 다 측정 중립이며 게임 동작/allowlist 계약을 바꾸지 않습니다. (선시작/kickoff 대상에서는
        ///    이 둘을 제외합니다 — 아래 kickUrls 주석 참조.)
        /// </summary>
        private static string GenerateEarlyFetchScript(string dataFile, string frameworkFile, string wasmFile, string loaderFile, string bundleVersion, long cacheDataSize, long cacheWasmSize)
        {
            var urls = new List<string>();
            if (!string.IsNullOrEmpty(dataFile)) urls.Add($"Build/{dataFile}");
            if (!string.IsNullOrEmpty(wasmFile)) urls.Add($"Build/{wasmFile}");
            if (!string.IsNullOrEmpty(frameworkFile)) urls.Add($"Build/{frameworkFile}");
            if (!string.IsNullOrEmpty(loaderFile)) urls.Add($"Build/{loaderFile}");

            if (urls.Count == 0) return "";

            // JSON 배열로 URL 목록 생성
            var urlsJson = "[" + string.Join(",", urls.ConvertAll(u => $"\"{u}\"")) + "]";

            // 선시작(prefetch/kickoff) 대상은 window.fetch 로 실제 소비되는 data/wasm 뿐이다 — 레거시·modern
            // 공통. loader 는 index.html 이, framework 은 로더(2022.3·6000.x 모두 createElement('script'))가
            // <script src> 로 소비해 window.fetch 를 타지 않으므로, 선시작하면 응답이 소진되지 않고 이중
            // 다운로드만 유발한다(2026-07 베타 E2E 에서 6000.x loader/framework 각 2회 다운로드로 실측 적발).
            // 어느 파일이 data/wasm 인지는 C# 이 명확히 알고 있으므로(파일명 sniffing 아님) 명시 리스트로 넘긴다.
            // 런타임에서 절대 URL(host+path 포함)에 substring 매칭하면 배포 도메인/경로에 우연히 '.data'/'.wasm'
            // 이 포함될 때 framework/loader 를 오탐 선시작할 수 있어 그 휴리스틱을 원천 배제한다.
            var kickUrls = new List<string>();
            if (!string.IsNullOrEmpty(dataFile)) kickUrls.Add($"Build/{dataFile}");
            if (!string.IsNullOrEmpty(wasmFile)) kickUrls.Add($"Build/{wasmFile}");
            var kickUrlsJson = "[" + string.Join(",", kickUrls.ConvertAll(u => $"\"{u}\"")) + "]";

            // Unity 6000.x 로더는 자체 IndexedDB(UnityCache)로 데이터를 검증 캐싱하므로 warm reload에서
            // 재다운로드가 없다 → 우리 Cache-Storage 오버라이드는 순이득이 없고 스테일/이중 저장 위험만 더한다.
            // 레거시(2021/2022) 로더는 데이터 캐시가 없어 warm reload마다 100MB를 재다운로드 → 로컬 preview/CDN
            // 순단에 노출된다. 따라서 Cache-Storage 워밍은 레거시에만 적용하고 6000.x는 기존 스크립트를 유지한다.
            if (!IsLegacyUnityLoader())
            {
                return GenerateEarlyFetchScriptModern(kickUrlsJson);
            }

            string cacheName = BuildDataCacheName(dataFile, cacheDataSize, cacheWasmSize, bundleVersion);
            return GenerateEarlyFetchScriptLegacyCaching(urlsJson, cacheName, kickUrlsJson);
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
        /// dataSize/wasmSize는 반드시 brotli q11 재인코딩 훅(있다면) 실행 '전' 크기를 넘겨야 한다 — 훅은
        /// 동일 콘텐츠라도 .br 산출 바이트 크기를 바꾸므로, 훅 실행 후 크기를 쓰면 재인코딩 온/오프
        /// 토글만으로 캐시명이 흔들려 콘텐츠 불변 빌드까지 스테일로 오판되어 불필요한 전체 재다운로드가
        /// 발생한다(호출부 CopyWebGLToPublic 참조).
        /// </summary>
        private static string BuildDataCacheName(string dataFile, long dataSize, long wasmSize, string bundleVersion)
        {
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
        /// urlsJson 에는 window.fetch 로 실제 소비되는 리소스(data/wasm)만 넘겨야 한다 — loader/framework 은
        /// 6000.x 에서도 &lt;script src&gt; 로 소비되어 prefetch 응답이 소진되지 않고 이중 다운로드가 된다
        /// (2026-07 베타 E2E CE 테스트에서 실측 적발, 디스패처가 kickUrlsJson 을 전달).
        /// </summary>
        internal static string GenerateEarlyFetchScriptModern(string urlsJson)
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
        ///    단, Content-Encoding 응답(.br/.gz 네이티브 서빙)은 해제된 byteLength 와 압축 크기
        ///    Content-Length 가 정의상 불일치라 길이 대조를 생략(잘린 CE 스트림은 디코더가
        ///    arrayBuffer 를 reject → 재시도 방어 유지). 미생략 시 콜드 부트마다 data+wasm 이
        ///    2회 전송된다(실측 2026-07).
        ///  - 콜드 로드에서 data/wasm 모두 결정적으로 캐싱되므로(이전 clone-tee가 CI에서 wasm을 못 담던 문제
        ///    해소) 이후 reload는 전량 HIT → 네트워크 접촉 0.
        ///  - 저메모리 기기(deviceMemory<4)는 ~80MB 버퍼링이 OOM 위험 → 버퍼링/캐싱을 생략하고 원본
        ///    스트리밍 fetch로 폴백(기존 동작 유지, 제품 워치독이 방어).
        ///  - 워치독 복구 reload는 SKIP_KEY로 캐시를 1회 우회 → 오염 캐시가 복구를 막지 않음(self-amplification 차단).
        ///  - 캐시명에 data/wasm 바이트 크기 포함 → 콘텐츠 변경 시 자동 버스팅(2021 고정 파일명 스테일 방지).
        ///  - EARLY KICKOFF: 콜드 로드에서 캐시 MISS 리소스(data/wasm/framework)의 bufferedFetch를 head 파싱
        ///    시점에 선시작하고, 로더의 fetch가 pending promise에 합류한다 → 로더 다운로드+파싱+초기화 갭만큼
        ///    크리티컬 다운로드가 앞당겨진다(modern 6000.x 경로와 동일 발상, 캐시 HIT/reload 는 선시작 없음).
        /// </summary>
        internal static string GenerateEarlyFetchScriptLegacyCaching(string urlsJson, string cacheName, string kickUrlsJson)
        {
            return $@"<script>
    (function() {{
        var urls = {urlsJson};
        if (!urls || !urls.length) return;
        var kickUrls = {kickUrlsJson};
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
        // Content-Encoding 응답(.br/.gz 네이티브 서빙)은 길이 대조를 생략한다: fetch 가 해제한
        // buf.byteLength(원본 크기)와 헤더의 Content-Length(압축 크기)는 정의상 항상 불일치라
        // 대조하면 완결 본문도 short read 로 오판 → 성공할 수 없는 재다운로드 루프(콜드 부트
        // data+wasm 2회 전송 실측). 잘린 CE 스트림은 디코더가 arrayBuffer 를 reject 하므로
        // 재시도 방어는 길이 대조 없이도 유지된다.
        function bufferedFetch(url, left) {{
            return originalFetch(url, {{ method: 'GET' }}).then(function(r) {{
                if (!r || !r.ok) throw new Error('bad status ' + (r && r.status));
                var ct = r.headers.get('Content-Type') || 'application/octet-stream';
                var expected = r.headers.get('Content-Encoding') ? -1 : parseInt(r.headers.get('Content-Length') || '-1', 10);
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

        // EARLY KICKOFF: 콜드(비리로드) 로드에서 known 리소스의 다운로드를 head 파싱 시점에 선시작한다.
        // 로더가 같은 URL을 fetch하면 아래 오버라이드가 pending promise를 재사용해 이중 다운로드 없이
        // 로더 다운로드+파싱+초기화 갭만큼 크리티컬 다운로드를 앞당긴다(modern 6000.x 경로와 동일 발상).
        //  · 캐시 가용 시 HIT 확인 후 MISS만 선시작 → 재방문(비리로드 내비게이션) 캐시 HIT에 네트워크 낭비 없음.
        //    (pendingEarly 엔트리는 동기 설정되므로 로더 fetch가 match 진행 중에 와도 같은 promise에 합류 — race 없음)
        //  · 선시작 대상은 C# 이 명시로 넘긴 kickUrls(= data/wasm 만): 레거시(2021/2022) 로더는 framework 을
        //    <script src> 로, index.html 은 loader 를 <script src> 로 소비해 window.fetch 로 재요청되지
        //    않으므로, 이 둘을 선시작하면 pending 이 소진되지 않고 CDN cache-control: max-age=0 에서
        //    이중 다운로드만 유발한다(2022.3 loader.js 의 createElement('script') 경로 grep 확인). 절대 URL
        //    substring 매칭이 아니라 명시 리스트라, 배포 host/path 에 '.data'/'.wasm' 이 들어가도 오탐 없음.
        //  · isReload 제외: 이전 문서 keep-alive 소켓 해체와 경합(ERR_CONNECTION_CLOSED 위험) + HTTP 캐시 이미 warm.
        //  · 저메모리(cacheOK=false)는 버퍼링 없이 bare 스트리밍 fetch로 선시작(OOM 방어 유지).
        //  · 실패는 엔트리 삭제 후 null → 오버라이드가 originalFetch로 폴백(bufferedFetch 자체가 재시도+폴백 내장).
        var pendingEarly = {{}};
        if (!isReload) {{
            for (var ki = 0; ki < kickUrls.length; ki++) (function(url) {{
                var p;
                if (cacheOK && !skipCacheOnce) {{
                    p = self.caches.open(CACHE_NAME).then(function(c) {{
                        return c.match(url, {{ ignoreSearch: true }});
                    }}).then(function(hit) {{
                        return (hit && hit.ok) ? hit : bufferedFetch(url, MAX_TRIES);
                    }});
                }} else if (cacheOK) {{
                    p = bufferedFetch(url, MAX_TRIES);
                }} else {{
                    p = originalFetch(url, {{ method: 'GET' }});
                }}
                pendingEarly[url] = p.catch(function() {{ delete pendingEarly[url]; return null; }});
                try {{ console.log('[AIT] cache: early-kick ' + url); }} catch (e) {{}}
            }})(new URL(kickUrls[ki], location.href).href);
        }}

        window.fetch = function(resource, init) {{
            var url = (typeof resource === 'string') ? new URL(resource, location.href).href : resource.url;
            if (!knownSet[url]) return originalFetch.apply(this, arguments);
            var pend = pendingEarly[url];
            if (pend) {{
                delete pendingEarly[url];
                // 로더가 취소 시그널을 넘기면(현행 레거시 로더는 미사용) kickoff 는 그것을 반영할 수 없으므로
                // pending 재사용 대신 실제 인자로 위임해 취소 시맨틱을 보존한다(향후 로더 변경 대비 방어).
                if (init && init.signal) return originalFetch.apply(this, arguments);
                var s3 = this, a3 = arguments;
                try {{ console.log('[AIT] cache: early-join ' + url); }} catch (e) {{}}
                return pend.then(function(r) {{
                    return (r && r.ok && !r.bodyUsed) ? r : originalFetch.apply(s3, a3);
                }});
            }}
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

        /// <summary>
        /// brotli q11 재인코딩(스파이크) 실효 활성 여부. 기본은 config.brotliRecompress(선언 기본 false).
        /// AIT_BROTLI_RECOMPRESS 환경 변수가 설정되면 오버라이드한다(1/true=활성, 0/false=비활성) —
        /// AIT_COMPRESSION_FORMAT 오버라이드와 동일 패턴. 값이 이상하면 경고 후 설정값으로 폴백.
        /// </summary>
        internal static bool EffectiveBrotliRecompress(AITEditorScriptObject config)
        {
            string env = System.Environment.GetEnvironmentVariable("AIT_BROTLI_RECOMPRESS");
            if (!string.IsNullOrEmpty(env))
            {
                string v = env.Trim().ToLowerInvariant();
                if (v == "1" || v == "true") return true;
                if (v == "0" || v == "false") return false;
                Debug.LogWarning($"[AIT] AIT_BROTLI_RECOMPRESS 환경 변수 값이 올바르지 않습니다: '{env}' (1/0/true/false 필요) — 설정값 사용");
            }

            return config != null && config.brotliRecompress;
        }

        /// <summary>
        /// ait-build 폴더 준비 (기존 결과물 정리).
        /// node_modules/설정 파일과 public/(정확히는 <see cref="MirroredPublicDirectories"/>)은 유지하고
        /// 나머지 이전 결과물(dist, index.html 등)을 지운다. public/을 통째로 지우면 매 빌드마다
        /// 수백 MB의 Unity 산출물을 다시 복사해야 해 CopyWebGLToPublic의 변경분 미러 복사가 항상
        /// 무효화되므로, 미러 대상만 남기고 그 외 public/ 항목은 <see cref="PrunePublicFolder"/>가 정리한다.
        /// internal 승격: facade(AITPackageBuilder) 및 EditMode 테스트(리플렉션)에서 호출하기 위함.
        /// </summary>
        /// <param name="preservePackagingOutputs">
        /// true면 이전 패키징 산출물(dist/ 와 ait-build 루트의 *.ait)을 정리 대상에서 제외해
        /// 보존한다. fastBuild(Deploy (Test)) 경로에서만 true로 전달해야 한다 —
        /// Package.PackageBuildStateMarker.ShouldSkipPackageBuild가 .ait 산출물 존재를 스킵
        /// 조건으로 검사하는데, 이 판정 전에 산출물이 지워지면 스킵이 절대 발동하지 않는다
        /// (모든 빌드 진입점이 이 함수를 가장 먼저 호출하기 때문).
        ///
        /// ⚠️ 루트 *.ait도 함께 보존해야 한다: web-framework 3.x의 `ait build`는 .ait를 dist/가
        /// 아니라 ait-build 루트에 emit하고(dist/에는 vite 산출물인 dist/web만 생성됨), 2.x도
        /// 루트에 emit한다. dist/만 보존하면 실제 산출물인 루트 *.ait가 여기서 지워져 스킵이
        /// 영원히 발동하지 않는다 (AITBuildValidator.ValidateDistOutput의 "루트 → dist/" 탐색
        /// 순서와 동일한 위치 규약).
        ///
        /// 스킵 판정이 결국 false로 나오면 호출부가 <see cref="DeletePreviousPackagingOutputs"/>로
        /// 실제 빌드 시작 직전 dist/와 루트 *.ait를 명시적으로 삭제해 "빌드는 항상 빈 산출물
        /// 상태에서 시작한다" 불변식을 복원한다. 기본값(false)은 Production/Build &amp; Package
        /// 경로의 기존 동작과 동일 — 산출물은 항상 이 함수에서 정리된다.
        /// </param>
        internal static void PrepareAitBuildFolder(string buildProjectPath, bool preservePackagingOutputs = false)
        {
            if (!Directory.Exists(buildProjectPath))
            {
                Directory.CreateDirectory(buildProjectPath);
                Debug.Log("[AIT] ait-build 폴더 생성");
            }
            else
            {
                Debug.Log("[AIT] 기존 빌드 결과물 정리 중... (node_modules·설정 파일과 public/ 미러 대상은 유지)");

                var itemsToKeep = new List<string>
                {
                    "node_modules",
                    "package.json",
                    "package-lock.json",
                    "pnpm-lock.yaml",
                    "pnpm-workspace.yaml",
                    "granite.config.ts",
                    "apps-in-toss.config.ts",
                    "vite.config.ts",
                    "tsconfig.json",
                    // public/은 CopyWebGLToPublic이 미러로 관리한다 — 여기서 통째로 지우면
                    // 변경분 복사가 매번 전량 복사로 퇴화한다. 미러 대상 밖 항목은 아래 PrunePublicFolder가 정리.
                    "public"
                };

                if (preservePackagingOutputs)
                {
                    itemsToKeep.Add("dist");
                }

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

                    // 루트 *.ait는 web-framework 2.x/3.x가 실제로 .ait를 emit하는 위치라
                    // itemsToKeep(고정 이름 매칭)으로는 표현할 수 없다 — 파일명이 appName에
                    // 따라 달라지므로 확장자로 판별한다.
                    if (!shouldKeep && preservePackagingOutputs && IsAitArchive(item))
                    {
                        shouldKeep = true;
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

                PrunePublicFolder(Path.Combine(buildProjectPath, "public"));
            }
        }

        /// <summary>
        /// 이전 패키징 산출물(ait-build/dist 와 ait-build 루트의 *.ait)을 삭제한다.
        /// <see cref="PrepareAitBuildFolder"/>(preservePackagingOutputs: true)가 보존해둔 이전
        /// 산출물을, Package.PackageBuildStateMarker.ShouldSkipPackageBuild 판정이 결국 false로
        /// 나와 실제 vite/ait build를 새로 시작하기 직전에 호출해 지운다 — "빌드는 항상 빈
        /// 산출물 상태에서 시작한다" 불변식을 복원해 옛 .ait와 새 .ait가 공존하는 것을
        /// 막는다(예: appName/version 변경으로 산출물 파일명이 달라지는 경우).
        /// preservePackagingOutputs:false 경로에서는 PrepareAitBuildFolder가 이미 둘 다
        /// 지웠으므로 이 호출은 언제나 안전한 no-op이다.
        /// </summary>
        internal static void DeletePreviousPackagingOutputs(string buildProjectPath)
        {
            string distPath = Path.Combine(buildProjectPath, "dist");
            if (Directory.Exists(distPath))
            {
                AITFileUtils.DeleteDirectory(distPath);
            }

            if (!Directory.Exists(buildProjectPath)) return;

            foreach (string item in Directory.GetFiles(buildProjectPath, "*", SearchOption.TopDirectoryOnly))
            {
                if (IsAitArchive(item))
                {
                    AITFileSystemHelper.SafeDelete(item);
                }
            }
        }

        /// <summary>
        /// ait-build 루트에 emit된 패키징 산출물(.ait) 여부. 파일명은 appName에 따라 달라지므로
        /// 확장자로만 판별한다. Windows의 8.3 단축명 때문에 <c>Directory.GetFiles(dir, "*.ait")</c>가
        /// .aitxxx 같은 더 긴 확장자까지 잡을 수 있어, 열거는 "*"로 하고 여기서 정확히 검사한다.
        /// </summary>
        private static bool IsAitArchive(string path)
        {
            return File.Exists(path)
                && string.Equals(Path.GetExtension(path), AitArchiveExtension, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// public/에서 미러 대상(<see cref="MirroredPublicDirectories"/>)만 남기고 나머지를 지운다.
        /// 미러 대상은 CopyWebGLToPublic이 변경분 복사 + stale 제거로 최종 상태를 보장하므로 보존해도
        /// 잔여물이 남지 않는다. 그 외 항목(사용자 BuildConfig~의 public/ 파일 등)은 매 빌드 다시
        /// 복사되므로 예전처럼 여기서 지워야 소스에서 삭제된 파일이 계속 서빙되지 않는다.
        /// </summary>
        private static void PrunePublicFolder(string publicPath)
        {
            if (!Directory.Exists(publicPath)) return;

            var mirrored = new HashSet<string>(MirroredPublicDirectories, System.StringComparer.Ordinal);

            foreach (string item in Directory.GetFileSystemEntries(publicPath))
            {
                string itemName = Path.GetFileName(item);

                if (Directory.Exists(item))
                {
                    if (mirrored.Contains(itemName)) continue;
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
