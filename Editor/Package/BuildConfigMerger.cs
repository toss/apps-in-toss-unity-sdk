using System;
using System.Collections.Generic;
using System.IO;
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
                File.Copy(pnpmLockProject, pnpmLockDst, true);
                Debug.Log("[AIT]   ✓ pnpm-lock.yaml (프로젝트에서 복사, 정합 확인됨)");
            }
            else if (File.Exists(pnpmLockSdk))
            {
                File.Copy(pnpmLockSdk, pnpmLockDst, true);
                Debug.Log("[AIT]   ✓ pnpm-lock.yaml (SDK에서 복사)");
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
            string projectFile = Path.Combine(projectBuildConfigPath, "apps-in-toss.config.ts");
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

            // 프로젝트 파일이 있으면 USER_CONFIG 영역만 보존
            if (File.Exists(projectFile))
            {
                string projectContent = File.ReadAllText(projectFile);

                string projectUserConfig = AITTemplateManager.ExtractMarkerSection(projectContent, "USER_CONFIG");
                if (projectUserConfig != null)
                {
                    finalContent = AITTemplateManager.ReplaceMarkerSection(finalContent, "USER_CONFIG", projectUserConfig);
                    Debug.Log("[AIT]   ✓ apps-in-toss.config.ts (SDK 최신 버전 + USER_CONFIG 보존)");
                }
                else
                {
                    Debug.Log("[AIT]   ✓ apps-in-toss.config.ts (SDK 최신 버전으로 갱신)");
                }
            }
            else
            {
                Debug.Log("[AIT]   ✓ apps-in-toss.config.ts (SDK에서 생성)");
            }

            File.WriteAllText(destFile, finalContent, new System.Text.UTF8Encoding(false));
        }
    }
}
