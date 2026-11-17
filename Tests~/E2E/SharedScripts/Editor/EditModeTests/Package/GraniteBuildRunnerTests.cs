// -----------------------------------------------------------------------
// GraniteBuildRunnerTests.cs - GraniteBuildRunner 단위 테스트
// Level 0: GetWebFrameworkMajor 버전 파싱 로직 회귀 방지
//   (Sentry APPS-IN-TOSS-UNITY-SDK-10X 참조 — .ait 빌드 파이프라인 주변 커버리지 추가)
//
// GetWebFrameworkMajor 는 package.json 에서 @apps-in-toss/web-framework 버전의
// 메이저 번호를 파싱한다. PackageBuildStateMarker 가 메이저 변경 감지에 사용하므로
// 파싱 오류는 빌드 스킵 판정 오류로 이어질 수 있다. 파싱 불가 시 0(알 수 없음)을 반환한다.
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
        // 파일 없음/파싱 불가 시 기본값 0 반환
        // =====================================================

        [Test]
        public void GetWebFrameworkMajor_NoPackageJson_Returns0()
        {
            int major = GraniteBuildRunner.GetWebFrameworkMajor(tempDir);
            Assert.AreEqual(0, major,
                "package.json 부재 시 0(알 수 없음)을 반환해야 한다");
        }

        [Test]
        public void GetWebFrameworkMajor_NoDependenciesKey_Returns0()
        {
            WritePackageJson(@"{""name"": ""test-app"", ""version"": ""1.0.0""}");
            int major = GraniteBuildRunner.GetWebFrameworkMajor(tempDir);
            Assert.AreEqual(0, major);
        }

        [Test]
        public void GetWebFrameworkMajor_NoWebFrameworkDep_Returns0()
        {
            WritePackageJson(@"{""dependencies"": {""some-other-package"": ""^1.0.0""}}");
            int major = GraniteBuildRunner.GetWebFrameworkMajor(tempDir);
            Assert.AreEqual(0, major);
        }

        [Test]
        public void GetWebFrameworkMajor_EmptyVersionString_Returns0()
        {
            WritePackageJson(
                @"{""dependencies"": {""@apps-in-toss/web-framework"": """"}}");
            int major = GraniteBuildRunner.GetWebFrameworkMajor(tempDir);
            Assert.AreEqual(0, major);
        }

        [Test]
        public void GetWebFrameworkMajor_InvalidJson_Returns0()
        {
            WritePackageJson("not valid json {{{");
            int major = GraniteBuildRunner.GetWebFrameworkMajor(tempDir);
            Assert.AreEqual(0, major,
                "JSON 파싱 실패 시 0(알 수 없음)을 반환해야 한다");
        }

        // =====================================================
        // 3.x 버전
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
                "선행 숫자만 메이저로 파싱해야 한다");
        }
    }
}
