// -----------------------------------------------------------------------
// TapAutoProbeTests.cs - 자동 탭 진단의 판정 로직 검증
// Level 0: 실기기·브라우저 없이 검증한다.
//
// 자동 진단은 실기기에서 딱 한 번 돌고 그 결과로 #1141의 방향이 갈린다. 집계는 맞는데
// 판정문이 틀리면 잘못된 결론을 그대로 믿게 되므로, 집계 → 결론 사상을 여기서 고정한다.
// Conclude/Summarize는 Unity 타입을 쓰지 않아 EditMode에서 그대로 부를 수 있다.
//
// 특히 중요한 것은 "하네스가 안 돌았다"와 "가설이 반증됐다"를 절대 섞지 않는 것이다.
// 합성 탭이 빗맞아 기록이 0건인 상황은 FIRE=NO가 아니라 판정 불가여야 한다.
// -----------------------------------------------------------------------

using System.Collections.Generic;
using NUnit.Framework;

[TestFixture]
public class TapAutoProbeTests
{
    private static TapAutoProbe.ConditionResult Control(int landed, int fired)
    {
        return new TapAutoProbe.ConditionResult
        {
            Name = "A 대조군",
            HasScrollRect = false,
            Attempted = landed,
            Landed = landed,
            UpReceived = landed,
            Fired = fired,
        };
    }

    private static TapAutoProbe.ConditionResult Scroll(string name, float velocity, int landed, int fired)
    {
        return new TapAutoProbe.ConditionResult
        {
            Name = name,
            VelocityY = velocity,
            HasScrollRect = true,
            Attempted = landed,
            Landed = landed,
            UpReceived = landed,
            Fired = fired,
        };
    }

    // =====================================================
    // 판정 불가 — 하네스가 안 돈 경우
    // =====================================================

    [Test]
    public void Conclude_NoResults_IsInconclusive()
    {
        StringAssert.Contains("불가", TapAutoProbe.Conclude(new List<TapAutoProbe.ConditionResult>()));
        StringAssert.Contains("불가", TapAutoProbe.Conclude(null));
    }

