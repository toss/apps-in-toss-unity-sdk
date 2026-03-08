// -----------------------------------------------------------------------
// BuildCleanupTests.cs - EditMode 빌드 결과물 정리 검증 테스트
// Level 0: PrepareAitBuildFolder 및 CopyWebGLToPublic의 정리 로직 검증
// -----------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AppsInToss.Editor;

[TestFixture]
public class BuildCleanupTests
{
    private string tempDir;
    private MethodInfo prepareMethod;

    [SetUp]
    public void Setup()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "ait-test-cleanup-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(tempDir);

        // PrepareAitBuildFolder은 private static이므로 리플렉션으로 접근
        var builderType = typeof(AITBuildValidator).Assembly
            .GetType("AppsInToss.Editor.AITPackageBuilder");
        Assert.IsNotNull(builderType, "AITPackageBuilder type should exist in AppsInTossSDKEditor assembly");

        prepareMethod = builderType.GetMethod("PrepareAitBuildFolder",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(prepareMethod, "PrepareAitBuildFolder method should exist");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, true);
        }
    }

    // =====================================================
    // PrepareAitBuildFolder: 빌드 결과물 삭제 검증
    // =====================================================

    [Test]
    public void PrepareAitBuildFolder_DeletesOldBuildArtifacts()
    {
        string buildPath = Path.Combine(tempDir, "ait-build");
        Directory.CreateDirectory(buildPath);

        // 이전 빌드 결과물 시뮬레이션
        Directory.CreateDirectory(Path.Combine(buildPath, "public"));
        Directory.CreateDirectory(Path.Combine(buildPath, "public", "Build"));
        File.WriteAllText(Path.Combine(buildPath, "public", "Build", "old.data"), "stale data");
        Directory.CreateDirectory(Path.Combine(buildPath, "dist"));
        File.WriteAllText(Path.Combine(buildPath, "dist", "index.html"), "stale dist");
        File.WriteAllText(Path.Combine(buildPath, "index.html"), "stale index");
        File.WriteAllText(Path.Combine(buildPath, "random-leftover.txt"), "leftover");

        prepareMethod.Invoke(null, new object[] { buildPath });

        Assert.IsFalse(Directory.Exists(Path.Combine(buildPath, "public")),
            "public/ should be deleted");
        Assert.IsFalse(Directory.Exists(Path.Combine(buildPath, "dist")),
            "dist/ should be deleted");
        Assert.IsFalse(File.Exists(Path.Combine(buildPath, "index.html")),
            "index.html should be deleted");
        Assert.IsFalse(File.Exists(Path.Combine(buildPath, "random-leftover.txt")),
            "Unrecognized files should be deleted");
    }

    // =====================================================
    // PrepareAitBuildFolder: node_modules/설정 파일 보존 검증
    // =====================================================

    [Test]
    public void PrepareAitBuildFolder_PreservesNodeModulesAndConfig()
    {
        string buildPath = Path.Combine(tempDir, "ait-build");
        Directory.CreateDirectory(buildPath);

        // 보존되어야 할 항목들
        Directory.CreateDirectory(Path.Combine(buildPath, "node_modules"));
        File.WriteAllText(Path.Combine(buildPath, "node_modules", "some-package.js"), "pkg");
        Directory.CreateDirectory(Path.Combine(buildPath, ".npm-cache"));
        File.WriteAllText(Path.Combine(buildPath, ".npm-cache", "cache-entry"), "cached");
        File.WriteAllText(Path.Combine(buildPath, "package.json"), "{}");
        File.WriteAllText(Path.Combine(buildPath, "package-lock.json"), "{}");
        File.WriteAllText(Path.Combine(buildPath, "pnpm-lock.yaml"), "lockfile: 1");
        File.WriteAllText(Path.Combine(buildPath, "granite.config.ts"), "export default {}");
        File.WriteAllText(Path.Combine(buildPath, "vite.config.ts"), "export default {}");
        File.WriteAllText(Path.Combine(buildPath, "tsconfig.json"), "{}");

        // 삭제되어야 할 항목도 추가
        File.WriteAllText(Path.Combine(buildPath, "index.html"), "stale");
        Directory.CreateDirectory(Path.Combine(buildPath, "public"));

        prepareMethod.Invoke(null, new object[] { buildPath });

        // 보존 확인
        Assert.IsTrue(Directory.Exists(Path.Combine(buildPath, "node_modules")),
            "node_modules/ should be preserved");
        Assert.IsTrue(File.Exists(Path.Combine(buildPath, "node_modules", "some-package.js")),
            "node_modules/ contents should be intact");
        Assert.IsTrue(Directory.Exists(Path.Combine(buildPath, ".npm-cache")),
            ".npm-cache/ should be preserved");
        Assert.IsTrue(File.Exists(Path.Combine(buildPath, "package.json")),
            "package.json should be preserved");
        Assert.IsTrue(File.Exists(Path.Combine(buildPath, "package-lock.json")),
            "package-lock.json should be preserved");
        Assert.IsTrue(File.Exists(Path.Combine(buildPath, "pnpm-lock.yaml")),
            "pnpm-lock.yaml should be preserved");
        Assert.IsTrue(File.Exists(Path.Combine(buildPath, "granite.config.ts")),
            "granite.config.ts should be preserved");
        Assert.IsTrue(File.Exists(Path.Combine(buildPath, "vite.config.ts")),
            "vite.config.ts should be preserved");
        Assert.IsTrue(File.Exists(Path.Combine(buildPath, "tsconfig.json")),
            "tsconfig.json should be preserved");

        // 삭제 확인
        Assert.IsFalse(File.Exists(Path.Combine(buildPath, "index.html")),
            "index.html should be deleted");
        Assert.IsFalse(Directory.Exists(Path.Combine(buildPath, "public")),
            "public/ should be deleted");
    }

    // =====================================================
    // PrepareAitBuildFolder: 폴더 미존재 시 생성
    // =====================================================

    [Test]
    public void PrepareAitBuildFolder_CreatesNewFolder_WhenNotExists()
    {
        string buildPath = Path.Combine(tempDir, "new-ait-build");
        Assert.IsFalse(Directory.Exists(buildPath), "Precondition: folder should not exist");

        prepareMethod.Invoke(null, new object[] { buildPath });

        Assert.IsTrue(Directory.Exists(buildPath),
            "PrepareAitBuildFolder should create the folder when it doesn't exist");
    }

    // =====================================================
    // CopyWebGLToPublic: 기존 Build 폴더 삭제 검증
    // CopyWebGLToPublic은 internal이지만 UnityUtil.GetEditorConf() 의존으로
    // 직접 호출이 어려우므로, 핵심 로직인 "Build 폴더 삭제 후 선별 복사"를
    // 파일시스템 수준에서 검증
    // =====================================================

    [Test]
    public void CopyWebGLToPublic_DeletesExistingBuildFolder()
    {
        // CopyWebGLToPublic의 핵심 정리 로직 (lines 820-825):
        //   if (Directory.Exists(buildDest)) AITFileUtils.DeleteDirectory(buildDest);
        //   Directory.CreateDirectory(buildDest);
        // 이를 직접 시뮬레이션하여 stale 파일이 제거되는 구조임을 검증

        string publicBuild = Path.Combine(tempDir, "public", "Build");
        Directory.CreateDirectory(publicBuild);

        // 이전 빌드의 stale 파일
        File.WriteAllText(Path.Combine(publicBuild, "old_build.data"), "stale data");
        File.WriteAllText(Path.Combine(publicBuild, "old_build.data.br"), "stale brotli");
        File.WriteAllText(Path.Combine(publicBuild, "old_build.wasm"), "stale wasm");
        File.WriteAllText(Path.Combine(publicBuild, "old_build.loader.js"), "stale loader");
        File.WriteAllText(Path.Combine(publicBuild, "old_build.framework.js"), "stale framework");

        Assert.AreEqual(5, Directory.GetFiles(publicBuild).Length, "Precondition: 5 stale files");

        // CopyWebGLToPublic의 정리 로직 재현
        AITFileUtils.DeleteDirectory(publicBuild);
        Directory.CreateDirectory(publicBuild);

        Assert.IsTrue(Directory.Exists(publicBuild), "Build dir should be recreated");
        Assert.AreEqual(0, Directory.GetFiles(publicBuild).Length,
            "Build dir should be empty after cleanup — no stale files remain");
    }

    // =====================================================
    // CopyWebGLToPublic: 필수 파일만 선별 복사 검증
    // 와일드카드가 아닌 FindFileInBuild로 찾은 특정 파일만 복사됨
    // =====================================================

    [Test]
    public void CopyWebGLToPublic_CopiesOnlyNamedFiles()
    {
        // WebGL Build/ 소스 폴더 시뮬레이션
        string buildSrc = Path.Combine(tempDir, "webgl", "Build");
        Directory.CreateDirectory(buildSrc);

        // 필수 파일 (CopyWebGLToPublic이 복사하는 대상)
        File.WriteAllText(Path.Combine(buildSrc, "build.loader.js"), "loader");
        File.WriteAllText(Path.Combine(buildSrc, "build.data"), "data");
        File.WriteAllText(Path.Combine(buildSrc, "build.framework.js"), "framework");
        File.WriteAllText(Path.Combine(buildSrc, "build.wasm"), "wasm");

        // stale 파일 (이전 Brotli 빌드의 잔여물 — 복사되면 안 됨)
        File.WriteAllText(Path.Combine(buildSrc, "build.data.br"), "stale brotli data");
        File.WriteAllText(Path.Combine(buildSrc, "build.framework.js.br"), "stale brotli fw");
        File.WriteAllText(Path.Combine(buildSrc, "build.wasm.br"), "stale brotli wasm");

        // CopyWebGLToPublic의 선별 복사 로직 재현 (lines 776-842)
        // FindFileInBuild → 패턴으로 파일 찾기 → 해당 파일만 복사
        var patterns = AITBuildValidator.GetFilePatterns(0); // compressionFormat=0 (Disabled)
        string loaderFile = AITBuildValidator.FindFileInBuild(buildSrc, patterns["loader"], isRequired: true);
        string dataFile = AITBuildValidator.FindFileInBuild(buildSrc, patterns["data"], isRequired: true);
        string frameworkFile = AITBuildValidator.FindFileInBuild(buildSrc, patterns["framework"], isRequired: true);
        string wasmFile = AITBuildValidator.FindFileInBuild(buildSrc, patterns["wasm"], isRequired: true);

        string buildDest = Path.Combine(tempDir, "ait-build", "public", "Build");
        Directory.CreateDirectory(buildDest);

        var filesToCopy = new List<string> { loaderFile, dataFile, frameworkFile, wasmFile };
        foreach (var fileName in filesToCopy)
        {
            File.Copy(Path.Combine(buildSrc, fileName), Path.Combine(buildDest, fileName));
        }

        // 검증: 정확히 4개 파일만 복사됨
        string[] copiedFiles = Directory.GetFiles(buildDest);
        Assert.AreEqual(4, copiedFiles.Length,
            "Only the 4 required files should be copied, not stale .br files");

        Assert.IsTrue(File.Exists(Path.Combine(buildDest, "build.loader.js")));
        Assert.IsTrue(File.Exists(Path.Combine(buildDest, "build.data")));
        Assert.IsTrue(File.Exists(Path.Combine(buildDest, "build.framework.js")));
        Assert.IsTrue(File.Exists(Path.Combine(buildDest, "build.wasm")));

        // stale 파일이 복사되지 않았는지 확인
        Assert.IsFalse(File.Exists(Path.Combine(buildDest, "build.data.br")),
            "Stale .br file should NOT be copied");
        Assert.IsFalse(File.Exists(Path.Combine(buildDest, "build.framework.js.br")),
            "Stale .br file should NOT be copied");
        Assert.IsFalse(File.Exists(Path.Combine(buildDest, "build.wasm.br")),
            "Stale .br file should NOT be copied");
    }

    // =====================================================
    // CopyWebGLToPublic: 인식되지 않은 파일 경고 로직 검증
    // Build/에 복사 대상이 아닌 파일이 있으면 경고 출력
    // =====================================================

    [Test]
    public void CopyWebGLToPublic_DetectsUnrecognizedFiles()
    {
        // Build/ 소스 폴더 시뮬레이션
        string buildSrc = Path.Combine(tempDir, "webgl", "Build");
        Directory.CreateDirectory(buildSrc);

        // 필수 파일
        File.WriteAllText(Path.Combine(buildSrc, "build.loader.js"), "loader");
        File.WriteAllText(Path.Combine(buildSrc, "build.data"), "data");
        File.WriteAllText(Path.Combine(buildSrc, "build.framework.js"), "framework");
        File.WriteAllText(Path.Combine(buildSrc, "build.wasm"), "wasm");

        // 인식되지 않은 파일 (경고 대상)
        File.WriteAllText(Path.Combine(buildSrc, "unknown-file.txt"), "mystery");
        File.WriteAllText(Path.Combine(buildSrc, "leftover.map"), "sourcemap");

        // CopyWebGLToPublic의 경고 로직 재현 (lines 846-856)
        var patterns = AITBuildValidator.GetFilePatterns(0);
        string loaderFile = AITBuildValidator.FindFileInBuild(buildSrc, patterns["loader"]);
        string dataFile = AITBuildValidator.FindFileInBuild(buildSrc, patterns["data"]);
        string frameworkFile = AITBuildValidator.FindFileInBuild(buildSrc, patterns["framework"]);
        string wasmFile = AITBuildValidator.FindFileInBuild(buildSrc, patterns["wasm"]);
        string symbolsFile = AITBuildValidator.FindFileInBuild(buildSrc, patterns["symbols"]);

        var copiedFileNames = new HashSet<string> { loaderFile, dataFile, frameworkFile, wasmFile };
        if (!string.IsNullOrEmpty(symbolsFile)) copiedFileNames.Add(symbolsFile);

        var unrecognized = new List<string>();
        foreach (var file in Directory.GetFiles(buildSrc))
        {
            string name = Path.GetFileName(file);
            if (!copiedFileNames.Contains(name))
            {
                unrecognized.Add(name);
            }
        }

        Assert.AreEqual(2, unrecognized.Count,
            "Should detect 2 unrecognized files");
        Assert.Contains("unknown-file.txt", unrecognized);
        Assert.Contains("leftover.map", unrecognized);
    }

    // =====================================================
    // 다중 빌드 사이클: Dev → Prod → Dev 전환 시 누적 없음
    // 핵심 시나리오: 여러 번 빌드해도 이전 결과물이 남지 않음
    // =====================================================

    [Test]
    public void MultipleBuildCycles_NoStaleFilesAccumulate()
    {
        string buildProjectPath = Path.Combine(tempDir, "ait-build");

        // === 사이클 1: Dev 빌드 ===
        prepareMethod.Invoke(null, new object[] { buildProjectPath });

        // Dev 빌드 결과물 시뮬레이션
        string publicPath = Path.Combine(buildProjectPath, "public");
        string publicBuild = Path.Combine(publicPath, "Build");
        Directory.CreateDirectory(publicBuild);
        File.WriteAllText(Path.Combine(publicBuild, "dev1.loader.js"), "dev loader");
        File.WriteAllText(Path.Combine(publicBuild, "dev1.data"), "dev data");
        File.WriteAllText(Path.Combine(publicBuild, "dev1.framework.js"), "dev framework");
        File.WriteAllText(Path.Combine(publicBuild, "dev1.wasm"), "dev wasm");
        File.WriteAllText(Path.Combine(buildProjectPath, "index.html"), "dev index");
        Directory.CreateDirectory(Path.Combine(buildProjectPath, "dist"));
        File.WriteAllText(Path.Combine(buildProjectPath, "dist", "output.js"), "dev dist");
        // 보존 대상 설정 파일
        File.WriteAllText(Path.Combine(buildProjectPath, "package.json"), "{\"name\":\"test\"}");
        File.WriteAllText(Path.Combine(buildProjectPath, "vite.config.ts"), "export default {}");

        // === 사이클 2: Prod 빌드 (PrepareAitBuildFolder가 이전 결과물 정리) ===
        prepareMethod.Invoke(null, new object[] { buildProjectPath });

        Assert.IsFalse(Directory.Exists(publicPath),
            "Cycle 2: public/ from Dev build should be deleted");
        Assert.IsFalse(Directory.Exists(Path.Combine(buildProjectPath, "dist")),
            "Cycle 2: dist/ from Dev build should be deleted");
        Assert.IsFalse(File.Exists(Path.Combine(buildProjectPath, "index.html")),
            "Cycle 2: index.html from Dev build should be deleted");
        Assert.IsTrue(File.Exists(Path.Combine(buildProjectPath, "package.json")),
            "Cycle 2: package.json should be preserved across builds");
        Assert.IsTrue(File.Exists(Path.Combine(buildProjectPath, "vite.config.ts")),
            "Cycle 2: vite.config.ts should be preserved across builds");

        // Prod 빌드 결과물 시뮬레이션
        Directory.CreateDirectory(publicBuild);
        File.WriteAllText(Path.Combine(publicBuild, "prod.loader.js"), "prod loader");
        File.WriteAllText(Path.Combine(publicBuild, "prod.data"), "prod data");
        File.WriteAllText(Path.Combine(publicBuild, "prod.framework.js"), "prod framework");
        File.WriteAllText(Path.Combine(publicBuild, "prod.wasm"), "prod wasm");
        File.WriteAllText(Path.Combine(buildProjectPath, "index.html"), "prod index");
        Directory.CreateDirectory(Path.Combine(buildProjectPath, "dist"));
        File.WriteAllText(Path.Combine(buildProjectPath, "dist", "output.js"), "prod dist");

        // === 사이클 3: 다시 Dev 빌드 ===
        prepareMethod.Invoke(null, new object[] { buildProjectPath });

        Assert.IsFalse(Directory.Exists(publicPath),
            "Cycle 3: public/ from Prod build should be deleted");
        Assert.IsFalse(Directory.Exists(Path.Combine(buildProjectPath, "dist")),
            "Cycle 3: dist/ from Prod build should be deleted");
        Assert.IsFalse(File.Exists(Path.Combine(buildProjectPath, "index.html")),
            "Cycle 3: index.html from Prod build should be deleted");

        // 설정 파일은 3번의 사이클을 거쳐도 보존
        Assert.IsTrue(File.Exists(Path.Combine(buildProjectPath, "package.json")),
            "Cycle 3: package.json should survive all build cycles");
        Assert.IsTrue(File.Exists(Path.Combine(buildProjectPath, "vite.config.ts")),
            "Cycle 3: vite.config.ts should survive all build cycles");

        // 빌드 폴더 자체는 존재 (빈 상태)
        Assert.IsTrue(Directory.Exists(buildProjectPath),
            "ait-build/ folder should still exist after cleanup");
    }

    // =====================================================
    // 압축 포맷 변경: 비압축 → Brotli 전환 시 stale 파일 없음
    // Build/ 폴더가 통째로 삭제 후 재생성되므로 이전 포맷 파일 잔류 불가
    // =====================================================

    [Test]
    public void CompressionFormatChange_NoStaleFilesRemain()
    {
        string buildProjectPath = Path.Combine(tempDir, "ait-build");
        string webglPath = Path.Combine(tempDir, "webgl");
        string buildSrc = Path.Combine(webglPath, "Build");

        // === 1단계: 비압축 빌드 ===
        prepareMethod.Invoke(null, new object[] { buildProjectPath });

        Directory.CreateDirectory(buildSrc);
        File.WriteAllText(Path.Combine(buildSrc, "build.loader.js"), "loader");
        File.WriteAllText(Path.Combine(buildSrc, "build.data"), "uncompressed data");
        File.WriteAllText(Path.Combine(buildSrc, "build.framework.js"), "uncompressed fw");
        File.WriteAllText(Path.Combine(buildSrc, "build.wasm"), "uncompressed wasm");

        // CopyWebGLToPublic의 Build 복사 로직 재현
        string publicBuild = Path.Combine(buildProjectPath, "public", "Build");
        Directory.CreateDirectory(publicBuild);

        var patterns0 = AITBuildValidator.GetFilePatterns(0); // Disabled
        foreach (var key in new[] { "loader", "data", "framework", "wasm" })
        {
            string fileName = AITBuildValidator.FindFileInBuild(buildSrc, patterns0[key]);
            if (!string.IsNullOrEmpty(fileName))
                File.Copy(Path.Combine(buildSrc, fileName), Path.Combine(publicBuild, fileName));
        }

        Assert.IsTrue(File.Exists(Path.Combine(publicBuild, "build.data")),
            "Precondition: uncompressed .data exists in public/Build/");

        // === 2단계: Brotli 빌드로 전환 ===
        // PrepareAitBuildFolder가 public/ 전체를 삭제
        prepareMethod.Invoke(null, new object[] { buildProjectPath });

        Assert.IsFalse(Directory.Exists(Path.Combine(buildProjectPath, "public")),
            "public/ should be deleted by PrepareAitBuildFolder");

        // WebGL 소스를 Brotli로 변경
        foreach (var f in Directory.GetFiles(buildSrc))
            File.Delete(f);
        File.WriteAllText(Path.Combine(buildSrc, "build.loader.js"), "loader");
        File.WriteAllText(Path.Combine(buildSrc, "build.data.br"), "brotli data");
        File.WriteAllText(Path.Combine(buildSrc, "build.framework.js.br"), "brotli fw");
        File.WriteAllText(Path.Combine(buildSrc, "build.wasm.br"), "brotli wasm");

        // CopyWebGLToPublic의 Build 삭제+재생성+선별복사 재현
        publicBuild = Path.Combine(buildProjectPath, "public", "Build");
        Directory.CreateDirectory(publicBuild);

        var patterns2 = AITBuildValidator.GetFilePatterns(2); // Brotli
        var copiedFiles = new List<string>();
        foreach (var key in new[] { "loader", "data", "framework", "wasm" })
        {
            string fileName = AITBuildValidator.FindFileInBuild(buildSrc, patterns2[key]);
            if (!string.IsNullOrEmpty(fileName))
            {
                File.Copy(Path.Combine(buildSrc, fileName), Path.Combine(publicBuild, fileName));
                copiedFiles.Add(fileName);
            }
        }

        // 검증: Brotli 파일만 존재, 비압축 파일 없음
        string[] filesInBuild = Directory.GetFiles(publicBuild);

        Assert.AreEqual(4, filesInBuild.Length,
            "public/Build/ should contain exactly 4 files after Brotli build");

        Assert.IsTrue(File.Exists(Path.Combine(publicBuild, "build.data.br")),
            "Brotli .data.br should exist");
        Assert.IsTrue(File.Exists(Path.Combine(publicBuild, "build.wasm.br")),
            "Brotli .wasm.br should exist");
        Assert.IsTrue(File.Exists(Path.Combine(publicBuild, "build.framework.js.br")),
            "Brotli .framework.js.br should exist");
        Assert.IsTrue(File.Exists(Path.Combine(publicBuild, "build.loader.js")),
            "Loader .js should exist (loader is never compressed)");

        // 이전 포맷 파일이 없음을 명시적 확인
        Assert.IsFalse(File.Exists(Path.Combine(publicBuild, "build.data")),
            "Uncompressed .data should NOT exist after switching to Brotli");
        Assert.IsFalse(File.Exists(Path.Combine(publicBuild, "build.framework.js")),
            "Uncompressed .framework.js should NOT exist after switching to Brotli");
        Assert.IsFalse(File.Exists(Path.Combine(publicBuild, "build.wasm")),
            "Uncompressed .wasm should NOT exist after switching to Brotli");
    }
}
