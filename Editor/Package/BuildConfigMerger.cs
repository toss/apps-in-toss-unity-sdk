using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AppsInToss.Editor.Package
{
    /// <summary>
    /// BuildConfig 파일(package.json, tsconfig.json, vite.config.ts, granite.config.ts)
    /// 병합 및 플레이스홀더 치환.
    /// 프로젝트의 BuildConfig 폴더가 있으면 SDK 기본값 위에 덮어쓴다.
    /// </summary>
    internal static class BuildConfigMerger
    {
        internal static void MergePackageJson(string projectBuildConfigPath, string sdkBuildConfigPath, string destPath)
        {
            string projectFile = Path.Combine(projectBuildConfigPath, "package.json");
            string sdkFile = Path.Combine(sdkBuildConfigPath, "package.json");
            string destFile = Path.Combine(destPath, "package.json");

            // 프로젝트 파일 없으면 SDK 복사
            if (!File.Exists(projectFile))
            {
                File.Copy(sdkFile, destFile, true);
                Debug.Log("[AIT]   ✓ package.json (SDK에서 복사)");
                return;
            }

            try
            {
                string projectContent = File.ReadAllText(projectFile);
                string sdkContent = File.ReadAllText(sdkFile);

                // 간단한 JSON 머지 (dependencies와 devDependencies)
                var projectJson = MiniJson.Deserialize(projectContent) as Dictionary<string, object>;
                var sdkJson = MiniJson.Deserialize(sdkContent) as Dictionary<string, object>;

                if (projectJson == null || sdkJson == null)
                {
                    Debug.Log("[AIT] package.json 파싱 실패, SDK 버전 사용");
                    File.Copy(sdkFile, destFile, true);
                    return;
                }

                // SDK의 기본 구조를 사용하고 dependencies만 머지
                var result = new Dictionary<string, object>(sdkJson);

                // dependencies 머지
                result["dependencies"] = MergeDependencies(
                    projectJson.ContainsKey("dependencies") ? projectJson["dependencies"] as Dictionary<string, object> : null,
                    sdkJson.ContainsKey("dependencies") ? sdkJson["dependencies"] as Dictionary<string, object> : null
                );

                // devDependencies 머지
                result["devDependencies"] = MergeDependencies(
                    projectJson.ContainsKey("devDependencies") ? projectJson["devDependencies"] as Dictionary<string, object> : null,
                    sdkJson.ContainsKey("devDependencies") ? sdkJson["devDependencies"] as Dictionary<string, object> : null
                );

                string mergedJson = MiniJson.Serialize(result);
                File.WriteAllText(destFile, mergedJson, new System.Text.UTF8Encoding(false));
                Debug.Log("[AIT]   ✓ package.json (dependencies 머지됨)");
            }
            catch (Exception e)
            {
                Debug.Log($"[AIT] package.json 머지 실패: {e}, SDK 버전 사용");
                File.Copy(sdkFile, destFile, true);
            }
        }

        /// <summary>
        /// pnpm-lock.yaml을 destPath로 복사한다. 프로젝트 lockfile이 package.json과 정합 상태일 때만 사용,
        /// 그렇지 않으면 SDK lockfile로 폴백한다 (stale lockfile 회귀 방지, Fix A).
        /// 둘 다 없으면 no-op.
        /// </summary>
        internal static void CopyPnpmLockfileWithFallback(string projectBuildConfigPath, string sdkBuildConfigPath, string destPath)
        {
            string pnpmLockProject = Path.Combine(projectBuildConfigPath, "pnpm-lock.yaml");
            string pnpmLockSdk = Path.Combine(sdkBuildConfigPath, "pnpm-lock.yaml");
            string pnpmLockDst = Path.Combine(destPath, "pnpm-lock.yaml");
            string projectPackageJson = Path.Combine(projectBuildConfigPath, "package.json");

            bool useProjectLockfile = false;
            if (File.Exists(pnpmLockProject))
            {
                if (File.Exists(projectPackageJson))
                {
                    if (LockfileValidator.IsLockfileInSync(projectPackageJson, pnpmLockProject, out string mismatchSummary))
                    {
                        useProjectLockfile = true;
                    }
                    else
                    {
                        // 의도된 폴백 경로(SDK lockfile 사용). Console 가시성은 유지하되
                        // 사용자 환경 의존 메시지(불일치 specifier 목록)가 Sentry fingerprint를
                        // 흩어놓아 노이즈를 만들므로 Sentry 전송은 차단. Sentry SDK-Q1/S2.
                        AITLog.Warning(
                            "[AIT]   ⚠ 프로젝트 pnpm-lock.yaml이 package.json과 정합되지 않아 SDK lockfile로 폴백합니다. " +
                            "불일치: " + mismatchSummary,
                            sentryCapture: false);
                    }
                }
                else
                {
                    AITLog.Warning(
                        "[AIT]   ⚠ 프로젝트 pnpm-lock.yaml은 있지만 package.json이 없어 검증 불가. SDK lockfile로 폴백합니다.",
                        sentryCapture: false);
                }
            }

            if (useProjectLockfile)
            {
                CopyWithIoExceptionFallback(pnpmLockProject, pnpmLockDst, "pnpm-lock.yaml (프로젝트에서 복사, 정합 확인됨)");
            }
            else if (File.Exists(pnpmLockSdk))
            {
                CopyWithIoExceptionFallback(pnpmLockSdk, pnpmLockDst, "pnpm-lock.yaml (SDK에서 복사)");
            }
        }

        /// <summary>
        /// File.Copy(overwrite:true)를 수행하되, 대상 파일이 다른 프로세스(pnpm, antivirus 등)에 의해
        /// 메모리 맵으로 열려 있어 덮어쓰기(overwrite) 모드가 IOException을 발생시키는 경우
        /// (Windows 한정, APPS-IN-TOSS-UNITY-SDK-132) 대상 파일을 삭제 후 재시도한다.
        /// 재시도도 실패하면 IOException을 다시 던진다.
        /// </summary>
        private static void CopyWithIoExceptionFallback(string src, string dst, string label)
        {
            try
            {
                File.Copy(src, dst, overwrite: true);
                Debug.Log($"[AIT]   ✓ {label}");
            }
            catch (IOException ex)
            {
                // Windows에서 대상 파일이 메모리 맵으로 열려 있으면 overwrite 모드에서 IOException이 발생한다.
                // 대상 파일을 먼저 삭제한 뒤 복사를 재시도한다.
                AITLog.Warning(
                    $"[AIT]   ⚠ {label} — File.Copy IOException, 대상 파일 삭제 후 재시도 중: {ex.Message}",
                    sentryCapture: false);
                try
                {
                    if (File.Exists(dst))
                        File.Delete(dst);
                    File.Copy(src, dst, overwrite: false);
                    Debug.Log($"[AIT]   ✓ {label} (삭제 후 재시도 성공)");
                }
                catch (Exception retryEx)
                {
                    throw new IOException(
                        $"[AIT] pnpm-lock.yaml 복사 재시도 실패 ({label}): {retryEx.Message} — 원래 오류: {ex.Message}",
                        retryEx);
                }
            }
        }

        /// <summary>
        /// pnpm-workspace.yaml을 빌드 디렉토리로 복사합니다.
        /// 이 파일은 공급망 보호(minimumReleaseAge)에서 @apps-in-toss/* 패키지를 예외 처리하는
        /// 정적 설정 파일이라 병합/검증 없이 그대로 복사합니다. pnpm은 이 설정을
        /// pnpm-workspace.yaml에서만 읽으므로(.npmrc/package.json#pnpm 불가) 빌드 디렉토리에
        /// 반드시 존재해야 미니앱 빌드 시 예외가 적용됩니다.
        /// 프로젝트에 동명 파일이 있으면 사용자 설정을 우선하고, 없으면 SDK 기본값을 사용합니다.
        /// </summary>
        internal static void CopyPnpmWorkspaceWithFallback(string projectBuildConfigPath, string sdkBuildConfigPath, string destPath)
        {
            string workspaceProject = Path.Combine(projectBuildConfigPath, "pnpm-workspace.yaml");
            string workspaceSdk = Path.Combine(sdkBuildConfigPath, "pnpm-workspace.yaml");
            string workspaceDst = Path.Combine(destPath, "pnpm-workspace.yaml");

            if (File.Exists(workspaceProject))
            {
                File.Copy(workspaceProject, workspaceDst, true);
                Debug.Log("[AIT]   ✓ pnpm-workspace.yaml (프로젝트에서 복사)");
            }
            else if (File.Exists(workspaceSdk))
            {
                File.Copy(workspaceSdk, workspaceDst, true);
                Debug.Log("[AIT]   ✓ pnpm-workspace.yaml (SDK에서 복사)");
            }
        }

        /// <summary>
        /// dependencies 딕셔너리를 머지합니다. SDK 패키지가 우선됩니다.
        /// </summary>
        internal static Dictionary<string, object> MergeDependencies(Dictionary<string, object> project, Dictionary<string, object> sdk)
        {
            var result = new Dictionary<string, object>();

            // 프로젝트 dependencies 먼저 추가
            if (project != null)
            {
                foreach (var kvp in project)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            // SDK dependencies로 덮어쓰기 (SDK가 우선)
            if (sdk != null)
            {
                foreach (var kvp in sdk)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            return result;
        }

        /// <summary>
        /// tsconfig.json을 머지합니다.
        /// SDK의 필수 옵션을 유지하면서 사용자 옵션을 추가합니다.
        /// </summary>
        internal static void MergeTsConfig(string projectBuildConfigPath, string sdkBuildConfigPath, string destPath)
        {
            string projectFile = Path.Combine(projectBuildConfigPath, "tsconfig.json");
            string sdkFile = Path.Combine(sdkBuildConfigPath, "tsconfig.json");
            string destFile = Path.Combine(destPath, "tsconfig.json");

            // 프로젝트 파일 없으면 SDK 복사
            if (!File.Exists(projectFile))
            {
                File.Copy(sdkFile, destFile, true);
                Debug.Log("[AIT]   ✓ tsconfig.json (SDK에서 복사)");
                return;
            }

            try
            {
                string projectContent = File.ReadAllText(projectFile);
                string sdkContent = File.ReadAllText(sdkFile);

                var projectJson = MiniJson.Deserialize(projectContent) as Dictionary<string, object>;
                var sdkJson = MiniJson.Deserialize(sdkContent) as Dictionary<string, object>;

                if (projectJson == null || sdkJson == null)
                {
                    Debug.Log("[AIT] tsconfig.json 파싱 실패, SDK 버전 사용");
                    File.Copy(sdkFile, destFile, true);
                    return;
                }

                // SDK의 기본 구조를 사용
                var result = new Dictionary<string, object>(sdkJson);

                // compilerOptions 머지
                var sdkCompilerOptions = sdkJson.ContainsKey("compilerOptions")
                    ? sdkJson["compilerOptions"] as Dictionary<string, object>
                    : new Dictionary<string, object>();
                var projectCompilerOptions = projectJson.ContainsKey("compilerOptions")
                    ? projectJson["compilerOptions"] as Dictionary<string, object>
                    : new Dictionary<string, object>();

                // SDK 필수 옵션 정의 (이 옵션들은 SDK 값으로 강제)
                var sdkRequiredOptions = new HashSet<string>
                {
                    "moduleResolution",  // bundler 필수
                    "esModuleInterop",   // 호환성 필수
                };

                // 머지된 compilerOptions 생성
                var mergedCompilerOptions = new Dictionary<string, object>();

                // 1. SDK 옵션 먼저 추가 (기본값)
                if (sdkCompilerOptions != null)
                {
                    foreach (var kvp in sdkCompilerOptions)
                    {
                        mergedCompilerOptions[kvp.Key] = kvp.Value;
                    }
                }

                // 2. 프로젝트 옵션으로 덮어쓰기 (SDK 필수 옵션 제외)
                if (projectCompilerOptions != null)
                {
                    foreach (var kvp in projectCompilerOptions)
                    {
                        if (!sdkRequiredOptions.Contains(kvp.Key))
                        {
                            mergedCompilerOptions[kvp.Key] = kvp.Value;
                        }
                    }
                }

                result["compilerOptions"] = mergedCompilerOptions;

                // include 배열 (프로젝트에 있으면 프로젝트 우선)
                if (projectJson.ContainsKey("include"))
                {
                    result["include"] = projectJson["include"];
                }

                // exclude 배열 (프로젝트에 있으면 사용)
                if (projectJson.ContainsKey("exclude"))
                {
                    result["exclude"] = projectJson["exclude"];
                }

                string mergedJson = MiniJson.Serialize(result);
                File.WriteAllText(destFile, mergedJson, new System.Text.UTF8Encoding(false));
                Debug.Log("[AIT]   ✓ tsconfig.json (compilerOptions 머지됨)");
            }
            catch (Exception e)
            {
                Debug.Log($"[AIT] tsconfig.json 머지 실패: {e}, SDK 버전 사용");
                File.Copy(sdkFile, destFile, true);
            }
        }

        /// <summary>
        /// vite.config.ts를 마커 기반으로 업데이트합니다.
        /// SDK 템플릿을 기반으로 하고, USER_CONFIG 영역만 프로젝트에서 보존합니다.
        /// import 문, SDK_PLUGINS, SDK_GENERATED는 항상 SDK 최신 버전으로 갱신됩니다.
        /// </summary>
        internal static void UpdateViteConfig(string projectBuildConfigPath, string sdkBuildConfigPath, string destPath, AITEditorScriptObject config)
        {
            string projectFile = Path.Combine(projectBuildConfigPath, "vite.config.ts");
            string sdkFile = Path.Combine(sdkBuildConfigPath, "vite.config.ts");
            string destFile = Path.Combine(destPath, "vite.config.ts");

            // SDK 템플릿 로드
            string sdkTemplate = File.ReadAllText(sdkFile);

            // 플레이스홀더 치환
            string finalContent = sdkTemplate
                .Replace("%AIT_VITE_HOST%", config.viteHost)
                .Replace("%AIT_VITE_PORT%", config.vitePort.ToString());

            // 프로젝트 파일이 있으면 USER_CONFIG 영역만 보존
            if (File.Exists(projectFile))
            {
                string projectContent = File.ReadAllText(projectFile);

                // 프로젝트의 USER_CONFIG 영역 추출
                string projectUserConfig = AITTemplateManager.ExtractMarkerSection(projectContent, "USER_CONFIG");
                if (projectUserConfig != null)
                {
                    // SDK 템플릿의 USER_CONFIG를 프로젝트의 USER_CONFIG로 교체
                    finalContent = AITTemplateManager.ReplaceMarkerSection(finalContent, "USER_CONFIG", projectUserConfig);
                    Debug.Log("[AIT]   ✓ vite.config.ts (SDK 최신 버전 + USER_CONFIG 보존)");
                }
                else
                {
                    Debug.Log("[AIT]   ✓ vite.config.ts (SDK 최신 버전으로 갱신)");
                }
            }
            else
            {
                Debug.Log("[AIT]   ✓ vite.config.ts (SDK에서 생성)");
            }

            File.WriteAllText(destFile, finalContent, new System.Text.UTF8Encoding(false));
        }

        /// <summary>
        /// granite.config.ts를 마커 기반으로 업데이트합니다.
        /// SDK 템플릿을 기반으로 하고, USER_CONFIG 영역만 프로젝트에서 보존합니다.
        /// import 문, SDK_GENERATED는 항상 SDK 최신 버전으로 갱신됩니다.
        /// </summary>
        internal static void UpdateGraniteConfig(string projectBuildConfigPath, string sdkBuildConfigPath, string destPath, AITEditorScriptObject config)
        {
            string projectFile = Path.Combine(projectBuildConfigPath, "granite.config.ts");
            string sdkFile = Path.Combine(sdkBuildConfigPath, "granite.config.ts");
            string destFile = Path.Combine(destPath, "granite.config.ts");

            // SDK 템플릿 로드
            string sdkTemplate = File.ReadAllText(sdkFile);

            // 플레이스홀더 치환
            Debug.Log("[AIT] granite.config.ts placeholder 치환 중...");
            string finalContent = sdkTemplate
                .Replace("%AIT_APP_NAME%", config.appName)
                .Replace("%AIT_DISPLAY_NAME%", config.displayName)
                .Replace("%AIT_PRIMARY_COLOR%", config.primaryColor)
                .Replace("%AIT_ICON_URL%", config.iconUrl)
                .Replace("%AIT_BRIDGE_COLOR_MODE%", config.GetBridgeColorModeString())
                .Replace("%AIT_WEBVIEW_TYPE%", config.GetWebViewTypeString())
                .Replace("%AIT_ALLOWS_INLINE_MEDIA_PLAYBACK%", config.allowsInlineMediaPlayback.ToString().ToLower())
                .Replace("%AIT_MEDIA_PLAYBACK_REQUIRES_USER_ACTION%", config.mediaPlaybackRequiresUserAction.ToString().ToLower())
                .Replace("%AIT_VITE_HOST%", config.viteHost)
                .Replace("%AIT_VITE_PORT%", config.vitePort.ToString())
                .Replace("%AIT_PERMISSIONS%", config.GetPermissionsJson())
                .Replace("%AIT_OUTDIR%", config.outdir);

            // 프로젝트 파일이 있으면 USER_CONFIG 영역만 보존
            if (File.Exists(projectFile))
            {
                string projectContent = File.ReadAllText(projectFile);

                // 프로젝트의 USER_CONFIG 영역 추출
                string projectUserConfig = AITTemplateManager.ExtractMarkerSection(projectContent, "USER_CONFIG");
                if (projectUserConfig != null)
                {
                    // SDK 템플릿의 USER_CONFIG를 프로젝트의 USER_CONFIG로 교체
                    finalContent = AITTemplateManager.ReplaceMarkerSection(finalContent, "USER_CONFIG", projectUserConfig);
                    Debug.Log("[AIT]   ✓ granite.config.ts (SDK 최신 버전 + USER_CONFIG 보존)");
                }
                else
                {
                    Debug.Log("[AIT]   ✓ granite.config.ts (SDK 최신 버전으로 갱신)");
                }
            }
            else
            {
                Debug.Log("[AIT]   ✓ granite.config.ts (SDK에서 생성)");
            }

            File.WriteAllText(destFile, finalContent, new System.Text.UTF8Encoding(false));
        }

        /// <summary>
        /// apps-in-toss.config.ts를 마커 기반으로 업데이트합니다 (web-framework 3.x ait build 전용).
        /// SDK 템플릿을 기반으로 하고, USER_CONFIG 영역만 프로젝트에서 보존합니다.
        /// 2.x granite build는 이 파일을 탐색하지 않으므로(granite.config.ts만 사용) 항상 emit해도
        /// stable 빌드에 영향이 없다. SDK 템플릿이 없으면(구버전 SDK) no-op.
        /// </summary>
        internal static void UpdateAppsInTossConfig(string projectBuildConfigPath, string sdkBuildConfigPath, string destPath, AITEditorScriptObject config)
        {
            string sdkFile = Path.Combine(sdkBuildConfigPath, "apps-in-toss.config.ts");
            string destFile = Path.Combine(destPath, "apps-in-toss.config.ts");

            // 구버전 SDK 템플릿에는 apps-in-toss.config.ts가 없을 수 있음 → 스킵 (granite.config.ts만으로 2.x 동작)
            if (!File.Exists(sdkFile))
            {
                return;
            }

            // SDK 템플릿 로드
            string sdkTemplate = File.ReadAllText(sdkFile);

            // 플레이스홀더 치환 (granite.config.ts와 동일한 값 소스 사용 — 권한 JSON 형식이 3.x와 호환)
            Debug.Log("[AIT] apps-in-toss.config.ts placeholder 치환 중...");
            string finalContent = sdkTemplate
                .Replace("%AIT_APP_NAME%", config.appName)
                .Replace("%AIT_PRIMARY_COLOR%", config.primaryColor)
                .Replace("%AIT_ALLOWS_INLINE_MEDIA_PLAYBACK%", config.allowsInlineMediaPlayback.ToString().ToLower())
                .Replace("%AIT_MEDIA_PLAYBACK_REQUIRES_USER_ACTION%", config.mediaPlaybackRequiresUserAction.ToString().ToLower())
                .Replace("%AIT_PERMISSIONS%", config.GetPermissionsJson());

            // USER_CONFIG 결정:
            //  1) 프로젝트 apps-in-toss.config.ts의 USER_CONFIG에 실제 내용이 있으면 그대로 보존
            //  2) 비어 있으면 granite.config.ts의 USER_CONFIG를 자동 이전 (2.x→3.x 마이그레이션, 수동 포팅 제거)
            //  3) 둘 다 비어 있으면 SDK 템플릿 기본값(빈 USER_CONFIG) 유지
            // SDK 관리 필드(brand/appName/permissions/webView 등)는 apps-in-toss.config.ts의 병합 로직에서
            // 항상 SDK 값이 우선하므로, granite USER_CONFIG를 그대로 가져와도 안전하다.
            string resolvedUserConfig = ResolveUserConfigForAppsInToss(projectBuildConfigPath);
            if (resolvedUserConfig != null)
            {
                finalContent = AITTemplateManager.ReplaceMarkerSection(finalContent, "USER_CONFIG", resolvedUserConfig);
            }
            else
            {
                Debug.Log("[AIT]   ✓ apps-in-toss.config.ts (SDK에서 생성)");
            }

            File.WriteAllText(destFile, finalContent, new System.Text.UTF8Encoding(false));
        }

        /// <summary>
        /// apps-in-toss.config.ts에 주입할 USER_CONFIG 섹션을 결정합니다.
        /// 프로젝트 apps-in-toss.config.ts의 USER_CONFIG가 비어 있으면 granite.config.ts의 USER_CONFIG를
        /// 자동 이전합니다(2.x→3.x 마이그레이션). 둘 다 비어 있으면 null(SDK 템플릿 기본값 유지)을 반환합니다.
        /// </summary>
        internal static string ResolveUserConfigForAppsInToss(string projectBuildConfigPath)
        {
            // 1) apps-in-toss.config.ts의 USER_CONFIG 우선
            string appsInTossFile = Path.Combine(projectBuildConfigPath, "apps-in-toss.config.ts");
            if (File.Exists(appsInTossFile))
            {
                string section = AITTemplateManager.ExtractMarkerSection(File.ReadAllText(appsInTossFile), "USER_CONFIG");
                if (section != null && !IsEffectivelyEmptyUserConfig(section))
                {
                    Debug.Log("[AIT]   ✓ apps-in-toss.config.ts (SDK 최신 버전 + USER_CONFIG 보존)");
                    return section;
                }
            }

            // 2) 비어 있으면 granite.config.ts의 USER_CONFIG를 자동 이전
            string graniteFile = Path.Combine(projectBuildConfigPath, "granite.config.ts");
            if (File.Exists(graniteFile))
            {
                string section = AITTemplateManager.ExtractMarkerSection(File.ReadAllText(graniteFile), "USER_CONFIG");
                if (section != null && !IsEffectivelyEmptyUserConfig(section))
                {
                    Debug.Log(
                        "[AIT]   ✓ apps-in-toss.config.ts (granite.config.ts의 USER_CONFIG 자동 이전 — 2.x→3.x 마이그레이션). " +
                        "SDK 관리 필드는 SDK 값이 우선하므로 안전합니다.");
                    return section;
                }
            }

            // 3) 둘 다 비어 있음 → SDK 템플릿 기본값 유지
            return null;
        }

        /// <summary>
        /// USER_CONFIG 마커 섹션이 실질적으로 비어 있는지(실제 프로퍼티 없이 주석/공백만 있는지) 판별합니다.
        /// 구조를 확신할 수 없으면(중괄호 미발견 등) 보수적으로 "내용 있음"으로 간주해 보존합니다.
        /// </summary>
        internal static bool IsEffectivelyEmptyUserConfig(string section)
        {
            if (string.IsNullOrEmpty(section)) return true;

            int open = section.IndexOf('{');
            int close = section.LastIndexOf('}');
            if (open == -1 || close == -1 || close <= open) return false; // 구조 불명 → 보존

            string body = section.Substring(open + 1, close - open - 1);
            body = Regex.Replace(body, @"/\*.*?\*/", "", RegexOptions.Singleline); // 블록 주석 제거
            body = Regex.Replace(body, @"//[^\n]*", "");                           // 줄 주석 제거
            return body.Trim().Length == 0;
        }
    }
}
