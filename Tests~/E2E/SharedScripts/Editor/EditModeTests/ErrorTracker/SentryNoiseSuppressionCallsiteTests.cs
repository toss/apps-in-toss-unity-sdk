// -----------------------------------------------------------------------
// SentryNoiseSuppressionCallsiteTests.cs - 정상 fallback warning 콜사이트 Sentry 억제 회귀 가드
// Level 0: PR #733에서 sentryCapture:false로 전환한 3개 콜사이트가 raw Debug.LogWarning/
//          LogError로 되돌아가지 않고 AITLog.Warning(..., sentryCapture: false) 형태를
//          유지하는지 소스 정적 스캔으로 보장한다 (CleanMenuSafetyTests #711 양식).
//
// 회귀 대상 Sentry 이슈 (모두 정상 흐름의 예측된 fallback — 호출부가 자가 복구, SDK 결함 아님):
//   - APPS-IN-TOSS-UNITY-SDK-QB : "[AIT] package.json 템플릿을 찾을 수 없습니다. 첫 빌드 시 자동으로 설치됩니다."
//   - APPS-IN-TOSS-UNITY-SDK-Q2 : "[AIT] SDK 패키지를 찾을 수 없습니다. … 패키지 설치 상태를 확인하세요."
//   - APPS-IN-TOSS-UNITY-SDK-10R: "[AIT] SDK 로딩 화면 템플릿을 찾을 수 없습니다. 첫 빌드 시 다시 시도됩니다."
//
// 진짜 빌드 실패는 다운스트림 CaptureBuildError(구조화 이벤트, errorCode fingerprint)가 별도로
// 캡처하므로, 이 fallback warning들을 Sentry에서 억제해도 가시성을 잃지 않는다.
//
// IL/리플렉션 대신 정적 소스 스캔을 쓰는 이유: 의존이 적고, Unity가 패키지를 어떤 경로로
// 마운트하든 AssetDatabase로 실제 .cs 경로를 얻을 수 있다 (#711과 동일).
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;

[TestFixture]
[Category("Unit")]
public class SentryNoiseSuppressionCallsiteTests
{
    [Test]
    public void QB_PackageJsonTemplateWarning_StaysSentrySuppressed()
        => AssertCallsiteSuppressed(
            "AITPackageInitializer.cs",
            "package.json 템플릿을 찾을 수 없습니다. 첫 빌드 시 자동으로 설치됩니다.",
            "QB");

    [Test]
    public void Q2_SdkPackageNotFoundWarning_StaysSentrySuppressed()
        => AssertCallsiteSuppressed(
            "AITPackagePathResolver.cs",
            "SDK 패키지를 찾을 수 없습니다.",
            "Q2");

    [Test]
    public void TenR_LoadingScreenTemplateWarning_StaysSentrySuppressed()
        => AssertCallsiteSuppressed(
            "AITPackageInitializer.cs",
            "SDK 로딩 화면 템플릿을 찾을 수 없습니다. 첫 빌드 시 다시 시도됩니다.",
            "10R");

