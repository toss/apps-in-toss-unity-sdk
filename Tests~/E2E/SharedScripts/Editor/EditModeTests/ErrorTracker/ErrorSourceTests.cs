// ---------------------------------------------------------------------------
// ErrorSourceTests.cs - DetermineErrorSource 단위 테스트
// 스택트레이스/메시지 분석을 통한 에러 출처 분류 로직을 검증합니다.
// ---------------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor.ErrorTracker;

[TestFixture]
[Category("Unit")]
public class ErrorSourceTests
{
    #region SDK Path Tests

    [Test]
    public void SdkPath_InPackages_ReturnsSdk()
    {
        string stackTrace =
            "at AppsInToss.Editor.AITConvertCore.DoExport () in Packages/im.toss.apps-in-toss-unity-sdk/Editor/AITConvertCore.cs:42";

        Assert.AreEqual("sdk", AITEditorErrorTracker.DetermineErrorSource(stackTrace, "some error"));
    }

    [Test]
    public void SdkPath_InPackageCacheWithVersion_ReturnsSdk()
    {
        string stackTrace =
            "at AppsInToss.Editor.AITConvertCore.DoExport () in Library/PackageCache/im.toss.apps-in-toss-unity-sdk@2.4.1/Editor/AITConvertCore.cs:42";

        Assert.AreEqual("sdk", AITEditorErrorTracker.DetermineErrorSource(stackTrace, "some error"));
    }

    [Test]
    public void SdkPath_InPackageCacheWithoutVersion_ReturnsSdk()
    {
        // Unity 6+ may use PackageCache without @version suffix
        string stackTrace =
            "at AppsInToss.Editor.AITConvertCore.DoExport () in Library/PackageCache/im.toss.apps-in-toss-unity-sdk/Editor/AITConvertCore.cs:42";

        Assert.AreEqual("sdk", AITEditorErrorTracker.DetermineErrorSource(stackTrace, "some error"));
    }

    #endregion

    #region User Project Path Tests

    [Test]
    public void UserPath_InAssets_ReturnsUserProject()
    {
        string stackTrace =
            "at MyGame.PlayerController.Start () in Assets/Scripts/PlayerController.cs:15";

        Assert.AreEqual("user_project", AITEditorErrorTracker.DetermineErrorSource(stackTrace, "some error"));
    }

    [Test]
    public void UserPath_InAssetsPlugins_ReturnsUserProject()
    {
        string stackTrace =
            "at ThirdParty.Plugin.Init () in Assets/Plugins/ThirdParty/Plugin.cs:10";

        Assert.AreEqual("user_project", AITEditorErrorTracker.DetermineErrorSource(stackTrace, "some error"));
    }

    #endregion

    #region Mixed Frame Tests

    [Test]
    public void MixedFrames_TopmostIsSdk_ReturnsSdk()
    {
        // User code calls SDK, SDK throws — topmost frame is SDK
        string stackTrace =
            "at AppsInToss.SDK.AIT.GetDeviceId () in Packages/im.toss.apps-in-toss-unity-sdk/Runtime/SDK/AIT.SystemInfo.cs:20\n" +
            "at MyGame.GameManager.Init () in Assets/Scripts/GameManager.cs:50";

        Assert.AreEqual("sdk", AITEditorErrorTracker.DetermineErrorSource(stackTrace, ""));
    }

    [Test]
    public void MixedFrames_TopmostIsUser_ReturnsUserProject()
    {
        // SDK calls user callback, user code throws — topmost frame is user
        string stackTrace =
            "at MyGame.Callback.OnResult () in Assets/Scripts/Callback.cs:30\n" +
            "at AppsInToss.SDK.AIT.InvokeCallback () in Packages/im.toss.apps-in-toss-unity-sdk/Runtime/SDK/AITCore.cs:100";

        Assert.AreEqual("user_project", AITEditorErrorTracker.DetermineErrorSource(stackTrace, ""));
    }

    #endregion

    #region Fallback Tests

    [Test]
    public void NoStackTrace_AitPrefixMessage_ReturnsSdk()
    {
        Assert.AreEqual("sdk", AITEditorErrorTracker.DetermineErrorSource(null, "[AIT] Build failed"));
    }

    [Test]
    public void NoStackTrace_NonAitMessage_ReturnsUnknown()
    {
        Assert.AreEqual("unknown", AITEditorErrorTracker.DetermineErrorSource(null, "Something went wrong"));
    }

    [Test]
    public void EmptyStackTrace_AitPrefixMessage_ReturnsSdk()
    {
        Assert.AreEqual("sdk", AITEditorErrorTracker.DetermineErrorSource("", "[AIT] Error occurred"));
    }

    [Test]
    public void EmptyStackTrace_NonAitMessage_ReturnsUnknown()
    {
        Assert.AreEqual("unknown", AITEditorErrorTracker.DetermineErrorSource("", "generic error"));
    }

    #endregion

    #region Null/Empty Input Tests

    [Test]
    public void NullInputs_ReturnsUnknown()
    {
        Assert.AreEqual("unknown", AITEditorErrorTracker.DetermineErrorSource(null, null));
    }

    [Test]
    public void EmptyInputs_ReturnsUnknown()
    {
        Assert.AreEqual("unknown", AITEditorErrorTracker.DetermineErrorSource("", ""));
    }

