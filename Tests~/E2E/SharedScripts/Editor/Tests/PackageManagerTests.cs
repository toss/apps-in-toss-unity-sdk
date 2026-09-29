using NUnit.Framework;
using AppsInToss.Editor;

namespace AppsInToss.Editor.Tests
{
    [TestFixture]
    public class PackageManagerTests
    {
        // === FindPackageManager 테스트 ===

        [Test]
        public void FindPackageManager_ReturnsNonNull_WhenEmbeddedNodeExists()
        {
            // 내장 Node.js가 설치된 환경에서 pnpm 또는 npm 경로를 반환해야 함
            string result = AITPackageManagerHelper.FindPackageManager(verbose: false);
            Assert.IsNotNull(result, "내장 Node.js가 있으면 패키지 매니저 경로를 반환해야 합니다");
        }

        [Test]
        public void FindPackageManager_ReturnsPnpmOrNpm()
        {
            string result = AITPackageManagerHelper.FindPackageManager(verbose: false);
            if (result != null)
            {
                string fileName = System.IO.Path.GetFileNameWithoutExtension(result);
                Assert.That(fileName, Is.EqualTo("pnpm").Or.EqualTo("npm"),
                    $"패키지 매니저는 pnpm 또는 npm이어야 합니다: {result}");
            }
        }

        [Test]
        public void FindPackageManager_ReturnsEmbeddedPath_NotSystemPath()
        {
            // 시스템 pnpm이 아닌 내장 경로를 반환해야 함
            string result = AITPackageManagerHelper.FindPackageManager(verbose: false);
            if (result != null)
            {
                Assert.That(result, Does.Contain(".ait-unity-sdk").Or.Contain("ait-unity-sdk"),
                    $"내장 Node.js 경로를 반환해야 합니다 (시스템 경로 아님): {result}");
            }
        }

        // === EmbeddedNodeBinPath 테스트 ===

        [Test]
        public void GetEmbeddedNodeBinPath_IsSetAfterFindPackageManager()
        {
            AITPackageManagerHelper.FindPackageManager(verbose: false);
            string binPath = AITPackageManagerHelper.GetEmbeddedNodeBinPath();
            Assert.IsNotNull(binPath, "FindPackageManager 호출 후 EmbeddedNodeBinPath가 설정되어야 합니다");
        }

        [Test]
        public void GetEmbeddedNodeBinPath_ContainsNodeExecutable()
        {
            AITPackageManagerHelper.FindPackageManager(verbose: false);
            string binPath = AITPackageManagerHelper.GetEmbeddedNodeBinPath();
            if (binPath != null)
            {
                string nodeExe = AITPlatformHelper.IsWindows ? "node.exe" : "node";
                string nodePath = System.IO.Path.Combine(binPath, nodeExe);
                Assert.IsTrue(System.IO.File.Exists(nodePath),
                    $"내장 Node.js bin 경로에 node 실행 파일이 있어야 합니다: {nodePath}");
            }
        }
    }

    [TestFixture]
    public class NpmRunnerTests
    {
        // === FindPnpmPath 테스트 ===

        [Test]
        public void FindPnpmPath_ReturnsEmbeddedPnpm_WhenAvailable()
        {
            // FindPackageManager를 먼저 호출하여 내장 Node.js 초기화
            AITPackageManagerHelper.FindPackageManager(verbose: false);

            string pnpmPath = AITNpmRunner.FindPnpmPath();
            if (pnpmPath != null)
            {
                // 시스템 pnpm이 아닌 내장 pnpm이어야 함
                Assert.That(pnpmPath, Does.Contain(".ait-unity-sdk").Or.Contain("ait-unity-sdk"),
                    $"내장 pnpm 경로를 반환해야 합니다 (시스템 pnpm 아님): {pnpmPath}");
            }
        }

        [Test]
        public void FindPnpmPath_NeverReturnsSystemPnpm()
        {
            // 시스템 pnpm fallback이 제거되었는지 확인
            AITPackageManagerHelper.FindPackageManager(verbose: false);

            string pnpmPath = AITNpmRunner.FindPnpmPath();
            if (pnpmPath != null)
            {
                // /usr/local/bin/pnpm 같은 시스템 경로가 아니어야 함
                Assert.That(pnpmPath, Does.Not.StartWith("/usr/local"),
                    $"시스템 pnpm을 반환하면 안 됩니다: {pnpmPath}");
                Assert.That(pnpmPath, Does.Not.StartWith("/opt"),
                    $"시스템 pnpm을 반환하면 안 됩니다: {pnpmPath}");
                // Homebrew 경로도 제외
                Assert.That(pnpmPath, Does.Not.Contain("/homebrew/"),
                    $"Homebrew pnpm을 반환하면 안 됩니다: {pnpmPath}");
            }
        }

        [Test]
        public void FindPnpmPath_VersionMatchesPNPM_VERSION()
        {
            AITPackageManagerHelper.FindPackageManager(verbose: false);
            string pnpmPath = AITNpmRunner.FindPnpmPath();

            if (pnpmPath != null)
            {
                string embeddedBinPath = AITPackageManagerHelper.GetEmbeddedNodeBinPath();
                string version = AITNpmRunner.GetPnpmVersion(pnpmPath, embeddedBinPath ?? ".");
                Assert.IsNotNull(version, "pnpm 버전을 확인할 수 있어야 합니다");
                Assert.AreEqual(AITPackageManagerHelper.PNPM_VERSION, version,
                    $"pnpm 버전이 PNPM_VERSION과 일치해야 합니다: expected={AITPackageManagerHelper.PNPM_VERSION}, actual={version}");
            }
        }
    }

    [TestFixture]
    public class NodeJSDownloaderTests
    {
        [Test]
        public void FindEmbeddedNpm_ReturnsValidPath()
        {
            string npmPath = AITNodeJSDownloader.FindEmbeddedNpm(autoDownload: false);
            // autoDownload: false로 호출하면 이미 설치된 경우만 반환
            if (npmPath != null)
            {
                Assert.IsTrue(System.IO.File.Exists(npmPath),
                    $"반환된 npm 경로가 실제로 존재해야 합니다: {npmPath}");
            }
        }

        [Test]
        public void FindEmbeddedNpm_PathContainsEmbeddedDirectory()
        {
            string npmPath = AITNodeJSDownloader.FindEmbeddedNpm(autoDownload: false);
            if (npmPath != null)
            {
                Assert.That(npmPath, Does.Contain(".ait-unity-sdk"),
                    $"내장 npm 경로에 .ait-unity-sdk가 포함되어야 합니다: {npmPath}");
            }
        }

        [Test]
        public void PNPM_VERSION_IsValidSemver()
        {
            string version = AITPackageManagerHelper.PNPM_VERSION;
            Assert.IsNotNull(version);
            Assert.IsNotEmpty(version);
            // semver 형식 확인 (예: 10.28.0)
            Assert.That(version, Does.Match(@"^\d+\.\d+\.\d+$"),
                $"PNPM_VERSION이 semver 형식이어야 합니다: {version}");
        }
    }
}
