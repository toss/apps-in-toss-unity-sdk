using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 탭 진단 자동 실행기.
///
/// 사람이 손으로 하던 A/B/C/D 절차를 그대로 기계가 돌립니다. 탭은 캔버스에 합성 터치
/// 이벤트를 쏘아서(<c>TAP_Tap</c>) 실제 입력 경로를 그대로 태우고, 스크롤 관성은
/// 손가락 플릭을 흉내내는 대신 <see cref="ScrollRect.velocity"/>를 직접 세팅합니다.
///
/// 관성을 직접 세팅하는 쪽을 고른 이유:
/// 검증하려는 가설이 "press 시점에 잔존 속도가 있으면 클릭이 죽는다"이므로 속도가 곧
/// 독립변수입니다. 플릭을 합성하면 속도가 우연히 정해지지만, 직접 세팅하면 계단식으로
/// 쓸어서 FIRE가 뒤집히는 임계 속도를 찾을 수 있습니다.
///
/// 한계 하나는 미리 밝혀 둡니다. 합성 이벤트는 untrusted라 iOS가 user activation으로
/// 쳐주지 않습니다. 따라서 "FIRE=YES인데 키보드가 안 뜬다"는 갈래는 이 자동 실행으로
/// 가려낼 수 없고 사람 손 탭이 한 번 필요합니다. 자동 실행이 답하는 것은 uGUI가 클릭을
/// 발화시키는가 하나입니다.
/// </summary>
public class TapAutoProbe : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>캔버스에 합성 터치 탭을 보냅니다. 좌표는 Unity 화면 정규화(좌하단 원점).</summary>
    [DllImport("__Internal")]
    private static extern void TAP_Tap(float nx, float ny, int holdMs);

    /// <summary>합성 탭이 아직 손을 떼지 않았으면 1.</summary>
    [DllImport("__Internal")]
    private static extern int TAP_DriveBusy();
