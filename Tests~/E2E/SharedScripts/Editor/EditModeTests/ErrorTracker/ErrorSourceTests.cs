// ---------------------------------------------------------------------------
// ErrorSourceTests.cs - DetermineErrorSource 단위 테스트
// 스택트레이스/메시지 분석을 통한 에러 출처 분류 로직을 검증합니다.
// ---------------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
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

    #region Non-SDK 메시지 분류 회귀 테스트

    [Test]
    public void Message_SpriteAtlasWarning_NoStack_ReturnsUnknown()
    {
        // Sprite atlas 중복 경고는 Unity 내부 메시지 — SDK로 분류되면 안 됨.
        // Sentry 관찰 이벤트에서도 error_source: unknown으로 태그되어 있음 (APPS-IN-TOSS-UNITY-SDK-DW).
        Assert.AreEqual("unknown", AITEditorErrorTracker.DetermineErrorSource(
            null,
            "Sprite object_19_01 matches more than one built-in atlases. Default to use the first available atlas."));
    }

    [Test]
    public void Message_SpriteAtlasWarning_NoAitInStack_ReturnsUnknown()
    {
        // 스택이 있더라도 AIT 경로가 없으면 SDK가 아님
        string stackTrace = "at UnityEngine.Sprite.GetBuiltinAtlas () [0x00000]";
        Assert.AreEqual("unknown", AITEditorErrorTracker.DetermineErrorSource(
            stackTrace,
            "Sprite cloud matches more than one built-in atlases. Default to use the first available atlas."));
    }

    #endregion

    #region AITLog sentryCapture=false 회귀 테스트

    [Test]
    public void AITLog_SentryCaptureFalse_SuppressesEnvelope()
    {
        LogAssert.Expect(LogType.Error, "[AIT] TEST: sentryCapture=false 경로 검증");

        AITEditorErrorTracker.BeginSuppressLogCapture();
        try
        {
            Debug.LogError("[AIT] TEST: sentryCapture=false 경로 검증");
        }
        finally
        {
            AITEditorErrorTracker.EndSuppressLogCapture();
        }

        Assert.Pass("Suppress scope 정상 종료");
    }

    [Test]
    public void AITLog_Error_WithSentryCaptureFalse_DoesNotThrow()
    {
        LogAssert.Expect(LogType.Error, "[AIT] TEST: sentryCapture=false 스모크");

        Assert.DoesNotThrow(() =>
        {
            AppsInToss.Editor.AITLog.Error(
                "[AIT] TEST: sentryCapture=false 스모크",
                sentryCapture: false);
        });
    }

    #endregion
}
