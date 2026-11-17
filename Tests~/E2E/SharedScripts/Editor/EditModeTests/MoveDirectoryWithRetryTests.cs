// -----------------------------------------------------------------------
// MoveDirectoryWithRetryTests.cs - AITFileSystemHelper.MoveDirectoryWithRetry 검증
// Level 0: Node.js 설치 원자적 이동(rename)의 재시도/경합/실패 경계 케이스
//
// 회귀: Sentry APPS-IN-TOSS-UNITY-SDK-100
//   "[AIT] 패키지 매니저 체크 중 예외: System.IO.IOException: Access to the path
//    '...nodejs...win-x64-installing-<hash>' is denied."
//   Windows AV/Defender가 추출 직후 staging dir 핸들을 점유하면 Directory.Move가
//   일시적 IOException으로 실패 → Node.js 설치 무산. 지수 백오프 재시도로 회복한다.
//
// 테스트 전략:
//   - 기본 경로(target 없음/이미 존재)는 실제 FS로 공개 API(MoveDirectoryWithRetry) 검증.
//   - 재시도/백오프/경합/소진 흐름은 파일잠금 대신 internal core(MoveDirectoryWithRetryCore)에
//     move/targetExists/sleep 델리게이트를 주입해 결정적·크로스플랫폼·고속으로 검증한다.
//     (Windows에서 디렉토리 rename은 자식 파일 핸들이 열려 있어도 허용될 수 있어 파일잠금 기반
//      재현은 신뢰도가 낮고, "실패 후 회복" 경로는 FS로는 결정적 재현이 불가하기 때문.)
// -----------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using UnityEngine;
using UnityEngine.TestTools;
using AppsInToss.Editor;

[TestFixture]
public class MoveDirectoryWithRetryTests
{
    private string tempDir;
    private MethodInfo moveMethod;
    private MethodInfo coreMethod;

    // 프로덕션 백오프 스케줄과 동일해야 하는 기대값 (AITFileSystemHelper.MoveRetryDelaysMs).
    private static readonly int[] ExpectedBackoffMs = { 100, 250, 500, 1000 };

    [SetUp]
    public void Setup()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "ait-test-move-retry-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(tempDir);

