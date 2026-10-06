using System;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    public partial class AITConfigurationWindow
    {
        private void DrawBuildSettings()
        {
            showBuildProfiles = EditorGUILayout.Foldout(showBuildProfiles, "빌드 프로필", true);

            if (!showBuildProfiles) return;

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.HelpBox(
                "Dev Server: 로컬 개발/테스트용 (빌드 속도 우선)\n" +
                "Production: 배포용 (Build & Package, Deploy for Online Test, Deploy Release Candidate에서 공통 사용)",
                MessageType.Info
            );

            GUILayout.Space(10);

            // Dev Server 프로필
            DrawBuildProfile(
                ref showDevServerProfile,
                "Dev Server",
                "로컬 개발/테스트용 (빌드 속도 우선)",
                config.devServerProfile,
                AITBuildProfile.CreateDevServerProfile()
            );

            GUILayout.Space(5);

            // Production 프로필
            DrawBuildProfile(
                ref showProductionProfile,
                "Production",
                "배포용 (Build & Package, Deploy for Online Test, Deploy Release Candidate에서 공통 사용)",
                config.productionProfile,
                AITBuildProfile.CreateProductionProfile()
            );

            GUILayout.Space(10);

            // 모든 프로필 초기화 버튼
            if (GUILayout.Button("모든 프로필 기본값으로 초기화"))
            {
                if (AITPlatformHelper.ShowConfirmDialog("프로필 초기화", "모든 빌드 프로필을 기본값으로 초기화하시겠습니까?", "예", "아니오", autoApprove: true))
                {
                    config.devServerProfile = AITBuildProfile.CreateDevServerProfile();
                    config.productionProfile = AITBuildProfile.CreateProductionProfile();
                    SaveSettings();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawBuildProfile(ref bool foldout, string name, string description, AITBuildProfile profile, AITBuildProfile defaultProfile)
        {
            EditorGUILayout.BeginVertical("box");

            foldout = EditorGUILayout.Foldout(foldout, $"{name}", true);

            if (!foldout)
            {
                // 접힌 상태에서 요약 표시
                EditorGUI.indentLevel++;
                string summary = GetProfileSummary(profile);
                EditorGUILayout.LabelField(summary, EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.HelpBox(description, MessageType.None);

            EditorGUI.indentLevel++;

            // 런타임 설정 헤더
            EditorGUILayout.LabelField("런타임 설정", EditorStyles.boldLabel);

            // 디버그 콘솔
            profile.enableDebugConsole = EditorGUILayout.Toggle(
                new GUIContent("디버그 콘솔 활성화", "개발/테스트 목적으로 콘솔 사용"),
                profile.enableDebugConsole
            );

            GUILayout.Space(5);

            // 빌드 설정 헤더
            EditorGUILayout.LabelField("빌드 설정", EditorStyles.boldLabel);

            // Development Build
            profile.developmentBuild = EditorGUILayout.Toggle(
                new GUIContent("Development Build", "빌드 속도 향상 및 디버깅 편의성 (배포용에서는 비활성화 권장)"),
                profile.developmentBuild
            );

            // LZ4 압축
            profile.enableLZ4Compression = EditorGUILayout.Toggle(
                new GUIContent("LZ4 압축", "빌드 프로세스 속도 향상을 위한 LZ4 압축"),
                profile.enableLZ4Compression
            );

            // 압축 포맷
            string[] compressionOptions = { "자동", "Disabled", "Gzip", "Brotli" };
            int compressionIndex = profile.compressionFormat < 0 ? 0 : profile.compressionFormat + 1;
            compressionIndex = EditorGUILayout.Popup(
                new GUIContent("WebGL 압축", "최종 빌드 결과물의 압축 포맷 (-1=자동: Brotli)"),
                compressionIndex,
                compressionOptions
            );
            profile.compressionFormat = compressionIndex == 0 ? -1 : compressionIndex - 1;

            // Stripping Level — 저장값은 UI 순서(1=Minimal, 2=Low, 3=Medium, 4=High)이며
            // 빌드 적용 시 AITBuildInitializer.ConvertToManagedStrippingLevel로 실제 enum에 매핑됨
            // WebGL(IL2CPP)은 Disabled를 지원하지 않아 옵션에서 제외, 레거시 저장값 0(Disabled)은 Minimal로 정규화
            if (profile.managedStrippingLevel == 0) profile.managedStrippingLevel = 1;
            else if (profile.managedStrippingLevel > 4) profile.managedStrippingLevel = -1;
            string[] strippingOptions = { "자동 (High)", "Minimal", "Low", "Medium", "High" };
            int strippingIndex = profile.managedStrippingLevel < 0 ? 0 : profile.managedStrippingLevel;
            strippingIndex = EditorGUILayout.Popup(
                new GUIContent("Managed Stripping", "코드 스트리핑 레벨. 높을수록 산출물이 작고 실측상 빌드도 빠름(IL2CPP로 넘어가는 관리 코드량이 줄어 변환·컴파일·링크 시간이 감소)"),
                strippingIndex,
                strippingOptions
            );
            profile.managedStrippingLevel = strippingIndex == 0 ? -1 : strippingIndex;

            // 디버그 심볼
            profile.debugSymbolsExternal = EditorGUILayout.Toggle(
                new GUIContent("디버그 심볼 외부 분리", "빌드 크기 감소를 위해 심볼을 외부 파일로 분리"),
                profile.debugSymbolsExternal
            );

            // 기본값과 다른 경우 리셋 버튼 표시
            if (!IsProfileDefault(profile, defaultProfile))
            {
                GUILayout.Space(5);
                if (GUILayout.Button($"{name} 프로필 기본값으로 복원", GUILayout.Height(20)))
                {
                    ResetProfile(profile, defaultProfile);
                }
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();
        }

        private void ResetProfile(AITBuildProfile profile, AITBuildProfile defaultProfile)
        {
            profile.enableDebugConsole = defaultProfile.enableDebugConsole;
            profile.developmentBuild = defaultProfile.developmentBuild;
            profile.enableLZ4Compression = defaultProfile.enableLZ4Compression;
            profile.compressionFormat = defaultProfile.compressionFormat;
            profile.managedStrippingLevel = defaultProfile.managedStrippingLevel;
            profile.debugSymbolsExternal = defaultProfile.debugSymbolsExternal;
        }

        private string GetProfileSummary(AITBuildProfile profile)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (profile.enableDebugConsole) parts.Add("Debug");
            if (profile.developmentBuild) parts.Add("Dev");
            if (profile.enableLZ4Compression) parts.Add("LZ4");

            // 압축 포맷
            string compression = profile.compressionFormat switch
            {
                0 => "NoCompress",
                1 => "Gzip",
                2 => "Brotli",
                _ => ""
            };
            if (!string.IsNullOrEmpty(compression)) parts.Add(compression);

            // Stripping (실제 적용될 enum 기준)
            string stripping = profile.managedStrippingLevel < 0
                ? ""
                : AITBuildInitializer.ConvertToManagedStrippingLevel(profile.managedStrippingLevel).ToString();
            if (!string.IsNullOrEmpty(stripping)) parts.Add(stripping);

            return parts.Count > 0 ? string.Join(", ", parts) : "(기본 설정)";
        }

        private bool IsProfileDefault(AITBuildProfile profile, AITBuildProfile defaultProfile)
        {
            return profile.enableDebugConsole == defaultProfile.enableDebugConsole &&
                   profile.developmentBuild == defaultProfile.developmentBuild &&
                   profile.enableLZ4Compression == defaultProfile.enableLZ4Compression &&
                   profile.compressionFormat == defaultProfile.compressionFormat &&
                   profile.managedStrippingLevel == defaultProfile.managedStrippingLevel &&
                   profile.debugSymbolsExternal == defaultProfile.debugSymbolsExternal;
        }

    }
}
