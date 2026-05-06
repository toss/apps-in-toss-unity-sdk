// -----------------------------------------------------------------------
// WebGLTemplateMismatchWarningTests.cs
// Sentry APPS-IN-TOSS-UNITY-SDK-R9 회귀 가드.
//
// `webglPath/Runtime` 폴더가 없을 때 WebGLBuildCopier가 출력하는 두 줄의
// 진단 경고는 "사용자가 AITTemplate 외 템플릿으로 빌드한 경우"의 폴백 경로
// 알림이지 SDK 결함이 아니다. 따라서 콘솔에는 남기되 Sentry로는 캡처되지
// 않아야 한다 — 즉 `Debug.LogWarning`이 아니라 `AITLog.Warning(..., sentryCapture: false)`
// 호출이어야 한다.
//
// 이 테스트는 `WebGLBuildCopier.cs` 소스를 읽어 그 두 줄이 회귀하지 않도록
// 텍스트 수준에서 가드한다 — `CopyWebGLToPublic`을 통째로 호출하면
// AITPackagePathResolver, AITTemplateManager, BuildMarker 등 의존 그래프가
// 너무 커서 EditMode 단위 테스트로 격리하기 어렵다.
//
// 제약: 검증은 라인 단위로 수행하므로 두 경고 호출은 **단일 라인**에
// `AITLog.Warning(..., sentryCapture: false)` 형태를 유지해야 한다.
// named argument를 다음 줄로 줄바꿈하면 false negative가 발생한다.
// -----------------------------------------------------------------------

using System.IO;
using NUnit.Framework;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
[Category("Unit")]
public class WebGLTemplateMismatchWarningTests
{
    private const string WarningMessage1Substring = "WebGL 빌드에 Runtime 폴더가 없습니다";
    private const string WarningMessage2Substring = "AITTemplate이 아닌 다른 템플릿으로 빌드되었을 수 있습니다";

    [Test]
    public void TemplateMismatchWarnings_AreSuppressedFromSentry()
    {
        string source = ReadCopierSource();

        AssertWarningSentryCaptureFalse(source, WarningMessage1Substring);
        AssertWarningSentryCaptureFalse(source, WarningMessage2Substring);
    }

    [Test]
    public void TemplateMismatchWarnings_DoNotUseRawDebugLogWarning()
    {
        // Debug.LogWarning은 ErrorTracker 로그 핸들러를 그대로 통과해 Sentry로
        // 캡처된다. 이 두 메시지에 대해서는 절대 Debug.LogWarning을 직접 호출하면 안 된다.
        string source = ReadCopierSource();

        Assert.IsFalse(
            ContainsLineWith(source, "Debug.LogWarning", WarningMessage1Substring),
            $"Debug.LogWarning(...\"{WarningMessage1Substring}...\") 직접 호출 금지 — Sentry 노이즈 발생.");
        Assert.IsFalse(
            ContainsLineWith(source, "Debug.LogWarning", WarningMessage2Substring),
            $"Debug.LogWarning(...\"{WarningMessage2Substring}...\") 직접 호출 금지 — Sentry 노이즈 발생.");
    }

    private static string ReadCopierSource()
    {
        Assert.IsTrue(
            AITPackagePathResolver.TryResolveFile(
                "Editor/Package/WebGLBuildCopier.cs",
                out string path,
                typeof(AITConvertCore)),
            "WebGLBuildCopier.cs 소스 경로를 찾지 못했습니다.");
        Assert.IsTrue(File.Exists(path), $"파일이 존재하지 않습니다: {path}");
        return File.ReadAllText(path);
    }

    private static bool ContainsLineWith(string source, string apiToken, string messageSubstring)
    {
        foreach (string line in source.Split('\n'))
        {
            if (line.Contains(apiToken) && line.Contains(messageSubstring))
                return true;
        }
        return false;
    }

    private static void AssertWarningSentryCaptureFalse(string source, string messageSubstring)
    {
        bool foundCorrect = false;
        foreach (string line in source.Split('\n'))
        {
            if (!line.Contains(messageSubstring)) continue;

            // 메시지를 담는 호출 라인은 AITLog.Warning이고 sentryCapture: false 여야 한다.
            if (line.Contains("AITLog.Warning") && line.Contains("sentryCapture: false"))
            {
                foundCorrect = true;
                break;
            }
        }

        Assert.IsTrue(
            foundCorrect,
            $"\"{messageSubstring}\" 경고는 AITLog.Warning(..., sentryCapture: false) 로 호출되어야 합니다. " +
            "Debug.LogWarning이나 sentryCapture: true 로의 회귀가 발생하면 Sentry APPS-IN-TOSS-UNITY-SDK-R9 노이즈가 다시 잡힙니다.");
    }
}
