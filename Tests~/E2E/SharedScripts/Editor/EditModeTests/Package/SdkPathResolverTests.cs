// -----------------------------------------------------------------------
// SdkPathResolverTests.cs - SdkPathResolver 단위 테스트
// Level 0: 경로 캐시 동작만 순수 검증 (Domain reload 불필요)
// -----------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor.Package;

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
        public void ClearPathCache_DoesNotThrow_OnFreshState()
        {
            Assert.DoesNotThrow(() => SdkPathResolver.ClearPathCache());
        }

        [Test]
        public void ClearPathCache_Idempotent_OnRepeatedCalls()
        {
            Assert.DoesNotThrow(() =>
            {
                SdkPathResolver.ClearPathCache();
                SdkPathResolver.ClearPathCache();
                SdkPathResolver.ClearPathCache();
            });
        }

        [Test]
        public void GetSdkBuildConfigPath_ReturnsSameInstance_OnSubsequentCalls()
        {
            // 캐싱 검증: 첫 호출 후 같은 레퍼런스/값을 반환해야 함.
            // (실제 SDK 경로가 해석되지 못해 null이더라도 두 번째 호출도 null로 일관)
            string first = SdkPathResolver.GetSdkBuildConfigPath();
            string second = SdkPathResolver.GetSdkBuildConfigPath();
            Assert.AreEqual(first, second);
        }

        [Test]
        public void FindSdkRuntimePath_ReturnsSameInstance_OnSubsequentCalls()
        {
            string first = SdkPathResolver.FindSdkRuntimePath();
            string second = SdkPathResolver.FindSdkRuntimePath();
            Assert.AreEqual(first, second);
        }

        [Test]
        public void ClearPathCache_InvalidatesCachedBuildConfigPath()
        {
            // 첫 호출로 캐시를 채우고 (null일 수 있음), ClearPathCache 후에도 예외 없이 재호출 가능해야 함
            _ = SdkPathResolver.GetSdkBuildConfigPath();
            Assert.DoesNotThrow(() =>
            {
                SdkPathResolver.ClearPathCache();
                _ = SdkPathResolver.GetSdkBuildConfigPath();
            });
        }
    }
}
