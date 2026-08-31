using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// InputField 탭 진단 프로브.
///
/// 스크롤 영역 안의 InputField가 실기기에서 탭에 반응하지 않는 증상을 가려내기 위한 계측입니다.
/// UIBuilder.CreateInputField가 만드는 모든 InputField에 붙어 press/release 시점의
/// PointerEventData 상태를 기록합니다.
///
/// StandaloneInputModule.ProcessTouchPress의 release 경로는 클릭을 발화시키기 전에
/// pointerUpHandler를 먼저 실행합니다(uGUI 6000.3 기준 :428, 판정은 :433-436). 그래서
/// OnPointerUp 안에서 모듈이 다섯 줄 뒤에 내릴 판정을 그대로 미리 계산할 수 있습니다:
///
///     var pointerClickHandler = ExecuteEvents.GetEventHandler&lt;IPointerClickHandler&gt;(currentOverGo);
///     if (pointerEvent.pointerClick == pointerClickHandler &amp;&amp; pointerEvent.eligibleForClick)
///
/// 구현하는 인터페이스는 IPointerDownHandler / IPointerUpHandler / IPointerClickHandler 셋뿐입니다.
/// Selectable이 이미 셋 다 구현하므로 ExecuteHierarchy(:428 기준 press 시 :396)가 고르는
/// GameObject가 달라지지 않고, ExecuteEvents.Execute는 대상 GameObject의 핸들러를 전부
/// 호출하므로 기존 라우팅에 영향이 없습니다.
///
/// IDragHandler와 IInitializePotentialDragHandler는 구현하면 안 됩니다. 특히 후자는
/// InputField에 없던 핸들러를 새로 만들어(:418에서 pointerDrag가 InputField로 잡히고
/// :421이 그 대상에게만 실행됨) ScrollRect 관성이 멈추는지 여부 자체를 바꿔버립니다.
/// 관측하려는 대상을 관측 행위가 고쳐버리는 셈이라, 진단이 무의미해집니다.
/// 이 제약은 PointerTapDiagnosticsTests의 인터페이스 가드가 강제합니다.
/// </summary>
public class PointerTapDiagnostics : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>진단 한 줄을 window.__AIT_TAPLOG에 쌓고 console.log로 흘립니다.</summary>
    [DllImport("__Internal")]
    private static extern void TAP_Log(string line);

    /// <summary>window.__AIT_TAPLOG를 비웁니다.</summary>
    [DllImport("__Internal")]
    private static extern void TAP_Clear();
