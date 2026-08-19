// -----------------------------------------------------------------------
// DeployMemoTests.cs - Deploy for Online Test/Deploy Release Candidate memo 생성·무해화·셸 이스케이프·빌드 플래그 검증
// Level 0: AITDeployManager.BuildDeployMemo / SanitizeMemo / EscapeMemoForShell / GetBuildFlags 를
//   Unity/pnpm 실행 없이 검증한다.
//
// 배경: ait deploy 명령은 bash -l -c "..." 문자열(Windows는 powershell -Command)로 조립되어
//   실행된다(AITPlatformHelper.CreateProcessStartInfo). memo는 -m "<memo>" 형태로 명령에 삽입되고
//   그 명령 문자열이 다시 한 번 이스케이프·argv 파싱을 거치므로, 이스케이프 층을 쌓는 대신
//   memo 생성 시점에 \ " ` $ 와 제어 문자를 무해화한다. EscapeMemoForShell은 남겨둔 심층 방어층.
//
// 메모: 이 파일은 AppsInTossEditModeTests 어셈블리에 속한다(DeployPathTests.cs와 동일 위치).
//   해당 어셈블리는 InternalsVisibleTo로 internal AITDeployManager/DeployKind에 접근 가능하다.
// -----------------------------------------------------------------------

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using NUnit.Framework;
using AppsInToss.Editor.Menu;  // AITDeployManager, DeployKind (internal, .Menu 하위 네임스페이스)

[TestFixture]
public class DeployMemoTests
{
    // =====================================================
    // BuildDeployMemo: 접두사 / 길이 제한
    // =====================================================

    [Test]
    public void BuildDeployMemo_Test_HasTestPrefix()
    {
        string memo = AITDeployManager.BuildDeployMemo(DeployKind.Test, "MyGame", "1.2.3");
        Assert.IsTrue(memo.StartsWith("[Test] "), $"Test memo는 [Test] 접두사로 시작해야 함. 실제: {memo}");
        Assert.IsTrue(memo.Contains("MyGame"), "memo에 appName이 포함되어야 함.");
        Assert.IsTrue(memo.Contains("1.2.3"), "memo에 version이 포함되어야 함.");
    }

    [Test]
    public void BuildDeployMemo_Production_HasProductionPrefix()
    {
        string memo = AITDeployManager.BuildDeployMemo(DeployKind.Production, "MyGame", "1.2.3");
        Assert.IsTrue(memo.StartsWith("[Production] "), $"Production memo는 [Production] 접두사로 시작해야 함. 실제: {memo}");
    }

    [Test]
    public void BuildDeployMemo_ExceedsMaxLength_IsTruncatedTo1000Chars()
    {
        // ait deploy CLI의 -m/--memo 최대 길이(1000자) 제약 검증.
        string longAppName = new string('a', 2000);
        string memo = AITDeployManager.BuildDeployMemo(DeployKind.Test, longAppName, "1.0.0");

        Assert.AreEqual(AITDeployManager.MaxMemoLength, memo.Length,
            $"1000자를 초과하는 memo는 {AITDeployManager.MaxMemoLength}자로 잘라내야 함. 실제 길이: {memo.Length}");
    }

    [Test]
    public void BuildDeployMemo_UnderMaxLength_IsNotTruncated()
    {
        string memo = AITDeployManager.BuildDeployMemo(DeployKind.Test, "MyGame", "1.0.0");
        Assert.Less(memo.Length, AITDeployManager.MaxMemoLength,
            "짧은 appName/version으로 생성된 memo는 잘리지 않아야 함.");
    }

    // =====================================================
    // BuildDeployMemo: 셸 인용을 깨는 문자 무해화 (소스 단계 sanitize)
    // =====================================================

    [Test]
    public void BuildDeployMemo_ReplacesShellQuotingChars_WithSingleQuote()
    {
        // 백틱/달러/백슬래시/큰따옴표는 이스케이프 중첩(bash -l -c 재이스케이프 + argv 파싱)에서
        // 원본에 없던 백슬래시를 남기고, Windows(-Command)에서는 인자 경계를 깬다.
        string memo = AITDeployManager.BuildDeployMemo(DeployKind.Test, "My\"Game`$Cool\\Studio", "1.0.0");

        Assert.IsFalse(memo.Contains("\""), $"큰따옴표가 memo에 남아있음: {memo}");
        Assert.IsFalse(memo.Contains("`"), $"백틱이 memo에 남아있음: {memo}");
        Assert.IsFalse(memo.Contains("$"), $"달러 기호가 memo에 남아있음: {memo}");
        Assert.IsFalse(memo.Contains("\\"), $"백슬래시가 memo에 남아있음: {memo}");

        // 4종은 제거가 아니라 작은따옴표 치환이므로 appName의 나머지 글자는 그대로 보존된다.
        Assert.IsTrue(memo.Contains("My'Game''Cool'Studio"), $"치환 결과가 예상과 다름: {memo}");
    }