    [Test]
    public void Conclude_ControlNeverLanded_IsInconclusiveNotRefutation()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(landed: 0, fired: 0),
            Scroll("B 정지", 0f, landed: 0, fired: 0),
        };

        string verdict = TapAutoProbe.Conclude(results);

        // 합성 탭이 아예 안 닿은 것이므로 가설에 대해 아무 말도 하면 안 된다.
        StringAssert.Contains("불가", verdict);
        StringAssert.DoesNotContain("폐기", verdict);
        StringAssert.DoesNotContain("확정", verdict);
    }

    [Test]
    public void Conclude_NoControlCondition_IsInconclusive()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Scroll("B 정지", 0f, landed: 3, fired: 3),
        };

        StringAssert.Contains("불가", TapAutoProbe.Conclude(results));
    }

    [Test]
    public void Conclude_ScrollTargetNeverLanded_IsInconclusive()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(landed: 3, fired: 3),
            Scroll("B 정지", 0f, landed: 0, fired: 0),
        };

        string verdict = TapAutoProbe.Conclude(results);

        StringAssert.Contains("불가", verdict);
        StringAssert.DoesNotContain("확정", verdict);
    }

    // =====================================================
    // 대조군이 답을 먼저 가른다
    // =====================================================

    [Test]
    public void Conclude_ControlAlsoFails_DiscardsEveryHypothesis()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(landed: 3, fired: 0),
            Scroll("B 정지", 0f, landed: 3, fired: 0),
        };

        string verdict = TapAutoProbe.Conclude(results);

        // 스크롤 밖에서도 클릭이 안 나면 ScrollRect 가설은 전부 무의미하다.
        StringAssert.Contains("폐기", verdict);
        StringAssert.Contains("무관", verdict);
    }

    [Test]
    public void Conclude_FlakyControl_WarnsButStillConcludes()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(landed: 3, fired: 2),
            Scroll("B 정지", 0f, landed: 3, fired: 3),
            Scroll("C -400", -400f, landed: 3, fired: 0),
        };

        string verdict = TapAutoProbe.Conclude(results);

        StringAssert.Contains("확정", verdict);
        // 대조군이 흔들렸다는 사실을 숨기면 결론의 신뢰도를 과대평가하게 된다.
        StringAssert.Contains("불안정", verdict);
    }

    // =====================================================
    // 관성 가설
    // =====================================================

    [Test]
    public void Conclude_StationaryAlsoFails_BlamesScrollRectNotVelocity()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(landed: 3, fired: 3),
            Scroll("B 정지", 0f, landed: 3, fired: 0),
            Scroll("C -400", -400f, landed: 3, fired: 0),
        };

        string verdict = TapAutoProbe.Conclude(results);

        StringAssert.Contains("관성 무관", verdict);
        StringAssert.DoesNotContain("확정", verdict);
    }

    [Test]
    public void Conclude_EveryVelocityFires_RefutesInertiaHypothesis()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(landed: 3, fired: 3),
            Scroll("B 정지", 0f, landed: 3, fired: 3),
            Scroll("C -400", -400f, landed: 3, fired: 3),
            Scroll("C +1600", 1600f, landed: 3, fired: 3),
        };

        string verdict = TapAutoProbe.Conclude(results);

        StringAssert.Contains("반증", verdict);
        StringAssert.DoesNotContain("확정", verdict);
    }

    [Test]
    public void Conclude_ReportsLowestFailingSpeedRegardlessOfSignAndOrder()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(landed: 3, fired: 3),
            Scroll("B 정지", 0f, landed: 3, fired: 3),
            Scroll("C -1600", -1600f, landed: 3, fired: 0),
            Scroll("C +800", 800f, landed: 3, fired: 1),
            Scroll("C -400", -400f, landed: 3, fired: 3),
            Scroll("C +200", 200f, landed: 3, fired: 3),
        };

        string verdict = TapAutoProbe.Conclude(results);

        StringAssert.Contains("확정", verdict);
        // 부호가 아니라 크기로, 목록 순서와 무관하게 가장 낮은 실패 속도를 집어야 한다.
        StringAssert.Contains("800", verdict);
        StringAssert.DoesNotContain("1600", verdict);
    }

    [Test]
    public void Conclude_PartialFailureAtOneSpeed_CountsAsFailure()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(landed: 3, fired: 3),
            Scroll("B 정지", 0f, landed: 3, fired: 3),
            // 3번 중 2번만 발화해도 결정적 실패다. 간헐 증상이라 전부 실패할 이유가 없다.
            Scroll("C -100", -100f, landed: 3, fired: 2),
        };

        StringAssert.Contains("확정", TapAutoProbe.Conclude(results));
        StringAssert.Contains("100", TapAutoProbe.Conclude(results));
    }

    // =====================================================
    // 부가 신호
    // =====================================================

    [Test]
    public void Conclude_MissingPointerUp_IsReportedSeparately()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(landed: 3, fired: 3),
            new TapAutoProbe.ConditionResult
            {
                Name = "C -800", VelocityY = -800f, HasScrollRect = true,
                Attempted = 3, Landed = 3, UpReceived = 1, Fired = 1,
            },
        };

        string verdict = TapAutoProbe.Conclude(results);

        // UP 미수신은 반증됐다고 본 드래그 취소 경로가 실재한다는 뜻이라 따로 보여야 한다.
        StringAssert.Contains("UP 미수신 2건", verdict);
        StringAssert.Contains("pointerPress", verdict);
    }

    [Test]
    public void Conclude_OverChanged_IsReportedSeparately()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(landed: 3, fired: 3),
            new TapAutoProbe.ConditionResult
            {
                Name = "C -800", VelocityY = -800f, HasScrollRect = true,
                Attempted = 3, Landed = 3, UpReceived = 3, Fired = 0, OverChanged = 3,
            },
        };

        string verdict = TapAutoProbe.Conclude(results);

        StringAssert.Contains("over 변화 3건", verdict);
        StringAssert.Contains("확정", verdict);
    }

    [Test]
    public void Conclude_CleanRun_HasNoSpuriousNotes()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(landed: 3, fired: 3),
            Scroll("B 정지", 0f, landed: 3, fired: 3),
            Scroll("C -400", -400f, landed: 3, fired: 3),
        };

        string verdict = TapAutoProbe.Conclude(results);

        StringAssert.DoesNotContain("미수신", verdict);
        StringAssert.DoesNotContain("over 변화", verdict);
        StringAssert.DoesNotContain("불안정", verdict);
    }

    [Test]
    public void Conclude_HeadlineIsTheFirstLine()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(landed: 3, fired: 3),
            new TapAutoProbe.ConditionResult
            {
                Name = "C -800", VelocityY = -800f, HasScrollRect = true,
                Attempted = 3, Landed = 3, UpReceived = 2, Fired = 0, OverChanged = 1,
            },
        };

        string first = TapAutoProbe.Conclude(results).Split('\n')[0];

        // 화면이 좁아 첫 줄만 보이는 경우가 흔하다. 결론이 거기 있어야 한다.
        StringAssert.StartsWith("판정:", first);
        StringAssert.Contains("확정", first);
    }

    // =====================================================
    // 집계표
    // =====================================================

    [Test]
    public void Summarize_ListsEveryConditionWithLandedRatio()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(landed: 3, fired: 3),
            new TapAutoProbe.ConditionResult
            {
                Name = "C -1600", VelocityY = -1600f, HasScrollRect = true,
                Attempted = 3, Landed = 1, UpReceived = 1, Fired = 0,
            },
        };

        string table = TapAutoProbe.Summarize(results);

        StringAssert.Contains("A 대조군", table);
        StringAssert.Contains("C -1600", table);
        // 빗맞은 탭이 몇 건인지 보여야 "실패"와 "미착지"를 사람이 구분할 수 있다.
        StringAssert.Contains("1/3", table);
        StringAssert.Contains("3/3", table);
    }

    [Test]
    public void Summarize_NoResults_SaysSo()
    {
        StringAssert.Contains("집계 없음", TapAutoProbe.Summarize(null));
        StringAssert.Contains("집계 없음", TapAutoProbe.Summarize(new List<TapAutoProbe.ConditionResult>()));
    }
}
