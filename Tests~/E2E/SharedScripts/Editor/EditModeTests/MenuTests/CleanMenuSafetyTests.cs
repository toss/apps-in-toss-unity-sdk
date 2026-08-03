// -----------------------------------------------------------------------
// CleanMenuSafetyTests.cs - AIT/Clean 메뉴 안전 삭제 회귀 검증
// Level 0: AppsInTossMenu.Clean이 Directory.Delete를 직접 호출하지 않고
//          AITFileUtils.DeleteDirectory(=AITFileSystemHelper.SafeDeleteDirectory)
//          를 통해 read-only 속성을 처리하도록 보장한다.
//
// 회귀 대상: Sentry APPS-IN-TOSS-UNITY-SDK-CA
//   "AIT: ait-build/ 폴더 삭제 실패: System.UnauthorizedAccessException:
//    Access to the path 'gen-mapping' is denied."
//   ait-build/node_modules/.../@jridgewell/gen-mapping에 깔린 read-only
//   파일을 Directory.Delete(recursive:true)가 처리하지 못해 발생.
// -----------------------------------------------------------------------

using NUnit.Framework;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using AppsInToss;

[TestFixture]
public class CleanMenuSafetyTests
{
    /// <summary>
    /// AppsInTossMenu.cs 소스 파일을 정적으로 읽어, Clean() 메서드 본문 영역 내부에
    /// `Directory.Delete(` 직호출이 없음을 확인한다.
    ///
    /// IL 기반 검증보다 단순하고 의존이 적다. Unity가 패키지를 어떻게 마운트하든
    /// AppsInTossMenu 타입에서 MonoScript를 통해 실제 .cs 경로를 얻을 수 있다.
    /// </summary>
    [Test]
    public void Clean_SourceDoesNotCall_DirectoryDelete()
    {
        string sourcePath = LocateAppsInTossMenuSource();
        Assert.IsNotNull(sourcePath,
            "AppsInTossMenu.cs 소스 경로를 찾을 수 없음 (MonoScript 또는 알려진 후보 경로 모두 실패).");
        Assert.IsTrue(File.Exists(sourcePath),
            $"AppsInTossMenu.cs가 존재해야 함: {sourcePath}");

        string source = File.ReadAllText(sourcePath);

        // Clean 메서드 본문을 추출 (signature ~ 다음 같은 들여쓰기 레벨의 닫는 중괄호)
        string cleanBody = ExtractMethodBody(source, "public static void Clean()");
        Assert.IsNotNull(cleanBody,
            "AppsInTossMenu.Clean() 메서드를 소스에서 찾을 수 없음.");

        // 핵심 회귀 가드:
        //   Directory.Delete( 직호출은 read-only 파일에서 UnauthorizedAccessException을 던진다.
        //   대신 AITFileUtils.DeleteDirectory 또는 AITFileSystemHelper.SafeDeleteDirectory를 사용해야 한다.
        bool callsDirectoryDelete = cleanBody.Contains("Directory.Delete(");
        Assert.IsFalse(callsDirectoryDelete,
            "AppsInTossMenu.Clean()은 Directory.Delete()를 직접 호출하면 안 됨. " +
            "ait-build/node_modules의 read-only 파일에서 UnauthorizedAccessException이 발생한다 " +
            "(Sentry APPS-IN-TOSS-UNITY-SDK-CA). " +
            "AITFileUtils.DeleteDirectory 또는 AITFileSystemHelper.SafeDeleteDirectory를 사용할 것.");

        bool usesSafeHelper =
            cleanBody.Contains("AITFileUtils.DeleteDirectory(") ||
            cleanBody.Contains("AITFileSystemHelper.SafeDeleteDirectory(");
        Assert.IsTrue(usesSafeHelper,
            "AppsInTossMenu.Clean()은 안전 삭제 헬퍼(AITFileUtils.DeleteDirectory 또는 " +
            "AITFileSystemHelper.SafeDeleteDirectory)를 사용해야 한다.");
    }

