// -----------------------------------------------------------------------
// AITBuildInitializerInitIntegrationTests.cs - Init 실경로 통합 검증
// Level 0: 변환 함수 단위(AITBuildProfileMappingTests)가 아니라 실제 빌드
// 진입점 AITBuildInitializer.Init(profile)을 그대로 호출해 PlayerSettings에
// 최종 반영되는 값을 검증한다.
// NHN 제보 버그의 실경로 회귀 가드: 드롭다운 'High'(저장값 4) 선택 시
// 직접 캐스팅으로 Minimal(=4)이 빌드에 적용되던 문제.
// -----------------------------------------------------------------------

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;
#if UNITY_6000_0_OR_NEWER
using UnityEditor.Build;
#endif

[TestFixture]
public class AITBuildInitializerInitIntegrationTests
{
    private PlayerSettingsSnapshot _backup;

    // 스냅샷이 커버하지 않는 항목 수동 백업
    private bool _useDefaultGraphicsAPIs;
    private UnityEngine.Rendering.GraphicsDeviceType[] _graphicsAPIs;

    // Init이 읽는 저장 설정(AITEditorScriptObject) 백업
    private int _savedExceptionSupport;
#if UNITY_2023_3_OR_NEWER
    private int _savedPowerPreference;
#endif

    [SetUp]
    public void Setup()
    {
        _backup = PlayerSettingsSnapshot.Capture();
        _useDefaultGraphicsAPIs = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.WebGL);
        _graphicsAPIs = PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL);

        var config = UnityUtil.GetEditorConf();
        _savedExceptionSupport = config.exceptionSupport;
#if UNITY_2023_3_OR_NEWER
        _savedPowerPreference = config.powerPreference;
#endif
    }

    [TearDown]
    public void TearDown()
    {
        var config = UnityUtil.GetEditorConf();
        config.exceptionSupport = _savedExceptionSupport;
#if UNITY_2023_3_OR_NEWER
        config.powerPreference = _savedPowerPreference;
#endif

        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, _useDefaultGraphicsAPIs);
        if (!_useDefaultGraphicsAPIs)
        {
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, _graphicsAPIs);
        }
        _backup.Restore();
    }

    private static ManagedStrippingLevel GetAppliedStrippingLevel()
    {
#if UNITY_6000_0_OR_NEWER
        return PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL);
#else
        return PlayerSettings.GetManagedStrippingLevel(BuildTargetGroup.WebGL);
#endif
    }

    private static AITBuildProfile ProductionProfileWithStripping(int storedValue)
    {
        var profile = AITBuildProfile.CreateProductionProfile();
        profile.managedStrippingLevel = storedValue;
        return profile;
    }

    // =====================================================
    // Managed Stripping: 저장값 → Init → PlayerSettings 최종 반영
    // =====================================================

    [Test]
    public void Init_ProfileStoredHigh_AppliesManagedStrippingHigh()
    {
        AITBuildInitializer.Init(ProductionProfileWithStripping(4));

        var applied = GetAppliedStrippingLevel();
        Assert.AreEqual(ManagedStrippingLevel.High, applied,
            "드롭다운 'High'(저장값 4)로 Init하면 PlayerSettings에 High가 반영되어야 함");
        Assert.AreNotEqual(ManagedStrippingLevel.Minimal, applied,
            "원래 버그: 저장값 4 직접 캐스팅으로 Minimal(=4)이 적용되던 회귀 방지");
    }

    [Test]
    public void Init_ProfileStoredMinimal_AppliesManagedStrippingMinimal()
    {
        // 원래 버그: 저장값 1("Minimal") 직접 캐스팅으로 Low(=1)가 적용됨
        AITBuildInitializer.Init(ProductionProfileWithStripping(1));

        Assert.AreEqual(ManagedStrippingLevel.Minimal, GetAppliedStrippingLevel());
    }

    [Test]
    public void Init_ProfileAuto_AppliesDefaultStripping()
    {
        AITBuildInitializer.Init(ProductionProfileWithStripping(-1));

        Assert.AreEqual(AITDefaultSettings.GetDefaultManagedStrippingLevel(),
            GetAppliedStrippingLevel());
    }

    // =====================================================
    // Exception Support: 저장 설정 → Init → PlayerSettings 최종 반영
    // =====================================================

    [Test]
    public void Init_ConfigExceptionSupportStored2_AppliesFullWithStacktrace()
    {
        // 원래 버그: 저장값 2("Full With Stacktrace") 직접 캐스팅으로
        // FullWithoutStacktrace(=2)가 적용되어 Sentry 스택트레이스가 사라짐
        UnityUtil.GetEditorConf().exceptionSupport = 2;

        AITBuildInitializer.Init(AITBuildProfile.CreateProductionProfile());

        Assert.AreEqual(WebGLExceptionSupport.FullWithStacktrace,
            PlayerSettings.WebGL.exceptionSupport);
    }

    [Test]
    public void Init_ConfigExceptionSupportStored3_AppliesFullWithoutStacktrace()
    {
        UnityUtil.GetEditorConf().exceptionSupport = 3;

        AITBuildInitializer.Init(AITBuildProfile.CreateProductionProfile());

        Assert.AreEqual(WebGLExceptionSupport.FullWithoutStacktrace,
            PlayerSettings.WebGL.exceptionSupport);
    }

#if UNITY_2023_3_OR_NEWER
    // =====================================================
    // Power Preference: 저장 설정 → Init → PlayerSettings 최종 반영 (Unity 2023.3+)
    // =====================================================

    [Test]
    public void Init_ConfigPowerPreferenceStored1_AppliesHighPerformance()
    {
        // 원래 버그: 저장값 1("HighPerformance") 직접 캐스팅으로 LowPower(=1)가 적용됨
        UnityUtil.GetEditorConf().powerPreference = 1;

        AITBuildInitializer.Init(AITBuildProfile.CreateProductionProfile());

        Assert.AreEqual(WebGLPowerPreference.HighPerformance,
            PlayerSettings.WebGL.powerPreference);
    }
#endif
}
