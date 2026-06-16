// -----------------------------------------------------------------------
// SentryContextEnricherNoiseTests.cs - CollectSafe IsPlatformUnavailable 노이즈 억제 회귀 가드
// Level 0: APPS-IN-TOSS-UNITY-SDK-11H
//
// AITSentryContextEnricher.CollectSafe() 에서 AITException.IsPlatformUnavailable=true 인
// 경우(예: "getPlatformOS is not a constant handler")를 Debug.LogWarning 이 아닌
// Debug.Log 로 기록하는지 정적 소스 스캔으로 검증한다.
//
// Debug.LogWarning 은 Sentry 에 전송되어 노이즈 이슈(APPS-IN-TOSS-UNITY-SDK-11H)가 된다.
// IsPlatformUnavailable=true 인 에러는 플랫폼이 해당 API 를 지원하지 않는 정상 상황이므로
// Info(Debug.Log) 레벨로 기록해야 Sentry 노이즈를 방지할 수 있다.
// -----------------------------------------------------------------------

using System.IO;
using NUnit.Framework;
using UnityEditor;

[TestFixture]
[Category("Unit")]
public class SentryContextEnricherNoiseTests
{
    /// <summary>
    /// 회귀 가드 — APPS-IN-TOSS-UNITY-SDK-11H:
    /// AITSentryContextEnricher.CollectSafe() 가 IsPlatformUnavailable=true 인
    /// AITException 을 받으면 Debug.LogWarning 이 아닌 Debug.Log 로 기록하는지 검증.
    /// Debug.LogWarning 이 되돌아오면 Sentry 노이즈 회귀가 발생한다.
    /// </summary>
    [Test]
    public void CollectSafe_PlatformUnavailableException_UsesDebugLogNotWarning()
    {
        string path = LocateRuntimeSource("AITSentryContextEnricher.cs");
        Assert.IsNotNull(path,
            "AITSentryContextEnricher.cs 소스 경로를 찾을 수 없음 (AssetDatabase 및 알려진 후보 경로 모두 실패).");
        Assert.IsTrue(File.Exists(path), $"AITSentryContextEnricher.cs 가 존재해야 함: {path}");

        string source = File.ReadAllText(path);

        // (1) IsPlatformUnavailable 분기가 존재하는지 확인
        Assert.IsTrue(source.Contains("IsPlatformUnavailable"),
            "[11H] AITSentryContextEnricher.cs 에 IsPlatformUnavailable 체크가 없음 — " +
            "플랫폼 미지원 예외를 Debug.LogWarning 으로 기록하면 Sentry 노이즈가 된다.");

        // (2) 플랫폼 미지원 케이스에 Debug.Log( 호출이 있는지 확인 (suppression 경로)
        Assert.IsTrue(source.Contains("Debug.Log($\"{Tag} {apiName} 호출 실패 (플랫폼 미지원)"),
            "[11H] IsPlatformUnavailable=true 케이스에 Debug.Log(플랫폼 미지원) 호출이 없음 — " +
            "이 경우 Debug.LogWarning 으로 기록되어 Sentry 노이즈가 된다.");

        // (3) IsPlatformUnavailable 체크 이후 else 분기에만 Debug.LogWarning 이 오는 구조인지
        //     간접 검증: "IsPlatformUnavailable" 이전에 raw "Debug.LogWarning($\"{Tag} {apiName}" 가 있으면 회귀.
        int platUnavailIdx = source.IndexOf("IsPlatformUnavailable", System.StringComparison.Ordinal);
        int rawWarnIdx = source.IndexOf(
            "Debug.LogWarning($\"{Tag} {apiName} 호출 실패: {ex.Message}\")",
            System.StringComparison.Ordinal);

        // rawWarnIdx 가 -1 이면 LogWarning 자체가 없는 것도 허용 (완전 제거 시)
        if (rawWarnIdx >= 0)
        {
            Assert.Greater(rawWarnIdx, platUnavailIdx,
                "[11H] Debug.LogWarning($\"{Tag} {apiName} 호출 실패\") 가 IsPlatformUnavailable 체크보다 " +
                "앞에 있음 — else 분기 없이 항상 LogWarning 을 호출하는 상태로 회귀했을 가능성이 있다. " +
                "Sentry 노이즈 이슈 APPS-IN-TOSS-UNITY-SDK-11H 재발 위험.");
        }
    }

    /// <summary>
    /// fileName(예: "AITSentryContextEnricher.cs") 의 실제 디스크 경로를 해석한다.
    /// 1) AssetDatabase MonoScript 검색 → 2) 알려진 후보 상대 경로 → 3) PackageCache glob.
    /// </summary>
    private static string LocateRuntimeSource(string fileName)
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
