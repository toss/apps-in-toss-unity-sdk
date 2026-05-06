// -----------------------------------------------------------------------
// AITFileSystemHelperTests.cs - SafeDelete/SafeDeleteDirectory 검증
// Level 0: 파일/디렉토리 안전 삭제 유틸의 경계 케이스 검증
// -----------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.TestTools;
using AppsInToss.Editor;

[TestFixture]
public class AITFileSystemHelperTests
{
    private const string DefaultLogPrefix = "[AIT]";

    private string tempDir;
    private MethodInfo safeDeleteMethod;
    private MethodInfo safeDeleteDirectoryMethod;

    [SetUp]
    public void Setup()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "ait-test-fs-helper-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(tempDir);

        // AITFileSystemHelper는 internal 클래스이므로 리플렉션으로 접근.
        // 메서드 접근자 변경(public ↔ internal)에도 견고하도록 Public|NonPublic 모두 탐색.
        var helperType = typeof(AITBuildValidator).Assembly
            .GetType("AppsInToss.Editor.AITFileSystemHelper");
        Assert.IsNotNull(helperType, "AITFileSystemHelper type should exist in AppsInTossSDKEditor assembly");

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        safeDeleteMethod = helperType.GetMethod("SafeDelete", flags);
        Assert.IsNotNull(safeDeleteMethod, "SafeDelete method should exist");