    /// <summary>
    /// fileName 소스에서 messageAnchor를 내보내는 가장 가까운 선행 로그 호출이
    /// AITLog.Warning(...)이고, 그 호출 인자에 sentryCapture: false가 포함됨을 정적으로 검증한다.
    /// </summary>
    private static void AssertCallsiteSuppressed(string fileName, string messageAnchor, string sentryIssue)
    {
        string path = LocateEditorSource(fileName);
        Assert.IsNotNull(path,
            $"{fileName} 소스 경로를 찾을 수 없음 (AssetDatabase 및 알려진 후보 경로 모두 실패).");
        Assert.IsTrue(File.Exists(path), $"{fileName}가 존재해야 함: {path}");

        string source = File.ReadAllText(path);

        // (1) 메시지 존재 — 메시지가 통째로 바뀌면 앵커도 함께 갱신해야 한다는 신호.
        int anchorIdx = source.IndexOf(messageAnchor, StringComparison.Ordinal);
        Assert.GreaterOrEqual(anchorIdx, 0,
            $"[{sentryIssue}] {fileName}에서 메시지를 찾을 수 없음: \"{messageAnchor}\". " +
            "메시지가 변경되었다면 이 가드의 앵커도 함께 갱신할 것.");

        // (2) 앵커 유일성 — 중복이면 회귀 검출이 모호해지므로 단일 출현을 요구.
        int secondIdx = source.IndexOf(messageAnchor, anchorIdx + 1, StringComparison.Ordinal);
        Assert.AreEqual(-1, secondIdx,
            $"[{sentryIssue}] 메시지 앵커가 {fileName}에 2회 이상 등장 — 더 구체적인 앵커가 필요.");

        // (3) 메시지를 내보내는 '가장 가까운 선행 호출'이 AITLog.Warning(이어야 함.
        //     raw Debug.Log* 직호출은 SuppressScope를 거치지 않아 Sentry로 전송된다.
        string[] emitTokens =
        {
            "AITLog.Warning(", "AITLog.Error(",
            "Debug.LogWarning(", "Debug.LogError(", "Debug.Log(",
        };
        int bestIdx = -1;
        string bestTok = null;
        foreach (var tok in emitTokens)
        {
            int idx = source.LastIndexOf(tok, anchorIdx, StringComparison.Ordinal);
            if (idx > bestIdx) { bestIdx = idx; bestTok = tok; }
        }
        Assert.AreEqual("AITLog.Warning(", bestTok,
            $"[{sentryIssue}] '{messageAnchor}' 메시지는 AITLog.Warning(...)으로 내보내야 함. " +
            $"가장 가까운 선행 호출이 '{bestTok ?? "(없음)"}'임 — raw Debug.Log* 직호출은 " +
            "SuppressScope를 거치지 않아 Sentry 노이즈로 전송된다.");

        // (4) 호출 인자 영역(여는 괄호 ~ 매칭 닫는 괄호)을 문자열 리터럴 인식 스캐너로 추출.
        int openParen = bestIdx + bestTok.Length - 1; // bestTok 끝의 '(' 위치
        int closeParen = FindMatchingParen(source, openParen);
        Assert.Greater(closeParen, openParen,
            $"[{sentryIssue}] AITLog.Warning(...) 호출의 닫는 괄호를 찾지 못함 (소스 구조 확인 필요).");

        // 앵커가 이 호출 인자 범위 안에 있어야 함 — 메시지가 정말 이 호출에 속함을 확인.
        Assert.IsTrue(anchorIdx > openParen && anchorIdx < closeParen,
            $"[{sentryIssue}] 메시지 앵커가 가장 가까운 AITLog.Warning(...) 인자 범위 밖 — 스캔 가정 위반.");

        string callArgs = source.Substring(openParen, closeParen - openParen + 1);

        // (5) 핵심 회귀 가드: sentryCapture: false (공백/개행 무관) 가 인자에 포함되어야 함.
        string normalized = Regex.Replace(callArgs, @"\s+", "");
        Assert.IsTrue(normalized.Contains("sentryCapture:false"),
            $"[{sentryIssue}] {fileName}의 정상 fallback warning은 sentryCapture: false 여야 함. " +
            "이 단언이 깨지면 정상 fallback이 다시 Sentry로 전송되는 노이즈 회귀다 " +
            $"(Sentry APPS-IN-TOSS-UNITY-SDK-{sentryIssue}). 호출: {callArgs.Trim()}");
    }

    /// <summary>
    /// fileName(예: "AITPackageInitializer.cs") 의 실제 디스크 경로를 해석한다.
    /// 1) AssetDatabase MonoScript 검색(가장 견고) → 2) 알려진 후보 상대 경로 → 3) PackageCache glob.
    /// (#711 LocateAppsInTossMenuSource를 파일명 파라미터로 일반화. typeof 체크는 internal/static
    ///  타입 접근성 문제를 피하려고 생략하고 파일명 일치만으로 식별 — 파일명이 저장소 내 유일.)
    /// </summary>
    private static string LocateEditorSource(string fileName)
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
            "Editor/" + fileName,
            "Packages/im.toss.apps-in-toss-unity-sdk/Editor/" + fileName,
        };
        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        const string packageCache = "Library/PackageCache";
        if (Directory.Exists(packageCache))
        {
            foreach (var d in Directory.GetDirectories(packageCache, "im.toss.apps-in-toss-unity-sdk*"))
            {
                string p = Path.Combine(d, "Editor", fileName);
                if (File.Exists(p)) return p;
            }
        }

        return null;
    }

    /// <summary>
    /// openIdx('(')에 대응하는 닫는 괄호 인덱스를 반환. 문자열("...", $"...")·문자('...')
    /// 리터럴과 //·/* */ 주석 내부의 괄호는 무시한다.
    ///
    /// 한계(#711 ExtractMethodBody와 동일한 실용적 단순화): 보간 문자열 $"...{Foo(")")}..."처럼
    /// 보간 구멍 안에 따옴표가 들어간 경우는 정확히 처리하지 못한다. 대상 3개 콜사이트에는
    /// 그런 패턴이 없으므로 충분하다(Q2의 보간 구멍은 단순 프로퍼티 접근뿐).
    /// </summary>
    private static int FindMatchingParen(string s, int openIdx)
    {
        int depth = 0;
        for (int i = openIdx; i < s.Length; i++)
        {
            char c = s[i];

            if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
            {
                int nl = s.IndexOf('\n', i);
                if (nl < 0) return -1;
                i = nl;
                continue;
            }
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
            {
                int end = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0) return -1;
                i = end + 1;
                continue;
            }
            if (c == '"')
            {
                i = SkipLiteral(s, i, '"');
                if (i < 0) return -1;
                continue;
            }
            if (c == '\'')
            {
                i = SkipLiteral(s, i, '\'');
                if (i < 0) return -1;
                continue;
            }

            if (c == '(') depth++;
            else if (c == ')')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// quoteIdx(여는 따옴표)에 대응하는 닫는 따옴표 인덱스를 반환. \ 이스케이프를 건너뛴다.
    /// </summary>
    private static int SkipLiteral(string s, int quoteIdx, char quote)
    {
        for (int i = quoteIdx + 1; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\') { i++; continue; }
            if (c == quote) return i;
        }
        return -1;
    }
}