#endif

    // 아래 넷은 실행 파라미터입니다. WebGL이 아닌 컴파일에서는 읽는 곳이 없어 private면
    // 미사용 경고가 나므로 public으로 둡니다 — 어차피 결과를 해석할 때 알아야 하는 값들입니다.

    /// <summary>손가락을 붙이고 있는 시간. 사람 탭의 하한쯤 됩니다.</summary>
    public const int HoldMs = 80;

    /// <summary>조건 하나당 탭 횟수.</summary>
    public const int TapsPerCondition = 3;

    /// <summary>쓸어볼 스크롤 속도(px/s). 부호를 뒤집어 위/아래 양쪽을 봅니다.</summary>
    public static readonly float[] SweepSpeeds = { 100f, 200f, 400f, 800f, 1600f };

    /// <summary>
    /// 합성 터치가 Unity 입력 큐를 타고 처리되기까지 한 프레임쯤 걸립니다. 그 사이 콘텐츠가
    /// 흐른 만큼 조준점을 미리 밀어 둡니다. 안 하면 빠른 속도에서 탭이 칸을 벗어나고,
    /// "클릭이 안 났다"와 "빗맞았다"가 섞여 버립니다.
    /// </summary>
    public const int LeadFrames = 1;

    public bool IsRunning { get; private set; }

    /// <summary>진행 상황 한 줄. UI가 매 프레임 읽어 갑니다.</summary>
    public string Progress { get; private set; } = "";

    /// <summary>마지막 실행의 판정문. 아직 안 돌렸으면 빈 문자열.</summary>
    public string Verdict { get; private set; } = "";

    private readonly List<ConditionResult> _results = new List<ConditionResult>();

    /// <summary>조건 하나(대조군 / 정지 / 속도 v)에 대한 집계.</summary>
    public struct ConditionResult
    {
        public string Name;
        public float VelocityY;
        public bool HasScrollRect;

        /// <summary>보낸 탭 수.</summary>
        public int Attempted;

        /// <summary>DOWN이 실제로 대상 프로브에 닿은 수. Attempted보다 작으면 빗맞은 것.</summary>
        public int Landed;

        public int UpReceived;

        /// <summary>uGUI가 클릭을 발화시킬 조건이 성립한 수.</summary>
        public int Fired;

        /// <summary>press와 release 사이에 포인터 밑 GameObject가 바뀐 수.</summary>
        public int OverChanged;

        public int ClickReceived;
    }

    // ─── 판정 ───

    /// <summary>
    /// 집계만 보고 결론을 냅니다. Unity 타입을 안 쓰므로 EditMode에서 그대로 검증됩니다.
    /// 첫 줄이 결론, 나머지는 근거입니다.
    /// </summary>
    public static string Conclude(IReadOnlyList<ConditionResult> results)
    {
        if (results == null || results.Count == 0)
        {
            return "판정: 불가 — 한 조건도 실행되지 않았습니다.";
        }

        var notes = new List<string>();

        ConditionResult control = default;
        bool hasControl = false;
        foreach (var r in results)
        {
            if (r.HasScrollRect) continue;
            control = r;
            hasControl = true;
            break;
        }

        if (!hasControl || control.Landed == 0)
        {
            return "판정: 불가 — 합성 탭이 대조군 입력칸에 닿지 않았습니다. 하네스가 안 돈 것이므로 "
                 + "이 결과로 가설을 판단하면 안 됩니다.";
        }

        if (control.Fired == 0)
        {
            return $"판정: 가설 전부 폐기 — 스크롤 밖 대조군도 FIRE=NO ({control.Fired}/{control.Landed}). "
                 + "ScrollRect와 무관한 원인입니다.";
        }

        if (control.Fired < control.Landed)
        {
            notes.Add($"주의: 대조군이 {control.Fired}/{control.Landed}로 불안정합니다. "
                    + "아래 결론의 신뢰도가 그만큼 낮습니다.");
        }

        var moving = new List<ConditionResult>();
        ConditionResult stationary = default;
        bool hasStationary = false;
        int scrollLanded = 0;
        int upMissing = 0;
        int overChanged = 0;

        foreach (var r in results)
        {
            upMissing += r.Landed - r.UpReceived;
            overChanged += r.OverChanged;
            if (!r.HasScrollRect) continue;
            scrollLanded += r.Landed;
            if (r.Landed == 0) continue;
            if (r.VelocityY == 0f)
            {
                stationary = r;
                hasStationary = true;
            }
            else
            {
                moving.Add(r);
            }
        }

        if (upMissing > 0)
        {
            notes.Add($"UP 미수신 {upMissing}건 — ProcessDrag가 pointerPress를 null로 만드는 경로가 "
                    + "실재합니다. 반증했다고 본 드래그 취소를 다시 봐야 합니다.");
        }
        if (overChanged > 0)
        {
            notes.Add($"press/release 사이 over 변화 {overChanged}건 — 손가락 밑에서 콘텐츠가 움직였습니다.");
        }

        string headline;
        if (scrollLanded == 0)
        {
            headline = "판정: 불가 — 스크롤 안 입력칸에 합성 탭이 한 번도 닿지 않았습니다.";
        }
        else if (hasStationary && stationary.Fired == 0)
        {
            headline = $"판정: 관성 무관 — 정지 상태에서도 FIRE=NO ({stationary.Fired}/{stationary.Landed}). "
                     + "속도가 아니라 ScrollRect 안에 있다는 사실 자체가 원인입니다.";
        }
        else
        {
            float threshold = 0f;
            bool found = false;
            foreach (var r in moving)
            {
                if (r.Fired >= r.Landed) continue;
                float speed = Mathf.Abs(r.VelocityY);
                if (!found || speed < threshold)
                {
                    threshold = speed;
                    found = true;
                }
            }

            if (!found)
            {
                headline = moving.Count == 0
                    ? "판정: 불가 — 관성 조건이 한 번도 착지하지 않았습니다."
                    : "판정: 관성 가설 반증 — 쓸어본 모든 속도에서 FIRE=YES였습니다. 합성 탭으로는 재현되지 않습니다.";
            }
            else
            {
                headline = $"판정: 관성 가설 확정 — |속도| {threshold:F0}px/s부터 클릭이 소실됩니다. "
                         + "ScrollRect에 IInitializePotentialDragHandler가 도달하지 않아 관성이 멈추지 않는 것이 근인입니다.";
            }
        }

        var sb = new StringBuilder(headline);
        foreach (var n in notes)
        {
            sb.Append('\n').Append(n);
        }
        return sb.ToString();
    }

    /// <summary>조건별 집계를 사람이 읽을 표로 만듭니다.</summary>
    public static string Summarize(IReadOnlyList<ConditionResult> results)
    {
        if (results == null || results.Count == 0) return "(집계 없음)";
        var sb = new StringBuilder();
        sb.Append("조건            착지 UP FIRE over\n");
        foreach (var r in results)
        {
            sb.Append(Pad(r.Name, 15))
              .Append(Pad($"{r.Landed}/{r.Attempted}", 5))
              .Append(Pad(r.UpReceived.ToString(), 3))
              .Append(Pad($"{r.Fired}", 5))
              .Append(r.OverChanged)
              .Append('\n');
        }
        return sb.ToString();
    }

    private static string Pad(string s, int width)
    {
        if (s == null) s = "";
        return s.Length >= width ? s.Substring(0, width - 1) + " " : s.PadRight(width);
    }

    // ─── 실행 ───

    /// <summary>버튼이 부르는 진입점. 이미 돌고 있으면 무시합니다.</summary>
    public void StartRun()
    {
        if (IsRunning) return;
        StartCoroutine(RunAll());
    }

    private IEnumerator RunAll()
    {
        IsRunning = true;
        Verdict = "";
        _results.Clear();

#if UNITY_WEBGL && !UNITY_EDITOR
        Progress = "준비 중…";
        PointerTapDiagnostics.Clear();
        yield return null;

        var control = FindProbe(wantScroll: false);
        var target = FindProbe(wantScroll: true);

        if (control == null)
        {
            Verdict = "판정: 불가 — 스크롤 밖 입력칸(대조군)을 찾지 못했습니다.";
            Progress = "";
            IsRunning = false;
            yield break;
        }

        yield return RunCondition("A 대조군", control, null, 0f);

        if (target == null)
        {
            _results.Add(new ConditionResult { Name = "B 정지", HasScrollRect = true });
            Verdict = Conclude(_results);
            Progress = "";
            IsRunning = false;
            yield break;
        }

        var scroll = target.OwningScrollRect;
        yield return RunCondition("B 정지", target, scroll, 0f);

        // 위/아래 양쪽을 봅니다. Elastic 리바운드도 결국 부호가 반대인 관성이라,
        // 경계 밖으로 밀어내는 대신 부호로 대신 덮습니다.
        foreach (float speed in SweepSpeeds)
        {
            yield return RunCondition($"C -{speed:F0}", target, scroll, -speed);
            yield return RunCondition($"C +{speed:F0}", target, scroll, speed);
        }

        if (scroll != null) scroll.velocity = Vector2.zero;
        Verdict = Conclude(_results);
        Progress = "";
        IsRunning = false;
#else
        Progress = "";
        Verdict = "판정: 불가 — 자동 진단은 WebGL 빌드에서만 동작합니다(합성 터치가 필요).";
        IsRunning = false;
        yield break;
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private IEnumerator RunCondition(string name, PointerTapDiagnostics probe, ScrollRect scroll, float velocityY)
    {
        var result = new ConditionResult
        {
            Name = name,
            VelocityY = velocityY,
            HasScrollRect = scroll != null,
        };

        var rt = probe.transform as RectTransform;
        for (int i = 0; i < TapsPerCondition; i++)
        {
            Progress = $"{name} — {i + 1}/{TapsPerCondition}";

            if (scroll != null)
            {
                CenterOn(scroll, rt);
                yield return null;
                scroll.velocity = new Vector2(0f, velocityY);
                // 속도를 세팅한 프레임에는 아직 콘텐츠가 안 움직였습니다. 한 프레임 흘려서
                // 실제로 흐르는 상태를 만든 뒤 조준합니다.
                yield return null;
            }

            float lead = velocityY * Time.unscaledDeltaTime * LeadFrames;
            Vector2 point;
            if (!TryGetScreenPoint(rt, scroll, lead, out point))
            {
                // 조준점이 뷰포트 밖이면 보내 봐야 빗맞습니다. 시도는 세되 탭은 생략합니다.
                result.Attempted++;
                continue;
            }

            int before = PointerTapDiagnostics.Records.Count;
            result.Attempted++;
            TAP_Tap(point.x / Screen.width, point.y / Screen.height, HoldMs);

            float deadline = Time.realtimeSinceStartup + 2f;
            while (TAP_DriveBusy() != 0 && Time.realtimeSinceStartup < deadline) yield return null;
            // touchend가 Unity 입력 큐를 타고 OnPointerUp/OnPointerClick까지 도는 시간.
            yield return new WaitForSecondsRealtime(0.2f);

            Tally(ref result, before, probe.Label);
        }

        if (scroll != null) scroll.velocity = Vector2.zero;
        _results.Add(result);
        yield return new WaitForSecondsRealtime(0.1f);
    }

    /// <summary>이번 탭으로 새로 생긴 기록만 골라 집계에 더합니다.</summary>
    private static void Tally(ref ConditionResult result, int startIndex, string label)
    {
        var records = PointerTapDiagnostics.Records;
        for (int i = startIndex; i < records.Count; i++)
        {
            var r = records[i];
            if (r.Label != label) continue;
            result.Landed++;
            if (r.UpReceived) result.UpReceived++;
            if (r.WillFireClick) result.Fired++;
            if (r.OverChanged) result.OverChanged++;
            if (r.ClickReceived) result.ClickReceived++;
        }
    }

    private static PointerTapDiagnostics FindProbe(bool wantScroll)
    {
        // 스크롤 안 후보가 여럿이면 Key 칸(첫 번째)이 잡힙니다 — 등록 순서가 곧 생성 순서입니다.
        foreach (var p in PointerTapDiagnostics.LiveProbes)
        {
            if (p == null || !p.isActiveAndEnabled) continue;
            if ((p.OwningScrollRect != null) == wantScroll) return p;
        }
        return null;
    }

    /// <summary>
    /// 대상을 뷰포트 한가운데로 스크롤합니다. 콘텐츠가 뷰포트보다 짧으면 할 일이 없습니다.
    /// </summary>
    private static void CenterOn(ScrollRect sr, RectTransform target)
    {
        if (sr == null || sr.content == null || sr.viewport == null || target == null) return;

        sr.velocity = Vector2.zero;
        Canvas.ForceUpdateCanvases();

        var content = sr.content;
        float viewH = sr.viewport.rect.height;
        float scrollable = content.rect.height - viewH;
        if (scrollable <= 0f) return;

        Vector3 inContent = content.InverseTransformPoint(target.TransformPoint(target.rect.center));
        float fromBottom = inContent.y + content.rect.height * content.pivot.y;
        float desired = Mathf.Clamp(fromBottom - viewH * 0.5f, 0f, scrollable);
        sr.verticalNormalizedPosition = desired / scrollable;
        Canvas.ForceUpdateCanvases();

        // 위 계산은 레이아웃 가정이 하나라도 어긋나면 빗나갑니다. 실제로 보이는지 확인하고,
        // 아니면 성기게 훑어서 보이는 지점을 찾습니다.
        Vector2 unused;
        if (TryGetScreenPoint(target, sr, 0f, out unused)) return;
        for (int i = 0; i <= 50; i++)
        {
            sr.verticalNormalizedPosition = i / 50f;
            Canvas.ForceUpdateCanvases();
            if (TryGetScreenPoint(target, sr, 0f, out unused)) return;
        }
    }

    /// <summary>
    /// 대상의 화면 좌표. 화면 밖이거나 스크롤 뷰포트에 가려져 있으면 false.
    /// </summary>
    private static bool TryGetScreenPoint(RectTransform rt, ScrollRect sr, float leadY, out Vector2 screenPoint)
    {
        screenPoint = Vector2.zero;
        if (rt == null) return false;

        // Screen Space - Overlay 캔버스라 카메라는 null입니다.
        Vector2 p = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
        p.y += leadY;
        screenPoint = p;

        if (p.x <= 0f || p.x >= Screen.width) return false;
        if (p.y <= 0f || p.y >= Screen.height) return false;
        if (sr != null && sr.viewport != null &&
            !RectTransformUtility.RectangleContainsScreenPoint(sr.viewport, p, null))
        {
            return false;
        }
        return true;
    }
#endif

    /// <summary>마지막 실행의 조건별 집계. UI가 표로 그립니다.</summary>
    public IReadOnlyList<ConditionResult> Results => _results;
}