#endif

    /// <summary>UP이 이 시간(초) 안에 안 오면 미수신으로 판정하고 한 줄 남깁니다.</summary>
    private const float UpTimeoutSeconds = 1.5f;

    /// <summary>버퍼에 들고 있는 최대 기록 수. 화면에는 이 중 최근 몇 건만 보여줍니다.</summary>
    public const int MaxLines = 60;

    private static readonly List<string> LinesBuffer = new List<string>();
    private static readonly List<PendingTap> PendingTaps = new List<PendingTap>();
    private static readonly List<TapRecord> RecordsBuffer = new List<TapRecord>();
    private static int _seqCounter;

    /// <summary>로그가 바뀔 때마다 증가합니다. UI가 매 프레임 문자열을 다시 만들지 않도록 하는 용도.</summary>
    public static int Revision { get; private set; }

    public static IReadOnlyList<string> Lines => LinesBuffer;

    /// <summary>
    /// 자동 진단이 판정에 쓰는 구조화 기록. 화면용 문자열과 달리 잘리지 않고 전부 남습니다 —
    /// 문자열을 되파싱하지 않고 이쪽을 읽습니다.
    /// </summary>
    public static IReadOnlyList<TapRecord> Records => RecordsBuffer;

    /// <summary>탭 하나에 대한 press/release 결과. 판정 로직이 이것만 보고 결론을 냅니다.</summary>
    public struct TapRecord
    {
        public int Seq;
        public string Label;
        public bool HasScrollRect;
        public float DownVelocityY;
        public string DownOver;
        public int DragThreshold;
        public bool UpReceived;
        public string UpOver;
        public bool WillFireClick;
        public float MovedPixels;
        public bool ClickReceived;

        /// <summary>press와 release 사이에 포인터 밑의 GameObject가 바뀌었는가.</summary>
        public bool OverChanged => UpReceived && DownOver != UpOver;
    }

    /// <summary>이 프로브를 구분할 이름. UIBuilder가 placeholder 문구로 채웁니다.</summary>
    public string Label = "input";

    private ScrollRect _scrollRect;
    private bool _scrollRectResolved;
    private int _currentSeq = -1;

    private static readonly List<PointerTapDiagnostics> Live = new List<PointerTapDiagnostics>();

    /// <summary>
    /// 살아 있는 프로브 목록. 자동 진단이 대상 InputField를 찾을 때 씁니다 —
    /// FindObjectsOfType은 Unity 6에서 이름이 바뀌어 5개 버전을 모두 지원하기 번거롭습니다.
    /// </summary>
    public static IReadOnlyList<PointerTapDiagnostics> LiveProbes => Live;

    /// <summary>Label이 일치하는 첫 프로브. 없으면 null.</summary>
    public static PointerTapDiagnostics FindByLabel(string label)
    {
        foreach (var p in Live)
        {
            if (p != null && p.Label == label) return p;
        }
        return null;
    }

    private void OnEnable() => Live.Add(this);

    private void OnDisable() => Live.Remove(this);

    /// <summary>이 프로브가 속한 ScrollRect. 없으면 null(스크롤 밖).</summary>
    public ScrollRect OwningScrollRect => ResolveScrollRect();

    private struct PendingTap
    {
        public int Seq;
        public string Label;
        public float DownTime;
    }

    /// <summary>
    /// press/release 시점에서 뽑아낸 값들. uGUI 타입을 담지 않아 EditMode에서 그대로 만들 수 있습니다.
    /// </summary>
    public struct TapSnapshot
    {
        public string Over;            // pointerCurrentRaycast.gameObject
        public string Press;           // pointerPressRaycast.gameObject
        public string PointerPress;
        public string PointerDrag;
        public string PointerClick;
        public string ReleaseHandler;  // release 시점 currentOverGo로 다시 구한 IPointerClickHandler
        public bool Eligible;
        public bool Dragging;
        public bool WillFireClick;
        public float MovedPixels;
        public int DragThreshold;
        public bool HasScrollRect;
        public Vector2 ScrollVelocity;
    }

    // ─── 판정 ───

    /// <summary>
    /// StandaloneInputModule이 :436에서 내리는 판정을 그대로 옮긴 것입니다.
    /// 값이 아니라 참조를 비교합니다 — 이름이 같은 GameObject가 둘 있어도 구분되어야 합니다.
    /// </summary>
    public static bool WillFireClick(GameObject pointerClick, GameObject releaseHandler, bool eligibleForClick)
    {
        return pointerClick == releaseHandler && eligibleForClick;
    }

    /// <summary>클릭이 발화하지 않는 이유를 한국어 한 조각으로 돌려줍니다. 발화하면 빈 문자열.</summary>
    public static string ExplainNoFire(TapSnapshot s)
    {
        if (s.WillFireClick) return "";
        if (!s.Eligible) return "eligibleForClick=0 — 드래그로 전환되며 클릭이 취소됨";
        return $"press 때 잡힌 핸들러({s.PointerClick})와 release 때 핸들러({s.ReleaseHandler})가 다름";
    }

    // ─── 포맷 ───

    public static string FormatDown(int seq, string label, TapSnapshot s)
    {
        string scroll = s.HasScrollRect
            ? $"scroll=YES vel=({s.ScrollVelocity.x:F0},{s.ScrollVelocity.y:F0})"
            : "scroll=NO";
        return $"#{seq} DOWN {label} | {scroll} thr={s.DragThreshold}\n"
             + $"    over={s.Over} press={s.PointerPress} drag={s.PointerDrag}";
    }

    public static string FormatUp(int seq, string label, TapSnapshot s)
    {
        string verdict = s.WillFireClick ? "FIRE=YES" : $"FIRE=NO — {ExplainNoFire(s)}";
        return $"#{seq} UP   {label} | moved={s.MovedPixels:F1}px elig={(s.Eligible ? 1 : 0)} dragging={(s.Dragging ? 1 : 0)}\n"
             + $"    over={s.Over} click={s.PointerClick} hnd={s.ReleaseHandler}\n"
             + $"    => {verdict}";
    }

    public static string FormatClick(int seq, string label, bool focused)
    {
        return $"#{seq} CLICK {label} | isFocused={(focused ? 1 : 0)}";
    }

    public static string FormatUpMissing(int seq, string label)
    {
        return $"#{seq} UP   {label} | 미수신 — ProcessDrag가 pointerPress를 null로 만든 뒤 release됨";
    }

    // ─── 로그 ───

    public static void Clear()
    {
        LinesBuffer.Clear();
        PendingTaps.Clear();
        RecordsBuffer.Clear();
        Revision++;
#if UNITY_WEBGL && !UNITY_EDITOR
        try { TAP_Clear(); } catch (System.Exception e) { Debug.LogError($"[TapDiag] TAP_Clear failed: {e.Message}"); }
#endif
    }

    private static void Append(string line)
    {
        LinesBuffer.Add(line);
        while (LinesBuffer.Count > MaxLines) LinesBuffer.RemoveAt(0);
        Revision++;

        Debug.Log($"[TapDiag] {line}");
#if UNITY_WEBGL && !UNITY_EDITOR
        try { TAP_Log(line); } catch (System.Exception e) { Debug.LogError($"[TapDiag] TAP_Log failed: {e.Message}"); }
#endif
    }

    /// <summary>
    /// UP이 안 온 press를 걷어냅니다. TapDiagnosticsTester.Update가 매 프레임 호출합니다.
    /// ProcessDrag가 pointerPress를 null로 만든 경우(:pointerPress != pointerDrag 분기)
    /// release 때 Execute 대상이 null이라 OnPointerUp이 아예 오지 않습니다 — 그 자체가 신호입니다.
    /// </summary>
    public static void PollPendingTaps()
    {
        if (PendingTaps.Count == 0) return;
        float now = Time.realtimeSinceStartup;
        for (int i = PendingTaps.Count - 1; i >= 0; i--)
        {
            if (now - PendingTaps[i].DownTime < UpTimeoutSeconds) continue;
            Append(FormatUpMissing(PendingTaps[i].Seq, PendingTaps[i].Label));
            PendingTaps.RemoveAt(i);
        }
    }

    // ─── 이벤트 핸들러 ───

    public void OnPointerDown(PointerEventData eventData)
    {
        _currentSeq = ++_seqCounter;
        var s = Sample(eventData, includeRelease: false);
        Append(FormatDown(_currentSeq, Label, s));
        PendingTaps.Add(new PendingTap { Seq = _currentSeq, Label = Label, DownTime = Time.realtimeSinceStartup });
        RecordsBuffer.Add(new TapRecord
        {
            Seq = _currentSeq,
            Label = Label,
            HasScrollRect = s.HasScrollRect,
            DownVelocityY = s.ScrollVelocity.y,
            DownOver = s.Over,
            DragThreshold = s.DragThreshold,
        });
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        int seq = _currentSeq;
        RemovePending(seq);
        var s = Sample(eventData, includeRelease: true);
        Append(FormatUp(seq, Label, s));
        Patch(seq, r =>
        {
            r.UpReceived = true;
            r.UpOver = s.Over;
            r.WillFireClick = s.WillFireClick;
            r.MovedPixels = s.MovedPixels;
            return r;
        });
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        var field = GetComponent<InputField>();
        Append(FormatClick(_currentSeq, Label, field != null && field.isFocused));
        Patch(_currentSeq, r => { r.ClickReceived = true; return r; });
    }

    private static void Patch(int seq, System.Func<TapRecord, TapRecord> edit)
    {
        for (int i = RecordsBuffer.Count - 1; i >= 0; i--)
        {
            if (RecordsBuffer[i].Seq != seq) continue;
            RecordsBuffer[i] = edit(RecordsBuffer[i]);
            return;
        }
    }

    private static void RemovePending(int seq)
    {
        for (int i = PendingTaps.Count - 1; i >= 0; i--)
        {
            if (PendingTaps[i].Seq == seq) PendingTaps.RemoveAt(i);
        }
    }

    private TapSnapshot Sample(PointerEventData e, bool includeRelease)
    {
        var overGo = e.pointerCurrentRaycast.gameObject;
        GameObject releaseHandler = null;
        if (includeRelease && overGo != null)
        {
            releaseHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(overGo);
        }

        var scroll = ResolveScrollRect();
        return new TapSnapshot
        {
            Over = Name(overGo),
            Press = Name(e.pointerPressRaycast.gameObject),
            PointerPress = Name(e.pointerPress),
            PointerDrag = Name(e.pointerDrag),
            PointerClick = Name(e.pointerClick),
            ReleaseHandler = includeRelease ? Name(releaseHandler) : "-",
            Eligible = e.eligibleForClick,
            Dragging = e.dragging,
            WillFireClick = includeRelease && WillFireClick(e.pointerClick, releaseHandler, e.eligibleForClick),
            MovedPixels = Vector2.Distance(e.position, e.pressPosition),
            DragThreshold = EventSystem.current != null ? EventSystem.current.pixelDragThreshold : -1,
            HasScrollRect = scroll != null,
            ScrollVelocity = scroll != null ? scroll.velocity : Vector2.zero,
        };
    }

    /// <summary>
    /// ScrollRect는 첫 이벤트 때 찾습니다. AddComponent 시점에는 부모 체인이 아직 완성되지
    /// 않았을 수 있어서 Awake에서 찾으면 놓칩니다.
    /// </summary>
    private ScrollRect ResolveScrollRect()
    {
        if (!_scrollRectResolved)
        {
            _scrollRect = GetComponentInParent<ScrollRect>();
            _scrollRectResolved = true;
        }
        return _scrollRect;
    }

    private static string Name(GameObject go) => go != null ? go.name : "(none)";
}