    /// <summary>
    /// 회귀 가드: Clean() 본문의 삭제 실패 보고 경로가 `Debug.LogError`를 사용하면
    /// 외부 file lock(다른 프로세스가 ait-build 핸들을 잡고 있는 사용자 환경 원인)이
    /// Sentry로 전파된다. 헬퍼(SafeDeleteDirectory)가 이미 sentry-suppressed Warning을
    /// 남기므로 호출자는 `AITLog.Error(..., sentryCapture: false)`를 사용해야 한다.
    ///
    /// 회귀 대상 Sentry 이슈:
    ///   - APPS-IN-TOSS-UNITY-SDK-RS: "ait-build/ 폴더 삭제 실패: C:\UNITY\Projects\HolmesOrchard\ait-build"
    ///   - APPS-IN-TOSS-UNITY-SDK-V3: "ait-build/ 폴더 삭제 실패: D:\New folder\BALANCE\..." (공백 포함 경로)
    ///   - APPS-IN-TOSS-UNITY-SDK-V0: "ait-build/ 폴더 삭제 실패: C:\project\Match in Dream_1.0.21_web\ait-build"
    /// 모두 read-only가 아닌 file lock에 의한 실패로, 메시지에 예외 타입이 없는 변형.
    /// </summary>
    [Test]
    public void Clean_DeleteFailureUsesSentrySuppressedLog()
    {
        string sourcePath = LocateAppsInTossMenuSource();
        Assert.IsNotNull(sourcePath, "AppsInTossMenu.cs 소스 경로를 찾을 수 없음.");

        string source = File.ReadAllText(sourcePath);
        string cleanBody = ExtractMethodBody(source, "public static void Clean()");
        Assert.IsNotNull(cleanBody, "AppsInTossMenu.Clean() 메서드를 소스에서 찾을 수 없음.");

        // 핵심 가드 #1: Clean() 본문에 Debug.LogError 직호출이 없어야 함.
        //   `Debug.LogError`는 AITLog의 SuppressScope를 거치지 않으므로 Sentry로 전송된다.
        bool callsDebugLogError = cleanBody.Contains("Debug.LogError(");
        Assert.IsFalse(callsDebugLogError,
            "AppsInTossMenu.Clean()은 Debug.LogError()를 직접 호출하면 안 됨. " +
            "외부 file lock(IDE/Node/AV)에 의한 삭제 실패가 Sentry로 전파된다 " +
            "(Sentry APPS-IN-TOSS-UNITY-SDK-RS/V3/V0). " +
            "AITLog.Error(..., sentryCapture: false)를 사용할 것.");

        // 핵심 가드 #2: 삭제 실패 분기가 명시적으로 sentryCapture: false를 사용해야 함.
        //   이중 안전망 — Clean() 본문에 "폴더 삭제 실패" 문구를 출력하는 라인이 있다면
        //   같은 라인 근처에 `sentryCapture: false`가 함께 있어야 한다.
        bool reportsFailure = cleanBody.Contains("폴더 삭제 실패");
        if (reportsFailure)
        {
            Assert.IsTrue(cleanBody.Contains("sentryCapture: false"),
                "AppsInTossMenu.Clean()이 '폴더 삭제 실패'를 출력한다면 sentryCapture: false로 " +
                "Sentry 전송을 억제해야 함. 헬퍼(SafeDeleteDirectory)가 이미 흡수된 Warning을 남긴다.");
        }
    }

    private static string LocateAppsInTossMenuSource()
    {
        // 1) 가장 견고한 방법: AssetDatabase에서 MonoScript를 검색해 자산 경로를 얻는다.
        //    Unity가 패키지를 어떤 경로(`Packages/...` 또는 `Library/PackageCache/...`)로
        //    마운트하든 AssetDatabase가 해석한 실제 경로를 돌려준다.
        var guids = AssetDatabase.FindAssets("AppsInTossMenu t:MonoScript");
        foreach (var guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath)) continue;
            // 정확히 AppsInTossMenu.cs만 매칭 (다른 파일에 클래스가 들어있을 수 있어 파일명으로 한 번 더 체크)
            if (Path.GetFileName(assetPath) == "AppsInTossMenu.cs" && File.Exists(assetPath))
            {
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
                if (ms != null && ms.GetClass() == typeof(AppsInTossMenu))
                {
                    return assetPath;
                }
            }
        }

        // 2) AssetDatabase 검색이 실패하면 알려진 상대 경로 후보를 순회.
        //    저장소 루트에서 직접 돌리는 경우 / Packages/ 임베드 / PackageCache 임베드.
        string[] candidates = new[]
        {
            "Editor/AppsInTossMenu.cs",
            "Packages/im.toss.apps-in-toss-unity-sdk/Editor/AppsInTossMenu.cs",
        };
        foreach (var c in candidates)
        {
            if (File.Exists(c)) return c;
        }

        // PackageCache 패턴 (`Library/PackageCache/im.toss.apps-in-toss-unity-sdk@<hash>/`)
        string packageCache = "Library/PackageCache";
        if (Directory.Exists(packageCache))
        {
            var dirs = Directory.GetDirectories(packageCache, "im.toss.apps-in-toss-unity-sdk*");
            foreach (var d in dirs)
            {
                string p = Path.Combine(d, "Editor", "AppsInTossMenu.cs");
                if (File.Exists(p)) return p;
            }
        }

        return null;
    }

    /// <summary>
    /// 단순한 brace 카운팅으로 메서드 본문(여는 중괄호 다음부터 매칭되는 닫는 중괄호 직전까지)을 추출.
    /// 문자열/주석 안의 중괄호는 정확하게 처리하지 못하지만, AppsInTossMenu.Clean의 실제 구현에는
    /// 그런 패턴이 없으므로 충분하다.
    /// </summary>
    private static string ExtractMethodBody(string source, string signatureSubstring)
    {
        int sigIdx = source.IndexOf(signatureSubstring);
        if (sigIdx < 0) return null;

        int openIdx = source.IndexOf('{', sigIdx);
        if (openIdx < 0) return null;

        int depth = 0;
        for (int i = openIdx; i < source.Length; i++)
        {
            char ch = source[i];
            if (ch == '{') depth++;
            else if (ch == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(openIdx + 1, i - openIdx - 1);
                }
            }
        }
        return null;
    }
}
