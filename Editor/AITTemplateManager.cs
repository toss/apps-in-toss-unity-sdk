using System.IO;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// WebGL 템플릿 관리 클래스
    /// </summary>
    internal static class AITTemplateManager
    {
        // 마커 상수 (TypeScript 설정 파일용)
        internal const string SDK_MARKER_START = "//// SDK_GENERATED_START";
        internal const string SDK_MARKER_END = "//// SDK_GENERATED_END ////";

        // HTML 마커 상수 (index.html용)
        internal const string HTML_USER_HEAD_START = "<!-- USER_HEAD_START";
        internal const string HTML_USER_HEAD_END = "<!-- USER_HEAD_END -->";
        internal const string HTML_USER_BODY_END_START = "<!-- USER_BODY_END_START";
        internal const string HTML_USER_BODY_END_END = "<!-- USER_BODY_END_END -->";

        /// <summary>
        /// WebGL 템플릿을 SDK에서 프로젝트로 복사합니다.
        /// 빌드 시마다 최신 SDK 템플릿으로 교체합니다.
        /// </summary>
        internal static void EnsureWebGLTemplatesExist()
        {
            // 프로젝트의 Assets/WebGLTemplates 경로
            string projectTemplatesPath = Path.Combine(Application.dataPath, "WebGLTemplates");
            string projectTemplate = Path.Combine(projectTemplatesPath, "AITTemplate");
            string projectIndexHtml = Path.Combine(projectTemplate, "index.html");

            // 항상 최신 SDK 템플릿으로 교체 (기존 조건 제거)
            // SDK 업데이트 시 새 템플릿이 적용되도록 함

            // SDK의 WebGLTemplates 경로 찾기 (여러 가능한 경로 시도)
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string[] possibleSdkPaths = new string[]
            {
                // Package로 설치된 경우 (Unity 프로젝트 루트 기준)
                Path.Combine(projectRoot, "Packages/im.toss.apps-in-toss-unity-sdk/WebGLTemplates"),
                Path.Combine(projectRoot, "Packages/com.appsintoss.miniapp/WebGLTemplates"),
                // Assembly 경로 기반
                Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(typeof(AITConvertCore).Assembly.Location)), "WebGLTemplates")
            };

            string sdkTemplatesPath = null;
            foreach (string path in possibleSdkPaths)
            {
                if (Directory.Exists(path))
                {
                    sdkTemplatesPath = path;
                    break;
                }
            }

            if (sdkTemplatesPath == null)
            {
                Debug.LogError($"[AIT] SDK WebGLTemplates 폴더를 찾을 수 없습니다.");
                return;
            }

            string sdkTemplate = Path.Combine(sdkTemplatesPath, "AITTemplate");
            string sdkIndexHtml = Path.Combine(sdkTemplate, "index.html");

            if (!Directory.Exists(sdkTemplate))
            {
                Debug.LogError($"[AIT] SDK 템플릿 폴더를 찾을 수 없습니다: {sdkTemplate}");
                return;
            }

            // 프로젝트 템플릿이 없으면 전체 복사
            if (!Directory.Exists(projectTemplate) || !File.Exists(projectIndexHtml))
            {
                Debug.Log("[AIT] WebGLTemplates를 프로젝트로 복사 중...");

                if (Directory.Exists(projectTemplate))
                {
                    Directory.Delete(projectTemplate, true);
                }

                Directory.CreateDirectory(projectTemplatesPath);
                UnityUtil.CopyDirectory(sdkTemplate, projectTemplate);
                Debug.Log("[AIT] ✓ WebGLTemplates 복사 완료");
                return;
            }

            // 프로젝트 템플릿이 있으면 마커 기반으로 업데이트
            UpdateProjectTemplate(projectTemplate, sdkTemplate);
        }

        /// <summary>
        /// 기존 프로젝트 템플릿을 SDK 템플릿으로 마커 기반 업데이트
        /// 사용자 커스텀 영역(USER_* 마커)은 보존하고 SDK 영역만 업데이트
        /// </summary>
        internal static void UpdateProjectTemplate(string projectTemplate, string sdkTemplate)
        {
            // index.html 마커 기반 업데이트
            string projectIndexHtml = Path.Combine(projectTemplate, "index.html");
            string sdkIndexHtml = Path.Combine(sdkTemplate, "index.html");

            if (File.Exists(sdkIndexHtml) && File.Exists(projectIndexHtml))
            {
                string projectContent = File.ReadAllText(projectIndexHtml);
                string sdkContent = File.ReadAllText(sdkIndexHtml);

                // 프로젝트에 마커가 없으면 (이전 버전) SDK 템플릿으로 교체하되 경고
                if (!projectContent.Contains(HTML_USER_HEAD_START))
                {
                    Debug.Log("[AIT] 템플릿 업데이트: 이전 버전 템플릿을 새 마커 기반 템플릿으로 교체합니다.");
                    Debug.LogWarning("[AIT] ⚠️ 기존 index.html에 커스텀 수정이 있었다면 수동으로 USER_* 마커 영역에 재적용하세요.");
                    File.WriteAllText(projectIndexHtml, sdkContent);
                }
                else
                {
                    // 마커가 있으면 사용자 영역 보존하고 SDK 영역만 업데이트
                    string updatedContent = MergeHtmlTemplates(projectContent, sdkContent);
                    if (updatedContent != projectContent)
                    {
                        File.WriteAllText(projectIndexHtml, updatedContent);
                        Debug.Log("[AIT] ✓ index.html 템플릿 업데이트 (사용자 커스텀 영역 보존)");
                    }
                }
            }

            // vite.config.ts, granite.config.ts 마커 기반 업데이트
            UpdateConfigFileWithMarkers(projectTemplate, sdkTemplate, "BuildConfig~/vite.config.ts");
            UpdateConfigFileWithMarkers(projectTemplate, sdkTemplate, "BuildConfig~/granite.config.ts");

            // Runtime 폴더는 항상 SDK 버전으로 덮어쓰기 (브릿지 코드)
            string projectRuntime = Path.Combine(projectTemplate, "Runtime");
            string sdkRuntime = Path.Combine(sdkTemplate, "Runtime");
            if (Directory.Exists(sdkRuntime))
            {
                if (Directory.Exists(projectRuntime))
                {
                    Directory.Delete(projectRuntime, true);
                }
                UnityUtil.CopyDirectory(sdkRuntime, projectRuntime);
            }

            // TemplateData는 항상 SDK 버전으로 덮어쓰기
            string projectTemplateData = Path.Combine(projectTemplate, "TemplateData");
            string sdkTemplateData = Path.Combine(sdkTemplate, "TemplateData");
            if (Directory.Exists(sdkTemplateData))
            {
                if (Directory.Exists(projectTemplateData))
                {
                    Directory.Delete(projectTemplateData, true);
                }
                UnityUtil.CopyDirectory(sdkTemplateData, projectTemplateData);
            }
        }

        /// <summary>
        /// HTML 템플릿 마커 기반 병합
        /// SDK 템플릿의 전체 구조를 사용하되, 프로젝트의 USER_* 마커 영역 내용을 보존
        /// </summary>
        internal static string MergeHtmlTemplates(string projectContent, string sdkContent)
        {
            string result = sdkContent;

            // USER_HEAD 영역 보존
            string projectUserHead = ExtractHtmlUserSection(projectContent, HTML_USER_HEAD_START, HTML_USER_HEAD_END);
            if (!string.IsNullOrEmpty(projectUserHead))
            {
                result = ReplaceHtmlUserSection(result, HTML_USER_HEAD_START, HTML_USER_HEAD_END, projectUserHead);
            }

            // USER_BODY_END 영역 보존
            string projectUserBodyEnd = ExtractHtmlUserSection(projectContent, HTML_USER_BODY_END_START, HTML_USER_BODY_END_END);
            if (!string.IsNullOrEmpty(projectUserBodyEnd))
            {
                result = ReplaceHtmlUserSection(result, HTML_USER_BODY_END_START, HTML_USER_BODY_END_END, projectUserBodyEnd);
            }

            return result;
        }

        /// <summary>
        /// Config 파일 마커 기반 업데이트 (vite.config.ts, granite.config.ts)
        /// SDK_GENERATED와 SDK_PLUGINS 섹션을 모두 업데이트합니다.
        /// </summary>
        internal static void UpdateConfigFileWithMarkers(string projectTemplate, string sdkTemplate, string relativePath)
        {
            string projectFile = Path.Combine(projectTemplate, relativePath);
            string sdkFile = Path.Combine(sdkTemplate, relativePath);

            if (!File.Exists(sdkFile)) return;

            // 프로젝트 파일이 없으면 SDK에서 복사
            if (!File.Exists(projectFile))
            {
                string dir = Path.GetDirectoryName(projectFile);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.Copy(sdkFile, projectFile);
                return;
            }

            string projectContent = File.ReadAllText(projectFile);
            string sdkContent = File.ReadAllText(sdkFile);

            // 프로젝트에 SDK_GENERATED 마커가 없으면 (구버전) SDK 파일로 교체
            if (!projectContent.Contains(SDK_MARKER_START))
            {
                Debug.Log($"[AIT] 템플릿 업데이트: {relativePath}를 새 마커 기반 버전으로 교체합니다.");
                Debug.LogWarning($"[AIT] ⚠️ 기존 {relativePath}에 커스텀 수정이 있었다면 USER_CONFIG 영역에 재적용하세요.");
                File.WriteAllText(projectFile, sdkContent);
                return;
            }

            string updatedContent = projectContent;

            // SDK_GENERATED 영역 업데이트
            string sdkGeneratedSection = ExtractMarkerSection(sdkContent, "SDK_GENERATED");
            if (!string.IsNullOrEmpty(sdkGeneratedSection))
            {
                updatedContent = ReplaceMarkerSection(updatedContent, "SDK_GENERATED", sdkGeneratedSection);
            }

            // SDK_PLUGINS 영역 업데이트 (vite.config.ts용)
            string sdkPluginsSection = ExtractMarkerSection(sdkContent, "SDK_PLUGINS");
            if (!string.IsNullOrEmpty(sdkPluginsSection))
            {
                if (updatedContent.Contains("//// SDK_PLUGINS_START"))
                {
                    // 기존 SDK_PLUGINS 섹션이 있으면 교체
                    updatedContent = ReplaceMarkerSection(updatedContent, "SDK_PLUGINS", sdkPluginsSection);
                }
                else
                {
                    // SDK_PLUGINS 섹션이 없으면 import 문 뒤에 삽입 (구버전 업그레이드)
                    int insertPos = FindImportEndPosition(updatedContent);
                    if (insertPos > 0)
                    {
                        updatedContent = updatedContent.Insert(insertPos, "\n" + sdkPluginsSection + "\n");
                        Debug.Log($"[AIT] ✓ {relativePath}에 SDK_PLUGINS 섹션 추가됨 (하위 호환성 업그레이드)");
                    }
                }
            }

            if (updatedContent != projectContent)
            {
                File.WriteAllText(projectFile, updatedContent);
            }
        }

        /// <summary>
        /// import 문이 끝나는 위치를 찾습니다.
        /// </summary>
        private static int FindImportEndPosition(string content)
        {
            int lastImportIdx = -1;
            int searchStart = 0;

            while (true)
            {
                int importIdx = content.IndexOf("import ", searchStart);
                if (importIdx == -1) break;

                int endOfLine = content.IndexOf('\n', importIdx);
                if (endOfLine == -1) endOfLine = content.Length;

                lastImportIdx = endOfLine;
                searchStart = endOfLine + 1;
            }

            return lastImportIdx > 0 ? lastImportIdx + 1 : -1;
        }

        /// <summary>
        /// 마커 기반으로 SDK 섹션을 교체합니다.
        /// </summary>
        internal static string ReplaceMarkerSection(string content, string newSdkSection)
        {
            int startIdx = content.IndexOf(SDK_MARKER_START);
            int endIdx = content.IndexOf(SDK_MARKER_END);

            if (startIdx == -1 || endIdx == -1)
            {
                Debug.LogWarning("[AIT] SDK 마커를 찾을 수 없습니다. 전체 파일을 SDK 버전으로 교체합니다.");
                return newSdkSection;
            }

            // 마커 포함하여 교체
            string before = content.Substring(0, startIdx);
            string after = content.Substring(endIdx + SDK_MARKER_END.Length);

            return before + newSdkSection + after;
        }

        /// <summary>
        /// SDK 마커 섹션을 추출합니다.
        /// </summary>
        internal static string ExtractSdkSection(string content)
        {
            int startIdx = content.IndexOf(SDK_MARKER_START);
            int endIdx = content.IndexOf(SDK_MARKER_END);

            if (startIdx == -1 || endIdx == -1)
            {
                return null;
            }

            return content.Substring(startIdx, endIdx + SDK_MARKER_END.Length - startIdx);
        }

        /// <summary>
        /// 지정된 마커 이름으로 섹션을 추출합니다.
        /// </summary>
        internal static string ExtractMarkerSection(string content, string markerName)
        {
            string startMarker = $"//// {markerName}_START";
            string endMarker = $"//// {markerName}_END ////";

            int startIdx = content.IndexOf(startMarker);
            int endIdx = content.IndexOf(endMarker);

            if (startIdx == -1 || endIdx == -1)
            {
                return null;
            }

            return content.Substring(startIdx, endIdx + endMarker.Length - startIdx);
        }

        /// <summary>
        /// 지정된 마커 이름으로 섹션을 교체합니다.
        /// </summary>
        internal static string ReplaceMarkerSection(string content, string markerName, string newSection)
        {
            string startMarker = $"//// {markerName}_START";
            string endMarker = $"//// {markerName}_END ////";

            int startIdx = content.IndexOf(startMarker);
            int endIdx = content.IndexOf(endMarker);

            if (startIdx == -1 || endIdx == -1)
            {
                Debug.LogWarning($"[AIT] 마커를 찾을 수 없습니다: {markerName}");
                return content;
            }

            string before = content.Substring(0, startIdx);
            string after = content.Substring(endIdx + endMarker.Length);

            return before + newSection + after;
        }

        /// <summary>
        /// HTML 마커 섹션을 추출합니다.
        /// </summary>
        internal static string ExtractHtmlUserSection(string content, string startMarker, string endMarker)
        {
            int startIdx = content.IndexOf(startMarker);
            int endIdx = content.IndexOf(endMarker);

            if (startIdx == -1 || endIdx == -1)
            {
                return null;
            }

            return content.Substring(startIdx, endIdx + endMarker.Length - startIdx);
        }

        /// <summary>
        /// HTML 마커 섹션을 교체합니다.
        /// </summary>
        internal static string ReplaceHtmlUserSection(string content, string startMarker, string endMarker, string newSection)
        {
            int startIdx = content.IndexOf(startMarker);
            int endIdx = content.IndexOf(endMarker);

            if (startIdx == -1 || endIdx == -1)
            {
                Debug.LogWarning($"[AIT] HTML 마커를 찾을 수 없습니다: {startMarker}");
                return content;
            }

            string before = content.Substring(0, startIdx);
            string after = content.Substring(endIdx + endMarker.Length);

            return before + newSection + after;
        }
    }
}