    [Test]
    public void BuildDeployMemo_RemovesControlCharacters()
    {
        string memo = AITDeployManager.BuildDeployMemo(DeployKind.Test, "My\nGame\r\tStudio", "1.0.0");

        Assert.IsFalse(memo.Contains("\n"), $"개행이 memo에 남아있음: {memo}");
        Assert.IsFalse(memo.Contains("\r"), $"캐리지 리턴이 memo에 남아있음: {memo}");
        Assert.IsFalse(memo.Contains("\t"), $"탭이 memo에 남아있음: {memo}");
        Assert.IsTrue(memo.Contains("MyGameStudio"), $"제어 문자만 제거되어야 함: {memo}");
    }

    [Test]
    public void BuildDeployMemo_SanitizedMemo_IsUnchangedByEscape()
    {
        // sanitize 이후에는 EscapeMemoForShell이 아무것도 바꾸지 않아야 한다
        // (= 이스케이프 백슬래시가 최종 memo에 잔존할 여지가 없다).
        string memo = AITDeployManager.BuildDeployMemo(DeployKind.Production, "Game`$\"\\Name", "2.0.0");

        Assert.AreEqual(memo, AITDeployManager.EscapeMemoForShell(memo),
            "무해화된 memo는 셸 이스케이프 대상 문자를 포함하지 않아야 함.");
    }

    [Test]
    public void BuildDeployMemo_SpecialCharsWithLongAppName_StillTruncatedToMaxLength()
    {
        // sanitize(치환/제거)는 길이를 늘리지 않으므로 절단 후 재팽창이 없다.
        string longAppName = new string('$', 2000);
        string memo = AITDeployManager.BuildDeployMemo(DeployKind.Test, longAppName, "1.0.0");

        Assert.AreEqual(AITDeployManager.MaxMemoLength, memo.Length,
            $"무해화 후에도 {AITDeployManager.MaxMemoLength}자로 잘라내야 함. 실제 길이: {memo.Length}");
        Assert.IsFalse(memo.Contains("$"), "절단된 memo에도 특수문자가 남으면 안 됨.");
    }

    [Test]
    public void SanitizeMemo_PlainText_IsUnchanged()
    {
        Assert.AreEqual("MyGame v1.0.0", AITDeployManager.SanitizeMemo("MyGame v1.0.0"));
    }

    [Test]
    public void SanitizeMemo_NullOrEmpty_ReturnsInput()
    {
        Assert.IsNull(AITDeployManager.SanitizeMemo(null));
        Assert.AreEqual(string.Empty, AITDeployManager.SanitizeMemo(string.Empty));
    }

    // =====================================================
    // ResolveTimeZoneAbbreviation: 순수 함수 — 시스템 TimeZoneInfo 조회 없이 문자열/TimeSpan만으로 검증.
    // TimeZoneInfo.FindSystemTimeZoneById 등 시스템 존 조회는 Windows CI에서 실패할 수 있어 사용하지 않는다.
    // =====================================================

    [Test]
    public void ResolveTimeZoneAbbreviation_AsiaSeoulId_ReturnsKst()
    {
        string abbr = AITDeployManager.ResolveTimeZoneAbbreviation("Asia/Seoul", TimeSpan.FromHours(9), "대한민국 표준시");
        Assert.AreEqual("KST", abbr);
    }

    [Test]
    public void ResolveTimeZoneAbbreviation_KoreaStandardTimeWindowsId_ReturnsKst()
    {
        string abbr = AITDeployManager.ResolveTimeZoneAbbreviation("Korea Standard Time", TimeSpan.FromHours(9), "대한민국 표준시");
        Assert.AreEqual("KST", abbr);
    }

    [Test]
    public void ResolveTimeZoneAbbreviation_EtcUtcId_ReturnsUtc()
    {
        string abbr = AITDeployManager.ResolveTimeZoneAbbreviation("Etc/UTC", TimeSpan.Zero, "UTC");
        Assert.AreEqual("UTC", abbr);
    }

