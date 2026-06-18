// -----------------------------------------------------------------------
// AITExceptionPlatformUnavailableTests.cs - AITSentryContextEnricher undefined 브리지 처리 회귀 가드
// Level 0: APPS-IN-TOSS-UNITY-SDK-11D 재현 — WebGL 환경에서 window.AppsInToss 가
//           undefined일 때 발생하는 JS TypeError가 Debug.LogWarning → Sentry 노이즈로
//           보고되지 않도록 CollectSafe()가 올바르게 처리하는지 정적 소스 스캔으로 검증.
//
// 배경:
//   - AITSentryContextEnricher.CollectSafe()는 AITException.IsPlatformUnavailable이
//     true이면 Debug.Log(info), false이면 Debug.LogWarning을 내보낸다.
//   - WebGL 빌드에서 Toss 앱 WebView 외부(브리지 미주입)로 실행될 때,
//     AppsInToss-GetDeviceId.jslib 안의 window.AppsInToss.getDeviceId() 호출이
//     TypeError: "Cannot read properties of undefined (reading 'getDeviceId')"를 던진다.
//   - AITException.CheckPlatformUnavailable()의 기존 패턴
//     (__GRANITE_NATIVE_EMITTER / ReactNativeWebView / is not a constant handler)에
//     일치하지 않아, CollectSafe()가 이를 명시적으로 처리하지 않으면
//     IsPlatformUnavailable=false → LogWarning → Sentry 전송됨.
//   - 이 테스트는 CollectSafe()가 "Cannot read properties of undefined" 패턴을
//     Info 레벨로 처리하는 코드를 유지하도록 정적 스캔으로 보장한다.
//   - 정적 소스 스캔을 쓰는 이유: CollectSafe는 async/delegate 구조라 EditMode에서
//     직접 호출이 불가능하고, WebGL 런타임 없이 동등한 검증이 가능하기 때문
//     (SentryNoiseSuppressionCallsiteTests, CleanMenuSafetyTests와 동일한 접근법).
// -----------------------------------------------------------------------

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;

[TestFixture]
[Category("Unit")]
public class AITExceptionPlatformUnavailableTests
{
    private const string FileName = "AITSentryContextEnricher.cs";
    // 수정한 패턴 (이 리터럴이 소스에 있어야 함)
    private const string UndefinedBridgeAnchor = "Cannot read properties of undefined";