        // AITFileSystemHelper는 internal 클래스이므로 리플렉션으로 접근.
        // 접근자 변경(public ↔ internal)에도 견고하도록 Public|NonPublic 모두 탐색.
        var helperType = typeof(AITBuildValidator).Assembly
            .GetType("AppsInToss.Editor.AITFileSystemHelper");
        Assert.IsNotNull(helperType, "AITFileSystemHelper type should exist in AppsInTossSDKEditor assembly");

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        moveMethod = helperType.GetMethod("MoveDirectoryWithRetry", flags);
        Assert.IsNotNull(moveMethod, "MoveDirectoryWithRetry method should exist");
        coreMethod = helperType.GetMethod("MoveDirectoryWithRetryCore", flags);
        Assert.IsNotNull(coreMethod, "MoveDirectoryWithRetryCore method should exist");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(tempDir))
        {
            // 잔여 read-only 속성 해제 후 정리 (실패 케이스 대비)
            foreach (var f in Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(f, FileAttributes.Normal); } catch { /* best-effort */ }
            }
            Directory.Delete(tempDir, true);
        }
    }

    private bool InvokeMove(string stagingPath, string targetPath, string logPrefix = "[NodeJS]")
    {
        return Unwrap(() => (bool)moveMethod.Invoke(null, new object[] { stagingPath, targetPath, logPrefix }));
    }

    private bool InvokeCore(string stagingPath, string targetPath, string logPrefix,
        Action move, Func<bool> targetExists, Action<int> sleep)
    {
        return Unwrap(() => (bool)coreMethod.Invoke(null,
            new object[] { stagingPath, targetPath, logPrefix, move, targetExists, sleep }));
    }

    // 리플렉션 래핑(TargetInvocationException)을 벗겨 원래 예외를 그대로 전파 —
    // Assert.Throws<T>가 직접 잡을 수 있도록 스택까지 보존.
    private static bool Unwrap(Func<bool> invoke)
    {
        try
        {
            return invoke();
        }
        catch (TargetInvocationException tie) when (tie.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
            throw; // unreachable
        }
    }

    private static void CreateStagingWithContent(string stagingPath)
    {
        Directory.CreateDirectory(stagingPath);
        Directory.CreateDirectory(Path.Combine(stagingPath, "bin"));
        File.WriteAllText(Path.Combine(stagingPath, "bin", "node"), "#!/bin/sh");
        File.WriteAllText(Path.Combine(stagingPath, "README.md"), "node");
    }

    // =====================================================
    // 기본 경로 — 실제 FS, 공개 API (cross-platform)
    // =====================================================

    [Test]
    public void MoveDirectoryWithRetry_TargetAbsent_MovesAndReturnsTrue()
    {
        string staging = Path.Combine(tempDir, "win-x64-installing-abc123");
        string target = Path.Combine(tempDir, "win-x64");
        CreateStagingWithContent(staging);

        bool moved = InvokeMove(staging, target);

        Assert.IsTrue(moved, "target이 없으면 이동을 수행하고 true를 반환해야 함");
        Assert.IsFalse(Directory.Exists(staging), "이동 후 staging은 사라져야 함");
        Assert.IsTrue(Directory.Exists(target), "target에 디렉토리가 존재해야 함");
        Assert.IsTrue(File.Exists(Path.Combine(target, "bin", "node")), "내용물이 함께 이동되어야 함");
    }

    [Test]
    public void MoveDirectoryWithRetry_TargetAlreadyExists_ReturnsFalseWithoutMoving()
    {
        // 다른 프로세스가 먼저 설치를 완료한 경합(race winner) 상황 —
        // 이동을 건너뛰고 false를 반환하며 기존 target을 건드리지 않아야 한다.
        string staging = Path.Combine(tempDir, "win-x64-installing-def456");
        string target = Path.Combine(tempDir, "win-x64");
        CreateStagingWithContent(staging);
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "EXISTING.txt"), "winner");

        bool moved = InvokeMove(staging, target);

        Assert.IsFalse(moved, "target이 이미 있으면 이동을 건너뛰고 false를 반환해야 함");
        Assert.IsTrue(File.Exists(Path.Combine(target, "EXISTING.txt")), "기존 target 내용이 보존되어야 함");
        Assert.IsTrue(Directory.Exists(staging), "이동을 건너뛰었으므로 staging 정리는 호출자(finally) 몫 — 메서드가 삭제하지 않음");
    }

    // =====================================================
    // 재시도/백오프/경합/소진 — 주입식 core (결정적, cross-platform, 실제 sleep 없음)
    // =====================================================

    [Test]
    public void Core_TargetAbsent_MovesFirstTryWithoutSleeping()
    {
        var sleeps = new List<int>();
        int moveCalls = 0;

        bool moved = InvokeCore("staging", "target", "[NodeJS]",
            move: () => moveCalls++,
            targetExists: () => false,
            sleep: ms => sleeps.Add(ms));

        Assert.IsTrue(moved, "첫 시도에 이동이 성공하면 true");
        Assert.AreEqual(1, moveCalls, "이동은 한 번만 시도되어야 함");
        Assert.AreEqual(0, sleeps.Count, "성공 시 백오프 대기는 발생하지 않아야 함");
    }

    [Test]
    public void Core_TransientFailure_RecoversAfterBackoff()
    {
        // 회귀(SDK-100): 첫 2회는 일시적 IOException(AV 핸들 점유), 3번째에 풀려 성공.
        var sleeps = new List<int>();
        int moveCalls = 0;

        bool moved = InvokeCore("staging", "target", "[NodeJS]",
            move: () =>
            {
                moveCalls++;
                if (moveCalls <= 2) throw new IOException("transient lock");
                // 3번째 호출은 성공
            },
            targetExists: () => false,
            sleep: ms => sleeps.Add(ms));

        Assert.IsTrue(moved, "백오프 재시도로 일시적 잠금에서 회복해 true를 반환해야 함");
        Assert.AreEqual(3, moveCalls, "2회 실패 후 3번째에 성공");
        CollectionAssert.AreEqual(new[] { 100, 250 }, sleeps,
            "실패한 두 시도 후 백오프가 순서대로(100ms, 250ms) 적용되어야 함");
    }

    [Test]
    public void Core_RaceWinnerDuringRetry_ReturnsFalse()
    {
        // 첫 시도는 IOException으로 실패하지만, 백오프 후 재진입 시점에 다른 프로세스가
        // target을 완성(race winner) → 이동을 포기하고 false를 반환해야 한다.
        var sleeps = new List<int>();
        int moveCalls = 0;
        int existsCalls = 0;

        bool moved = InvokeCore("staging", "target", "[NodeJS]",
            move: () =>
            {
                moveCalls++;
                throw new IOException("locked");
            },
            targetExists: () =>
            {
                existsCalls++;
                // 1번째 확인(attempt 0): 아직 없음 → 이동 시도 → 실패 → sleep
                // 2번째 확인(attempt 1): race winner가 완성 → true
                return existsCalls >= 2;
            },
            sleep: ms => sleeps.Add(ms));

        Assert.IsFalse(moved, "재시도 중 target이 완성되면 false(race winner)");
        Assert.AreEqual(1, moveCalls, "두 번째 진입에서 target 존재로 이동을 시도하지 않아야 함");
        CollectionAssert.AreEqual(new[] { 100 }, sleeps, "첫 실패 후 한 번만 백오프");
    }

    [Test]
    public void Core_UnrecoverableFailure_ExhaustsBackoffThenThrowsWithWarning()
    {
        // 전체 재시도 윈도우 동안 계속 실패 → 백오프 4회를 모두 소진(100→250→500→1000)하고
        // 경고 1회 출력 후 마지막 IOException을 호출자에게 다시 던져야 한다(이동 실패는 치명적).
        var sleeps = new List<int>();
        int moveCalls = 0;

        LogAssert.Expect(LogType.Warning,
            new System.Text.RegularExpressions.Regex(@"\[NodeJS\] 디렉토리 이동 재시도 5회 모두 실패"));

        var ex = Assert.Throws<IOException>(() => InvokeCore("staging", "target", "[NodeJS]",
            move: () =>
            {
                moveCalls++;
                throw new IOException("permanent lock #" + moveCalls);
            },
            targetExists: () => false,
            sleep: ms => sleeps.Add(ms)));

        Assert.AreEqual(5, moveCalls, "즉시 1회 + 백오프 4회 = 총 5회 시도");
        CollectionAssert.AreEqual(ExpectedBackoffMs, sleeps,
            "백오프가 100→250→500→1000ms 순서로 정확히 4회 적용되어야 함");
        StringAssert.Contains("permanent lock #5", ex.Message,
            "마지막(5번째) 시도의 예외가 전파되어야 함");
    }

    [Test]
    public void Core_NonIOException_PropagatesImmediatelyWithoutRetry()
    {
        // IOException이 아닌 예외(예: 권한/ACL 문제의 UnauthorizedAccessException)는
        // 일시적 잠금이 아니므로 재시도하지 않고 즉시 전파해야 한다 — 무의미한 ~1.85s 지연 방지.
        var sleeps = new List<int>();
        int moveCalls = 0;

        Assert.Throws<UnauthorizedAccessException>(() => InvokeCore("staging", "target", "[NodeJS]",
            move: () =>
            {
                moveCalls++;
                throw new UnauthorizedAccessException("ACL denied");
            },
            targetExists: () => false,
            sleep: ms => sleeps.Add(ms)));

        Assert.AreEqual(1, moveCalls, "IOException이 아니면 재시도 없이 1회만 시도");
        Assert.AreEqual(0, sleeps.Count, "재시도하지 않으므로 백오프 대기도 없어야 함");
    }
}