    [Test]
    public void ResolveTimeZoneAbbreviation_UnknownId_ShortUppercaseTzName_UsesTzNameAsIs()
    {
        // 1단계 known 매핑에 없는 id라도 tzName이 이미 2~5자 대문자 약어 형태면 2단계 휴리스틱으로 그대로 사용.
        string abbr = AITDeployManager.ResolveTimeZoneAbbreviation("Europe/Paris", TimeSpan.FromHours(1), "CET");
        Assert.AreEqual("CET", abbr);
    }

    [Test]
    public void ResolveTimeZoneAbbreviation_UnknownId_LocalizedLongName_FallsBackToPositiveOffset()
    {
        // known 매핑도 없고 tzName도 약어 형태가 아니면(로컬라이즈된 긴 이름) 3단계 오프셋 폴백.
        string abbr = AITDeployManager.ResolveTimeZoneAbbreviation("Unknown/Zone", TimeSpan.FromHours(9), "일본 표준시");
        Assert.AreEqual("UTC+9", abbr);
    }

    [Test]
    public void ResolveTimeZoneAbbreviation_UnknownId_LocalizedLongName_FallsBackToNegativeOffset()
    {
        string abbr = AITDeployManager.ResolveTimeZoneAbbreviation("Unknown/Zone", TimeSpan.FromHours(-5), "Eastern Standard Time (Localized)");
        Assert.AreEqual("UTC-5", abbr);
    }

    [Test]
    public void ResolveTimeZoneAbbreviation_UnknownId_HalfHourOffset_FallsBackWithMinutes()
    {
        string abbr = AITDeployManager.ResolveTimeZoneAbbreviation("Unknown/Zone", new TimeSpan(5, 30, 0), "Indian Standard Time (Localized)");
        Assert.AreEqual("UTC+5:30", abbr);
    }

    [Test]
    public void ResolveTimeZoneAbbreviation_UnknownId_ZeroOffset_FallsBackToUtc()
    {
        string abbr = AITDeployManager.ResolveTimeZoneAbbreviation("Unknown/Zone", TimeSpan.Zero, "Greenwich Mean Time (Localized)");
        Assert.AreEqual("UTC", abbr);
    }

    // =====================================================
    // FormatDeployTimestamp: 고정 DateTime + InvariantCulture — 테스트 러너 문화권과 무관해야 함.
    // =====================================================

    [Test]
    public void FormatDeployTimestamp_FixedDateTime_FormatsAsExpected()
    {
        var fixedDateTime = new DateTime(2026, 8, 13, 14, 5, 0);
        string formatted = AITDeployManager.FormatDeployTimestamp(fixedDateTime, "KST");
        Assert.AreEqual("2026-08-13 14:05 KST", formatted);
    }

    [Test]
    public void FormatDeployTimestamp_UsesInvariantCulture_RegardlessOfCurrentCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            // 일부 문화권(예: fr-FR)은 날짜 구분자·자릿수 표기가 달라진다 — InvariantCulture 강제 검증.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var fixedDateTime = new DateTime(2026, 1, 5, 9, 3, 0);
            string formatted = AITDeployManager.FormatDeployTimestamp(fixedDateTime, "UTC");
            Assert.AreEqual("2026-01-05 09:03 UTC", formatted);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    // =====================================================
    // BuildDeployMemo: 타임스탬프 첨부 (기존 memo 조립 파이프라인과의 결합)
    // =====================================================

    [Test]
    public void BuildDeployMemo_EndsWithSeparatorAndTimestampPattern()
    {
        string memo = AITDeployManager.BuildDeployMemo(DeployKind.Test, "MyGame", "1.2.3");

        Assert.IsTrue(Regex.IsMatch(memo, @" · \d{4}-\d{2}-\d{2} \d{2}:\d{2} \S+$"),
            $"memo는 ' · ' 구분자 + 타임스탬프 패턴으로 끝나야 함. 실제: {memo}");
    }

    // =====================================================
    // EscapeMemoForShell: 특수 문자 이스케이프 (심층 방어층 — 단독 계약 유지)
    // =====================================================

    [Test]
    public void EscapeMemoForShell_EscapesDoubleQuote()
    {
        string escaped = AITDeployManager.EscapeMemoForShell("say \"hi\"");
        Assert.AreEqual("say \\\"hi\\\"", escaped);
    }

