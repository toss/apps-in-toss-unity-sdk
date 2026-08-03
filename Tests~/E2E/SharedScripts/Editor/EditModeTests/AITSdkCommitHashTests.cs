// -----------------------------------------------------------------------
// AITSdkCommitHashTests.cs - .ait 메타데이터 sdkCommitHash 회귀 가드
// Level 0: 필드 존재 + 로컬 폴백 계약(throw 금지, 빈 문자열 금지, hex 형식)
//          검증 (빌드/파일시스템 비의존, git 프로세스 호출은 환경 의존이므로 계약만 확인)
// -----------------------------------------------------------------------

using NUnit.Framework;
using System.Text.RegularExpressions;
using AppsInToss.Editor;

[TestFixture]
public class AITSdkCommitHashTests
{
    // 필드 존재 회귀 가드: 값과 무관하게 sdkCommitHash 키는 항상 나온다.
    [Test]
    public void UnityMetadataJson_ContainsSdkCommitHashKey()
    {
        string json = AITUnityMetadata.BuildMetadataJson();
        StringAssert.Contains("\"sdkCommitHash\":", json);
    }

    // granite로 전달되는 환경변수 딕셔너리에도 동일하게 실린다.
    [Test]
    public void UnityMetadataEnv_CarriesSdkCommitHashKey()
    {
        var env = AITUnityMetadata.BuildEnvironmentVariables();
        Assert.IsTrue(env.ContainsKey("UNITY_METADATA"));
        StringAssert.Contains("\"sdkCommitHash\":", env["UNITY_METADATA"]);
    }

    // 로컬 폴백 계약: 예외를 던지지 않는다(git 부재/실패해도 안전).
    [Test]
    public void LocalResolver_NeverThrows()
    {
        Assert.DoesNotThrow(() => AITSdkCommitResolver.TryResolveLocalGitCommitHash());
    }

    // 로컬 폴백 계약: null 또는 hex 문자열. 빈 문자열("")은 절대 반환하지 않는다.
    // (호출부 ResolveSdkCommitHash의 "?? \"\"" 이중 폴백이 정상 동작하려면 이 계약이 필요)
    [Test]
    public void LocalResolver_ReturnsNullOrHexNeverEmpty()
    {
        string hash = AITSdkCommitResolver.TryResolveLocalGitCommitHash();
        if (hash != null)
        {
            Assert.IsNotEmpty(hash);
            Assert.IsTrue(Regex.IsMatch(hash, "^[0-9a-f]{7,40}$"),
                $"기대: 7~40자리 hex, 실제: '{hash}'");
        }
    }
}
