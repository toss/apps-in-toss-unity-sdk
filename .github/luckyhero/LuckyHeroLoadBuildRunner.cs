using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using AppsInToss;

/// <summary>
/// LuckyHeroDefence(또는 임의의 AIT 게임 프로젝트)를 Apps in Toss SDK로
/// 비대화형(batchmode) 빌드하는 러너.
///
/// CI(.github/workflows/luckyhero-load-build.yml)가 게임 프로젝트의
/// Assets/Editor/ 로 복사한 뒤 -executeMethod LuckyHeroLoadBuildRunner.CommandLineBuild
/// 로 호출한다.
///
/// E2EBuildRunner 와 달리 벤치마크 씬을 새로 만들지 않는다. 실제 게임의
/// 기존 EditorBuildSettings.scenes / AIT Config / productionProfile 을 그대로
/// 사용해 "현실 그대로의" 빌드를 생성한다 (로드타임 before/after 측정용).
///
/// 빌드 설정(압축/IL2CPP config/개발빌드 여부)은 SDK 의 AITBuildInitializer 가
/// 환경변수(AIT_COMPRESSION_FORMAT / AIT_DEVELOPMENT_BUILD / AIT_IL2CPP_CONFIGURATION)
/// 와 SDK 기본값(엔진 strip · managed High · Brotli · DiskSizeLTO 등)으로 적용한다.
/// 이 러너는 빌드 트리거만 담당한다.
/// </summary>
public static class LuckyHeroLoadBuildRunner
{
    public static void CommandLineBuild()
    {
        try
        {
            Debug.Log("======== LuckyHero Load Build (batchmode) ========");

            // websockify 포트 충돌 방지 (E2EBuildRunner 와 동일)
            EditorUserBuildSettings.connectProfiler = false;

            var config = UnityUtil.GetEditorConf();
            if (config == null)
            {
                Debug.LogError("[LuckyHeroBuild] AIT Editor Config 를 찾을 수 없습니다 — 게임에 Apps in Toss 설정이 없습니까?");
                EditorApplication.Exit(3);
                return;
            }

            Debug.Log($"[LuckyHeroBuild] appName={config.appName}, displayName={config.displayName}, version={config.version}");

            var scenes = EditorBuildSettings.scenes;
            int enabledScenes = 0;
            foreach (var s in scenes) if (s.enabled) enabledScenes++;
            Debug.Log($"[LuckyHeroBuild] EditorBuildSettings.scenes = {scenes.Length}개 (enabled={enabledScenes})");
            foreach (var s in scenes)
                Debug.Log($"  - {(s.enabled ? "[x]" : "[ ]")} {s.path}");

            if (enabledScenes == 0)
            {
                Debug.LogError("[LuckyHeroBuild] 활성화된 빌드 씬이 없습니다 — 게임의 Build Settings 씬 구성을 확인하세요.");
                EditorApplication.Exit(5);
                return;
            }

            // 빌드 직전 미flush 변경분 디스크 반영
            AssetDatabase.SaveAssets();

            Debug.Log("[LuckyHeroBuild] SDK Init...");
            AITConvertCore.Init();

            // 기본은 incremental (Library/Bee 캐시 재사용). AIT_CLEAN_BUILD=1 이면 클린.
            string cleanEnv = Environment.GetEnvironmentVariable("AIT_CLEAN_BUILD");
            bool cleanBuild = cleanEnv == "1" || string.Equals(cleanEnv, "true", StringComparison.OrdinalIgnoreCase);
            Debug.Log($"[LuckyHeroBuild] cleanBuild={cleanBuild} (productionProfile 사용)");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = AITConvertCore.DoExport(
                buildWebGL: true,
                doPackaging: true,
                cleanBuild: cleanBuild,
                profile: config.productionProfile,
                profileName: "LuckyHero Load Build"
            );
            sw.Stop();

            if (result == AITConvertCore.AITExportError.SUCCEED)
            {
                Debug.Log($"[LuckyHeroBuild] ✓ 빌드 성공 (소요 {sw.Elapsed.TotalSeconds:F1}s)");
                Debug.Log("LuckyHero Load Build - SUCCESS");
                // -quit 가 정상 종료 처리 ("Exiting batchmode successfully now!")
            }
            else
            {
                Debug.LogError($"[LuckyHeroBuild] ✗ 빌드 실패: {AITConvertCore.GetErrorMessage(result)} (result={result})");
                Debug.LogError("LuckyHero Load Build - FAILED");
                EditorApplication.Exit(1);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LuckyHeroBuild] 예외 발생: {e}");
            EditorApplication.Exit(4);
        }
    }
}
