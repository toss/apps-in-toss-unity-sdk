using System;
using System.IO;
using UnityEngine;

namespace AppsInToss.Editor.Menu
{
    /// <summary>
    /// Dev Server의 devtools(@apps-in-toss/devtools) mock 활성화 여부를 판단하고, vite.config.ts가
    /// 읽는 환경변수를 구성합니다.
    ///
    /// Editor → Vite 환경변수 계약:
    /// - AIT_DEVTOOLS: "1"/"0". Editor가 서버 실행 시 항상 명시 설정 (Dev+조건충족 → "1", 그 외 → "0")
    /// - AIT_DEVTOOLS_PANEL: "1"/"0". Editor가 항상 명시 (config.devtools.panel)
    /// - AIT_DEVTOOLS_MCP: "1" 또는 미설정. config.devtools.mcp가 true일 때만 "1" 설정
    /// - AIT_DEVTOOLS_TUNNEL: 사람 수동 전용. Editor/CI는 절대 설정하지 않음
    ///
    /// internal 멤버는 Editor/AssemblyInfo.cs 의 InternalsVisibleTo 를 통해 테스트 어셈블리에서 접근됩니다.
    /// </summary>
    internal static class DevtoolsSupport
    {
        /// <summary>
        /// buildProjectPath/node_modules/@apps-in-toss/devtools/package.json 존재 여부로
        /// devtools 패키지 설치 여부를 판단합니다.
        /// </summary>
        internal static bool IsDevtoolsInstalled(string buildProjectPath)
        {
            try
            {
                if (string.IsNullOrEmpty(buildProjectPath)) return false;
                string pkgJsonPath = Path.Combine(buildProjectPath, "node_modules", "@apps-in-toss", "devtools", "package.json");
                return File.Exists(pkgJsonPath);
            }
            catch (Exception e)
            {
                Debug.Log($"[AIT] devtools 설치 여부 확인 중 오류 (미설치로 간주): {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// devtools mock 활성화 여부를 게이트 순서대로 판단합니다.
        /// 게이트: 환경변수 AIT_DEVTOOLS 오버라이드 → Dev 서버만 → config.devtools.enabled →
        /// vite 단독(viteOnly) 모드만(2.x는 granite CLI 경로라 vite.config가 적용되지 않음) → devtools 설치 확인.
        /// 전부 통과하면 true. reason은 항상 사용자 대상 한국어 문장으로 채워지며, 절대 throw하지 않습니다
        /// (판단 실패는 fail-safe로 비활성 처리).
        /// </summary>
        internal static bool ShouldEnable(
            AITEditorScriptObject config, ServerType type, string buildProjectPath, bool viteOnly,
            out string reason)
        {
            try
            {
                string envOverride = Environment.GetEnvironmentVariable("AIT_DEVTOOLS");
                if (!string.IsNullOrEmpty(envOverride))
                {
                    string normalized = envOverride.Trim();
                    if (normalized == "0" || normalized.Equals("false", StringComparison.OrdinalIgnoreCase))
                    {
                        reason = "환경변수 AIT_DEVTOOLS=0 오버라이드로 비활성화되었습니다.";
                        return false;
                    }
                }

                if (type != ServerType.Dev)
                {
                    reason = "Dev Server가 아니므로 devtools를 사용하지 않습니다.";
                    return false;
                }

                if (config == null || config.devtools == null || !config.devtools.enabled)
                {
                    reason = "AIT Configuration의 Devtools 설정이 꺼져 있습니다.";
                    return false;
                }

                if (!viteOnly)
                {
                    reason = "web-framework 2.x(granite CLI 경로)에서는 vite.config.ts가 적용되지 않아 devtools를 사용할 수 없습니다.";
                    return false;
                }

                if (!IsDevtoolsInstalled(buildProjectPath))
                {
                    reason = "@apps-in-toss/devtools가 설치되어 있지 않습니다. AIT > Build & Package 등으로 ait-build를 재설치해주세요.";
                    return false;
                }

                reason = "devtools 활성화 조건을 모두 충족했습니다.";
                return true;
            }
            catch (Exception e)
            {
                reason = $"devtools 활성화 여부 판단 중 오류가 발생해 비활성화합니다: {e.Message}";
                return false;
            }
        }

        /// <summary>
        /// vite.config.ts가 읽는 devtools 관련 환경변수를 envVars에 채웁니다.
        /// AIT_DEVTOOLS/AIT_DEVTOOLS_PANEL은 항상 명시 설정하고, AIT_DEVTOOLS_MCP는
        /// config.devtools.mcp가 true일 때만 키를 추가합니다 (AIT_DEVTOOLS_TUNNEL은 여기서 다루지 않음 —
        /// 사람 수동 전용이라 Editor/CI가 설정하지 않습니다).
        /// </summary>
        internal static void AddEnvVars(System.Collections.Generic.Dictionary<string, string> envVars, AITEditorScriptObject config, bool enabled)
        {
            try
            {
                if (envVars == null) return;

                envVars["AIT_DEVTOOLS"] = enabled ? "1" : "0";

                bool panelOn = config != null && config.devtools != null && config.devtools.panel;
                envVars["AIT_DEVTOOLS_PANEL"] = panelOn ? "1" : "0";

                bool mcpOn = config != null && config.devtools != null && config.devtools.mcp;
                if (mcpOn)
                {
                    envVars["AIT_DEVTOOLS_MCP"] = "1";
                }
            }
            catch (Exception e)
            {
                Debug.Log($"[AIT] devtools 환경변수 구성 중 오류 (무시됨): {e.Message}");
            }
        }
    }
}
