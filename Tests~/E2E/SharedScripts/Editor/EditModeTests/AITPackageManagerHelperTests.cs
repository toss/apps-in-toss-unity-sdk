// -----------------------------------------------------------------------
// AITPackageManagerHelperTests.cs - 패키지 매니저 헬퍼 결정적 로직 검증
// Level 0: PNPM 버전 핀 / Node.js 경로 설명 / ait-build 경로 등 프로세스를
//          띄우지 않는 순수·결정적 메서드의 특성화 테스트.
// (FindPackageManager/RunPackageCommand 등은 실제 프로세스·FS 작업이라
//  단위 테스트 대상에서 제외 — E2E 빌드 경로에서 커버된다.)
// -----------------------------------------------------------------------

using NUnit.Framework;
using System.IO;
using System.Text.RegularExpressions;
using AppsInToss.Editor;

[TestFixture]
public class AITPackageManagerHelperTests
{
    // =====================================================
    // PNPM_VERSION — 고정 핀
    // =====================================================

    [Test]
    public void PnpmVersion_IsNonEmptySemver()
    {
        // 주의: 이 상수는 package.json 3종 + Editor 핀과 항상 동기화돼야 한다
        // (CLAUDE.md "pnpm 버전 핀 동기화" 참조). 값 변경 시 4곳을 함께 bump할 것.
        string v = AITPackageManagerHelper.PNPM_VERSION;
        Assert.IsFalse(string.IsNullOrEmpty(v), "PNPM_VERSION은 비어있으면 안 된다");
        Assert.IsTrue(Regex.IsMatch(v, @"^\d+\.\d+\.\d+$"),
            $"PNPM_VERSION은 X.Y.Z 형식이어야 한다 (실제: '{v}')");
    }

    // =====================================================
    // GetNodejsPathDescription — 플랫폼 의존
    // =====================================================

    [Test]
    public void GetNodejsPathDescription_RespectsPlatform()
    {
        string desc = AITPackageManagerHelper.GetNodejsPathDescription();
        Assert.IsFalse(string.IsNullOrEmpty(desc));
        // 플랫폼 무관하게 SDK 전용 nodejs 경로를 가리켜야 한다.
        StringAssert.Contains("ait-unity-sdk", desc);
        StringAssert.Contains("nodejs", desc);

        if (AITPlatformHelper.IsWindows)
        {
            StringAssert.Contains("%LOCALAPPDATA%", desc);
        }
        else
        {
            Assert.AreEqual("~/.ait-unity-sdk/nodejs/", desc);
        }
    }

    // =====================================================
    // GetBuildPath — ait-build 절대 경로
    // =====================================================

    [Test]
    public void GetBuildPath_EndsWithAitBuildAndIsRooted()
    {
        string buildPath = AITPackageManagerHelper.GetBuildPath();
        Assert.IsFalse(string.IsNullOrEmpty(buildPath));
        Assert.IsTrue(Path.IsPathRooted(buildPath), "빌드 경로는 절대 경로여야 한다");
        Assert.AreEqual("ait-build", Path.GetFileName(buildPath),
            "빌드 경로의 마지막 세그먼트는 'ait-build'여야 한다");
    }
}
