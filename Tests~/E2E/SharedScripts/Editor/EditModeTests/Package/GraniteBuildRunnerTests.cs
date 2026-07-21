// -----------------------------------------------------------------------
// GraniteBuildRunnerTests.cs - GraniteBuildRunner 단위 테스트
// Level 0: GetWebFrameworkMajor 버전 파싱 로직 회귀 방지
//   (Sentry APPS-IN-TOSS-UNITY-SDK-10X 참조 — .ait 빌드 파이프라인 주변 커버리지 추가)
//
// GetWebFrameworkMajor 는 package.json 에서 @apps-in-toss/web-framework 버전의
// 메이저 번호를 파싱한다. 버전에 따라 빌드 경로가 달라지므로 (2.x: granite build,
// 3.x: vite→ait build) 파싱 오류는 잘못된 빌드 경로로 이어질 수 있다.
// -----------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.IO;
using AppsInToss.Editor.Package;

namespace AppsInToss.Editor.Package.Tests
{
    [TestFixture]
    public class GraniteBuildRunnerTests
    {
        private string tempDir;

        [SetUp]
        public void Setup()
        {
            tempDir = Path.Combine(
                Path.GetTempPath(),
                "ait-test-granite-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }

        private void WritePackageJson(string content)
        {
            File.WriteAllText(Path.Combine(tempDir, "package.json"), content);
        }

        // =====================================================
        // 파일 없음/파싱 불가 시 기본값 2 반환 (보수적 폴백)
        // =====================================================

        [Test]
        public void GetWebFrameworkMajor_NoPackageJson_Returns2()
        {
            // package.json 이 없으면 2.x(granite) 경로로 폴백해야 한다
            int major = GraniteBuildRunner.GetWebFrameworkMajor(tempDir);
            Assert.AreEqual(2, major,
                "package.json 부재 시 보수적 기본값 2 를 반환해야 한다");
        }

        [Test]
        public void GetWebFrameworkMajor_NoDependenciesKey_Returns2()
        {
            WritePackageJson(@"{""name"": ""test-app"", ""version"": ""1.0.0""}");
            int major = GraniteBuildRunner.GetWebFrameworkMajor(tempDir);
            Assert.AreEqual(2, major);
        }

        [Test]
        public void GetWebFrameworkMajor_NoWebFrameworkDep_Returns2()
        {
            WritePackageJson(@"{""dependencies"": {""some-other-package"": ""^1.0.0""}}");
            int major = GraniteBuildRunner.GetWebFrameworkMajor(tempDir);
            Assert.AreEqual(2, major);
        }

        [Test]
        public void GetWebFrameworkMajor_EmptyVersionString_Returns2()
        {
            WritePackageJson(
                @"{""dependencies"": {""@apps-in-toss/web-framework"": """"}}");
            int major = GraniteBuildRunner.GetWebFrameworkMajor(tempDir);
            Assert.AreEqual(2, major);
        }

        [Test]
        public void GetWebFrameworkMajor_InvalidJson_Returns2()
        {
            WritePackageJson("not valid json {{{");
            int major = GraniteBuildRunner.GetWebFrameworkMajor(tempDir);
            Assert.AreEqual(2, major,
                "JSON 파싱 실패 시 보수적 기본값 2 를 반환해야 한다");
        }

        // =====================================================
        // 2.x 버전
        // =====================================================

        [Test]
        public void GetWebFrameworkMajor_Version2_WithCaret_Returns2()
        {
            WritePackageJson(
                @"{""dependencies"": {""@apps-in-toss/web-framework"": ""^2.6.1""}}");
            int major = GraniteBuildRunner.GetWebFrameworkMajor(tempDir);
            Assert.AreEqual(2, major);
        }

        [Test]
        public void GetWebFrameworkMajor_Version2_ExactVersion_Returns2()
        {
            WritePackageJson(
                @"{""dependencies"": {""@apps-in-toss/web-framework"": ""2.0.0""}}");
            int major = GraniteBuildRunner.GetWebFrameworkMajor(tempDir);
            Assert.AreEqual(2, major);
        }

        // =====================================================
        // 3.x 버전 — vite→ait build 경로로 전환되는 기준
        // =====================================================

        [Test]
        public void GetWebFrameworkMajor_Version3_WithCaret_Returns3()
        {
            WritePackageJson(
                @"{""dependencies"": {""@apps-in-toss/web-framework"": ""^3.0.0""}}");
            int major = GraniteBuildRunner.GetWebFrameworkMajor(tempDir);
            Assert.AreEqual(3, major,
                "3.x 버전은 vite build → ait build (2단계) 경로로 전환되므로 3 을 반환해야 한다");
        }

        [Test]
        public void GetWebFrameworkMajor_Version3_BetaPrerelease_Returns3()
        {
            // 프리릴리즈 버전: "3.0.0-beta.9d42c0b" → 메이저 3 추출
            WritePackageJson(
                @"{""dependencies"": {""@apps-in-toss/web-framework"": ""3.0.0-beta.9d42c0b""}}");
            int major = GraniteBuildRunner.GetWebFrameworkMajor(tempDir);
            Assert.AreEqual(3, major,
                "프리릴리즈 버전에서도 메이저 번호를 올바르게 추출해야 한다");
        }

        [Test]
        public void GetWebFrameworkMajor_Version3_WithTildeAndMinor_Returns3()
        {
            WritePackageJson(
                @"{""dependencies"": {""@apps-in-toss/web-framework"": ""~3.1.2""}}");
            int major = GraniteBuildRunner.GetWebFrameworkMajor(tempDir);
            Assert.AreEqual(3, major);
        }

        // =====================================================
        // 4+ 버전도 3.x 경로(major >= 3) 를 따름
        // =====================================================

        [Test]
        public void GetWebFrameworkMajor_Version4_Returns4()
        {
            WritePackageJson(
                @"{""dependencies"": {""@apps-in-toss/web-framework"": ""^4.0.0""}}");
            int major = GraniteBuildRunner.GetWebFrameworkMajor(tempDir);
            Assert.AreEqual(4, major,
                "4.x 이상도 major >= 3 조건으로 3.x 빌드 경로를 따라야 한다");
        }
    }
}
