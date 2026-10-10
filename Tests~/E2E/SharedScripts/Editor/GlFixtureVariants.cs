using UnityEditor;
using UnityEngine;
using AppsInToss;

/// <summary>
/// GL 컨텍스트 레이어(ait-gl.js) 검증용 perf 픽스처 변형.
///
/// heavy 픽스처는 QualitySettings 의 antiAliasing 이 0 이라 "context antialias 를 끄면 Unity 가 자체 MSAA RT 를 만드는가"를
/// 확인할 수 없다. 이 변형들은 모든 품질 레벨의 antiAliasing 을 4 로 올려서 그 질문에 답하는 빌드를 만든다.
/// perf.yml 의 workflow_dispatch <c>variant</c> 입력(→ AIT_PERF_VARIANT)에 이름을 적어 쓴다. 등록 규약은 <see cref="HeavyBuildVariants"/> 참조.
///
///   gl-aa4        : AA=4, webglAntialiasOpt 는 자동(-1). 기본 동작(antialias 해제 없음) 기준선.
///   gl-aa4-drop   : AA=4, webglAntialiasOpt=1. 모바일(UA/DPR/메모리 조건)에서 context antialias 요청을 끈다.
///   gl-aa4-stock  : AA=4, webglAntialiasOpt=0. getContext 훅 자체를 설치하지 않는 대조군.
///
/// 확인 방법: 빌드를 모바일 UA 로 열고 <c>?aitglprobe=1</c> 로 RT 인벤토리를 본다(Tests~/E2E/tests/perf-gl.test.js).
/// gl-aa4-drop 에서 kind=rbMS 행이 없으면 context AA 를 꺼도 Unity 가 자체 MSAA RT 를 만들지 않는 것이고,
/// 있으면 AA drop 의 기본값을 자동으로 올리면 안 된다(메모리가 오히려 늘 수 있다).
/// 앞의 판정은 PERF_GL_EXPECT_NO_RBMS=1 로 테스트에서 단언할 수 있다.
/// </summary>
public static class GlFixtureVariants
{
    private const int FixtureAntiAliasing = 4;

    [HeavyVariant("gl-aa4")]
    public static void ApplyAa4(AITEditorScriptObject config)
    {
        SetAntiAliasingOnAllQualityLevels(FixtureAntiAliasing);
        config.webglAntialiasOpt = -1;
    }

    [HeavyVariant("gl-aa4-drop")]
    public static void ApplyAa4Drop(AITEditorScriptObject config)
    {
        SetAntiAliasingOnAllQualityLevels(FixtureAntiAliasing);
        config.webglAntialiasOpt = 1;
    }

    [HeavyVariant("gl-aa4-stock")]
    public static void ApplyAa4Stock(AITEditorScriptObject config)
    {
        SetAntiAliasingOnAllQualityLevels(FixtureAntiAliasing);
        config.webglAntialiasOpt = 0;
    }

    private static void SetAntiAliasingOnAllQualityLevels(int samples)
    {
        int originalLevel = QualitySettings.GetQualityLevel();
        int levelCount = QualitySettings.names.Length;
        for (int i = 0; i < levelCount; i++)
        {
            QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
            QualitySettings.antiAliasing = samples;
        }
        QualitySettings.SetQualityLevel(originalLevel, applyExpensiveChanges: false);
        AssetDatabase.SaveAssets();

        Debug.Log($"[heavy] GL 변형: QualitySettings.antiAliasing = {samples} → {levelCount} 레벨 전부 적용");
    }
}
