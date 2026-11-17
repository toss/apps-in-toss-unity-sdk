// -----------------------------------------------------------------------
// WebGLTemplateMismatchWarningTests.cs
// Sentry APPS-IN-TOSS-UNITY-SDK-R9 / R8 회귀 가드.
//
// `webglPath/Runtime` 폴더가 없을 때 WebGLBuildCopier가 출력하는 진단
// 메시지는 "사용자가 AITTemplate 외 템플릿으로 빌드한 경우"의 폴백 경로
// 알림이지 SDK 결함이 아니다. 따라서 콘솔에는 남기되 Sentry로는 캡처되지
// 않아야 한다.
//
// 허용 형태:
//   - `Debug.Log(...)` (LogWarning이 아니므로 ErrorTracker가 Sentry로 보내지 않음)
//   - `AITLog.Warning(..., sentryCapture: false)` (명시적 노이즈 차단)
//
// 금지 형태:
//   - `Debug.LogWarning(...)` 직접 호출 — Sentry로 캡처되어 노이즈 발생
//   - `AITLog.Warning(..., sentryCapture: true)` 또는 sentryCapture 기본값 호출
//
// 이 테스트는 `WebGLBuildCopier.cs` 소스를 읽어 회귀를 텍스트 수준에서
// 가드한다 — `CopyWebGLToPublic`을 통째로 호출하면 AITPackagePathResolver,
// AITTemplateManager, BuildMarker 등 의존 그래프가 너무 커서 EditMode
// 단위 테스트로 격리하기 어렵다.
//
// 제약: 검증은 라인 단위로 수행하므로 메시지가 포함된 호출은 **단일
// 라인**에 위 허용 형태 중 하나로 유지해야 한다. named argument를 다음
// 줄로 줄바꿈하면 false negative가 발생한다.
// -----------------------------------------------------------------------

using System.IO;
using NUnit.Framework;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
[Category("Unit")]
public class WebGLTemplateMismatchWarningTests
{
    private const string TemplateMismatchMessageSubstring = "WebGL 빌드에 Runtime 폴더가 없";

    [Test]
    public void TemplateMismatchWarning_IsSuppressedFromSentry()
    {
        string source = ReadCopierSource();

        bool foundCorrect = false;
        foreach (string line in source.Split('\n'))
        {
            if (!line.Contains(TemplateMismatchMessageSubstring)) continue;

            // 허용 형태 1: Debug.Log (LogWarning 아님 → ErrorTracker가 Sentry로 송신 안 함)
            // 허용 형태 2: AITLog.Warning(..., sentryCapture: false) (명시적 노이즈 차단)
            bool isPlainDebugLog = line.Contains("Debug.Log(") && !line.Contains("Debug.LogWarning");
            bool isSuppressedAitLog = line.Contains("AITLog.Warning") && line.Contains("sentryCapture: false");
            if (isPlainDebugLog || isSuppressedAitLog)
            {
                foundCorrect = true;
                break;
            }
        }

        Assert.IsTrue(
            foundCorrect,
            $"\"{TemplateMismatchMessageSubstring}...\" 메시지는 Debug.Log 또는 AITLog.Warning(..., sentryCapture: false) 로 호출되어야 합니다. " +
            "Debug.LogWarning이나 sentryCapture: true 로의 회귀가 발생하면 Sentry APPS-IN-TOSS-UNITY-SDK-R8/R9 노이즈가 다시 잡힙니다.");
    }

    [Test]
    public void TemplateMismatchWarning_DoesNotUseRawDebugLogWarning()
    {
        // Debug.LogWarning은 ErrorTracker 로그 핸들러를 그대로 통과해 Sentry로
        // 캡처된다. 이 메시지에 대해서는 절대 Debug.LogWarning을 직접 호출하면 안 된다.
        string source = ReadCopierSource();

        Assert.IsFalse(
            ContainsLineWith(source, "Debug.LogWarning", TemplateMismatchMessageSubstring),
            $"Debug.LogWarning(...\"{TemplateMismatchMessageSubstring}...\") 직접 호출 금지 — Sentry 노이즈 발생.");
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
}
