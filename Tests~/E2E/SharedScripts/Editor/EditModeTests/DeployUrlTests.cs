// -----------------------------------------------------------------------
// DeployUrlTests.cs - 배포 출력에서 intoss-private:// 딥링크 추출 검증
// Level 0: AITDeployManager.ExtractDeployUrl 을 Unity/pnpm 실행 없이 검증한다.
//
// 배경: ait CLI는 배포 URL을 고정폭 유니코드 박스(│ ... │) 안에 출력하므로 긴 URL
//   (UUID deploymentId, host 파라미터)은 여러 줄로 래핑된다. 줄 단위 정규식은 URL을
//   중간에서 자르므로 연속 줄 접합이 필요하다. 또한 SDK 3.0(V3 host) 딥링크는
//   host=appsInTossHost 파라미터가 없으면 V3로 출시된 적 없는 스킴에서 진입이 불가하다
//   (CDN deployment.json 부재) — CLI가 붙이지 않은 경우 추출 시점에 부가한다.
//
// 메모: 이 파일은 AppsInTossEditModeTests 어셈블리에 속한다(DeployMemoTests.cs와 동일 위치).
//   해당 어셈블리는 InternalsVisibleTo로 internal AITDeployManager에 접근 가능하다.
// -----------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor.Menu;  // AITDeployManager (internal, .Menu 하위 네임스페이스)

[TestFixture]
public class DeployUrlTests
{
    // =====================================================
    // 박스 래핑 접합
    // =====================================================

    [Test]
    public void ExtractDeployUrl_BoxWrappedUrl_JoinsContinuationLines()
    {
        // 실제 CI 배포 로그에서 관측된 형태: UUID가 박스 폭에서 잘려 다음 줄로 이어짐
        string output =
            "╭──────────────────────────────────────────────────────────────────────────────╮\n" +
            "│  intoss-private://unity-sdk-sample?_deploymentId=01a01868-f10b-7279-b96f-ab  │\n" +
            "│  bcd6865b68  │\n" +
            "╰──────────────────────────────────────────────────────────────────────────────╯\n";

        string url = AITDeployManager.ExtractDeployUrl(output);
        Assert.AreEqual(
            "intoss-private://unity-sdk-sample?_deploymentId=01a01868-f10b-7279-b96f-abbcd6865b68&host=appsInTossHost",
            url);
    }

    [Test]
    public void ExtractDeployUrl_HostParamWrappedToSecondLine_JoinsAndKeepsHost()
    {
        // 최신 ait CLI 출력 형태: CLI가 host 파라미터까지 붙이고 그 부분이 래핑됨
        string output =
            "│  intoss-private://ait?_deploymentId=01a018fc-a0e5-7558-9a3a-166fcf  │\n" +
            "│  e4e4e1&host=appsInTossHost  │\n";

        string url = AITDeployManager.ExtractDeployUrl(output);
        Assert.AreEqual(
            "intoss-private://ait?_deploymentId=01a018fc-a0e5-7558-9a3a-166fcfe4e4e1&host=appsInTossHost",
            url);
    }

    [Test]
    public void ExtractDeployUrl_UnwrappedUrlFollowedByText_DoesNotSwallowNextLine()
    {
        // URL이 줄 끝까지 닿지 않으면(래핑 아님) 다음 줄의 토큰을 이어붙이면 안 됨
        string output =
            "│  intoss-private://app?_deploymentId=0198c10b-68c3-7d2b-a0ab-2c9626b475ec 완료  │\n" +
            "│  SUCCESS  │\n";

        string url = AITDeployManager.ExtractDeployUrl(output);
        Assert.AreEqual(
            "intoss-private://app?_deploymentId=0198c10b-68c3-7d2b-a0ab-2c9626b475ec&host=appsInTossHost",
            url);
    }

    // =====================================================
    // SDK 3.0 host 파라미터 부가 (멱등)
    // =====================================================

    [Test]
    public void ExtractDeployUrl_HostAlreadyPresent_DoesNotDuplicate()
    {
        string output = "intoss-private://app?_deploymentId=0198c10b-68c3-7d2b-a0ab-2c9626b475ec&host=appsInTossHost\n";

        string url = AITDeployManager.ExtractDeployUrl(output);
        Assert.AreEqual(
            "intoss-private://app?_deploymentId=0198c10b-68c3-7d2b-a0ab-2c9626b475ec&host=appsInTossHost",
            url);
    }

    [Test]
    public void ExtractDeployUrl_NoQueryString_AppendsHostWithQuestionMark()
    {
        string url = AITDeployManager.ExtractDeployUrl("intoss-private://app\n");
        Assert.AreEqual("intoss-private://app?host=appsInTossHost", url);
    }

    // =====================================================
    // 경계 케이스
    // =====================================================

    [Test]
    public void ExtractDeployUrl_NoUrlInOutput_ReturnsNull()
    {
        Assert.IsNull(AITDeployManager.ExtractDeployUrl("배포 완료. URL 없음.\n"));
        Assert.IsNull(AITDeployManager.ExtractDeployUrl(""));
        Assert.IsNull(AITDeployManager.ExtractDeployUrl(null));
    }
}
