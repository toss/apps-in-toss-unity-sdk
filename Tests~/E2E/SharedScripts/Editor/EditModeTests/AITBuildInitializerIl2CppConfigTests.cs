// -----------------------------------------------------------------------
// AITBuildInitializerIl2CppConfigTests.cs - IL2CPP 컴파일러 구성/Code Generation 결정 로직 검증
// Level 0: AITBuildInitializer.ResolveIl2CppConfiguration / ResolveIl2CppCodeGeneration 순수 함수
//   단위 테스트 (env AIT_IL2CPP_CONFIGURATION/AIT_IL2CPP_CODE_GENERATION 오버라이드는 각 함수 밖에서
//   별도 적용되므로 대상 아님) + Init(profile, fastBuild) 실경로 통합 검증
// 빠른 빌드(Dev Server·Deploy (Test)) 속도 개선(IL2CPP Compiler Configuration=Debug,
// Code Generation=OptimizeSize) 회귀 방지용
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
public class AITBuildInitializerIl2CppConfigTests
{
    // =====================================================
    // ResolveIl2CppConfiguration 순수 함수 단위 테스트
    // =====================================================

    [Test]
    public void Resolve_ExplicitEditorConfigValue_TakesPriorityOverFastBuild()
    {
        // 사용자가 Configuration Window에서 명시적으로 Master(2)를 선택한 경우,
        // 빠른 빌드여도 사용자 선택을 존중해야 한다.
        var result = AITBuildInitializer.ResolveIl2CppConfiguration(
            editorConfigValue: (int)Il2CppCompilerConfiguration.Master, fastBuild: true);

        Assert.AreEqual(Il2CppCompilerConfiguration.Master, result);
    }

    [Test]
    public void Resolve_AutoValue_FastBuild_ReturnsDebug()
    {
        var result = AITBuildInitializer.ResolveIl2CppConfiguration(editorConfigValue: -1, fastBuild: true);

        Assert.AreEqual(Il2CppCompilerConfiguration.Debug, result);
    }

    [Test]
    public void Resolve_AutoValue_NotFastBuild_ReturnsDefault()
    {
        var result = AITBuildInitializer.ResolveIl2CppConfiguration(editorConfigValue: -1, fastBuild: false);

        Assert.AreEqual(AITDefaultSettings.GetDefaultIl2CppConfiguration(), result);
        // 회귀 방지: Production/Deploy (Production)/Build & Package 경로는 절대 Debug가 되면 안 됨
        Assert.AreNotEqual(Il2CppCompilerConfiguration.Debug, result);
    }

    [Test]
    public void Resolve_ExplicitReleaseValue_NotFastBuild_ReturnsRelease()
    {
        var result = AITBuildInitializer.ResolveIl2CppConfiguration(
            editorConfigValue: (int)Il2CppCompilerConfiguration.Release, fastBuild: false);

        Assert.AreEqual(Il2CppCompilerConfiguration.Release, result);
    }

    // =====================================================
    // ResolveIl2CppCodeGeneration 순수 함수 단위 테스트
    // =====================================================

    [Test]
    public void ResolveCodeGeneration_FastBuild_ReturnsOptimizeSize()
    {
        var result = AITBuildInitializer.ResolveIl2CppCodeGeneration(fastBuild: true);

        Assert.AreEqual(UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize, result);
    }

    [Test]
    public void ResolveCodeGeneration_NotFastBuild_ReturnsNull_KeepsProjectSetting()
    {
        var result = AITBuildInitializer.ResolveIl2CppCodeGeneration(fastBuild: false);

        // 회귀 방지: Production/Deploy (Production)/Build & Package 경로는 프로젝트에 이미 설정된
        // Player Settings 값을 그대로 유지해야 한다(예: 사용자가 명시적으로 선택한 OptimizeSize를
        // 조용히 OptimizeSpeed로 되돌리면 안 됨) — 그래서 값을 강제하지 않고 null을 반환한다.
        Assert.IsNull(result);
    }

    // =====================================================
    // Init 실경로 통합 검증 (PlayerSettings 최종 반영)
    // =====================================================

    private PlayerSettingsSnapshot _backup;
    private int _savedIl2CppConfiguration;

    // 스냅샷이 커버하지 않는 항목 수동 백업 (AITBuildInitializerInitIntegrationTests.cs와 동일 사유:
    // Init()이 PlayerSettings.SetUseDefaultGraphicsAPIs/SetGraphicsAPIs를 직접 호출함)
    private bool _useDefaultGraphicsAPIs;
    private UnityEngine.Rendering.GraphicsDeviceType[] _graphicsAPIs;