    #endregion

    #region Unparseable Stack Trace Tests

    [Test]
    public void UnparseableStackTrace_NoAitMessage_ReturnsUnknown()
    {
        // Stack trace with no "at" lines — no frames parsed
        string stackTrace = "some random log output\nwithout stack frames";

        Assert.AreEqual("unknown", AITEditorErrorTracker.DetermineErrorSource(stackTrace, "error"));
    }

    [Test]
    public void FramesWithoutFilename_FallsBackToMessage()
    {
        // Frames without file paths (e.g., native or stripped)
        string stackTrace = "at System.String.Format (System.String format)";

        Assert.AreEqual("sdk", AITEditorErrorTracker.DetermineErrorSource(stackTrace, "[AIT] format error"));
    }

    #endregion

    #region SDK Message Pattern Classification

    [Test]
    public void Message_AppsInTossKeyword_ReturnsSdk()
    {
        Assert.AreEqual("sdk", AITEditorErrorTracker.DetermineErrorSource(null, "AppsInToss something failed"));
    }

    [Test]
    public void Message_AppsInTossPackageId_ReturnsSdk()
    {
        Assert.AreEqual("sdk", AITEditorErrorTracker.DetermineErrorSource(null, "Error in apps-in-toss build"));
    }

    [Test]
    public void Message_ValidationTag_ReturnsSdk()
    {
        Assert.AreEqual("sdk", AITEditorErrorTracker.DetermineErrorSource(null, "[Validation] missing icon URL"));
    }

    [Test]
    public void Message_SentryPrefix_ReturnsSdk()
    {
        Assert.AreEqual("sdk", AITEditorErrorTracker.DetermineErrorSource(null, "Sentry: Failed to send envelope"));
    }

    [Test]
    public void Message_PnpmTag_ReturnsSdk()
    {
        Assert.AreEqual("sdk", AITEditorErrorTracker.DetermineErrorSource(null, "[pnpm] install failed"));
    }

    [Test]
    public void Message_WebglBuildPath_ReturnsSdk()
    {
        Assert.AreEqual("sdk", AITEditorErrorTracker.DetermineErrorSource(null, "Brotli webgl/Build/main.unityweb crashed"));
    }

    #endregion
}

#region IsKnownNonSdkMessage Tests

[TestFixture]
[Category("Unit")]
public class IsKnownNonSdkMessageTests
{
    #region Null/Empty Tests

    [Test]
    public void NullMessage_ReturnsFalse()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(null));
    }

    [Test]
    public void EmptyMessage_ReturnsFalse()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(""));
    }

    #endregion

    #region SDK 보호: AIT 키워드 포함 시 절대 필터링 안 함

    [Test]
    public void AitPrefix_NeverFiltered()
    {
        // [AIT] 접두사가 있는 메시지는 다른 패턴이 매칭되어도 필터링되면 안 됨
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage("[AIT] GfxDevice renderer is null"));
    }

    [Test]
    public void AitWarnPrefix_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage("[AITWarn] some warning"));
    }

    [Test]
    public void AppsInTossKeyword_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage("AppsInToss build error matches more than one built-in atlases"));
    }

    [Test]
    public void AppsInTossPackageId_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage("apps-in-toss config exceeds previous array size"));
    }

    #endregion

    #region Unity 내부 경고 패턴

    [Test]
    public void GfxDeviceRendererNull_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage("GfxDevice renderer is null"));
    }

    [Test]
    public void IgnoringLocale_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage("Ignoring locale en-US because it is not supported"));
    }

    [Test]
    public void UnableToLoadBuildReport_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage("Unable to load build report at Library/LastBuild.buildreport"));
    }

    [Test]
    public void CannotReadBuildLayout_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage("Cannot read BuildLayout header"));
    }

    [Test]
    public void ServicesCoreTag_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage("[ServicesCore] Initialization error"));
    }

    [Test]
    public void ProfileValueReferenceEmpty_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage("ProfileValueReference: GetValue called with empty id"));
    }

    [Test]
    public void Editor32BitPlugins_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage("The Editor does not support 32-bit plugins"));
    }

    #endregion

    #region 사용자 프로젝트 에셋 문제

    [Test]
    public void SpriteAtlasDuplicate_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Sprite Foo matches more than one built-in atlases"));
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

    #endregion

    #region Unity 패키지 내부

    [Test]
    public void LocalizationStringTables_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Localization-String-Tables-Shared something"));
    }

    [Test]
    public void GraphInUnityPackage_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Warning in Graph at Packages/com.unity.visualscripting/foo"));
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
    public void FieldsSerializedInGeneric_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Fields serialized in MyClass type"));
    }

    #endregion

    #region 외부 패키지

    [Test]
    public void MetaButFolderMissing_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Foo.meta) exists but its folder doesn't"));
    }

    #endregion

    #region Unity URP 내부

    [Test]
    public void ExceedsPreviousArraySize_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Index 5 exceeds previous array size 3"));
    }

    #endregion

    #region SDK 관련 메시지는 통과 (negative cases)

    [Test]
    public void GenericSdkErrorWithoutPattern_ReturnsFalse()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage("Random SDK error"));
    }

    [Test]
    public void EmptyAitNothingMatches_ReturnsFalse()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage("Some unrelated message"));
    }

    #endregion
}

#endregion
