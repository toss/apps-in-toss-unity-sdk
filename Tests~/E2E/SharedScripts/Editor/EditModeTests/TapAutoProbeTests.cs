// -----------------------------------------------------------------------
// TapAutoProbeTests.cs - 자동 탭 진단의 판정 로직과 관성 산수 검증
// Level 0: 실기기·브라우저 없이 검증한다.
//
// 자동 진단은 실기기에서 딱 한 번 돌고 그 결과로 #1141의 방향이 갈린다. 집계는 맞는데
// 판정문이 틀리면 팀이 잘못된 결론을 그대로 믿는다. Conclude/Summarize와 관성 산수는
// Unity 타입에 의존하지 않으므로 전 분기를 여기서 고정한다.
//
// 이 파일이 지키는 가장 중요한 성질: "하네스가 안 돌았다"와 "가설이 반증됐다"를 절대 섞지
// 않는다. 합성 탭이 빗맞아 표본이 없는 상황은 FIRE=NO가 아니라 판정 불가여야 한다.
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class TapAutoProbeTests
{
    private const float Hold = TapAutoProbe.HoldMs / 1000f;
    private const float Decel = 0.135f;

    private static TapAutoProbe.HarnessInfo Info(bool inertia = true, float targetHeight = 40f)
    {
        return new TapAutoProbe.HarnessInfo
        {
            Resolved = true,
            ControlName = TapAutoProbe.ControlLabel,
            TargetName = TapAutoProbe.TargetLabel,
            Inertia = inertia,
            MovementType = "Elastic",
            DecelerationRate = Decel,
            ScaleFactor = 3f,
            TargetHeight = targetHeight,
        };
    }

    private static TapAutoProbe.ConditionResult Control(
        int attempted, int landed, int fired, int contaminated = 0)
    {
        return new TapAutoProbe.ConditionResult
        {
            Name = "A 대조군",
            TargetLabel = TapAutoProbe.ControlLabel,
            HasScrollRect = false,
            Attempted = attempted,
            Landed = landed,
            UpReceived = landed,
            Fired = fired,
            Contaminated = contaminated,
        };
    }

    /// <summary>
    /// 스크롤 조건 하나. up을 안 주면 전부 수신, measured를 안 주면 명령 속도가 그대로
    /// 유지된 것으로 본다 — 즉 기본값은 "깨끗한 조건"이다.
    /// </summary>
    private static TapAutoProbe.ConditionResult Scroll(
        string name, float displacement, int attempted, int landed, int fired,
        int up = -1, float measured = -1f, int contaminated = 0,
        int overChanged = 0, int timedOut = 0, int dragSelf = 0)
    {
        float velocity = TapAutoProbe.VelocityForDisplacement(displacement, Hold, Decel);
        if (up < 0) up = landed;
        if (measured < 0f) measured = Mathf.Abs(velocity);

        return new TapAutoProbe.ConditionResult
        {
            Name = name,
            TargetLabel = TapAutoProbe.TargetLabel,
            HasScrollRect = true,
            CommandedVelocity = velocity,
            ExpectedDisplacement = displacement,
            Attempted = attempted,
            Landed = landed,
            UpReceived = up,
            Fired = fired,
            Contaminated = contaminated,
            OverChanged = overChanged,
            TimedOut = timedOut,
            DragWasSelf = dragSelf,
            MeasuredVelocityAbsSum = measured * landed,
            MeasuredSamples = landed,
        };
    }

    private static TapAutoProbe.ConditionResult Stationary(int attempted, int landed, int fired)
    {
        return new TapAutoProbe.ConditionResult
        {
            Name = "B 정지",
            TargetLabel = TapAutoProbe.TargetLabel,
            HasScrollRect = true,
            CommandedVelocity = 0f,
            ExpectedDisplacement = 0f,
            Attempted = attempted,
            Landed = landed,
            UpReceived = landed,
            Fired = fired,
        };
    }

    // =====================================================
    // 관성 산수 — 스윕 설계의 근거
    // =====================================================

    [Test]
    public void DisplacementPerUnitVelocity_MatchesClosedForm()
    {
        // ScrollRect의 v *= pow(rate, dt); pos += v*dt 를 연속 근사하면
        // ∫₀ᵀ rate^t dt = (rate^T − 1) / ln(rate). 80ms·0.135에서 0.0739.
        float f = TapAutoProbe.DisplacementPerUnitVelocity(Hold, Decel);
        Assert.AreEqual(0.073918f, f, 1e-4f);
    }

    [Test]
    public void DisplacementPerUnitVelocity_NoDecay_IsLinear()
    {
        // 감쇠가 없으면 그냥 v*T다. 계수가 범위를 벗어난 값으로 들어와도 NaN이 나오면 안 된다.
        Assert.AreEqual(0.08f, TapAutoProbe.DisplacementPerUnitVelocity(0.08f, 1f), 1e-6f);
        Assert.AreEqual(0.08f, TapAutoProbe.DisplacementPerUnitVelocity(0.08f, 0f), 1e-6f);
        Assert.AreEqual(0f, TapAutoProbe.DisplacementPerUnitVelocity(0f, Decel), 1e-6f);
    }

    [Test]
    public void VelocityForDisplacement_RoundTripsThroughDisplacement()
    {
        foreach (float d in new[] { 10f, 20f, 50f, 90f })
        {
            float v = TapAutoProbe.VelocityForDisplacement(d, Hold, Decel);
            float back = v * TapAutoProbe.DisplacementPerUnitVelocity(Hold, Decel);
            Assert.AreEqual(d, back, 1e-2f, $"변위 {d} 왕복 실패");
        }
    }

    [Test]
    public void SweepDisplacements_StraddleTheWidgetFlipThreshold()
    {
        // 합성 탭은 손가락을 안 움직이므로 클릭이 죽는 경로는 "콘텐츠가 흘러 조준점이 위젯을
        // 벗어나는 것"뿐이다. 문턱은 위젯 높이(40)의 절반인 20. 스윕이 그 아래와 위를 모두
        // 덮지 않으면 가설을 시험한 적이 없는데도 결론이 나온다.
        float flip = Info().FlipThreshold;
        var sweep = TapAutoProbe.SweepDisplacements;

        Assert.Greater(sweep.Length, 2, "스윕이 너무 성기다");
        Assert.Less(sweep[0], flip, "문턱 아래 음성 대조군이 있어야 한다");
        Assert.Greater(sweep[sweep.Length - 1], flip * 2f, "문턱을 넉넉히 넘는 조건이 있어야 한다");

        int above = 0;
        for (int i = 1; i < sweep.Length; i++)
        {
            Assert.Greater(sweep[i], sweep[i - 1], "스윕은 오름차순이어야 한다");
            if (sweep[i] >= flip) above++;
        }
        Assert.GreaterOrEqual(above, 3, "문턱 위 조건이 최소 3개는 있어야 임계를 좁힐 수 있다");
    }

    // =====================================================
    // 하네스 전제 — 안 서면 아무 말도 하지 않는다
    // =====================================================

    [Test]
    public void Conclude_NotResolved_ReportsTheProblemAndNothingElse()
    {
        var info = Info();
        info.Resolved = false;
        info.Problem = "라벨로 표적을 찾지 못했습니다(표적 \"test-key\"=없음).";

        string verdict = TapAutoProbe.Conclude(info, new List<TapAutoProbe.ConditionResult>());

        StringAssert.Contains("불가", verdict);
        StringAssert.Contains("test-key", verdict);
        StringAssert.DoesNotContain("관성 가설", verdict);
    }

    [Test]
    public void Conclude_InertiaOff_IsInconclusiveNotRefutation()
    {
        // inertia가 꺼져 있으면 velocity가 매 프레임 지워져 스윕이 통째로 무의미하다.
        // 이때 전 조건 FIRE=YES가 나오는데, 이걸 반증이라고 말하면 최악의 오판이다.
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(3, 3, 3),
            Stationary(3, 3, 3),
            Scroll("C +90", 90f, 3, 3, 3),
        };

        string verdict = TapAutoProbe.Conclude(Info(inertia: false), results);

        StringAssert.Contains("불가", verdict);
        StringAssert.Contains("inertia", verdict);
        StringAssert.DoesNotContain("관성 가설 반증", verdict);
    }

    [Test]
    public void Conclude_NoResults_IsInconclusive()
    {
        StringAssert.Contains("불가", TapAutoProbe.Conclude(Info(), new List<TapAutoProbe.ConditionResult>()));
        StringAssert.Contains("불가", TapAutoProbe.Conclude(Info(), null));
    }

    // =====================================================
    // 대조군이 먼저 답을 가른다
    // =====================================================

    [Test]
    public void Conclude_ControlNeverLanded_IsInconclusiveNotRefutation()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(3, 0, 0),
            Stationary(3, 0, 0),
        };

        string verdict = TapAutoProbe.Conclude(Info(), results);

        StringAssert.Contains("불가", verdict);
        StringAssert.DoesNotContain("폐기", verdict);
        StringAssert.DoesNotContain("유력", verdict);
    }

    [Test]
    public void Conclude_ControlUnderSampled_IsInconclusive()
    {
        // 3번 시도해 1번 착지, 그 1번이 발화. Fired == Landed라 예전 로직은 "정상"으로 통과시켰다.
        // 표본 1개짜리 대조군으로는 나머지를 해석할 수 없다.
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(3, 1, 1),
            Stationary(3, 3, 3),
            Scroll("C +90", 90f, 3, 3, 0),
        };

        string verdict = TapAutoProbe.Conclude(Info(), results);

        StringAssert.StartsWith("판정: 불가", verdict);
        StringAssert.Contains("표본", verdict);
        StringAssert.DoesNotContain("유력", verdict);
    }

    [Test]
    public void Conclude_ControlContaminated_IsInconclusive()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(3, 3, 3, contaminated: 2),
            Stationary(3, 3, 3),
        };

        string verdict = TapAutoProbe.Conclude(Info(), results);

        StringAssert.Contains("불가", verdict);
        StringAssert.Contains("만지지", verdict);
    }

    [Test]
    public void Conclude_ControlAlsoFails_DiscardsEveryHypothesis()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(3, 3, 0),
            Stationary(3, 3, 0),
        };

        string verdict = TapAutoProbe.Conclude(Info(), results);

        // 스크롤 밖에서도 클릭이 안 나면 ScrollRect 가설은 전부 무의미하다.
        StringAssert.Contains("폐기", verdict);
        StringAssert.Contains("무관", verdict);
    }

    [Test]
    public void Conclude_FlakyControl_WarnsButStillConcludes()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(3, 3, 2),
            Stationary(3, 3, 3),
            Scroll("C -50", 50f, 3, 3, 0),
        };

        string verdict = TapAutoProbe.Conclude(Info(), results);

        StringAssert.Contains("유력", verdict);
        // 대조군이 흔들렸다는 사실을 숨기면 결론의 신뢰도를 과대평가하게 된다.
        StringAssert.Contains("흔들", verdict);
    }

    // =====================================================
    // 명령한 속도가 실제로 유지됐는가
    // =====================================================

    [Test]
    public void Conclude_CommandedVelocityNotHonored_IsInconclusive()
    {
        // 탄성 경계나 관성 억제가 개입해 press 시점 속도가 사라지면, 다른 값이 다 깨끗해도
        // 관성 가설을 시험한 셈이 아니다.
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(3, 3, 3),
            Stationary(3, 3, 3),
            Scroll("C +30", 30f, 3, 3, 3),
            Scroll("C +90", 90f, 3, 3, 3, measured: 12f),
        };

        string verdict = TapAutoProbe.Conclude(Info(), results);

        StringAssert.StartsWith("판정: 불가", verdict);
        StringAssert.Contains("C +90", verdict);
        StringAssert.Contains("실측", verdict);
        StringAssert.DoesNotContain("관성 가설 반증", verdict);
    }

    // =====================================================
    // 표본·미착지를 결론에 반영한다
    // =====================================================

    [Test]
    public void Conclude_UnderSampledFailure_IsNotAThresholdCandidate()
    {
        // 3번 시도해 1번 착지, 그 1번이 실패. 표본 1개로 "임계 여기부터"를 발표하면 안 된다.
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(3, 3, 3),
            Stationary(3, 3, 3),
            Scroll("C +30", 30f, 3, 1, 0),
        };

        string verdict = TapAutoProbe.Conclude(Info(), results);

        StringAssert.DoesNotContain("변위 30", verdict);
        StringAssert.Contains("미검증", verdict);
    }

    [Test]
    public void Conclude_HighSpeedConditionsNeverLanded_IsNotRefutation()
    {
        // 저속만 착지·발화하고 고속이 전부 빗맞은 경우. 예전 로직은 미착지 조건을 소리 없이
        // 빼고 "쓸어본 모든 속도에서 FIRE=YES"라고 단정했다 — 가설을 시험한 적이 없는데도.
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(3, 3, 3),
            Stationary(3, 3, 3),
            Scroll("C -10", 10f, 3, 3, 3),
            Scroll("C +10", 10f, 3, 3, 3),
            Scroll("C -50", 50f, 3, 0, 0),
            Scroll("C +90", 90f, 3, 0, 0),
        };

        string verdict = TapAutoProbe.Conclude(Info(), results);

        StringAssert.Contains("부분 불가", verdict);
        StringAssert.DoesNotContain("관성 가설 반증", verdict);
        StringAssert.DoesNotContain("모든 변위", verdict);
        // 무엇이 미검증인지 이름으로 열거해야 사람이 다시 돌릴 판단을 할 수 있다.
        StringAssert.Contains("C -50", verdict);
        StringAssert.Contains("C +90", verdict);
    }

    [Test]
    public void Conclude_UntestedConditionsQualifyTheThreshold()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(3, 3, 3),
            Stationary(3, 3, 3),
            Scroll("C +50", 50f, 3, 3, 0),
            Scroll("C +30", 30f, 3, 0, 0),
        };

        string verdict = TapAutoProbe.Conclude(Info(), results);

        StringAssert.Contains("유력", verdict);
        // 미검증 구간이 있으면 임계는 "시험된 범위에서"의 최솟값일 뿐이다.
        StringAssert.Contains("시험된 범위", verdict);
        StringAssert.Contains("낮을 수 있습니다", verdict);
    }

    // =====================================================
    // UP 미수신은 관성과 다른 경로다
    // =====================================================

    [Test]
    public void Conclude_AllMovingConditionsMissUp_GetsItsOwnHeadline()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(3, 3, 3),
            Stationary(3, 3, 3),
            Scroll("C +50", 50f, 3, 3, 0, up: 0),
            Scroll("C +90", 90f, 3, 3, 0, up: 1),
        };

        string verdict = TapAutoProbe.Conclude(Info(), results);

        StringAssert.Contains("UP 미수신", verdict);
        StringAssert.Contains("pointerPress", verdict);
        // 관성 임계로 흡수되면 안 된다.
        StringAssert.DoesNotContain("유력", verdict);
        StringAssert.DoesNotContain("변위 50", verdict);
    }

    [Test]
    public void Conclude_SomeMissUp_ExcludedFromThresholdSearch()
    {
        // UP 미수신 조건은 구조상 Fired < Landed라 임계 후보가 되어버린다. 더 낮은 변위에
        // 있으면 임계를 통째로 왜곡한다. 높이 44라 문턱 22 — 아래 숫자와 겹치지 않게 잡았다.
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(3, 3, 3),
            Stationary(3, 3, 3),
            Scroll("C +20", 20f, 3, 3, 0, up: 1),
            Scroll("C +50", 50f, 3, 3, 0),
        };

        string verdict = TapAutoProbe.Conclude(Info(targetHeight: 44f), results);

        StringAssert.Contains("변위 50", verdict);
        StringAssert.DoesNotContain("변위 20", verdict);
        StringAssert.Contains("임계 탐색에서 제외", verdict);
    }

    // =====================================================
    // 임계 탐색
    // =====================================================

    [Test]
    public void Conclude_ReportsLowestFailingDisplacementRegardlessOfOrder()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(3, 3, 3),
            Stationary(3, 3, 3),
            Scroll("C -90", 90f, 3, 3, 0),
            Scroll("C +50", 50f, 3, 3, 1),
            Scroll("C -30", 30f, 3, 3, 3),
            Scroll("C +20", 20f, 3, 3, 3),
        };

        string verdict = TapAutoProbe.Conclude(Info(), results);

        StringAssert.Contains("유력", verdict);
        // 부호가 아니라 크기로, 목록 순서와 무관하게 가장 낮은 실패 변위를 집어야 한다.
        StringAssert.Contains("변위 50", verdict);
        StringAssert.DoesNotContain("변위 90", verdict);
    }

    [Test]
    public void Conclude_PartialFailureAtOneDisplacement_CountsAsFailure()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(3, 3, 3),
            Stationary(3, 3, 3),
            // 3번 중 2번만 발화해도 실패다. 간헐 증상이라 전부 실패할 이유가 없다.
            Scroll("C -30", 30f, 3, 3, 2),
        };

        StringAssert.Contains("변위 30", TapAutoProbe.Conclude(Info(), results));
    }

    [Test]
    public void Conclude_FailureBelowFlipThreshold_FlagsAnomaly()
    {
        // 변위 10은 위젯 문턱 20을 못 넘어 구조적으로 클릭이 죽을 수 없다. 여기서 죽었다면
        // 콘텐츠 이동 말고 다른 원인이 함께 있다는 뜻이고, 그 사실을 반드시 알려야 한다.
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(3, 3, 3),
            Stationary(3, 3, 3),
            Scroll("C -10", 10f, 3, 3, 0),
        };

        string verdict = TapAutoProbe.Conclude(Info(), results);

        StringAssert.Contains("이상", verdict);
        StringAssert.Contains("문턱", verdict);
    }

    [Test]
    public void Conclude_EveryTestedDisplacementFires_RefutesInertiaHypothesis()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(3, 3, 3),
            Stationary(3, 3, 3),
            Scroll("C -30", 30f, 3, 3, 3),
            Scroll("C +90", 90f, 3, 3, 3),
        };

        string verdict = TapAutoProbe.Conclude(Info(), results);

        StringAssert.Contains("관성 가설 반증", verdict);
        StringAssert.DoesNotContain("유력", verdict);
    }

    [Test]
    public void Conclude_StationaryAlsoFails_BlamesScrollRectNotVelocity()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(3, 3, 3),
            Stationary(3, 3, 0),
            Scroll("C -50", 50f, 3, 3, 0),
        };

        string verdict = TapAutoProbe.Conclude(Info(), results);

        StringAssert.Contains("관성 무관", verdict);
        StringAssert.DoesNotContain("유력", verdict);
    }

    // =====================================================
    // 부가 신호와 문장 형태
    // =====================================================

    [Test]
    public void Conclude_DragWasSelf_IsReportedAsDirectEvidence()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(3, 3, 3),
            Stationary(3, 3, 3),
            Scroll("C -50", 50f, 3, 3, 0, overChanged: 3, dragSelf: 3),
        };

        string verdict = TapAutoProbe.Conclude(Info(), results);

        // pointerDrag가 InputField 자신이면 ScrollRect가 드래그 대상으로 안 잡혔다는 직접 증거다.
        StringAssert.Contains("pointerDrag", verdict);
        StringAssert.Contains("over 변화 3건", verdict);
    }

    [Test]
    public void Conclude_CleanRun_HasNoSpuriousNotes()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(3, 3, 3),
            Stationary(3, 3, 3),
            Scroll("C -30", 30f, 3, 3, 3),
        };

        string verdict = TapAutoProbe.Conclude(Info(), results);

        Assert.AreEqual(1, verdict.Split('\n').Length, "깨끗한 런에 각주가 붙으면 안 된다:\n" + verdict);
    }

    [Test]
    public void Conclude_HeadlineIsTheFirstLine()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(3, 3, 3),
            Stationary(3, 3, 3),
            Scroll("C -50", 50f, 3, 3, 0, overChanged: 2, timedOut: 1),
        };

        string first = TapAutoProbe.Conclude(Info(), results).Split('\n')[0];

        // 화면이 좁아 첫 줄만 보이는 경우가 흔하다. 결론이 거기 있어야 한다.
        StringAssert.StartsWith("판정:", first);
        StringAssert.Contains("유력", first);
    }

    [Test]
    public void Conclude_NeverClaimsUnmeasuredMechanismAsFact()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(3, 3, 3),
            Stationary(3, 3, 3),
            Scroll("C -50", 50f, 3, 3, 0),
        };

        string verdict = TapAutoProbe.Conclude(Info(), results);

        // 프로브는 IInitializePotentialDragHandler를 일부러 구현하지 않아 그 호출 여부를 못 본다.
        // 관측하는 것은 클릭 판정식뿐이므로 근인을 단정하면 안 된다.
        StringAssert.DoesNotContain("근인", verdict);
        StringAssert.DoesNotContain("확정", verdict);
    }

    // =====================================================
    // 집계표 — 자동 판정이 틀렸을 때 대조할 원본
    // =====================================================

    [Test]
    public void Summarize_ShowsHarnessPreconditionsAndTargetLabels()
    {
        string table = TapAutoProbe.Summarize(Info(), new List<TapAutoProbe.ConditionResult>
        {
            Control(3, 3, 3),
        });

        // 무엇을 때렸는지, 어떤 전제 위에서 돌았는지가 결과지에 남아야 한다.
        StringAssert.Contains(TapAutoProbe.ControlLabel, table);
        StringAssert.Contains(TapAutoProbe.TargetLabel, table);
        StringAssert.Contains("inertia=1", table);
        StringAssert.Contains("Elastic", table);
        StringAssert.Contains("문턱=20", table);
    }

    [Test]
    public void Summarize_ShowsMeasuredVelocityAndLandedRatio()
    {
        var results = new List<TapAutoProbe.ConditionResult>
        {
            Control(3, 3, 3),
            Scroll("C -90", 90f, 3, 1, 0, measured: 42f),
        };

        string table = TapAutoProbe.Summarize(Info(), results);

        StringAssert.Contains("C -90", table);
        // 빗맞은 탭이 몇 건인지 보여야 "실패"와 "미착지"를 사람이 구분할 수 있다.
        StringAssert.Contains("1/3", table);
        StringAssert.Contains("3/3", table);
        // 실측 속도가 없으면 명령값이 유지됐는지 사람이 확인할 방법이 없다.
        StringAssert.Contains("42", table);
        StringAssert.Contains("표본부족", table);
    }

    [Test]
    public void Summarize_NoResults_StillShowsHarnessInfo()
    {
        string table = TapAutoProbe.Summarize(Info(), new List<TapAutoProbe.ConditionResult>());

        StringAssert.Contains("집계 없음", table);
        StringAssert.Contains("inertia=1", table);
    }

    // =====================================================
    // C# ↔ jslib 정합성
    //
    // 저장소의 자동 invariant 검사(sdk-runtime-generator~의 vitest)는 Runtime/SDK만 훑는다.
    // 테스트용 브리지의 신규 함수는 그 그물 밖이라, 한쪽 시그니처만 바뀌면 실기기에서
    // "함수를 찾을 수 없다"로 죽을 때까지 아무도 모른다. 여기서 양쪽 소스를 직접 대조한다.
    // =====================================================

    /// <summary>이 테스트 파일의 컴파일 시점 경로. 저장소 안 다른 파일을 찾는 기준점이다.</summary>
    private static string ThisFile([CallerFilePath] string path = null) => path;

    private static string ReadSibling(string relative)
    {
        string dir = Path.GetDirectoryName(ThisFile());
        if (string.IsNullOrEmpty(dir)) return null;
        string full = Path.GetFullPath(Path.Combine(dir, relative));
        return File.Exists(full) ? File.ReadAllText(full) : null;
    }

    [Test]
    public void SyntheticTapBridge_SignaturesMatchOnBothSides()
    {
        string cs = ReadSibling("../../Runtime/TapAutoProbe.cs");
        string js = ReadSibling("../../Plugins/E2ETestBridge.jslib");
        if (cs == null || js == null)
        {
            Assert.Ignore("소스 경로를 찾지 못했다(패키지가 복사된 환경). 대조를 건너뛴다.");
        }

        var csTap = Regex.Match(cs, @"static\s+extern\s+void\s+TAP_Tap\s*\(([^)]*)\)");
        var jsTap = Regex.Match(js, @"TAP_Tap\s*:\s*function\s*\(([^)]*)\)");
        Assert.IsTrue(csTap.Success, "C#에 TAP_Tap extern이 없다");
        Assert.IsTrue(jsTap.Success, "jslib에 TAP_Tap이 없다");
        Assert.AreEqual(ArgCount(csTap.Groups[1].Value), ArgCount(jsTap.Groups[1].Value),
            "TAP_Tap의 인자 개수가 C#과 jslib에서 다르다");

        var csBusy = Regex.Match(cs, @"static\s+extern\s+int\s+TAP_DriveBusy\s*\(([^)]*)\)");
        var jsBusy = Regex.Match(js, @"TAP_DriveBusy\s*:\s*function\s*\(([^)]*)\)");
        Assert.IsTrue(csBusy.Success, "C#에 TAP_DriveBusy extern이 없다");
        Assert.IsTrue(jsBusy.Success, "jslib에 TAP_DriveBusy가 없다");
        Assert.AreEqual(0, ArgCount(csBusy.Groups[1].Value));
        Assert.AreEqual(0, ArgCount(jsBusy.Groups[1].Value));
    }

    [Test]
    public void TapLoggingBridge_ExistsOnBothSides()
    {
        string cs = ReadSibling("../../Runtime/PointerTapDiagnostics.cs");
        string js = ReadSibling("../../Plugins/E2ETestBridge.jslib");
        if (cs == null || js == null) Assert.Ignore("소스 경로를 찾지 못했다. 대조를 건너뛴다.");

        foreach (string name in new[] { "TAP_Log", "TAP_Clear" })
        {
            StringAssert.Contains($"extern void {name}", cs);
            StringAssert.Contains($"{name}: function", js);
        }
    }

    private static int ArgCount(string paramList)
    {
        paramList = paramList.Trim();
        return paramList.Length == 0 ? 0 : paramList.Split(',').Length;
    }
}
