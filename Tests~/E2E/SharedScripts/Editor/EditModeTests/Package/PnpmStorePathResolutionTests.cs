// -----------------------------------------------------------------------
// PnpmStorePathResolutionTests.cs - 크로스 드라이브 pnpm store 해석 테스트
// Level 0: 순수 함수(ResolveStorePathForProject / GetWindowsDriveRoot)라
// 실행 OS와 무관하게 Windows 경로 문자열 판정을 검증할 수 있다.
// 판정이 잘못되면 (a) 크로스 드라이브에서 하드링크 대신 복사(원래 문제 재발)
// 또는 (b) 잘못된 위치에 store 생성 — 폴백(홈 store) 동작을 집중 검증한다.
// -----------------------------------------------------------------------

using System.IO;
using NUnit.Framework;

namespace AppsInToss.Editor.Package.Tests
{
    [TestFixture]
    public class PnpmStorePathResolutionTests
    {
        private const string HomeStoreC = @"C:\Users\dev\AppData\Local\.ait-unity-sdk\pnpm-store";
        private const string HomeStoreUnix = "/Users/dev/.ait-unity-sdk/pnpm-store";

        // =====================================================
        // GetWindowsDriveRoot: 드라이브 문자 파싱
        // =====================================================

        [TestCase(@"C:\Users\dev\project", "C:\\")]
        [TestCase(@"d:\game\ait-build", "D:\\")]
        [TestCase("D:/game/ait-build", "D:\\")]
        [TestCase("E:", "E:\\")]
        public void GetWindowsDriveRoot_ParsesDriveLetterPaths(string path, string expectedRoot)
        {
            Assert.AreEqual(expectedRoot, PnpmStoreManager.GetWindowsDriveRoot(path));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("/Users/dev/project")]
        [TestCase(@"\\server\share\project")]
        [TestCase("relative/path")]
        [TestCase("1:\\not-a-letter")]
        [TestCase("C")]
        public void GetWindowsDriveRoot_ReturnsNull_ForNonDrivePaths(string path)
        {
            Assert.IsNull(PnpmStoreManager.GetWindowsDriveRoot(path));
        }

        // =====================================================
        // ResolveStorePathForProject: 같은 드라이브/비드라이브 → 홈 store 유지
        // =====================================================

        [Test]
        public void Resolve_KeepsHomeStore_WhenSameDrive()
        {
            Assert.AreEqual(HomeStoreC,
                PnpmStoreManager.ResolveStorePathForProject(HomeStoreC, @"C:\game\ait-build"));
        }

        [Test]
        public void Resolve_KeepsHomeStore_WhenSameDrive_CaseInsensitive()
        {
            Assert.AreEqual(HomeStoreC,
                PnpmStoreManager.ResolveStorePathForProject(HomeStoreC, @"c:\game\ait-build"));
        }

        [Test]
        public void Resolve_KeepsHomeStore_ForUnixPaths()
        {
            Assert.AreEqual(HomeStoreUnix,
                PnpmStoreManager.ResolveStorePathForProject(HomeStoreUnix, "/Users/dev/game/ait-build"));
        }

        [Test]
        public void Resolve_KeepsHomeStore_WhenProjectPathIsNullOrEmpty()
        {
            Assert.AreEqual(HomeStoreC, PnpmStoreManager.ResolveStorePathForProject(HomeStoreC, null));
            Assert.AreEqual(HomeStoreC, PnpmStoreManager.ResolveStorePathForProject(HomeStoreC, ""));
        }

        [Test]
        public void Resolve_KeepsHomeStore_WhenProjectOnUncShare()
        {
            // 네트워크 공유 루트에 store 디렉토리를 만드는 것은 침습적 — 홈 store 유지
            Assert.AreEqual(HomeStoreC,
                PnpmStoreManager.ResolveStorePathForProject(HomeStoreC, @"\\nas\projects\game\ait-build"));
        }

        // =====================================================
        // ResolveStorePathForProject: 크로스 드라이브 → 드라이브별 store
        // =====================================================

        [Test]
        public void Resolve_UsesProjectDriveStore_WhenDifferentDrive()
        {
            Assert.AreEqual(@"D:\.ait-unity-sdk\pnpm-store",
                PnpmStoreManager.ResolveStorePathForProject(HomeStoreC, @"D:\game\ait-build"));
        }

        [Test]
        public void Resolve_UsesProjectDriveStore_WhenDifferentDrive_LowercaseAndForwardSlash()
        {
            Assert.AreEqual(@"D:\.ait-unity-sdk\pnpm-store",
                PnpmStoreManager.ResolveStorePathForProject(HomeStoreC, "d:/game/ait-build"));
        }

        // =====================================================
        // GetSharedPnpmStorePath(buildProjectPath): 현재 OS 실경로 통합 확인
        // =====================================================

        [Test]
        public void GetSharedPnpmStorePath_WithProjectPath_ReturnsPureResolutionOrHomeFallback()
        {
            // 실제 환경 통합 확인: 순수 해석 결과 또는 (드라이브 루트 쓰기 불가 시) 홈 store 폴백만 허용
            string homeStore = PnpmStoreManager.GetSharedPnpmStorePath();
            string projectPath = Path.Combine(Path.GetTempPath(), "ait-store-tests");
            string pure = PnpmStoreManager.ResolveStorePathForProject(homeStore, projectPath);

            string resolved = PnpmStoreManager.GetSharedPnpmStorePath(projectPath);

            Assert.That(resolved, Is.EqualTo(pure).Or.EqualTo(homeStore));
        }
    }
}
