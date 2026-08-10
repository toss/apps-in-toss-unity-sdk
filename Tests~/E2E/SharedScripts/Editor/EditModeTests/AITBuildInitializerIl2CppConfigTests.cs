// -----------------------------------------------------------------------
// AITBuildInitializerIl2CppConfigTests.cs - IL2CPP 컴파일러 구성 결정 로직 검증
// Level 0: AITBuildInitializer.ResolveIl2CppConfiguration 순수 함수 단위 테스트
//   (env AIT_IL2CPP_CONFIGURATION 오버라이드는 이 함수 밖에서 별도 적용되므로 대상 아님)
//   + Init(profile, isDevServerBuild) 실경로 통합 검증
// Dev Server 빌드 속도 개선(devServerProfile IL2CPP Compiler Configuration=Debug) 회귀 방지용
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
    public void Resolve_ExplicitEditorConfigValue_TakesPriorityOverDevServer()
    {
        // 사용자가 Configuration Window에서 명시적으로 Master(2)를 선택한 경우,
        // Dev Server 빌드여도 사용자 선택을 존중해야 한다.
        var result = AITBuildInitializer.ResolveIl2CppConfiguration(
            editorConfigValue: (int)Il2CppCompilerConfiguration.Master, isDevServerBuild: true);

        Assert.AreEqual(Il2CppCompilerConfiguration.Master, result);
    }

    [Test]
    public void Resolve_AutoValue_DevServerBuild_ReturnsDebug()
    {
        var result = AITBuildInitializer.ResolveIl2CppConfiguration(editorConfigValue: -1, isDevServerBuild: true);

        Assert.AreEqual(Il2CppCompilerConfiguration.Debug, result);
    }

    [Test]
    public void Resolve_AutoValue_NotDevServerBuild_ReturnsDefault()
    {
        var result = AITBuildInitializer.ResolveIl2CppConfiguration(editorConfigValue: -1, isDevServerBuild: false);

        Assert.AreEqual(AITDefaultSettings.GetDefaultIl2CppConfiguration(), result);
        // 회귀 방지: Production/Deploy/Build & Package 경로는 절대 Debug가 되면 안 됨
        Assert.AreNotEqual(Il2CppCompilerConfiguration.Debug, result);
    }

    [Test]
    public void Resolve_ExplicitReleaseValue_NotDevServerBuild_ReturnsRelease()
    {
        var result = AITBuildInitializer.ResolveIl2CppConfiguration(
            editorConfigValue: (int)Il2CppCompilerConfiguration.Release, isDevServerBuild: false);

        Assert.AreEqual(Il2CppCompilerConfiguration.Release, result);
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
    public void Init_DevServerBuild_ConfigAuto_AppliesDebug()
    {
        UnityUtil.GetEditorConf().il2cppConfiguration = -1;

        AITBuildInitializer.Init(AITBuildProfile.CreateProductionProfile(), isDevServerBuild: true);

        Assert.AreEqual(Il2CppCompilerConfiguration.Debug, GetAppliedIl2CppConfiguration());
    }

    [Test]
    public void Init_NotDevServerBuild_ConfigAuto_AppliesDefault_NotDebug()
    {
        UnityUtil.GetEditorConf().il2cppConfiguration = -1;

        AITBuildInitializer.Init(AITBuildProfile.CreateProductionProfile(), isDevServerBuild: false);

        Assert.AreEqual(AITDefaultSettings.GetDefaultIl2CppConfiguration(), GetAppliedIl2CppConfiguration());
        Assert.AreNotEqual(Il2CppCompilerConfiguration.Debug, GetAppliedIl2CppConfiguration(),
            "Production/Deploy/Build & Package 경로는 Dev Server 플래그 없이 Debug가 적용되면 안 됨");
    }

    [Test]
    public void Init_DevServerBuild_ConfigExplicitMaster_RespectsUserChoice()
    {
        // 사용자의 명시적 선택은 Dev Server 빌드에서도 그대로 존중되어야 한다.
        UnityUtil.GetEditorConf().il2cppConfiguration = (int)Il2CppCompilerConfiguration.Master;

        AITBuildInitializer.Init(AITBuildProfile.CreateProductionProfile(), isDevServerBuild: true);

        Assert.AreEqual(Il2CppCompilerConfiguration.Master, GetAppliedIl2CppConfiguration());
    }

    [Test]
    public void Init_DefaultIsDevServerBuildFalse_BehavesLikeExistingCallers()
    {
        // isDevServerBuild 인자를 생략한 기존 호출부와 동일 동작 (기본값 false)
        UnityUtil.GetEditorConf().il2cppConfiguration = -1;

        AITBuildInitializer.Init(AITBuildProfile.CreateProductionProfile());

        Assert.AreEqual(AITDefaultSettings.GetDefaultIl2CppConfiguration(), GetAppliedIl2CppConfiguration());
    }
}
