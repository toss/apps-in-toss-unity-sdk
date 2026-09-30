using System;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    public partial class AITConfigurationWindow
    {
        private void DrawWebGLOptimizationSettings()
        {
            showWebGLSettings = EditorGUILayout.Foldout(showWebGLSettings, "WebGL 최적화 설정", true);

            if (!showWebGLSettings) return;

            EditorGUILayout.BeginVertical("box");

            // 현재 Unity 버전 정보
            EditorGUILayout.HelpBox(
                $"현재 Unity 버전: {AITDefaultSettings.GetUnityVersionGroup()}\n" +
                "각 설정은 Unity 버전에 맞게 자동으로 최적화됩니다.",
                MessageType.Info
            );

            GUILayout.Space(5);

            // 메모리 크기
            DrawMemorySizeSetting();

            // 스레딩 지원
            DrawThreadsSupportSetting();

            // 데이터 캐싱: 베타 기능 — 정식 공개 전까지 설정 UI에서 제외
            // (config.dataCaching 저장값과 빌드 적용 로직은 유지 — AITBuildInitializer 참조)

            // 파일 해싱
            config.nameFilesAsHashes = EditorGUILayout.Toggle("파일명 해싱", config.nameFilesAsHashes);

            GUILayout.Space(10);

            // first-interactive 계측
            DrawFirstInteractiveLogSetting();

            GUILayout.Space(10);

            // PlayerPrefs 영속화
            DrawPlayerPrefsPersistenceSetting();

            GUILayout.Space(10);

            // 변경된 설정 개수 표시
            int modifiedCount = CountModifiedWebGLSettings();
            if (modifiedCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{modifiedCount}개 설정이 기본값에서 변경됨",
                    MessageType.Warning
                );

                if (GUILayout.Button("모든 WebGL 설정 기본값으로 복원"))
                {
                    ResetWebGLSettings();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawMemorySizeSetting()
        {
            int defaultMemory = AITDefaultSettings.GetDefaultMemorySize();
            bool isModified = config.memorySize > 0 && config.memorySize != defaultMemory;

            EditorGUILayout.BeginHorizontal();

            // 하이라이트 표시
            DrawModifiedIndicator(isModified);

            string label = config.memorySize <= 0
                ? $"초기 메모리 크기 (자동: {defaultMemory}MB)"
                : "초기 메모리 크기";

            string[] options = { $"자동 ({defaultMemory}MB)", "256MB", "512MB", "768MB", "1024MB", "1536MB" };
            int currentIndex = GetMemorySizeIndex(config.memorySize, defaultMemory);
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            config.memorySize = IndexToMemorySize(newIndex);

            if (isModified && DrawResetButton())
            {
                config.memorySize = -1;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawThreadsSupportSetting()
        {
            bool defaultThreads = AITDefaultSettings.GetDefaultThreadsSupport();
            bool isModified = config.threadsSupport >= 0 && (config.threadsSupport == 1) != defaultThreads;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string label = config.threadsSupport < 0
                ? $"스레딩 지원 (자동: {(defaultThreads ? "활성화" : "비활성화")})"
                : "스레딩 지원";

            string[] options = { $"자동 ({(defaultThreads ? "활성화" : "비활성화")})", "비활성화", "활성화" };
            int currentIndex = config.threadsSupport < 0 ? 0 : config.threadsSupport + 1;
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            config.threadsSupport = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.threadsSupport = -1;
            }

            EditorGUILayout.EndHorizontal();
        }

        private int GetMemorySizeIndex(int memorySize, int defaultMemory)
        {
            if (memorySize <= 0) return 0;

            switch (memorySize)
            {
                case 256: return 1;
                case 512: return 2;
                case 768: return 3;
                case 1024: return 4;
                case 1536: return 5;
                default: return 0;
            }
        }

        private int IndexToMemorySize(int index)
        {
            switch (index)
            {
                case 0: return -1;  // 자동
                case 1: return 256;
                case 2: return 512;
                case 3: return 768;
                case 4: return 1024;
                case 5: return 1536;
                default: return -1;
            }
        }

        private void DrawFirstInteractiveLogSetting()
        {
            bool defaultValue = AITDefaultSettings.GetDefaultFirstInteractiveLog();
            bool isModified = config.firstInteractiveLog >= 0 && (config.firstInteractiveLog == 1) != defaultValue;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string autoLabel = defaultValue ? "활성화" : "비활성화";
            string label = config.firstInteractiveLog < 0
                ? $"first-interactive 계측 (자동: {autoLabel})"
                : "first-interactive 계측";

            string[] options = { $"자동 ({autoLabel})", "비활성화", "활성화" };
            int currentIndex = config.firstInteractiveLog < 0 ? 0 : config.firstInteractiveLog + 1;
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "원래 첫 씬 로드 완료 시점(time-to-original-scene)을 호스트에 전송합니다. " +
                    "픽셀 불변·세션당 1회 단일 이벤트이므로 기본 활성화됩니다."),
                currentIndex, options);
            config.firstInteractiveLog = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.firstInteractiveLog = -1;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPlayerPrefsPersistenceSetting()
        {
            bool defaultValue = AITDefaultSettings.GetDefaultPlayerPrefsPersistence();
            bool isModified = config.playerPrefsPersistence >= 0 && (config.playerPrefsPersistence == 1) != defaultValue;

            EditorGUILayout.BeginHorizontal();

            DrawModifiedIndicator(isModified);

            string autoLabel = defaultValue ? "활성화" : "비활성화";
            string label = config.playerPrefsPersistence < 0
                ? $"PlayerPrefs 영속화 (자동: {autoLabel})"
                : "PlayerPrefs 영속화";

            string[] options = { $"자동 ({autoLabel})", "비활성화", "활성화" };
            int currentIndex = config.playerPrefsPersistence < 0 ? 0 : config.playerPrefsPersistence + 1;
            int newIndex = EditorGUILayout.Popup(
                new GUIContent(label,
                    "PlayerPrefs 영속화 (앱인토스 Storage): WebGL의 IndexedDB 영속성이 보장되지 않는 웹뷰 환경에서 " +
                    "PlayerPrefs 데이터를 앱인토스 Storage에 투명하게 백업/복원합니다. 기본 활성화됩니다."),
                currentIndex, options);
            config.playerPrefsPersistence = newIndex == 0 ? -1 : newIndex - 1;

            if (isModified && DrawResetButton())
            {
                config.playerPrefsPersistence = -1;
            }

            EditorGUILayout.EndHorizontal();
        }

        private int CountModifiedWebGLSettings()
        {
            int count = 0;

            int defaultMemory = AITDefaultSettings.GetDefaultMemorySize();
            if (config.memorySize > 0 && config.memorySize != defaultMemory) count++;

            bool defaultThreads = AITDefaultSettings.GetDefaultThreadsSupport();
            if (config.threadsSupport >= 0 && (config.threadsSupport == 1) != defaultThreads) count++;

            bool defaultFirstInteractive = AITDefaultSettings.GetDefaultFirstInteractiveLog();
            if (config.firstInteractiveLog >= 0 && (config.firstInteractiveLog == 1) != defaultFirstInteractive) count++;

            bool defaultPlayerPrefsPersistence = AITDefaultSettings.GetDefaultPlayerPrefsPersistence();
            if (config.playerPrefsPersistence >= 0 && (config.playerPrefsPersistence == 1) != defaultPlayerPrefsPersistence) count++;

            return count;
        }

        private void ResetWebGLSettings()
        {
            config.memorySize = -1;
            config.threadsSupport = -1;
            // dataCaching은 UI에서 숨겨진 베타 설정이라 리셋 대상에서도 제외 —
            // 화면에 보이지 않는 값을 복원 버튼이 조용히 덮어쓰면 안 됨
            config.firstInteractiveLog = -1;
            config.playerPrefsPersistence = -1;
        }

    }
}
