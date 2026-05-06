// -----------------------------------------------------------------------
// BuildResultMappingTests.cs - BuildResult → AITExportError 매핑 회귀 테스트
// Level 0: BuildResult.Cancelled가 사용자 의사로 분류되는지 검증
//   (Sentry APPS-IN-TOSS-UNITY-SDK-8E: 사용자 빌드 취소가 SDK 실패로 잘못 보고되던 회귀 방지)
// -----------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss;
using UnityEditor.Build.Reporting;

[TestFixture]
public class BuildResultMappingTests
{
    [Test]
    public void Cancelled_MapsTo_CANCELLED_NotBuildFailure()
    {
        // 사용자가 빌드를 취소한 경우 SDK 결함이 아니다.
        // BUILD_WEBGL_FAILED로 매핑되어 Sentry 노이즈를 만들면 안 된다.
        Assert.AreEqual(
            AITConvertCore.AITExportError.CANCELLED,
            AITConvertCore.MapBuildResultToExportError(BuildResult.Cancelled));
    }

    [Test]
    public void Succeeded_MapsTo_SUCCEED()
    {
        Assert.AreEqual(
            AITConvertCore.AITExportError.SUCCEED,
            AITConvertCore.MapBuildResultToExportError(BuildResult.Succeeded));
    }

    [Test]
    public void Failed_MapsTo_BUILD_WEBGL_FAILED()
    {
        Assert.AreEqual(
            AITConvertCore.AITExportError.BUILD_WEBGL_FAILED,
            AITConvertCore.MapBuildResultToExportError(BuildResult.Failed));
    }

    [Test]
    public void Unknown_MapsTo_BUILD_WEBGL_FAILED()
    {
        // BuildResult.Unknown은 Unity가 빌드 결과를 정상적으로 보고하지 않은 비정상 상태.
        // SDK가 진단할 수 있는 추가 정보가 없으므로 일반 빌드 실패로 매핑한다.
        Assert.AreEqual(
            AITConvertCore.AITExportError.BUILD_WEBGL_FAILED,
            AITConvertCore.MapBuildResultToExportError(BuildResult.Unknown));
    }
}
