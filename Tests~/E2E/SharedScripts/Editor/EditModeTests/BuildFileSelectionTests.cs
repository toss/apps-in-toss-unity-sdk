// -----------------------------------------------------------------------
// BuildFileSelectionTests.cs - EditMode Build 파일 선별 테스트
// Level 0: FindFileInBuild의 패턴 매칭 및 중복 파일 최신 선택 로직 검증
// -----------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;
using AppsInToss.Editor;

[TestFixture]
public class BuildFileSelectionTests
{
    private string tempDir;

    [SetUp]
    public void Setup()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "ait-test-build-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // fallback 테스트가 권한을 복구하지 못한 경우 임시 디렉토리 누수 허용
        }
        catch (IOException)
        {
            // 다른 테스트가 파일을 잡고 있는 경우 best-effort
        }
    }

    // =====================================================
    // 단일 매치 — 정확한 파일명 반환
    // =====================================================

    [Test]
    public void FindFileInBuild_SingleMatch_ReturnsFileName()
    {
        string filePath = Path.Combine(tempDir, "build.loader.js");
        File.WriteAllText(filePath, "// loader");

        string result = AITBuildValidator.FindFileInBuild(tempDir, "*.loader.js");

        Assert.AreEqual("build.loader.js", result);
    }

    // =====================================================
    // 다중 매치 — 최신 파일 선택
    // Build 선별 복사의 핵심 변경을 검증:
    // 이전 코드는 파일시스템 순서로 반환했지만,
    // 현재 코드는 LastWriteTime 기준 최신 파일을 선택함
    // =====================================================

    [Test]
    public void FindFileInBuild_MultipleMatches_SelectsNewestFile()
    {
        // 오래된 파일 (이전 빌드 잔여물)
        string oldFile = Path.Combine(tempDir, "old_hash.loader.js");
        File.WriteAllText(oldFile, "// old loader");
        File.SetLastWriteTime(oldFile, new DateTime(2025, 1, 1, 0, 0, 0));

        // 최신 파일 (현재 빌드)
        string newFile = Path.Combine(tempDir, "new_hash.loader.js");
        File.WriteAllText(newFile, "// new loader");
        File.SetLastWriteTime(newFile, new DateTime(2026, 2, 1, 0, 0, 0));

        string result = AITBuildValidator.FindFileInBuild(tempDir, "*.loader.js");

        Assert.AreEqual("new_hash.loader.js", result,
            "FindFileInBuild should select the newest file when multiple matches exist");
    }

    // =====================================================
    // 매치 없음 — 빈 문자열 반환
    // =====================================================

    [Test]
    public void FindFileInBuild_NoMatch_ReturnsEmpty()
    {
        // Build 폴더에 관련 없는 파일만 존재
        File.WriteAllText(Path.Combine(tempDir, "unrelated.txt"), "nothing");

        string result = AITBuildValidator.FindFileInBuild(tempDir, "*.loader.js");

        Assert.AreEqual("", result);
    }

    // =====================================================
    // 압축 확장자 (.br, .gz) 매치
    // Unity 압축 설정에 따라 .wasm.br, .wasm.gz 파일이 생성됨
    // =====================================================

    [TestCase("build.wasm.br", "*.wasm*")]
    [TestCase("build.wasm.gz", "*.wasm*")]
    [TestCase("build.data.unityweb", "*.data*")]
    [TestCase("build.framework.js.br", "*.framework.js*")]
    [TestCase("build.wasm.unityweb", "*.wasm.unityweb")]
    [TestCase("build.data.unityweb", "*.data.unityweb")]
    [TestCase("build.framework.js.unityweb", "*.framework.js.unityweb")]
    [TestCase("build.symbols.json.unityweb", "*.symbols.json.unityweb")]
    public void FindFileInBuild_CompressedExtensions_Matches(string fileName, string pattern)
    {
        File.WriteAllText(Path.Combine(tempDir, fileName), "compressed");

        string result = AITBuildValidator.FindFileInBuild(tempDir, pattern);

        Assert.AreEqual(fileName, result,
            $"Pattern '{pattern}' should match compressed file '{fileName}'");
    }

    // =====================================================
    // 존재하지 않는 경로 — 빈 문자열 반환
    // =====================================================

    [Test]
    public void FindFileInBuild_NonExistentPath_ReturnsEmpty()
    {
        string fakePath = Path.Combine(tempDir, "nonexistent");

        string result = AITBuildValidator.FindFileInBuild(fakePath, "*.loader.js");

        Assert.AreEqual("", result);
    }

    // =====================================================
    // GetFilePatterns — decompressionFallback=true 시 .unityweb 패턴 반환
    // =====================================================

    [Test]
    public void GetFilePatterns_DecompressionFallback_ReturnsUnitywebPatterns()
    {
        var patterns = AITBuildValidator.GetFilePatterns(0, decompressionFallback: true);

        Assert.AreEqual("*.loader.js", patterns["loader"]);
        Assert.AreEqual("*.data.unityweb", patterns["data"]);
        Assert.AreEqual("*.framework.js.unityweb", patterns["framework"]);
        Assert.AreEqual("*.wasm.unityweb", patterns["wasm"]);
        Assert.AreEqual("*.symbols.json.unityweb", patterns["symbols"]);
    }

    [Test]
    public void GetFilePatterns_DecompressionFallback_OverridesCompressionFormat()
    {
        // Brotli(2) + decompressionFallback=true → .unityweb가 우선
        var patterns = AITBuildValidator.GetFilePatterns(2, decompressionFallback: true);

        Assert.AreEqual("*.data.unityweb", patterns["data"],
            "decompressionFallback should override compressionFormat (Brotli)");
        Assert.AreEqual("*.wasm.unityweb", patterns["wasm"]);
        Assert.AreEqual("*.framework.js.unityweb", patterns["framework"]);
    }

    [Test]
    public void GetFilePatterns_NoDecompressionFallback_ReturnsStandardPatterns()
    {
        var patterns = AITBuildValidator.GetFilePatterns(0, decompressionFallback: false);

        Assert.AreEqual("*.loader.js", patterns["loader"]);
        Assert.AreEqual("*.data", patterns["data"]);
        Assert.AreEqual("*.framework.js", patterns["framework"]);
        Assert.AreEqual("*.wasm", patterns["wasm"]);
        Assert.AreEqual("*.symbols.json", patterns["symbols"]);
    }

    // =====================================================
    // 다중 매치 3개 이상 — 가장 최신 파일 선택
    // 이전 빌드가 여러 번 쌓인 경우를 시뮬레이션
    // =====================================================

    [Test]
    public void FindFileInBuild_ThreeMatches_SelectsNewest()
    {
        // 가장 오래된 파일
        string file1 = Path.Combine(tempDir, "aaa.data");
        File.WriteAllText(file1, "data1");
        File.SetLastWriteTime(file1, new DateTime(2025, 1, 1));

        // 중간 파일
        string file2 = Path.Combine(tempDir, "bbb.data");
        File.WriteAllText(file2, "data2");
        File.SetLastWriteTime(file2, new DateTime(2025, 6, 1));

        // 최신 파일
        string file3 = Path.Combine(tempDir, "ccc.data");
        File.WriteAllText(file3, "data3");
        File.SetLastWriteTime(file3, new DateTime(2026, 2, 1));

        string result = AITBuildValidator.FindFileInBuild(tempDir, "*.data*");

        Assert.AreEqual("ccc.data", result,
            "FindFileInBuild should select the newest among 3+ matching files");
    }

    // =====================================================
    // 중복 파일 자동 정리 — 최신 외 오래된 파일 삭제
    // Sentry 이슈 SDK-7F/7G/7H/7J 대응:
    // 중복 감지 시 경고만 출력 → 자동 삭제로 전환하여 반복 경고 제거
    // =====================================================

    [Test]
    public void FindFileInBuild_MultipleMatches_DeletesStaleFiles()
    {
        // 오래된 파일 (이전 빌드 잔여물)
        string oldFile = Path.Combine(tempDir, "old_hash.loader.js");
        File.WriteAllText(oldFile, "// old loader");
        File.SetLastWriteTime(oldFile, new DateTime(2025, 1, 1, 0, 0, 0));

        // 최신 파일 (현재 빌드)
        string newFile = Path.Combine(tempDir, "new_hash.loader.js");
        File.WriteAllText(newFile, "// new loader");
        File.SetLastWriteTime(newFile, new DateTime(2026, 2, 1, 0, 0, 0));

        string result = AITBuildValidator.FindFileInBuild(tempDir, "*.loader.js");

        Assert.AreEqual("new_hash.loader.js", result);
        Assert.IsFalse(File.Exists(oldFile),
            "Stale duplicate should be deleted to prevent repeated Sentry warnings");
        Assert.IsTrue(File.Exists(newFile),
            "Newest file must remain after cleanup");
    }

    [Test]
    public void FindFileInBuild_MultipleMatches_DeletesMetaFiles()
    {
        // Unity .meta 파일도 함께 정리해야 중복 asset 경고 방지
        string oldFile = Path.Combine(tempDir, "old_hash.loader.js");
        File.WriteAllText(oldFile, "// old");
        File.SetLastWriteTime(oldFile, new DateTime(2025, 1, 1));

        string oldMeta = oldFile + ".meta";
        File.WriteAllText(oldMeta, "fileFormatVersion: 2\nguid: deadbeef\n");

        string newFile = Path.Combine(tempDir, "new_hash.loader.js");
        File.WriteAllText(newFile, "// new");
        File.SetLastWriteTime(newFile, new DateTime(2026, 2, 1));

        AITBuildValidator.FindFileInBuild(tempDir, "*.loader.js");

        Assert.IsFalse(File.Exists(oldFile), "stale file should be deleted");
        Assert.IsFalse(File.Exists(oldMeta), "stale .meta should be deleted");
    }

    [Test]
    public void FindFileInBuild_ThreeMatches_DeletesAllButNewest()
    {
        string file1 = Path.Combine(tempDir, "aaa.data");
        File.WriteAllText(file1, "old1");
        File.SetLastWriteTime(file1, new DateTime(2025, 1, 1));

        string file2 = Path.Combine(tempDir, "bbb.data");
        File.WriteAllText(file2, "old2");
        File.SetLastWriteTime(file2, new DateTime(2025, 6, 1));

        string file3 = Path.Combine(tempDir, "ccc.data");
        File.WriteAllText(file3, "newest");
        File.SetLastWriteTime(file3, new DateTime(2026, 2, 1));

        string result = AITBuildValidator.FindFileInBuild(tempDir, "*.data*");

        Assert.AreEqual("ccc.data", result);
        Assert.IsFalse(File.Exists(file1), "oldest should be deleted");
        Assert.IsFalse(File.Exists(file2), "middle should be deleted");
        Assert.IsTrue(File.Exists(file3), "newest should remain");
    }

    [Test]
    public void FindFileInBuild_SingleFile_NotDeleted()
    {
        // 단일 파일은 삭제 로직을 거치지 않아야 함 (회귀 방지)
        string file = Path.Combine(tempDir, "build.loader.js");
        File.WriteAllText(file, "// only");

        string result = AITBuildValidator.FindFileInBuild(tempDir, "*.loader.js");

        Assert.AreEqual("build.loader.js", result);
        Assert.IsTrue(File.Exists(file),
            "Single match should never be deleted");
    }

    // =====================================================
    // LastWriteTime 동률 — 파일명 내림차순 타이브레이크로 결정적 선택
    // Array.Sort가 불안정하므로 명시적 이차 정렬 키가 필요
    // =====================================================

    [Test]
    public void FindFileInBuild_SameTimestamp_DeterministicByName()
    {
        var time = new DateTime(2026, 1, 1, 12, 0, 0);

        string fileA = Path.Combine(tempDir, "aaa.loader.js");
        File.WriteAllText(fileA, "// a");
        File.SetLastWriteTime(fileA, time);

        string fileZ = Path.Combine(tempDir, "zzz.loader.js");
        File.WriteAllText(fileZ, "// z");
        File.SetLastWriteTime(fileZ, time);

        string result = AITBuildValidator.FindFileInBuild(tempDir, "*.loader.js");

        // CompareOrdinal 내림차순 → "zzz" > "aaa" → zzz 선택
        Assert.AreEqual("zzz.loader.js", result,
            "Tie on LastWriteTime should break deterministically by filename (descending)");
        Assert.IsFalse(File.Exists(fileA), "Tie loser should be deleted");
        Assert.IsTrue(File.Exists(fileZ), "Tie winner should remain");
    }

    // =====================================================
    // 로그 레벨 검증 — 자동 정리 성공 시 Warning/Error 0건
    // Sentry 노이즈 제거의 핵심 목적 회귀 방지.
    // LogAssert.NoUnexpectedReceived는 Error/Assert/Exception만 감시하므로
    // Warning까지 확실히 막으려면 logMessageReceived 훅으로 직접 수집.
    // =====================================================

    [Test]
    public void FindFileInBuild_DuplicateCleanup_EmitsInfoLogNotWarning()
    {
        string oldFile = Path.Combine(tempDir, "old.loader.js");
        File.WriteAllText(oldFile, "// old");
        File.SetLastWriteTime(oldFile, new DateTime(2025, 1, 1));

        string newFile = Path.Combine(tempDir, "new.loader.js");
        File.WriteAllText(newFile, "// new");
        File.SetLastWriteTime(newFile, new DateTime(2026, 2, 1));

        var noisy = CollectNoisyLogs(() =>
            AITBuildValidator.FindFileInBuild(tempDir, "*.loader.js"));

        Assert.IsEmpty(noisy,
            "성공 경로는 Warning/Error 로그를 발생시키면 안 됨: " +
            string.Join(" | ", noisy));
    }

    // =====================================================
    // Fallback 경로 검증 (Unix) — chmod로 삭제 실패 유도
    // Windows는 POSIX 권한 모델이 달라 별도 테스트 (file lock)에서 커버
    // =====================================================

    [Test]
    public void FindFileInBuild_DeleteFails_FallsBackToWarning_Unix()
    {
        Assume.That(Application.platform != RuntimePlatform.WindowsEditor,
            "Unix-only: chmod-based directory permission manipulation");

        string oldFile = Path.Combine(tempDir, "old.loader.js");
        File.WriteAllText(oldFile, "// old");
        File.SetLastWriteTime(oldFile, new DateTime(2025, 1, 1));

        string newFile = Path.Combine(tempDir, "new.loader.js");
        File.WriteAllText(newFile, "// new");
        File.SetLastWriteTime(newFile, new DateTime(2026, 2, 1));

        try
        {
            Syscall_Chmod(tempDir, "0555");

            // root 사용자면 0555가 무시되어 삭제가 성공 → false positive
            // 실제로 쓰기 불가한지 선검증
            Assume.That(IsDirectoryWriteBlocked(tempDir),
                "현재 사용자는 0555 디렉토리에도 쓸 수 있음 (root?) — 테스트 의미 없음");

            LogAssert.Expect(LogType.Warning, new Regex(@"\[AIT\].+중복 파일 \d+개 정리 실패"));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[AIT\].+Clean Build"));

            string result = AITBuildValidator.FindFileInBuild(tempDir, "*.loader.js");

            Assert.AreEqual("new.loader.js", result,
                "삭제 실패 시에도 최신 파일명은 정상 반환되어야 함");
            Assert.IsTrue(File.Exists(oldFile),
                "chmod로 삭제를 막았으므로 oldFile은 남아있어야 함");
        }
        finally
        {
            // TearDown이 tempDir을 지우려면 쓰기 권한 복구 필요
            try { Syscall_Chmod(tempDir, "0755"); } catch { /* best-effort */ }
        }
    }

    // =====================================================
    // Fallback 경로 검증 (Windows) — FileStream 락으로 IOException 유도
    // Windows는 열린 파일을 다른 핸들에서 삭제 불가(sharing violation)
    // =====================================================

    [Test]
    public void FindFileInBuild_DeleteFails_FallsBackToWarning_Windows()
    {
        Assume.That(Application.platform == RuntimePlatform.WindowsEditor,
            "Windows-only: file lock semantics differ on Unix");

        string oldFile = Path.Combine(tempDir, "old.loader.js");
        File.WriteAllText(oldFile, "// old");
        File.SetLastWriteTime(oldFile, new DateTime(2025, 1, 1));

        string newFile = Path.Combine(tempDir, "new.loader.js");
        File.WriteAllText(newFile, "// new");
        File.SetLastWriteTime(newFile, new DateTime(2026, 2, 1));

        // Windows에서 FileShare.Read로 열면 다른 프로세스의 Delete가 sharing violation 발생
        using (File.Open(oldFile, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"\[AIT\].+중복 파일 \d+개 정리 실패"));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[AIT\].+Clean Build"));

            string result = AITBuildValidator.FindFileInBuild(tempDir, "*.loader.js");

            Assert.AreEqual("new.loader.js", result,
                "삭제 실패 시에도 최신 파일명은 정상 반환되어야 함");
            Assert.IsTrue(File.Exists(oldFile),
                "파일이 열려있어 삭제 실패 → oldFile은 남아있어야 함");
        }
    }

    // -----------------------------------------------------------------
    // 유틸리티: 동작 중 Warning/Error 로그를 수집 (LogAssert 보강)
    // -----------------------------------------------------------------
    private static List<string> CollectNoisyLogs(Action action)
    {
        var noisy = new List<string>();
        Application.LogCallback handler = null;
        handler = (msg, _, type) =>
        {
            if (type == LogType.Warning || type == LogType.Error ||
                type == LogType.Exception || type == LogType.Assert)
            {
                noisy.Add($"[{type}] {msg}");
            }
        };
        Application.logMessageReceived += handler;
        try { action(); }
        finally { Application.logMessageReceived -= handler; }
        return noisy;
    }

    private static bool IsDirectoryWriteBlocked(string dir)
    {
        string probe = Path.Combine(dir, "probe-" + Guid.NewGuid().ToString("N").Substring(0, 6));
        try
        {
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return false;
        }
        catch (UnauthorizedAccessException) { return true; }
        catch (IOException) { return true; }
    }

    private static void Syscall_Chmod(string path, string mode)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("chmod", $"{mode} \"{path}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using (var proc = System.Diagnostics.Process.Start(psi))
        {
            if (!proc.WaitForExit(3000))
            {
                try { proc.Kill(); } catch { /* best-effort */ }
                throw new InvalidOperationException($"chmod {mode} {path} timed out");
            }
            if (proc.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"chmod {mode} {path} failed: {proc.StandardError.ReadToEnd()}");
            }
        }
    }
}
