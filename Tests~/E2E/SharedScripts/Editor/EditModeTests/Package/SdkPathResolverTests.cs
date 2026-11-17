// -----------------------------------------------------------------------
// SdkPathResolverTests.cs - SdkPathResolver 단위 테스트
// Level 0: 경로 캐시 동작만 순수 검증 (Domain reload 불필요)
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AppsInToss.Editor.Package.Tests
{
    [TestFixture]
    public class SdkPathResolverTests
    {
        [SetUp]
        public void ResetCacheBeforeEachTest()
        {
            SdkPathResolver.ClearPathCache();
        }

        [Test]
        public void GetSdkBuildConfigPath_ReturnsEqualValue_OnSubsequentCalls()
        {
            // 캐싱 검증: 첫 호출 후 같은 값을 반환해야 함.
            // (실제 SDK 경로가 해석되지 못해 null이더라도 두 번째 호출도 null로 일관)
            string first = SdkPathResolver.GetSdkBuildConfigPath();
            string second = SdkPathResolver.GetSdkBuildConfigPath();
            Assert.AreEqual(first, second);
        }

        [Test]
        public void FindSdkRuntimePath_ReturnsEqualValue_OnSubsequentCalls()
        {
            string first = SdkPathResolver.FindSdkRuntimePath();
            string second = SdkPathResolver.FindSdkRuntimePath();
            Assert.AreEqual(first, second);
        }

        [Test]
        public void ClearPathCache_InvalidatesCachedBuildConfigPath()
        {
            // 첫 호출로 캐시 채움. 경로 해석 성공 시 "[AIT] SDK BuildConfig 경로 캐싱: ..." 로그가 찍힘.
            string first = SdkPathResolver.GetSdkBuildConfigPath();

            // 패키지 루트가 해석되지 않는 테스트 환경에서는 이 테스트를 inconclusive로 보고한다.
            // (캐시 무효화 의미를 검증할 수 없으므로 silent pass 보다 inconclusive가 더 정확한 신호)
            Assume.That(first, Is.Not.Null, "SDK BuildConfig 경로 해석 실패 — 이 환경에서는 캐시 무효화를 검증할 수 없음");

            // ClearPathCache 후 재호출하면 캐시 미스로 인해 같은 로그가 다시 찍혀야 함 (재해석 증명).
            // LogAssert.Expect 는 프레임 내에 해당 로그가 발생해야 성공하므로, 캐시 단락 시 실패한다.
            SdkPathResolver.ClearPathCache();
            LogAssert.Expect(LogType.Log, new Regex(@"\[AIT\] SDK BuildConfig 경로 캐싱: .+"));
            string second = SdkPathResolver.GetSdkBuildConfigPath();
            Assert.AreEqual(first, second);
        }
    }
}