    [SetUp]
    public void Setup()
    {
        _backup = PlayerSettingsSnapshot.Capture();
        _savedIl2CppConfiguration = UnityUtil.GetEditorConf().il2cppConfiguration;
        _useDefaultGraphicsAPIs = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.WebGL);
        _graphicsAPIs = PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL);
    }

    [TearDown]
    public void TearDown()
    {
        UnityUtil.GetEditorConf().il2cppConfiguration = _savedIl2CppConfiguration;

        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, _useDefaultGraphicsAPIs);
        if (!_useDefaultGraphicsAPIs)
        {
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, _graphicsAPIs);
        }
        _backup.Restore();
    }

    private static Il2CppCompilerConfiguration GetAppliedIl2CppConfiguration()
    {
#if UNITY_6000_0_OR_NEWER
        return PlayerSettings.GetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL);
#else
        return PlayerSettings.GetIl2CppCompilerConfiguration(BuildTargetGroup.WebGL);
#endif
    }

    [Test]
    public void Init_FastBuild_ConfigAuto_AppliesDebug()
    {
        UnityUtil.GetEditorConf().il2cppConfiguration = -1;

        AITBuildInitializer.Init(AITBuildProfile.CreateProductionProfile(), fastBuild: true);

        Assert.AreEqual(Il2CppCompilerConfiguration.Debug, GetAppliedIl2CppConfiguration());
    }

    [Test]
    public void Init_NotFastBuild_ConfigAuto_AppliesDefault_NotDebug()
    {
        UnityUtil.GetEditorConf().il2cppConfiguration = -1;

        AITBuildInitializer.Init(AITBuildProfile.CreateProductionProfile(), fastBuild: false);

        Assert.AreEqual(AITDefaultSettings.GetDefaultIl2CppConfiguration(), GetAppliedIl2CppConfiguration());
        Assert.AreNotEqual(Il2CppCompilerConfiguration.Debug, GetAppliedIl2CppConfiguration(),
            "Production/Deploy (Production)/Build & Package 경로는 fastBuild 없이 Debug가 적용되면 안 됨");
    }

    [Test]
    public void Init_FastBuild_ConfigExplicitMaster_RespectsUserChoice()
    {
        // 사용자의 명시적 선택은 빠른 빌드에서도 그대로 존중되어야 한다.
        UnityUtil.GetEditorConf().il2cppConfiguration = (int)Il2CppCompilerConfiguration.Master;

        AITBuildInitializer.Init(AITBuildProfile.CreateProductionProfile(), fastBuild: true);

        Assert.AreEqual(Il2CppCompilerConfiguration.Master, GetAppliedIl2CppConfiguration());
    }

    [Test]
    public void Init_DefaultFastBuildFalse_BehavesLikeExistingCallers()
    {
        // fastBuild 인자를 생략한 기존 호출부와 동일 동작 (기본값 false)
        UnityUtil.GetEditorConf().il2cppConfiguration = -1;

        AITBuildInitializer.Init(AITBuildProfile.CreateProductionProfile());

        Assert.AreEqual(AITDefaultSettings.GetDefaultIl2CppConfiguration(), GetAppliedIl2CppConfiguration());
    }

    // =====================================================
    // ResolveIl2CppCodeGeneration 실경로 통합 검증 (PlayerSettings 최종 반영)
    // =====================================================

    private static UnityEditor.Build.Il2CppCodeGeneration GetAppliedIl2CppCodeGeneration()
    {
#if UNITY_2022_2_OR_NEWER
        return PlayerSettings.GetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL);
#else
        return EditorUserBuildSettings.il2CppCodeGeneration;
#endif
    }

    [Test]
    public void Init_FastBuild_AppliesOptimizeSize()
    {
        AITBuildInitializer.Init(AITBuildProfile.CreateProductionProfile(), fastBuild: true);

        Assert.AreEqual(UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize, GetAppliedIl2CppCodeGeneration());
    }

    private static void SetAppliedIl2CppCodeGeneration(UnityEditor.Build.Il2CppCodeGeneration value)
    {
#if UNITY_2022_2_OR_NEWER
        PlayerSettings.SetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL, value);
#else
        EditorUserBuildSettings.il2CppCodeGeneration = value;
#endif
    }

    [Test]
    public void Init_NotFastBuild_PreservesExistingOptimizeSize_DoesNotOverwrite()
    {
        // 사용자가 Player Settings에서 명시적으로 OptimizeSize("Faster (smaller) builds")를
        // 선택해 둔 경우, fastBuild 없는 경로(Production/Deploy (Production)/Build & Package)는
        // 이를 조용히 OptimizeSpeed로 되돌리면 안 된다.
        SetAppliedIl2CppCodeGeneration(UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);

        AITBuildInitializer.Init(AITBuildProfile.CreateProductionProfile(), fastBuild: false);

        Assert.AreEqual(UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize, GetAppliedIl2CppCodeGeneration(),
            "fastBuild 없는 경로는 프로젝트에 이미 설정된 IL2CPP Code Generation 값을 유지해야 함");
    }
}
