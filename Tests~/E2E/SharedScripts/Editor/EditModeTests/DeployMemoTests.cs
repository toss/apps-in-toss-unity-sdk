// -----------------------------------------------------------------------
// DeployMemoTests.cs - Deploy (Test)/(Production) memo 생성·셸 이스케이프·빌드 플래그 검증
// Level 0: AITDeployManager.BuildDeployMemo / EscapeMemoForShell / GetBuildFlags 를
//   Unity/pnpm 실행 없이 검증한다.
//
// 배경: ait deploy 명령은 bash -l -c "..." 문자열로 조립되어 실행된다(AITPlatformHelper.
//   CreateProcessStartInfo). memo는 -m "<memo>" 형태로 명령에 삽입되므로, appName 등에 포함된
//   큰따옴표/달러 기호/백틱/백슬래시가 이스케이프되지 않으면 명령 구조가 깨질 수 있다.
//
// 메모: 이 파일은 AppsInTossEditModeTests 어셈블리에 속한다(DeployPathTests.cs와 동일 위치).
//   해당 어셈블리는 InternalsVisibleTo로 internal AITDeployManager/DeployKind에 접근 가능하다.
// -----------------------------------------------------------------------

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
    // EscapeMemoForShell: 특수 문자 이스케이프
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

        Assert.IsTrue(cleanBuild, "Deploy (Production)은 클린 빌드여야 함.");
        Assert.IsFalse(fastBuild, "Deploy (Production)은 빠른 빌드 레버(IL2CPP Debug/OptimizeSize)를 켜면 안 됨.");
    }

    [Test]
    public void GetBuildFlags_Test_ReturnsCleanBuildFalse_FastBuildTrue()
    {
        // Test는 반복 배포 속도를 위해 증분 빌드 + 빠른 빌드(IL2CPP Debug/OptimizeSize + 에셋 최적화 검사 스킵)를 사용해야 한다.
        var (cleanBuild, fastBuild) = AITDeployManager.GetBuildFlags(DeployKind.Test);

        Assert.IsFalse(cleanBuild, "Deploy (Test)는 증분 빌드여야 함.");
        Assert.IsTrue(fastBuild, "Deploy (Test)는 빠른 빌드 레버를 켜야 함 (Dev Server와 동일).");
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
