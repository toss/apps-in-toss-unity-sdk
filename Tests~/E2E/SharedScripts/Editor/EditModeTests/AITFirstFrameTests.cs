// -----------------------------------------------------------------------
// AITFirstFrameTests.cs - 부팅 훅 지연 실행기(AITFirstFrame) 순수 로직 검증
//   - Post 한 작업은 DeferFrames 번째 Tick 전에는 실행되지 않고, 그 Tick 에서 등록 순서대로 실행된다
//   - 비운 뒤 Post 는 즉시 실행, 작업 예외는 삼켜진다
//   - EveryFrame 구독자는 매 Tick 불린다
//   - LoadModeName 은 Enum.ToString 과 같은 문자열을 준다
// -----------------------------------------------------------------------

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AppsInToss;

[TestFixture]
public class AITFirstFrameTests
{
    [SetUp]
    public void SetUp()
    {
        AITFirstFrame.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        AITFirstFrame.ResetForTests();
    }

    [Test]
    public void Post_RunsOnlyAfterDeferFrames_InRegistrationOrder()
    {
        var order = new List<int>();
        AITFirstFrame.Post(() => order.Add(1));
        AITFirstFrame.Post(() => order.Add(2));

        for (int i = 0; i < AITFirstFrame.DeferFrames - 1; i++)
        {
            AITFirstFrame.Tick();
            Assert.AreEqual(0, order.Count, "DeferFrames 번째 Tick 전에는 실행되지 않아야 한다");
            Assert.IsFalse(AITFirstFrame.Drained);
        }

        AITFirstFrame.Tick();
        CollectionAssert.AreEqual(new[] { 1, 2 }, order);
        Assert.IsTrue(AITFirstFrame.Drained);

        AITFirstFrame.Tick();
        Assert.AreEqual(2, order.Count, "한 번 실행한 작업은 다시 실행되지 않아야 한다");
    }

    [Test]
    public void Post_AfterDrained_RunsImmediately()
    {
        for (int i = 0; i < AITFirstFrame.DeferFrames; i++) AITFirstFrame.Tick();

        bool ran = false;
        AITFirstFrame.Post(() => ran = true);
        Assert.IsTrue(ran);
    }

    [Test]
    public void Post_ThrowingAction_DoesNotBlockOthers()
    {
        bool second = false;
        AITFirstFrame.Post(() => throw new System.InvalidOperationException("boom"));
        AITFirstFrame.Post(() => second = true);

        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"지연 부팅 작업 오류"));
        for (int i = 0; i < AITFirstFrame.DeferFrames; i++) AITFirstFrame.Tick();

        Assert.IsTrue(second);
    }

    [Test]
    public void EveryFrame_IsInvokedOnEachTick()
    {
        int count = 0;
        AITFirstFrame.EveryFrame += () => count++;

        AITFirstFrame.Tick();
        AITFirstFrame.Tick();
        AITFirstFrame.Tick();

        Assert.AreEqual(3, count);
    }

    [Test]
    public void LoadModeName_MatchesEnumToString()
    {
        Assert.AreEqual(LoadSceneMode.Single.ToString(), AITPerformanceLogger.LoadModeName(LoadSceneMode.Single));
        Assert.AreEqual(LoadSceneMode.Additive.ToString(), AITPerformanceLogger.LoadModeName(LoadSceneMode.Additive));
        Assert.AreEqual(((LoadSceneMode)99).ToString(), AITPerformanceLogger.LoadModeName((LoadSceneMode)99));
    }
}
