// IsKnownNonSdkMessageTests.UserProject.cs - 사용자 에셋·직렬화·외부 패키지·템플릿
using NUnit.Framework;
using AppsInToss.Editor.ErrorTracker;

public partial class IsKnownNonSdkMessageTests
{
    #region 사용자 프로젝트 에셋 문제

    [Test]
    public void SpriteAtlasDuplicate_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Sprite Foo matches more than one built-in atlases"));
    }

    [Test]
    public void SpriteAtlasDuplicate_FullSentryMessage_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-DW에서 실제 관찰된 원문 fixture.
        // 현재 필터는 IndexOf 부분 매칭이라 후행 문구 유무는 결과에 영향 없지만,
        // 실제 입력 예시를 테스트로 박아두면 향후 필터가 anchored/regex로 바뀔 때
        // 이 실측 메시지를 놓치지 않도록 방어한다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Sprite object_19_01 matches more than one built-in atlases. Default to use the first available atlas."));
    }

    // 필터가 향후 anchored/regex로 tightening되어도 실제 Unity 출력 변형을 놓치지 않도록
    // 구조적으로 서로 다른 리스크 프로파일의 변형만 선별해 검증 (단순 이름 swap은 제외).
    [TestCase(
        "Sprite object_04_02 matches more than one built-in atlases. Default to use the first available atlas.",
        TestName = "SpriteAtlas_StandardLayout_ReturnsTrue")]
    [TestCase(
        "[Warn]  Sprite cloud matches more than one built-in atlases.",
        TestName = "SpriteAtlas_WithPrefixAndWhitespace_ReturnsTrue")]
    [TestCase(
        "Sprite 'player idle frame' matches more than one built-in atlases (fallback applied).",
        TestName = "SpriteAtlas_QuotedNameAndTrailingParen_ReturnsTrue")]
    // Sentry APPS-IN-TOSS-UNITY-SDK-RM / RN — "UnityWarning: " prefix가 붙은 실측 변형.
    [TestCase(
        "UnityWarning: Sprite object_03_07 matches more than one built-in atlases. Default to use the first available atlas.",
        TestName = "SpriteAtlas_UnityWarningPrefix_ObjectName_ReturnsTrue")]
    [TestCase(
        "UnityWarning: Sprite background_theme1_3 matches more than one built-in atlases. Default to use the first available atlas.",
        TestName = "SpriteAtlas_UnityWarningPrefix_UnderscoredName_ReturnsTrue")]
    public void SpriteAtlasDuplicate_RealisticVariants_ReturnsTrue(string message)
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(message));
    }

    [Test]
    public void ScriptMissingInUserAssets_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Script attached to 'GameObject' in Assets/Scenes/Main.unity is missing or no valid script"));
    }

    [Test]
    public void ScriptMissingWithoutAssets_ReturnsFalse()
    {
        // "Script attached to" + "is missing"이지만 Assets/ 경로가 없으면 필터링 안 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Script attached to 'GameObject' is missing or no valid script"));
    }

    [TestCase(
        "Script attached to '_iOSUpdate' in scene 'Assets/Scenes/Main.unity' is missing or no valid script is attached.",
        TestName = "ScriptMissingInUserScene_iOSUpdate_ReturnsTrue")]
    [TestCase(
        "Script attached to 'Image_Popup' in scene 'Assets/Scenes/Main.unity' is missing or no valid script is attached.",
        TestName = "ScriptMissingInUserScene_ImagePopup_ReturnsTrue")]
    [TestCase(
        "Script attached to '_AdManager' in scene 'Assets/Scenes/Main.unity' is missing or no valid script is attached.",
        TestName = "ScriptMissingInUserScene_AdManager_ReturnsTrue")]
    public void ScriptMissingInUserScene_RealisticVariants_ReturnsTrue(string message)
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-H0/GX/GW — 사용자 씬의 missing script 경고.
        // "in scene 'Assets/...'" 형태도 기존 composite 가드(Script attached to + is missing + Assets/)가 매칭한다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(message));
    }

    [Test]
    public void ScriptMissingInUserScene_WithAitPrefix_NeverFiltered()
    {
        // composite missing-script 가드의 세 조건(Script attached to + is missing + Assets/)을 모두 만족하지만,
        // "[AIT" 키워드가 MessageContainsSdkKeyword 가드에 먼저 매칭되어 composite 가드 도달 전에 return false 한다.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Script attached to '_iOSUpdate' in scene 'Assets/Scenes/Main.unity' is missing or no valid script is attached."));
    }

    [Test]
    public void ScriptMissingInUserPrefab_LasercahrgingFixture_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-H1 실측 메시지.
        // "Script attached to" + "is missing" + "Assets/" composite 조건으로 매칭됨.
        // 필터가 향후 anchored/regex로 tightening되어도 이 실측 메시지를 놓치지 않도록 fixture로 박아둠.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Script attached to '_NotUse_Effetc_Lasercharging' in asset 'Assets/Resources/Effect/_NotUse_Effetc_Lasercharging.prefab' is missing or no valid script is attached."));
    }

    [Test]
    public void ScriptMissingInUserPrefab_HitMissileFixture_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-GY 실측 메시지.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Script attached to '_NotUse_HitMissile_P10' in asset 'Assets/Resources/Effect/_NotUse_HitMissile_P10.prefab' is missing or no valid script is attached."));
    }

    [Test]
    public void ScriptMissingInUserPrefab_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙으면 SDK 자체 로그로 간주되어 필터링되지 않아야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Script attached to 'Foo' in asset 'Assets/Resources/Foo.prefab' is missing or no valid script is attached."));
    }

    [Test]
    public void AudioClipImportWarning_ReturnsTrue()
    {
        // Sentry GT/GV — 사용자 프로젝트 에셋 import 경고
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Warnings during import of AudioClip Assets/Sounds/bgm.wav"));
    }

    [Test]
    public void AudioClipImportWarning_WithAitPrefix_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Warnings during import of AudioClip Assets/Sounds/bgm.wav"));
    }

    [Test]
    public void FmodSoundCreateError_ReturnsTrue()
    {
        // Sentry NM/NK/N9/CJ 등 — 사용자 프로젝트의 FMOD 오디오 에셋 문제
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Error: Cannot create FMOD::Sound instance for clip \"button_tick\" (FMOD error: Couldn't perform seek operation. ...)"));
    }

    [Test]
    public void FmodSoundCreateError_WithAitPrefix_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Cannot create FMOD::Sound instance for clip \"foo\""));
    }

    [Test]
    public void FmodFsbLoadStateError_ReturnsTrue()
    {
        // Sentry CX/N5/N7/N8 — FSB 로드 실패
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Failed getting load state of FSB for audio clip \"bensound-ukulele\""));
    }

    [Test]
    public void AudioClipLoadError_ReturnsTrue()
    {
        // Sentry P2/P3 — 오디오 데이터 로드 실패
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Error: Cannot load audio data for audio clip \"button_tick\""));
    }

    [Test]
    public void AnimatorTransitionMissingExitTime_ReturnsTrue()
    {
        // Sentry NY — 사용자 Animator 컨트롤러 설정 누락
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Asset 'Machine': Transition 'Spin -> Exit' in state 'Spin' doesn't have an Exit Time or any condition, transition will be ignored"));
    }

    #endregion

    #region 사용자 프로젝트 직렬화

    [Test]
    public void AssemblyCSharpSerialization_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Fields serialized in [Assembly-CSharp] MyClass can't be serialized"));
    }

    [Test]
    public void FieldsSerializedInSdkAssembly_NeverFiltered()
    {
        // SDK 어셈블리(AppsInTossSDKEditor 등)의 직렬화 경고는 보호되어야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Fields serialized in [AppsInTossSDKEditor] AITConfig changed"));
    }

    [Test]
    public void AssemblyCSharpTypeMismatch_ReturnsTrue()
    {
        // Sentry NG/NF — 사용자 코드 타입의 player/editor 직렬화 mismatch
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Type '[Assembly-CSharp]AdSwitcher' has an extra field 'adGoogleAdPlacementManager' of type 'AdGoogleAdPlacementManager' in the player and thus can't be serialized"));
    }

    [Test]
    public void SdkAssemblyTypeMismatch_NeverFiltered()
    {
        // SDK 어셈블리의 동일 패턴은 필터링되지 않아야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Type '[AppsInTossSDKEditor]AITConfig' has an extra field 'foo'"));
    }

    [Test]
    public void FailedToCompilePlayerScripts_ReturnsTrue()
    {
        // Sentry H3 — 사용자 게임 코드 컴파일 실패 (스택 없이 메시지만 도착)
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Failed to compile player scripts"));
    }

    [Test]
    public void FailedToCompilePlayerScripts_WithAitPrefix_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Failed to compile player scripts"));
    }

    [Test]
    public void ExecGitTrace_ShowHead_ReturnsTrue()
    {
        // Sentry NX — Unity Editor가 외부 git 명령 실행 시 stdout으로 출력하는 trace.
        // SDK 코드 어디에도 "Exec>" 문자열이 없음을 grep으로 확인하여 안전하게 노이즈로 분류.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Exec> git show -s --pretty=%D HEAD"));
    }

    [Test]
    public void ExecGitTrace_LogShortSha_ReturnsTrue()
    {
        // Sentry NW
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Exec> git log -1 --pretty=format:%h"));
    }

    [Test]
    public void ExecGitTrace_WithAitPrefix_NeverFiltered()
    {
        // SDK 보호 가드 회귀 방지: AIT prefix가 붙은 동일 메시지는 SDK 자체 로그로 간주
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Exec> git show -s --pretty=%D HEAD"));
    }

    [Test]
    public void ExecWithoutGit_ReturnsFalse()
    {
        // "Exec> " 단독은 너무 일반적이라 매칭 안 함 — git 호출 trace에 한정
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Exec> npm install"));
    }

    #endregion

    #region 외부 패키지

    [Test]
    public void MetaButFolderMissing_ReturnsTrue()
    {
        // Unity가 괄호를 붙이는 버전
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Foo.meta) exists but its folder doesn't"));
    }

    [Test]
    public void MetaButFolderMissing_WithoutParen_ReturnsTrue()
    {
        // Unity가 괄호 없이 출력하는 버전 — 패턴은 두 경우 모두 매칭
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Foo.meta exists but its folder doesn't"));
    }

    [Test]
    public void MetaMissingInImmutableFolder_AppleSignin_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-10E — 외부 UPM 패키지(apple-signin-unity)의 immutable 폴더에
        // .meta 파일이 없을 때 Unity 에디터가 직접 출력하는 표준 경고. 사용자가 조치 불가한 Unity 자체 노이즈.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Asset 'Packages/com.lupidan.apple-signin-unity/AppleAuthSampleProject/ProjectSettings/ProjectSettings.asset' has no meta file, but it's in an immutable folder. The asset will be ignored."));
    }

    [Test]
    public void MetaMissingInImmutableFolder_AppleSigninAltPath_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-10D — 동일 패키지의 또 다른 immutable 경로 변형도
        // 단일 "has no meta file, but it's in an immutable folder" 부분 문자열로 모두 매칭됨을 검증.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Asset 'Packages/com.lupidan.apple-signin-unity/AppleAuthSampleProject/.editorconfig' has no meta file, but it's in an immutable folder. The asset will be ignored."));
    }

    [Test]
    public void MetaMissingInImmutableFolder_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙은 동일 본문은 SDK 자체 로그로 간주되어 필터링되지 않아야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Asset 'Packages/foo' has no meta file, but it's in an immutable folder."));
    }

    [Test]
    public void MetaButAssetMissing_ExternalPackagePath_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-ZQ — .meta는 있으나 외부 패키지(com.wooshii.foldericons)의
        // 대응 에셋을 못 찾을 때 Unity가 출력하는 표준 경고. asset 경로가 포함된 변형.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "A meta data file (.meta) exists but its asset 'Packages/com.wooshii.foldericons/Editor/Icons/folder.png' can't be found. When the asset is reimported, its data will be lost."));
    }

    [Test]
    public void MetaButAssetMissing_GenericForm_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-ZS — asset 경로 없이 핵심 문구만 출력되는 변형.
        // 동일한 "exists but its asset" 패턴이 두 변형을 모두 매칭함을 검증.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "A meta data file (.meta) exists but its asset can't be found. When the asset is reimported, its data will be lost."));
    }

    [Test]
    public void MetaButAssetMissing_WithAitPrefix_NeverFiltered()
    {
        // SDK 보호 가드 회귀 방지: [AIT] prefix가 붙은 동일 본문은 SDK 자체 로그로 간주되어 필터링되지 않아야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] A meta data file (.meta) exists but its asset can't be found."));
    }

    #endregion

    #region 사용자 게임 코드 IAP 진단 (SDK-CY)

    [Test]
    public void TossIap_InitializeFailed_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-CY — 사용자 게임 코드(외부 IAP 모듈) 진단.
        // SDK 코드에 'Toss IAP' 문자열은 없음.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Toss IAP: Initialize failed or no products"));
    }

    #endregion

    #region 외부 WebGL 템플릿 (SDK-VJ)

    [Test]
    public void ExternalWebGLTemplate_UnityWebviewSourceNotFound_ReturnsTrue()
    {
        // Sentry SDK-VJ — 사용자 프로젝트가 사용하는 다른 WebGL 템플릿(Fill)이 출력한 진단.
        // SDK는 "[WebGL]" prefix를 사용하지 않음.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[WebGL] unity-webview.js source not found: " +
            "/Users/ad03148361/jenkins_workspace/attack-web/Assets/WebGLTemplates/Fill/TemplateData/unity-webview.js"));
    }

    [Test]
    public void ExternalWebGLTemplate_UnityWarningPrefix_ReturnsTrue()
    {
        // Sentry SDK-VJ — "UnityWarning: " prefix가 붙은 실측 변형.
        // 매칭은 부분 문자열(IndexOf) 기반이므로 기존 "[WebGL] unity-webview.js source not found"
        // 패턴이 prefix 유무와 무관하게 그대로 매칭한다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityWarning: [WebGL] unity-webview.js source not found: " +
            "/Users/ad03148361/jenkins_workspace/attack-web/Assets/WebGLTemplates/Fill/TemplateData/unity-webview.js"));
    }

    [Test]
    public void ExternalWebGLTemplate_UnityWebviewSourceNotFound_WithAitPrefix_NeverFiltered()
    {
        // SDK 자체 로그("[AIT" prefix)는 AitKeywords 가드로 보호되어 절대 필터링하지 않음.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] [WebGL] unity-webview.js source not found: " +
            "/Users/foo/Assets/WebGLTemplates/AITTemplate/TemplateData/unity-webview.js"));
    }

    [Test]
    public void WebGL_OtherWarning_NotFiltered()
    {
        // 다른 "[WebGL]" prefix 메시지는 패턴이 좁혀 있어 통과 (unity-webview source 한정).
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[WebGL] unity-webview.js compiled successfully"));
    }

    #endregion

    #region 고아 .meta 에셋 / CS1998 / 외부 IAP wrapper (SDK-ZS, ZQ, Z6, ZV)

    [Test]
    public void OrphanMetaAsset_AssetsPath_ReturnsTrue()
    {
        // Sentry SDK-ZS — 사용자가 Unity 외부에서 .cs를 삭제해 .meta만 고아로 남음.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "A meta data file (.meta) exists but its asset 'Assets/02_Scripts/00_Common/AppsInTossProductSkus.cs' can't be found. When moving or deleting files outside of Unity, please ensure that the corresponding .meta file is moved or deleted along with it."));
    }

    [Test]
    public void OrphanMetaAsset_PackagesPath_ReturnsTrue()
    {
        // Sentry SDK-ZQ — 외부 패키지(com.wooshii.foldericons)의 고아 .meta.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "A meta data file (.meta) exists but its asset 'Packages/com.wooshii.foldericons/package-lock.json' can't be found. When moving or deleting files outside of Unity, please ensure that the corresponding .meta file is moved or deleted along with it."));
    }

    [Test]
    public void OrphanMetaAsset_WithAitPrefix_NeverFiltered()
    {
        // SDK 자체 로그는 보호 — "[AIT]" prefix가 붙으면 SDK 키워드 가드로 통과시킨다(array 도달 전 차단).
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] 에셋 검증 중: exists but its asset placeholder"));
    }

    [Test]
    public void UserCodeCs1998_AppsInTossFolder_ReturnsTrue()
    {
        // Sentry SDK-Z6 — 사용자 폴더명에 AppsInToss가 포함된 .cs의 async/await 경고.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets\\Scripts\\1. System\\AppsInToss\\TossManager.cs(260,43): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread."));
    }

    [Test]
    public void Cs1998_SdkPackagesPath_NeverFiltered()
    {
        // SDK 자체 코드(Packages/ 경로)의 CS1998은 Assets/ 가드 미충족 + SDK 키워드 가드로 보호되어 필터링 안 됨.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Packages/im.toss.apps-in-toss-unity-sdk/Runtime/Foo.cs(10,5): warning CS1998: This async method lacks 'await' operators and will run synchronously."));
    }

    [Test]
    public void ExternalIapManagerPrefix_ReturnsTrue()
    {
        // Sentry SDK-ZV — 사용자/샘플 IAP wrapper 클래스 로그. SDK는 "[AppsInTossIAPManager]" prefix를 출력하지 않음.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AppsInTossIAPManager] IAPGetPendingOrders: null (앱 버전 미지원 등)"));
    }

    [Test]
    public void SdkIapLog_WithAitPrefix_NeverFiltered()
    {
        // SDK 자체 IAP 로그("[AIT]" prefix)는 보호 — 새 ExternalAitPrefix가 너무 넓지 않음을 검증.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] IAPGetPendingOrders 호출: 보류 주문 0건"));
    }

    #endregion
}