    /// <summary>
    /// 회귀 가드: CollectSafe()가 "Cannot read properties of undefined" 패턴(window.AppsInToss
    /// 브리지 미주입 에러)을 Debug.Log (Info 레벨)로 처리하는지 정적 소스 스캔으로 검증.
    ///
    /// 이 테스트가 실패하면:
    ///   1) 앵커 문자열이 소스에서 삭제됨 → 회귀 (Sentry 노이즈 재발)
    ///   2) 해당 패턴 처리가 Debug.LogWarning으로 되돌아감 → 노이즈 회귀
    ///   (APPS-IN-TOSS-UNITY-SDK-11D)
    /// </summary>
    [Test]
    public void CollectSafe_UndefinedBridgePattern_HandledAsInfoNotWarning()
    {
        string path = LocateSentrySource(FileName);
        Assert.IsNotNull(path,
            $"{FileName} 소스 경로를 찾을 수 없음 (AssetDatabase 및 알려진 후보 경로 모두 실패).");
        Assert.IsTrue(File.Exists(path), $"{FileName}가 존재해야 함: {path}");

        string source = File.ReadAllText(path);

        // (1) 패턴 존재 검증 — 없으면 fix가 삭제된 것
        int anchorIdx = source.IndexOf(UndefinedBridgeAnchor, System.StringComparison.Ordinal);
        Assert.GreaterOrEqual(anchorIdx, 0,
            $"[SDK-11D] {FileName}에서 undefined 브리지 처리 패턴을 찾을 수 없음: \"{UndefinedBridgeAnchor}\". " +
            "이 패턴이 없으면 window.AppsInToss 미주입 에러가 Debug.LogWarning으로 전송되어 Sentry 노이즈가 발생한다.");

        // (2) 앵커 앞 가장 가까운 로그 호출 확인 — Debug.Log (Info)여야 하고 Debug.LogWarning이 아니어야 함
        //     패턴 앞 2000자를 스캔해 nearest 로그 호출을 찾는다.
        int scanStart = System.Math.Max(0, anchorIdx - 2000);
        string region = source.Substring(scanStart, anchorIdx - scanStart);

        // 로그 호출 토큰 (LogWarning이 Log보다 구체적이므로 먼저 검사)
        string[] logTokens = { "Debug.LogWarning(", "Debug.LogError(", "Debug.Log(" };
        int bestIdx = -1;
        string bestTok = null;
        foreach (var tok in logTokens)
        {
            int idx = region.LastIndexOf(tok, System.StringComparison.Ordinal);
            if (idx > bestIdx) { bestIdx = idx; bestTok = tok; }
        }

        Assert.AreEqual("Debug.Log(",  bestTok,
            $"[SDK-11D] \"{UndefinedBridgeAnchor}\" 앞의 로그 호출이 'Debug.Log('여야 하는데 '{bestTok ?? "(없음)"}'임. " +
            "이 패턴은 플랫폼 브리지 미주입(예상된 동작)이므로 Info 레벨로 기록해야 합니다. " +
            "LogWarning이면 Sentry 노이즈(APPS-IN-TOSS-UNITY-SDK-11D)가 재발합니다.");
    }

    // =====================================================
    // 기존 IsPlatformUnavailable 패턴 회귀 가드
    // (AITException.CheckPlatformUnavailable이 기존 패턴을 유지하는지)
    // =====================================================

    [TestCase("__GRANITE_NATIVE_EMITTER is not defined")]
    [TestCase("ReactNativeWebView is not defined")]
    [TestCase("getLocale is not a constant handler")]
    [TestCase("getDeviceId is not a constant handler")]
    public void KnownPlatformUnavailableMessages_ReturnTrue(string message)
    {
        var ex = new AppsInToss.AITException(message);
        Assert.IsTrue(ex.IsPlatformUnavailable,
            $"'{message}' 는 플랫폼 미지원으로 분류되어야 합니다.");
    }

    [TestCase("NullReferenceException: Object reference not set to an instance of an object")]
    [TestCase("IndexOutOfRangeException: Index was outside the bounds of the array")]
    [TestCase("Network request failed")]
    [TestCase("Unauthorized")]
    public void RealSdkErrors_ReturnFalse(string message)
    {
        var ex = new AppsInToss.AITException(message);
        Assert.IsFalse(ex.IsPlatformUnavailable,
            $"'{message}' 는 실제 SDK 버그이므로 IsPlatformUnavailable=false여야 합니다.");
    }

    // ===================================================================
    // 헬퍼: AITSentryContextEnricher.cs 소스 경로 해석
    // ===================================================================

    private static string LocateSentrySource(string fileName)
    {
        string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
        foreach (var guid in AssetDatabase.FindAssets(nameNoExt + " t:MonoScript"))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath)) continue;
            if (Path.GetFileName(assetPath) == fileName && File.Exists(assetPath))
                return assetPath;
        }

        string[] candidates =
        {
            "Runtime/Sentry/" + fileName,
            "Packages/im.toss.apps-in-toss-unity-sdk/Runtime/Sentry/" + fileName,
        };
        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        const string packageCache = "Library/PackageCache";
        if (Directory.Exists(packageCache))
        {
            foreach (var d in Directory.GetDirectories(packageCache, "im.toss.apps-in-toss-unity-sdk*"))
            {
                string p = Path.Combine(d, "Runtime", "Sentry", fileName);
                if (File.Exists(p)) return p;
            }
        }

        return null;
    }
}
