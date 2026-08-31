// -----------------------------------------------------------------------
// PointerTapDiagnosticsTests.cs - 탭 진단 프로브의 판정식과 인터페이스 제약 검증
// Level 0: 실기기·브라우저 없이 검증한다.
//
// 이 파일에서 가장 중요한 것은 인터페이스 가드다. 프로브는 스크롤 영역 안 InputField가
// 탭에 반응하지 않는 증상을 관측하려고 붙이는 것인데, IInitializePotentialDragHandler를
// 구현해버리면 InputField에 없던 핸들러가 생겨 ScrollRect 관성이 멈추는지 여부 자체가
// 바뀐다. 관측이 대상을 고쳐버리면 진단이 무의미해지므로 컴파일 타임 실수를 여기서 잡는다.
//
// 메모: AppsInTossEditModeTests 어셈블리는 overrideReferences=true라 UnityEngine.UI.dll을
//   참조하지 않는다. 그래서 uGUI 이벤트 인터페이스를 타입으로 쓰지 않고 FullName 문자열로
//   대조한다.
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class PointerTapDiagnosticsTests
{
    private const string EventSystemsNs = "UnityEngine.EventSystems.";

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
        {
            if (go != null) Object.DestroyImmediate(go);
        }
        _spawned.Clear();
    }

    private GameObject NewGo(string name)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        return go;
    }

    // =====================================================
    // 인터페이스 가드
    // =====================================================

    [Test]
    public void Probe_ImplementsExactlyTheThreeAllowedEventInterfaces()
    {
        var actual = typeof(PointerTapDiagnostics).GetInterfaces()
            .Select(t => t.FullName)
            .Where(n => n != null && n.StartsWith(EventSystemsNs))
            .OrderBy(n => n)
            .ToArray();

        var expected = new[]
        {
            // 세 핸들러가 모두 상속하는 마커 인터페이스라 transitive로 딸려온다. 동작을 담지 않는다.
            EventSystemsNs + "IEventSystemHandler",
            EventSystemsNs + "IPointerClickHandler",
            EventSystemsNs + "IPointerDownHandler",
            EventSystemsNs + "IPointerUpHandler",
        };

        // 하나라도 늘거나 줄면 실패한다. InputField(Selectable)가 이미 구현한 셋과 정확히
        // 같아야 ExecuteHierarchy가 고르는 GameObject가 달라지지 않는다.
        CollectionAssert.AreEqual(expected, actual);
    }

    [Test]
    public void Probe_MustNotImplementDragInterfaces()
    {
        var implemented = typeof(PointerTapDiagnostics).GetInterfaces()
            .Select(t => t.FullName)
            .ToArray();

        // IInitializePotentialDragHandler가 특히 위험하다 — InputField에 없던 핸들러가 생겨
        // ScrollRect의 관성 정지 동작이 바뀐다. 나머지도 드래그 라우팅에 관여하므로 함께 막는다.
        foreach (var forbidden in new[]
        {
            "IInitializePotentialDragHandler",
            "IBeginDragHandler",
            "IDragHandler",
            "IEndDragHandler",
            "IDropHandler",
            "IScrollHandler",
        })
        {
            CollectionAssert.DoesNotContain(implemented, EventSystemsNs + forbidden,
                $"{forbidden}를 구현하면 프로브가 관측 대상의 동작을 바꾼다");
        }
    }

    // =====================================================
    // 판정식 — StandaloneInputModule.cs:436과 같아야 한다
    // =====================================================

    [Test]
    public void WillFireClick_SameHandlerAndEligible_IsTrue()
    {
        var handler = NewGo("InputField");
        Assert.IsTrue(PointerTapDiagnostics.WillFireClick(handler, handler, true));
    }

    [Test]
    public void WillFireClick_DifferentHandler_IsFalse()
    {
        var pressHandler = NewGo("InputField");
        var releaseHandler = NewGo("OtherInputField");

        // 이름이 아니라 참조로 비교해야 한다. 같은 이름의 GameObject가 둘 있어도 구분되어야 하므로
        // 이름이 다른 쌍과 같은 쌍을 모두 확인한다.
        Assert.IsFalse(PointerTapDiagnostics.WillFireClick(pressHandler, releaseHandler, true));

        var sameNameA = NewGo("InputField");
        var sameNameB = NewGo("InputField");
        Assert.IsFalse(PointerTapDiagnostics.WillFireClick(sameNameA, sameNameB, true));
    }

    [Test]
    public void WillFireClick_NotEligible_IsFalse()
    {
        var handler = NewGo("InputField");
        Assert.IsFalse(PointerTapDiagnostics.WillFireClick(handler, handler, false));
    }

    [Test]
    public void WillFireClick_BothNull_MatchesUguiBehaviour()
    {
        // uGUI도 null == null을 통과시킨다(:436). 통과해도 Execute(null, ...)이 no-op이라
        // 무해하다. 프로브는 모듈의 판정을 그대로 비추는 게 목적이므로 여기서 갈라지면 안 된다.
        Assert.IsTrue(PointerTapDiagnostics.WillFireClick(null, null, true));
    }

    // =====================================================
    // 사유 문구
    // =====================================================

    [Test]
    public void ExplainNoFire_WhenFiring_IsEmpty()
    {
        var s = new PointerTapDiagnostics.TapSnapshot { WillFireClick = true, Eligible = true };
        Assert.AreEqual("", PointerTapDiagnostics.ExplainNoFire(s));
    }

    [Test]
    public void ExplainNoFire_NotEligible_BlamesDragCancellation()
    {
        var s = new PointerTapDiagnostics.TapSnapshot { WillFireClick = false, Eligible = false };
        StringAssert.Contains("eligibleForClick=0", PointerTapDiagnostics.ExplainNoFire(s));
    }

    [Test]
    public void ExplainNoFire_HandlerMismatch_NamesBothHandlers()
    {
        var s = new PointerTapDiagnostics.TapSnapshot
        {
            WillFireClick = false,
            Eligible = true,
            PointerClick = "InputField",
            ReleaseHandler = "(none)",
        };

        string reason = PointerTapDiagnostics.ExplainNoFire(s);

        // 두 핸들러를 모두 적어야 실기기 로그만 보고 원인을 갈라낼 수 있다.
        StringAssert.Contains("InputField", reason);
        StringAssert.Contains("(none)", reason);
        StringAssert.DoesNotContain("eligibleForClick", reason);
    }

    // =====================================================
    // 포맷 — 실기기에서 이 줄만 보고 판단할 수 있어야 한다
    // =====================================================

    [Test]
    public void FormatDown_ShowsScrollStateAndThreshold()
    {
        var s = new PointerTapDiagnostics.TapSnapshot
        {
            Over = "InputField",
            PointerPress = "InputField",
            PointerDrag = "InputField",
            DragThreshold = 20,
            HasScrollRect = true,
            ScrollVelocity = new Vector2(0f, -812f),
        };

        string line = PointerTapDiagnostics.FormatDown(3, "test-key", s);

        StringAssert.Contains("#3 DOWN test-key", line);
        StringAssert.Contains("scroll=YES", line);
        // press 순간 관성이 남아 있는지가 남은 가설의 핵심이라 속도를 반드시 찍어야 한다.
        StringAssert.Contains("-812", line);
        StringAssert.Contains("thr=20", line);
    }

    [Test]
    public void FormatDown_OutsideScrollRect_SaysSo()
    {
        var s = new PointerTapDiagnostics.TapSnapshot { Over = "InputField", HasScrollRect = false };
        string line = PointerTapDiagnostics.FormatDown(1, "Search...", s);

        // 스크롤 밖 검색바가 대조군이므로 한눈에 구분되어야 한다.
        StringAssert.Contains("scroll=NO", line);
        StringAssert.DoesNotContain("scroll=YES", line);
    }

    [Test]
    public void FormatUp_FiringCase_SaysFireYes()
    {
        var s = new PointerTapDiagnostics.TapSnapshot
        {
            Over = "InputField",
            PointerClick = "InputField",
            ReleaseHandler = "InputField",
            Eligible = true,
            WillFireClick = true,
            MovedPixels = 2.4f,
        };

        string line = PointerTapDiagnostics.FormatUp(7, "test-key", s);

        StringAssert.Contains("FIRE=YES", line);
        StringAssert.DoesNotContain("FIRE=NO", line);
        StringAssert.Contains("moved=2.4px", line);
    }

    [Test]
    public void FormatUp_NonFiringCase_CarriesTheReason()
    {
        var s = new PointerTapDiagnostics.TapSnapshot
        {
            Over = "Text",
            PointerClick = "InputField",
            ReleaseHandler = "(none)",
            Eligible = true,
            WillFireClick = false,
            MovedPixels = 11.2f,
        };

        string line = PointerTapDiagnostics.FormatUp(8, "test-key", s);

        StringAssert.Contains("FIRE=NO", line);
        StringAssert.Contains("InputField", line);
        StringAssert.Contains("(none)", line);
    }

    [Test]
    public void FormatUpMissing_ExplainsWhyNoUpArrived()
    {
        string line = PointerTapDiagnostics.FormatUpMissing(9, "test-key");

        StringAssert.Contains("#9", line);
        StringAssert.Contains("미수신", line);
        // UP이 안 오는 경로는 ProcessDrag가 pointerPress를 null로 만든 경우뿐이다.
        StringAssert.Contains("pointerPress", line);
    }
}
