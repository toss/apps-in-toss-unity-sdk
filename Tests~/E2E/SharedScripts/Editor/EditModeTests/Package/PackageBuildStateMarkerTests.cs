// -----------------------------------------------------------------------
// PackageBuildStateMarkerTests.cs - 패키징(vite/ait build) 스킵 판정 마커 단위 테스트
// Level 0: 임시 디렉토리 fixture로 판정 로직만 확인 (pnpm/vite/ait 실행 없음)
// 판정이 잘못되면 스테일 .ait가 그대로 배포될 수 있어 — fail-closed 동작을 집중 검증한다.
// (PnpmInstallStateMarkerTests.cs와 동일한 스타일/패턴을 따름)
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace AppsInToss.Editor.Package.Tests
{
    [TestFixture]
    public class PackageBuildStateMarkerTests
    {
        private const string WebFrameworkVersion = "3.0.3";
        private const int WebFrameworkMajor = 3;

        private string _tempDir;

        [SetUp]
        public void CreateTempDir()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "ait-package-marker-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void CleanupTempDir()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        // =====================================================
        // fixture 헬퍼
        // =====================================================

        private void WriteFile(string relPath, string content)
        {
            string fullPath = Path.Combine(_tempDir, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, content);
        }

        /// <summary>
        /// 스킵 가능한 정상 상태 fixture: package.json(웹프레임워크 3.x) + 설정 파일들 +
        /// public/ 산출물 + dist/*.ait + 성공 마커.
        /// </summary>
        private void CreateValidBuiltState()
        {
            WriteFile("package.json", "{\"dependencies\":{\"@apps-in-toss/web-framework\":\"" + WebFrameworkVersion + "\"}}");
            WriteFile("pnpm-lock.yaml", "lockfileVersion: '9.0'\n");
            WriteFile("vite.config.ts", "export default {};\n");
            WriteFile("apps-in-toss.config.ts", "export default { appName: 'test' };\n");
            WriteFile("index.html", "<html><body>unity</body></html>");

            WriteFile("public/Build/game.wasm", "wasm-bytes-placeholder");
            WriteFile("public/TemplateData/style.css", "body{}");

            WriteFile("dist/output.ait", "ait-archive-placeholder");

            PackageBuildStateMarker.RecordSuccessfulBuild(_tempDir, WebFrameworkMajor);
        }

        // =====================================================
        // 마커 없음 → 스킵 불가
        // =====================================================

        [Test]
        public void ShouldSkipPackageBuild_ReturnsFalse_WhenMarkerMissing()
        {
            // RecordSuccessfulBuild를 호출하지 않은 상태 — public/config/dist는 만들어도 마커가 없음
            WriteFile("package.json", "{\"dependencies\":{\"@apps-in-toss/web-framework\":\"" + WebFrameworkVersion + "\"}}");
            WriteFile("public/Build/game.wasm", "wasm-bytes-placeholder");
            WriteFile("dist/output.ait", "ait-archive-placeholder");

            Assert.IsFalse(PackageBuildStateMarker.ShouldSkipPackageBuild(_tempDir, out _));
        }

        // =====================================================
        // 기록 후 무변경 → 스킵 가능
        // =====================================================

        [Test]
        public void RecordSuccessfulBuild_ThenShouldSkipPackageBuild_RoundTrips()
        {
            CreateValidBuiltState();

            bool skip = PackageBuildStateMarker.ShouldSkipPackageBuild(_tempDir, out string reason);

            Assert.IsTrue(skip);
            Assert.IsNotNull(reason);
        }

        [Test]
        public void ShouldSkipPackageBuild_MarkerFileExists_AtExpectedPath()
        {
            CreateValidBuiltState();

            string markerPath = Path.Combine(_tempDir, PackageBuildStateMarker.MarkerFileName);
            Assert.IsTrue(File.Exists(markerPath));
        }

        // =====================================================
        // public/ 변경 → 무효화
        // =====================================================

        [Test]
        public void ShouldSkipPackageBuild_ReturnsFalse_WhenPublicFileContentAndSizeChanged()
        {
            CreateValidBuiltState();

            // 크기가 다른 내용으로 덮어쓰면 mtime도 함께 전진한다 (실제 CopyFileIfChanged 재복사와 동일)
            WriteFile("public/Build/game.wasm", "completely-different-and-longer-wasm-bytes-placeholder");

            Assert.IsFalse(PackageBuildStateMarker.ShouldSkipPackageBuild(_tempDir, out _));
        }

        [Test]
        public void ShouldSkipPackageBuild_ReturnsFalse_WhenPublicFileMtimeChangedOnly()
        {
            // 크기는 동일하되 mtime만 미래로 전진한 경우 — CopyFileIfChanged가 재복사를 수행한
            // 상황(내용이 달라져 다시 쓰였지만 우연히 길이는 같은 경우)을 흉내낸다.
            CreateValidBuiltState();

            string filePath = Path.Combine(_tempDir, "public", "Build", "game.wasm");
            File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddDays(1));

            Assert.IsFalse(PackageBuildStateMarker.ShouldSkipPackageBuild(_tempDir, out _));
        }

        [Test]
        public void ShouldSkipPackageBuild_ReturnsFalse_WhenNewFileAddedToPublic()
        {
            CreateValidBuiltState();

            WriteFile("public/Runtime/new-file.js", "console.log('new');");

            Assert.IsFalse(PackageBuildStateMarker.ShouldSkipPackageBuild(_tempDir, out _));
        }

        [Test]
        public void ShouldSkipPackageBuild_ReturnsFalse_WhenFileRemovedFromPublic()
        {
            CreateValidBuiltState();

            File.Delete(Path.Combine(_tempDir, "public", "TemplateData", "style.css"));

            Assert.IsFalse(PackageBuildStateMarker.ShouldSkipPackageBuild(_tempDir, out _));
        }

        // =====================================================
        // config 파일 변경 → 무효화
        // =====================================================

        [Test]
        public void ShouldSkipPackageBuild_ReturnsFalse_WhenPackageJsonContentChanged()
        {
            CreateValidBuiltState();

            WriteFile("package.json", "{\"dependencies\":{\"@apps-in-toss/web-framework\":\"" + WebFrameworkVersion + "\",\"extra\":\"1.0.0\"}}");

            Assert.IsFalse(PackageBuildStateMarker.ShouldSkipPackageBuild(_tempDir, out _));
        }

        [Test]
        public void ShouldSkipPackageBuild_ReturnsFalse_WhenViteConfigChanged()
        {
            CreateValidBuiltState();

            WriteFile("vite.config.ts", "export default { build: { minify: false } };\n");

            Assert.IsFalse(PackageBuildStateMarker.ShouldSkipPackageBuild(_tempDir, out _));
        }

        [Test]
        public void ShouldSkipPackageBuild_ReturnsFalse_WhenIndexHtmlChanged()
        {
            // index.html은 public/ 밖(루트)에 있고 매 빌드 무조건 재작성되므로 mtime 트릭이 아닌
            // 내용 해시(configFilesHash)로 커버된다 — Configuration/PlayerSettings 값 변경이
            // index.html 내용에 반영되면 이 경로로 감지되어야 한다.
            CreateValidBuiltState();

            WriteFile("index.html", "<html><body>changed-display-name</body></html>");

            Assert.IsFalse(PackageBuildStateMarker.ShouldSkipPackageBuild(_tempDir, out _));
        }

        [Test]
        public void ShouldSkipPackageBuild_ReturnsFalse_WhenTrackedConfigFileNewlyAdded()
        {
            // 처음엔 존재하지 않던(=absent로 해시됨) 추적 대상 파일이 새로 생기는 경우도 감지되어야 함
            CreateValidBuiltState();

            WriteFile("granite.config.ts", "export default {};\n");

            Assert.IsFalse(PackageBuildStateMarker.ShouldSkipPackageBuild(_tempDir, out _));
        }

        [Test]
        public void ShouldSkipPackageBuild_ReturnsFalse_WhenTrackedConfigFileRemoved()
        {
            CreateValidBuiltState();

            File.Delete(Path.Combine(_tempDir, "apps-in-toss.config.ts"));

            Assert.IsFalse(PackageBuildStateMarker.ShouldSkipPackageBuild(_tempDir, out _));
        }

        [Test]
        public void ShouldSkipPackageBuild_ReturnsFalse_WhenSrcDirectoryFileAdded()
        {
            // 현재 템플릿은 ait-build/src/ 를 쓰지 않지만, 향후를 위해 존재 시 전체 파일을
            // 추적한다 — 새로 생기면 감지되어야 함.
            CreateValidBuiltState();

            WriteFile("src/unity-bridge.ts", "export function bridge() {}\n");

            Assert.IsFalse(PackageBuildStateMarker.ShouldSkipPackageBuild(_tempDir, out _));
        }

        // =====================================================
        // webFrameworkMajor 불일치 → 무효화 (해시가 전부 일치해도 별도 게이트)
        // =====================================================

        [Test]
        public void ShouldSkipPackageBuild_ReturnsFalse_WhenWebFrameworkMajorMismatch()
        {
            CreateValidBuiltState();

            // 해시는 실제 파일과 정확히 일치시키되 webFrameworkMajor만 조작 — 필드 자체의
            // 독립적인 게이트 역할을 검증 (PnpmInstallStateMarkerTests의 schemaVersion 패턴과 동일)
            var marker = new Dictionary<string, object>
            {
                { "schemaVersion", PackageBuildStateMarker.SchemaVersion },
                { "publicManifestHash", PackageBuildStateMarker.ComputePublicManifestHash(_tempDir) },
                { "configFilesHash", PackageBuildStateMarker.ComputeConfigFilesHash(_tempDir) },
                { "webFrameworkMajor", WebFrameworkMajor + 1 },
            };
            File.WriteAllText(PackageBuildStateMarker.GetMarkerPath(_tempDir), MiniJson.Serialize(marker));

            Assert.IsFalse(PackageBuildStateMarker.ShouldSkipPackageBuild(_tempDir, out _));
        }

        [Test]
        public void ShouldSkipPackageBuild_ReturnsFalse_WhenSchemaVersionMismatch()
        {
            CreateValidBuiltState();

            var marker = new Dictionary<string, object>
            {
                { "schemaVersion", PackageBuildStateMarker.SchemaVersion + 998 },
                { "publicManifestHash", PackageBuildStateMarker.ComputePublicManifestHash(_tempDir) },
                { "configFilesHash", PackageBuildStateMarker.ComputeConfigFilesHash(_tempDir) },
                { "webFrameworkMajor", WebFrameworkMajor },
            };
            File.WriteAllText(PackageBuildStateMarker.GetMarkerPath(_tempDir), MiniJson.Serialize(marker));

            Assert.IsFalse(PackageBuildStateMarker.ShouldSkipPackageBuild(_tempDir, out _));
        }

        // =====================================================
        // dist/*.ait 부재 → 스킵 불가
        // =====================================================

        [Test]
        public void ShouldSkipPackageBuild_ReturnsFalse_WhenAitFileMissing()
        {
            CreateValidBuiltState();

            File.Delete(Path.Combine(_tempDir, "dist", "output.ait"));

            // dist/ 디렉토리는 남아있으므로 AITBuildValidator.ValidateDistOutput까지 도달하며,
            // 그 내부에서 진단용 Debug.LogError를 남긴다 — Unity Test Runner가 미처리 에러 로그를
            // 실패로 취급하므로 여기서 명시적으로 기대해준다.
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("\\.ait 파일이 생성되지 않았습니다"));

            Assert.IsFalse(PackageBuildStateMarker.ShouldSkipPackageBuild(_tempDir, out _));
        }

        [Test]
        public void ShouldSkipPackageBuild_ReturnsFalse_WhenDistDirectoryMissing()
        {
            CreateValidBuiltState();

            Directory.Delete(Path.Combine(_tempDir, "dist"), recursive: true);

            Assert.IsFalse(PackageBuildStateMarker.ShouldSkipPackageBuild(_tempDir, out _));
        }

        // =====================================================
        // 킬스위치 → 스킵 불가
        // =====================================================

        [TestCase("1")]
        [TestCase("true")]
        [TestCase("TRUE")]
        public void ShouldSkipPackageBuild_ReturnsFalse_WhenKillSwitchEnvVarSet(string value)
        {
            CreateValidBuiltState();
            Environment.SetEnvironmentVariable(PackageBuildStateMarker.KillSwitchEnvVar, value);
            try
            {
                Assert.IsFalse(PackageBuildStateMarker.ShouldSkipPackageBuild(_tempDir, out _));
            }
            finally
            {
                Environment.SetEnvironmentVariable(PackageBuildStateMarker.KillSwitchEnvVar, null);
            }
        }

        [TestCase("0")]
        [TestCase("false")]
        public void ShouldSkipPackageBuild_StillSkips_WhenKillSwitchExplicitlyOff(string value)
        {
            CreateValidBuiltState();
            Environment.SetEnvironmentVariable(PackageBuildStateMarker.KillSwitchEnvVar, value);
            try
            {
                Assert.IsTrue(PackageBuildStateMarker.ShouldSkipPackageBuild(_tempDir, out _));
            }
            finally
            {
                Environment.SetEnvironmentVariable(PackageBuildStateMarker.KillSwitchEnvVar, null);
            }
        }

        [Test]
        public void ShouldSkipPackageBuild_ReturnsFalse_AndWarns_WhenKillSwitchValueUnrecognized()
        {
            CreateValidBuiltState();
            Environment.SetEnvironmentVariable(PackageBuildStateMarker.KillSwitchEnvVar, "yes");
            try
            {
                UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning,
                    new System.Text.RegularExpressions.Regex("인식할 수 없어 킬스위치 활성"));
                Assert.IsFalse(PackageBuildStateMarker.ShouldSkipPackageBuild(_tempDir, out _));
            }
            finally
            {
                Environment.SetEnvironmentVariable(PackageBuildStateMarker.KillSwitchEnvVar, null);
            }
        }

        [TestCase(null, false)]
        [TestCase("", false)]
        [TestCase("   ", false)]
        [TestCase("0", false)]
        [TestCase("false", false)]
        [TestCase("1", true)]
        [TestCase("true", true)]
        public void IsKillSwitchActive_ParsesRecognizedValues(string value, bool expected)
        {
            Assert.AreEqual(expected, PackageBuildStateMarker.IsKillSwitchActive(value));
        }

        // =====================================================
        // 마커 손상 → 스킵 불가 (fail-closed)
        // =====================================================

        [Test]
        public void ShouldSkipPackageBuild_ReturnsFalse_WhenMarkerJsonCorrupted()
        {
            CreateValidBuiltState();
            File.WriteAllText(PackageBuildStateMarker.GetMarkerPath(_tempDir), "{not-valid-json!!");

            Assert.IsFalse(PackageBuildStateMarker.ShouldSkipPackageBuild(_tempDir, out _));
        }

        [Test]
        public void ShouldSkipPackageBuild_ReturnsFalse_WhenMarkerIsEmptyFile()
        {
            CreateValidBuiltState();
            File.WriteAllText(PackageBuildStateMarker.GetMarkerPath(_tempDir), string.Empty);

            Assert.IsFalse(PackageBuildStateMarker.ShouldSkipPackageBuild(_tempDir, out _));
        }

        [Test]
        public void ShouldSkipPackageBuild_ReturnsFalse_WhenMarkerMissingRequiredField()
        {
            CreateValidBuiltState();

            // publicManifestHash 필드 자체가 없는 마커 (구버전 스키마 등)
            var marker = new Dictionary<string, object>
            {
                { "schemaVersion", PackageBuildStateMarker.SchemaVersion },
                { "configFilesHash", PackageBuildStateMarker.ComputeConfigFilesHash(_tempDir) },
                { "webFrameworkMajor", WebFrameworkMajor },
            };
            File.WriteAllText(PackageBuildStateMarker.GetMarkerPath(_tempDir), MiniJson.Serialize(marker));

            Assert.IsFalse(PackageBuildStateMarker.ShouldSkipPackageBuild(_tempDir, out _));
        }

        // =====================================================
        // InvalidateMarker: 실패 대비 무효화
        // =====================================================

        [Test]
        public void InvalidateMarker_RemovesMarkerFile()
        {
            CreateValidBuiltState();

            PackageBuildStateMarker.InvalidateMarker(_tempDir);

            Assert.IsFalse(File.Exists(PackageBuildStateMarker.GetMarkerPath(_tempDir)));
            Assert.IsFalse(PackageBuildStateMarker.ShouldSkipPackageBuild(_tempDir, out _));
        }

        [Test]
        public void InvalidateMarker_DoesNotThrow_WhenMarkerAbsent()
        {
            Assert.DoesNotThrow(() => PackageBuildStateMarker.InvalidateMarker(_tempDir));
        }

        // =====================================================
        // 해시 형식/안정성
        // =====================================================

        [Test]
        public void ComputePublicManifestHash_IsStable_ForUnchangedTree()
        {
            CreateValidBuiltState();

            string hash1 = PackageBuildStateMarker.ComputePublicManifestHash(_tempDir);
            string hash2 = PackageBuildStateMarker.ComputePublicManifestHash(_tempDir);

            Assert.AreEqual(hash1, hash2);
            StringAssert.StartsWith("sha256:", hash1);
        }

        [Test]
        public void ComputePublicManifestHash_EmptyWhenPublicMissing_DoesNotThrow()
        {
            // public/ 자체가 없는 비정상 상태에서도 예외 없이 안정적인 해시를 반환해야 한다
            // (dist 존재 검사가 별도로 스킵을 막으므로 이 메서드 자체는 fail-closed일 필요 없음)
            string hash = null;
            Assert.DoesNotThrow(() => hash = PackageBuildStateMarker.ComputePublicManifestHash(_tempDir));
            StringAssert.StartsWith("sha256:", hash);
        }

        [Test]
        public void ComputeConfigFilesHash_IsStable_ForUnchangedFiles()
        {
            CreateValidBuiltState();

            string hash1 = PackageBuildStateMarker.ComputeConfigFilesHash(_tempDir);
            string hash2 = PackageBuildStateMarker.ComputeConfigFilesHash(_tempDir);

            Assert.AreEqual(hash1, hash2);
            StringAssert.StartsWith("sha256:", hash1);
        }

        [Test]
        public void ComputeConfigFilesHash_DiffersFromAbsentFilesState()
        {
            // 아무 config 파일도 없는 상태(전부 absent)와 실제 파일이 있는 상태의 해시는 달라야 한다
            string emptyStateHash = PackageBuildStateMarker.ComputeConfigFilesHash(_tempDir);

            WriteFile("package.json", "{}");
            string withFileHash = PackageBuildStateMarker.ComputeConfigFilesHash(_tempDir);

            Assert.AreNotEqual(emptyStateHash, withFileHash);
        }

        // =====================================================
        // RecordSuccessfulBuild
        // =====================================================

        [Test]
        public void RecordSuccessfulBuild_OverwritesStaleMarker()
        {
            CreateValidBuiltState();
            string firstMarkerContent = File.ReadAllText(PackageBuildStateMarker.GetMarkerPath(_tempDir));

            // 내용을 바꾸고 다시 성공 기록 — 새 상태와 일치하는 마커로 갱신되어야 한다
            WriteFile("package.json", "{\"dependencies\":{\"@apps-in-toss/web-framework\":\"" + WebFrameworkVersion + "\",\"x\":\"1\"}}");
            PackageBuildStateMarker.RecordSuccessfulBuild(_tempDir, WebFrameworkMajor);

            Assert.IsTrue(PackageBuildStateMarker.ShouldSkipPackageBuild(_tempDir, out _));

            string secondMarkerContent = File.ReadAllText(PackageBuildStateMarker.GetMarkerPath(_tempDir));
            Assert.AreNotEqual(firstMarkerContent, secondMarkerContent);
        }

        [Test]
        public void RecordSuccessfulBuild_DoesNotThrow_WhenDirectoriesMissing()
        {
            Assert.DoesNotThrow(() => PackageBuildStateMarker.RecordSuccessfulBuild(_tempDir, WebFrameworkMajor));
        }
    }
}