        safeDeleteDirectoryMethod = helperType.GetMethod("SafeDeleteDirectory", flags);
        Assert.IsNotNull(safeDeleteDirectoryMethod, "SafeDeleteDirectory method should exist");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, true);
        }
    }

    private bool InvokeSafeDelete(string path, bool logOnFailure = true, string logPrefix = DefaultLogPrefix)
    {
        return (bool)safeDeleteMethod.Invoke(null, new object[] { path, logOnFailure, logPrefix });
    }

    private bool InvokeSafeDeleteDirectory(string path, bool logOnFailure = true, string logPrefix = DefaultLogPrefix)
    {
        return (bool)safeDeleteDirectoryMethod.Invoke(null, new object[] { path, logOnFailure, logPrefix });
    }

    // =====================================================
    // SafeDelete
    // =====================================================

    [Test]
    public void SafeDelete_ExistingFile_RemovesFileAndReturnsTrue()
    {
        string path = Path.Combine(tempDir, "file.txt");
        File.WriteAllText(path, "content");

        bool result = InvokeSafeDelete(path);

        Assert.IsTrue(result, "Should return true on successful delete");
        Assert.IsFalse(File.Exists(path), "File should be deleted");
    }

    [Test]
    public void SafeDelete_MissingFile_ReturnsTrue()
    {
        string path = Path.Combine(tempDir, "nonexistent.txt");

        bool result = InvokeSafeDelete(path);

        Assert.IsTrue(result, "Missing file should be treated as successful (idempotent)");
    }

    [Test]
    public void SafeDelete_NullOrEmpty_ReturnsTrue()
    {
        Assert.IsTrue(InvokeSafeDelete(null), "null path should return true");
        Assert.IsTrue(InvokeSafeDelete(""), "empty path should return true");
    }

    [Test]
    public void SafeDelete_ReadOnlyFile_RemovesFile()
    {
        string path = Path.Combine(tempDir, "readonly.txt");
        File.WriteAllText(path, "content");
        File.SetAttributes(path, FileAttributes.ReadOnly);

        try
        {
            bool result = InvokeSafeDelete(path);

            Assert.IsTrue(result, "Read-only file should be deleted after attribute clear");
            Assert.IsFalse(File.Exists(path), "File should be deleted");
        }
        finally
        {
            // TearDown이 읽기 전용 속성을 처리하지 못하면 실패하므로 복구
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }
        }
    }

    [Test]
    public void SafeDelete_AlsoRemovesMetaFile()
    {
        string path = Path.Combine(tempDir, "asset.txt");
        string metaPath = path + ".meta";
        File.WriteAllText(path, "content");
        File.WriteAllText(metaPath, "guid: ...");

        bool result = InvokeSafeDelete(path);

        Assert.IsTrue(result);
        Assert.IsFalse(File.Exists(path), "Main file should be deleted");
        Assert.IsFalse(File.Exists(metaPath), ".meta file should also be deleted");
    }

    // =====================================================
    // SafeDeleteDirectory
    // =====================================================

    [Test]
    public void SafeDeleteDirectory_ExistingDirectory_RemovesRecursively()
    {
        string dir = Path.Combine(tempDir, "nested");
        Directory.CreateDirectory(Path.Combine(dir, "sub"));
        File.WriteAllText(Path.Combine(dir, "a.txt"), "a");
        File.WriteAllText(Path.Combine(dir, "sub", "b.txt"), "b");

        bool result = InvokeSafeDeleteDirectory(dir);

        Assert.IsTrue(result);
        Assert.IsFalse(Directory.Exists(dir), "Directory should be removed recursively");
    }

    [Test]
    public void SafeDeleteDirectory_MissingDirectory_ReturnsTrue()
    {
        string dir = Path.Combine(tempDir, "nonexistent");

        bool result = InvokeSafeDeleteDirectory(dir);

        Assert.IsTrue(result, "Missing directory should be treated as successful (idempotent)");
    }

    [Test]
    public void SafeDeleteDirectory_NullOrEmpty_ReturnsTrue()
    {
        Assert.IsTrue(InvokeSafeDeleteDirectory(null));
        Assert.IsTrue(InvokeSafeDeleteDirectory(""));
    }

    [Test]
    public void SafeDeleteDirectory_WithReadOnlyFiles_RemovesAll()
    {
        // Windows: 내부 파일이 읽기 전용이면 Directory.Delete(recursive:true)가 실패함.
        // SafeDeleteDirectory는 ReadOnly 속성을 선제적으로 해제해야 한다.
        string dir = Path.Combine(tempDir, "readonly-nested");
        Directory.CreateDirectory(dir);
        string readOnlyFile = Path.Combine(dir, "locked.txt");
        File.WriteAllText(readOnlyFile, "content");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);

        try
        {
            bool result = InvokeSafeDeleteDirectory(dir);

            Assert.IsTrue(result, "Read-only 파일이 있어도 디렉토리가 재귀 삭제되어야 함");
            Assert.IsFalse(Directory.Exists(dir));
        }
        finally
        {
            // TearDown이 read-only 속성 때문에 실패하지 않도록 복구
            if (File.Exists(readOnlyFile))
            {
                File.SetAttributes(readOnlyFile, FileAttributes.Normal);
            }
        }
    }

    [Test]
    public void SafeDeleteDirectory_NodeModulesShape_WithDeepReadOnlyFiles_RemovesAll()
    {
        // 회귀: Sentry APPS-IN-TOSS-UNITY-SDK-CA
        // ait-build/node_modules/.pnpm/.../@jridgewell/gen-mapping/... 처럼
        // 깊게 중첩된 read-only 트리(pnpm/npm 의존성)를 정리하지 못하면
        // "ait-build/ 폴더 삭제 실패: UnauthorizedAccessException ... gen-mapping" 발생.
        string aitBuild = Path.Combine(tempDir, "ait-build");
        string nodeModules = Path.Combine(aitBuild, "node_modules");
        string pnpmStore = Path.Combine(nodeModules, ".pnpm", "@jridgewell+gen-mapping@0.3.5", "node_modules", "@jridgewell", "gen-mapping");
        Directory.CreateDirectory(pnpmStore);

        // Symlink 형태로 의존성을 노출하는 pnpm 레이아웃을 모사 — 실제 심볼릭 링크 생성은
        // 권한 의존이라 디렉토리/파일로 대체하되, read-only 속성은 동일하게 깐다.
        string distDir = Path.Combine(pnpmStore, "dist");
        Directory.CreateDirectory(distDir);

        var readOnlyPaths = new[]
        {
            Path.Combine(pnpmStore, "package.json"),
            Path.Combine(pnpmStore, "README.md"),
            Path.Combine(distDir, "gen-mapping.umd.js"),
            Path.Combine(distDir, "gen-mapping.mjs"),
            Path.Combine(distDir, "types", "gen-mapping.d.ts"),
        };

        Directory.CreateDirectory(Path.Combine(distDir, "types"));
        foreach (var p in readOnlyPaths)
        {
            File.WriteAllText(p, "{}");
            File.SetAttributes(p, FileAttributes.ReadOnly);
        }

        try
        {
            bool result = InvokeSafeDeleteDirectory(aitBuild);

            Assert.IsTrue(result,
                "node_modules에 read-only 파일이 깔린 트리도 ait-build 전체가 재귀 삭제되어야 함");
            Assert.IsFalse(Directory.Exists(aitBuild),
                "ait-build/ 폴더가 완전히 제거되어야 함");
        }
        finally
        {
            // TearDown 보호: 실패해서 일부 파일이 남아있을 경우에도 read-only 속성 해제
            foreach (var p in readOnlyPaths)
            {
                if (File.Exists(p))
                {
                    File.SetAttributes(p, FileAttributes.Normal);
                }
            }
        }
    }

    // =====================================================
    // logOnFailure / logPrefix (Windows 한정: 파일 공유 락으로 실패 강제)
    // macOS/Linux는 `FileShare.None`이 `File.Delete`를 막지 않아
    // cross-platform 실패 유도가 불가하므로 [Platform("Win")]으로 제한.
    // =====================================================

    [Test]
    [Platform("Win")]
    public void SafeDelete_LockedFile_WithLogOnFailureTrue_EmitsWarning()
    {
        string path = Path.Combine(tempDir, "locked-warn.txt");
        File.WriteAllText(path, "content");

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            // `AITLog.Warning`은 내부적으로 `UnityEngine.Debug.LogWarning`을 호출하므로 LogAssert로 감시 가능
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[AIT\] 파일 삭제 실패"));
            bool result = InvokeSafeDelete(path, logOnFailure: true);
            Assert.IsFalse(result, "락 걸린 파일 삭제는 실패로 보고되어야 함");
        }

        File.Delete(path);
    }

    [Test]
    [Platform("Win")]
    public void SafeDelete_LockedFile_WithLogOnFailureFalse_SuppressesWarning()
    {
        // Unity Test Runner의 `LogAssert.NoUnexpectedReceived`는 Warning을 감시하지 않으므로
        // Application.logMessageReceived 콜백으로 직접 매칭되는 Warning 개수를 센다.
        string path = Path.Combine(tempDir, "locked-silent.txt");
        File.WriteAllText(path, "content");

        int matchingWarnings = 0;
        Application.LogCallback handler = (condition, stackTrace, type) =>
        {
            if (type == LogType.Warning && condition != null && condition.Contains("파일 삭제 실패"))
            {
                matchingWarnings++;
            }
        };
        Application.logMessageReceived += handler;

        try
        {
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                bool result = InvokeSafeDelete(path, logOnFailure: false);
                Assert.IsFalse(result, "락 걸린 파일 삭제는 실패로 보고되어야 함");
            }
        }
        finally
        {
            Application.logMessageReceived -= handler;
        }

        Assert.AreEqual(0, matchingWarnings,
            "logOnFailure=false일 때 '파일 삭제 실패' Warning이 발생하면 안 됨");

        File.Delete(path);
    }

    [Test]
    [Platform("Win")]
    public void SafeDelete_LockedFile_UsesCustomLogPrefix()
    {
        string path = Path.Combine(tempDir, "locked-prefix.txt");
        File.WriteAllText(path, "content");

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[NodeJS\] 파일 삭제 실패"));
            InvokeSafeDelete(path, logOnFailure: true, logPrefix: "[NodeJS]");
        }

        File.Delete(path);
    }
}