    [Test]
    public void EscapeMemoForShell_EscapesDollarSign()
    {
        string escaped = AITDeployManager.EscapeMemoForShell("$HOME price $5");
        Assert.AreEqual("\\$HOME price \\$5", escaped);
    }

    [Test]
    public void EscapeMemoForShell_EscapesBacktick()
    {
        string escaped = AITDeployManager.EscapeMemoForShell("run `whoami`");
        Assert.AreEqual("run \\`whoami\\`", escaped);
    }

    [Test]
    public void EscapeMemoForShell_EscapesBackslash()
    {
        string escaped = AITDeployManager.EscapeMemoForShell(@"C:\path\to\game");
        Assert.AreEqual(@"C:\\path\\to\\game", escaped);
    }

    [Test]
    public void EscapeMemoForShell_EscapesAllSpecialCharsTogether()
    {
        // 따옴표·달러 기호·백틱·백슬래시가 모두 포함된 appName 케이스 (지시서 요구 케이스)
        string appName = "My\"Game`$Cool\\Studio";
        string memo = AITDeployManager.BuildDeployMemo(DeployKind.Test, appName, "1.0.0");
        string escaped = AITDeployManager.EscapeMemoForShell(memo);

        // 이스케이프 후 명령 문자열에 삽입했을 때 -m "..." 인자 경계를 깨는 원시 특수문자가
        // (이스케이프 백슬래시 없이) 단독으로 남아있지 않아야 한다.
        Assert.IsFalse(ContainsUnescaped(escaped, '"'), $"이스케이프되지 않은 큰따옴표가 남아있음: {escaped}");
        Assert.IsFalse(ContainsUnescaped(escaped, '$'), $"이스케이프되지 않은 달러 기호가 남아있음: {escaped}");
        Assert.IsFalse(ContainsUnescaped(escaped, '`'), $"이스케이프되지 않은 백틱이 남아있음: {escaped}");
    }

    [Test]
    public void EscapeMemoForShell_PlainText_IsUnchanged()
    {
        string escaped = AITDeployManager.EscapeMemoForShell("MyGame v1.0.0");
        Assert.AreEqual("MyGame v1.0.0", escaped);
    }

    [Test]
    public void EscapeMemoForShell_NullOrEmpty_ReturnsInput()
    {
        Assert.IsNull(AITDeployManager.EscapeMemoForShell(null));
        Assert.AreEqual(string.Empty, AITDeployManager.EscapeMemoForShell(string.Empty));
    }

    // =====================================================
    // GetBuildFlags: DeployKind별 (cleanBuild, fastBuild) 매트릭스
    // =====================================================

    [Test]
    public void GetBuildFlags_Production_ReturnsCleanBuildTrue_FastBuildFalse()
    {
        // Production은 현행 Publish와 동일하게 클린 빌드 + 기존 IL2CPP 설정을 유지해야 한다.
        var (cleanBuild, fastBuild) = AITDeployManager.GetBuildFlags(DeployKind.Production);

        Assert.IsTrue(cleanBuild, "Deploy Release Candidate는 클린 빌드여야 함.");
        Assert.IsFalse(fastBuild, "Deploy Release Candidate는 빠른 빌드 레버(IL2CPP Debug/OptimizeSize)를 켜면 안 됨.");
    }

    [Test]
    public void GetBuildFlags_Test_ReturnsCleanBuildFalse_FastBuildTrue()
    {
        // Test는 반복 배포 속도를 위해 증분 빌드 + 빠른 빌드(IL2CPP Debug/OptimizeSize + 에셋 최적화 검사 스킵)를 사용해야 한다.
        var (cleanBuild, fastBuild) = AITDeployManager.GetBuildFlags(DeployKind.Test);

        Assert.IsFalse(cleanBuild, "Deploy for Online Test는 증분 빌드여야 함.");
        Assert.IsTrue(fastBuild, "Deploy for Online Test는 빠른 빌드 레버를 켜야 함 (Dev Server와 동일).");
    }

    // =====================================================
    // 헬퍼
    // =====================================================

    /// <summary>
    /// target 문자가 escaped 문자열 안에서 바로 앞에 이스케이프 백슬래시 없이 등장하는지 확인한다.
    /// </summary>
    private static bool ContainsUnescaped(string escaped, char target)
    {
        for (int i = 0; i < escaped.Length; i++)
        {
            if (escaped[i] != target) continue;
            if (i == 0 || escaped[i - 1] != '\\') return true;
        }
        return false;
    }
}
