using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 탭 진단 자동 실행기.
///
/// 사람이 손으로 밟던 A/B/C 절차를 기계가 그대로 돌립니다. 탭은 캔버스에 합성 터치 이벤트를
/// 쏘아서(<c>TAP_Tap</c>) 실제 입력 경로를 그대로 태우고, 스크롤 관성은 손가락 플릭을 흉내내는
/// 대신 <see cref="ScrollRect.velocity"/>를 직접 세팅합니다. 검증하려는 가설의 독립변수가
/// "press 시점의 잔존 속도"라서 속도를 직접 만드는 편이 정확하고, 계단식으로 쓸면 클릭이
/// 죽기 시작하는 지점을 집을 수 있습니다.
///
/// 단위가 이 파일에서 가장 헷갈리는 부분입니다. 세 가지가 섞입니다.
///   - 캔버스 단위: ScrollRect.velocity, RectTransform.rect, 콘텐츠 변위. 참조 해상도 390x844 기준.
///   - 화면 픽셀: Screen.width/height, WorldToScreenPoint 결과. 캔버스 단위 x canvas.scaleFactor.
///   - 정규화 좌표: TAP_Tap에 넘기는 값. 화면 픽셀 / Screen.wh.
/// 관성 계산은 전부 캔버스 단위로 하고, 조준할 때 한 번만 scaleFactor를 곱합니다.
///
/// 스윕을 속도가 아니라 **변위**로 파라미터화한 이유:
/// 합성 탭은 손가락을 움직이지 않으므로 클릭이 죽는 경로는 "콘텐츠가 흘러 조준점이 위젯 밖으로
/// 나가는 것" 하나뿐입니다. 그러려면 홀드 시간 동안의 변위가 위젯 높이의 절반을 넘어야 합니다.
/// 속도로 스윕하면 감쇠율과 홀드 시간에 따라 하위 구간이 통째로 "구조적으로 실패 불가"가 되어,
/// 가설을 시험한 적도 없이 FIRE=YES만 쌓입니다. 변위로 스윕하면 위젯 높이를 기준으로 임계를
/// 사이에 두고 양쪽을 확실히 덮을 수 있고, 임계 아래 조건은 음성 대조군 노릇까지 합니다.
///
/// 한계 하나는 미리 밝혀 둡니다. 합성 이벤트는 untrusted라 iOS가 user activation으로 쳐주지
/// 않습니다. 따라서 "클릭은 나는데 키보드가 안 뜬다"는 갈래는 이 자동 실행으로 가려낼 수 없고
/// 사람 손 탭이 한 번 필요합니다. 자동 실행이 답하는 것은 uGUI가 클릭을 발화시키는가 하나입니다.
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

    // 실행 파라미터. 결과를 해석할 때 알아야 하는 값들이라 공개해 둡니다.

    /// <summary>손가락을 붙이고 있는 시간. 사람 탭의 하한쯤 됩니다.</summary>
    public const int HoldMs = 80;

    /// <summary>조건 하나당 탭 횟수.</summary>
    public const int TapsPerCondition = 3;

    /// <summary>
    /// 합성 터치가 Unity 입력 큐를 타고 처리되기까지 걸리는 프레임 수. 그 사이 콘텐츠가 흐른 만큼
    /// 조준점을 미리 밀어 둡니다. 안 하면 빠른 조건에서 탭이 칸을 벗어나고, "클릭이 안 났다"와
    /// "빗맞았다"가 섞여 버립니다.
    /// </summary>
    public const int LeadFrames = 1;

    /// <summary>
    /// 조건 하나를 판정에 쓰려면 최소 이만큼은 착지해야 합니다. 한 번 착지한 것으로 임계를
    /// 발표하면 표본 1개로 단정하는 셈입니다.
    /// </summary>
    public const int MinLandedForVerdict = 2;

    /// <summary>UP을 기다리는 시간. PointerTapDiagnostics의 미수신 판정과 같은 값이어야 합니다.</summary>
    public const float UpWaitSeconds = 1.5f;

    /// <summary>
    /// 명령한 속도가 press 시점에 이 비율 아래로 떨어져 있으면 그 조건은 신뢰하지 않습니다.
    /// inertia가 꺼져 있거나 탄성 경계에 걸린 경우가 여기 걸립니다.
    /// </summary>
    public const float VelocityHonoredRatio = 0.5f;

    /// <summary>대조군 라벨. ScrollRect 밖에 있어야 합니다.</summary>
    public const string ControlLabel = "Search...";

    /// <summary>표적 라벨. ScrollRect 안에 있어야 합니다.</summary>
    public const string TargetLabel = "test-key";

    /// <summary>
    /// 쓸어볼 콘텐츠 변위(캔버스 단위). 홀드 시간 동안 콘텐츠가 이만큼 흐르도록 초기 속도를
    /// 역산합니다. 위젯 높이 40의 절반인 20을 사이에 두도록 골랐습니다 — 10은 구조적으로
    /// 클릭이 죽을 수 없는 음성 대조군이고, 여기서 FIRE=NO가 나오면 관성 이동 말고 다른 원인이
    /// 있다는 뜻입니다.
    /// </summary>
    public static readonly float[] SweepDisplacements = { 10f, 20f, 30f, 50f, 90f };

    public bool IsRunning { get; private set; }

    /// <summary>진행 상황 한 줄. UI가 매 프레임 읽어 갑니다.</summary>
    public string Progress { get; private set; } = "";

    /// <summary>마지막 실행의 판정문. 아직 안 돌렸으면 빈 문자열.</summary>
    public string Verdict { get; private set; } = "";

    private readonly List<ConditionResult> _results = new List<ConditionResult>();
    private HarnessInfo _info;

    /// <summary>마지막 실행의 조건별 집계.</summary>
    public IReadOnlyList<ConditionResult> Results => _results;

    /// <summary>마지막 실행이 놓인 환경. 판정이 전제를 확인했는지 여기서 드러납니다.</summary>
    public HarnessInfo Info => _info;

    /// <summary>
    /// 하네스가 놓인 환경. uGUI 타입을 담지 않아 판정 로직이 순수하게 유지됩니다 —
    /// EditMode에서 전 분기를 그대로 검증할 수 있어야 합니다.
    /// </summary>
    public struct HarnessInfo
    {
        /// <summary>표적 두 개를 라벨로 찾았고 배치도 기대와 같은가.</summary>
        public bool Resolved;

        /// <summary>Resolved가 false인 사유. 판정문에 그대로 실립니다.</summary>
        public string Problem;

        public string ControlName;
        public string TargetName;

        /// <summary>ScrollRect.inertia. false면 velocity가 매 프레임 0으로 지워져 스윕이 무의미합니다.</summary>
        public bool Inertia;

        public string MovementType;
        public float DecelerationRate;

        /// <summary>캔버스 단위 → 화면 픽셀 배율.</summary>
        public float ScaleFactor;

        /// <summary>표적 위젯 높이(캔버스 단위).</summary>
        public float TargetHeight;

        /// <summary>조준점이 위젯을 벗어나는 최소 변위. 위젯 높이의 절반입니다.</summary>
        public float FlipThreshold => TargetHeight * 0.5f;
    }

    /// <summary>조건 하나(대조군 / 정지 / 변위 d)에 대한 집계.</summary>
    public struct ConditionResult
    {
        public string Name;

        /// <summary>이 조건이 실제로 때린 프로브의 라벨. 결과지만 보고도 표적을 확인할 수 있어야 합니다.</summary>
        public string TargetLabel;

        public bool HasScrollRect;

        /// <summary>세팅한 속도(캔버스 단위/초).</summary>
        public float CommandedVelocity;

        /// <summary>홀드 시간 동안 흐를 것으로 계산한 변위(캔버스 단위).</summary>
        public float ExpectedDisplacement;

        /// <summary>보낸 탭 수.</summary>
        public int Attempted;

        /// <summary>DOWN이 실제로 표적 프로브에 닿은 수.</summary>
        public int Landed;

        public int UpReceived;

        /// <summary>uGUI가 클릭을 발화시킬 조건이 성립한 수.</summary>
        public int Fired;

        /// <summary>press와 release 사이에 포인터 밑 GameObject가 바뀐 수.</summary>
        public int OverChanged;

        public int ClickReceived;

        /// <summary>release 시점 pointerDrag가 표적 자신이던 수. 가설 메커니즘의 직접 증거입니다.</summary>
        public int DragWasSelf;

        /// <summary>UP을 기다리다 시간이 다 된 수.</summary>
        public int TimedOut;

        /// <summary>탭 한 번에 기록이 둘 이상 생긴 수. 사람 손이 섞였다는 뜻입니다.</summary>
        public int Contaminated;

        /// <summary>press 시점 실측 속도의 절댓값 합.</summary>
        public float MeasuredVelocityAbsSum;

        public int MeasuredSamples;

        public float MeasuredVelocityAbsMean =>
            MeasuredSamples > 0 ? MeasuredVelocityAbsSum / MeasuredSamples : 0f;

        /// <summary>
        /// 판정에 쓸 수 있는 표본인가. 오염이 있거나 착지가 부족하면 이 조건은 결론에서 빠지고
        /// "미검증"으로 보고됩니다 — 조용히 빠지면 안 됩니다.
        /// </summary>
        public bool Eligible =>
            Contaminated == 0 && Landed >= MinLandedForVerdict && Landed * 3 >= Attempted * 2;

        /// <summary>명령한 속도가 press 시점까지 유지됐는가.</summary>
        public bool VelocityHonored
        {
            get
            {
                float commanded = Mathf.Abs(CommandedVelocity);
                if (commanded < 1f) return true;
                return MeasuredSamples > 0 && MeasuredVelocityAbsMean >= commanded * VelocityHonoredRatio;
            }
        }
    }

    // ─── 관성 산수 ───

    /// <summary>
    /// 초기 속도 1에 대해 홀드 시간 동안 흐르는 거리.
    ///
    /// ScrollRect는 매 프레임 <c>v *= pow(rate, dt)</c> 후 <c>pos += v * dt</c>를 합니다
    /// (ScrollRect.cs의 관성 분기). 연속 근사는 ∫₀ᵀ rate^t dt = (rate^T − 1) / ln(rate).
    /// </summary>
    public static float DisplacementPerUnitVelocity(float holdSeconds, float decelerationRate)
    {
        if (holdSeconds <= 0f) return 0f;
        // 감쇠가 없거나 계수가 범위를 벗어나면 등속으로 근사합니다.
        if (decelerationRate <= 0f || decelerationRate >= 1f) return holdSeconds;
        return (Mathf.Pow(decelerationRate, holdSeconds) - 1f) / Mathf.Log(decelerationRate);
    }

    /// <summary>원하는 변위를 만드는 초기 속도. 변위 스윕을 속도 명령으로 바꿉니다.</summary>
    public static float VelocityForDisplacement(float displacement, float holdSeconds, float decelerationRate)
    {
        float factor = DisplacementPerUnitVelocity(holdSeconds, decelerationRate);
        return factor <= 1e-4f ? 0f : displacement / factor;
    }

    // ─── 판정 ───

    /// <summary>
    /// 집계만 보고 결론을 냅니다. Unity 타입을 안 쓰므로 EditMode에서 전 분기가 그대로 검증됩니다.
    /// 첫 줄이 결론, 나머지는 근거입니다 — 화면이 좁아 첫 줄만 보이는 경우가 흔합니다.
    ///
    /// 판정 불가와 반증을 절대 섞지 않는 것이 이 함수의 존재 이유입니다. 하네스가 안 돌았을 때
    /// "가설이 틀렸다"고 말하면, 실기기 런이 사실상 1회성이라 그 문장이 그대로 조사 방향이 됩니다.
    /// </summary>
    public static string Conclude(HarnessInfo info, IReadOnlyList<ConditionResult> results)
    {
        if (!info.Resolved)
        {
            return "판정: 불가 — " + (string.IsNullOrEmpty(info.Problem) ? "하네스가 표적을 확정하지 못했습니다." : info.Problem);
        }
        if (results == null || results.Count == 0)
        {
            return "판정: 불가 — 한 조건도 실행되지 않았습니다.";
        }
        if (!info.Inertia)
        {
            return "판정: 불가 — ScrollRect.inertia가 꺼져 있어 세팅한 속도가 매 프레임 지워집니다. "
                 + "관성 조건이 성립하지 않으므로 이 런으로는 가설을 시험할 수 없습니다.";
        }

        // ─ 대조군 ─
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
            return "판정: 불가 — 합성 탭이 대조군 입력칸에 한 번도 닿지 않았습니다. 하네스가 안 돈 것이므로 "
                 + "이 결과로 가설을 판단하면 안 됩니다.";
        }
        if (control.Contaminated > 0)
        {
            return $"판정: 불가 — 대조군에 예상 밖 입력이 {control.Contaminated}건 섞였습니다. "
                 + "실행 중 화면을 만지지 말고 다시 돌리세요.";
        }
        if (!control.Eligible)
        {
            return $"판정: 불가 — 대조군 표본이 부족합니다({control.Landed}/{control.Attempted} 착지). "
                 + "대조군이 흔들린 런은 나머지가 깨끗해도 쓸 수 없습니다.";
        }
        if (control.Fired == 0)
        {
            return $"판정: 가설 전부 폐기 — 스크롤 밖 대조군도 클릭이 안 납니다({control.Fired}/{control.Landed}). "
                 + "ScrollRect와 무관한 원인입니다.";
        }

        var notes = new List<string>();
        if (control.Fired < control.Landed)
        {
            notes.Add($"주의: 대조군이 {control.Fired}/{control.Landed}로 흔들립니다. 아래 결론의 신뢰도가 그만큼 낮습니다.");
        }

        // ─ 스크롤 조건 분류 ─
        var eligibleMoving = new List<ConditionResult>();
        var untested = new List<ConditionResult>();
        var dishonored = new List<ConditionResult>();
        var contaminated = new List<ConditionResult>();
        ConditionResult stationary = default;
        bool hasStationary = false;
        int scrollLanded = 0;

        foreach (var r in results)
        {
            if (!r.HasScrollRect) continue;
            scrollLanded += r.Landed;

            if (r.Contaminated > 0) { contaminated.Add(r); continue; }

            if (Mathf.Abs(r.CommandedVelocity) < 1f)
            {
                if (r.Eligible) { stationary = r; hasStationary = true; }
                else untested.Add(r);
                continue;
            }

            if (!r.Eligible) { untested.Add(r); continue; }
            if (!r.VelocityHonored) { dishonored.Add(r); continue; }
            eligibleMoving.Add(r);
        }

        if (contaminated.Count > 0)
        {
            return $"판정: 불가 — 스크롤 조건 {contaminated.Count}개에 예상 밖 입력이 섞였습니다"
                 + $"({Names(contaminated)}). 실행 중 화면을 만지지 말고 다시 돌리세요.";
        }
        if (scrollLanded == 0)
        {
            return "판정: 불가 — 스크롤 안 입력칸에 합성 탭이 한 번도 닿지 않았습니다.";
        }
        if (dishonored.Count > 0)
        {
            var d = dishonored[0];
            return $"판정: 불가 — 명령한 속도가 press 시점에 유지되지 않았습니다"
                 + $"({d.Name}: 명령 {d.CommandedVelocity:F0}, 실측 {d.MeasuredVelocityAbsMean:F0}). "
                 + $"탄성 경계나 관성 억제가 개입한 것이므로 관성 가설을 시험한 셈이 아닙니다. "
                 + $"해당 조건 {dishonored.Count}개.";
        }

        // ─ 정지 조건 ─
        if (hasStationary && stationary.Fired == 0)
        {
            var sb0 = new StringBuilder(
                $"판정: 관성 무관 — 정지 상태에서도 클릭이 안 납니다({stationary.Fired}/{stationary.Landed}). "
                + "속도가 아니라 ScrollRect 안에 있다는 사실 자체가 원인입니다.");
            AppendCommonNotes(sb0, notes, results, untested);
            return sb0.ToString();
        }

        if (eligibleMoving.Count == 0)
        {
            return "판정: 불가 — 관성 조건이 하나도 판정 가능한 표본을 만들지 못했습니다"
                 + (untested.Count > 0 ? $" (미검증: {Names(untested)})." : ".");
        }

        // ─ UP 미수신은 관성과 다른 경로다. 임계 탐색에 섞으면 안 된다. ─
        var upClean = new List<ConditionResult>();
        var upDirty = new List<ConditionResult>();
        foreach (var r in eligibleMoving)
        {
            if (r.UpReceived < r.Landed) upDirty.Add(r);
            else upClean.Add(r);
        }

        if (upClean.Count == 0)
        {
            var sb1 = new StringBuilder(
                $"판정: UP 미수신 — 관성 조건 {upDirty.Count}개 전부에서 release 이벤트가 오지 않았습니다"
                + $"({Names(upDirty)}). ProcessDrag가 pointerPress를 null로 만드는 경로이며 관성 이동과는 다른 원인입니다. "
                + "관성 임계 판정은 유보합니다.");
            AppendCommonNotes(sb1, notes, results, untested);
            return sb1.ToString();
        }
        if (upDirty.Count > 0)
        {
            notes.Add($"UP 미수신 조건 {upDirty.Count}개({Names(upDirty)})는 다른 경로이므로 임계 탐색에서 제외했습니다.");
        }

        // ─ 임계 탐색 ─
        bool found = false;
        ConditionResult worst = default;
        foreach (var r in upClean)
        {
            if (r.Fired >= r.Landed) continue;
            if (!found || r.ExpectedDisplacement < worst.ExpectedDisplacement)
            {
                worst = r;
                found = true;
            }
        }

        var sb = new StringBuilder();
        if (!found)
        {
            if (untested.Count > 0)
            {
                sb.Append($"판정: 부분 불가 — 시험된 조건({Names(upClean)})에서는 클릭이 전부 났지만 "
                        + $"{untested.Count}개 조건이 착지하지 못해 미검증입니다({Names(untested)}). "
                        + "반증이라고 말할 수 없습니다.");
            }
            else
            {
                sb.Append($"판정: 관성 가설 반증 — 쓸어본 모든 변위({Names(upClean)})에서 클릭이 났습니다. "
                        + "합성 탭으로는 재현되지 않습니다.");
            }
        }
        else
        {
            bool belowFlip = worst.ExpectedDisplacement < info.FlipThreshold;
            string qualifier = untested.Count > 0 ? "시험된 범위에서 " : "";
            sb.Append($"판정: 관성 가설 유력 — {qualifier}콘텐츠 변위 {worst.ExpectedDisplacement:F0}"
                    + $"(속도 {worst.CommandedVelocity:F0})부터 클릭이 소실됩니다. "
                    + $"조준점이 위젯을 벗어나는 계산상 문턱은 {info.FlipThreshold:F0}입니다. "
                    + "press 때 잡힌 핸들러와 release 때 핸들러가 갈리는 정황과 일치합니다.");
            if (belowFlip)
            {
                notes.Add($"이상: {worst.Name}의 변위({worst.ExpectedDisplacement:F0})가 문턱({info.FlipThreshold:F0})보다 "
                        + "작은데도 클릭이 죽었습니다. 콘텐츠 이동 말고 다른 원인이 함께 있습니다.");
            }
            if (untested.Count > 0)
            {
                notes.Add($"미검증 {untested.Count}개({Names(untested)}) — 실제 문턱은 이보다 낮을 수 있습니다.");
            }
        }

        AppendCommonNotes(sb, notes, results, untested);
        return sb.ToString();
    }

    /// <summary>
    /// 헤드라인과 무관하게 항상 유효한 부가 신호만 붙입니다. 분기별 문구를 여기 섞으면
    /// 확정 헤드라인 밑에 반증용 각주가 달리는 모순이 생깁니다.
    /// </summary>
    private static void AppendCommonNotes(StringBuilder sb, List<string> notes,
        IReadOnlyList<ConditionResult> results, List<ConditionResult> untested)
    {
        int overChanged = 0, timedOut = 0, dragSelf = 0, landed = 0;
        foreach (var r in results)
        {
            overChanged += r.OverChanged;
            timedOut += r.TimedOut;
            dragSelf += r.DragWasSelf;
            landed += r.Landed;
        }

        if (overChanged > 0)
        {
            notes.Add($"press/release 사이 over 변화 {overChanged}건 — 손가락 밑에서 콘텐츠가 움직였습니다.");
        }
        if (dragSelf > 0 && landed > 0)
        {
            notes.Add($"release 시점 pointerDrag가 InputField 자신이던 경우 {dragSelf}/{landed}건 — "
                    + "ScrollRect가 아니라 InputField가 드래그 대상으로 잡혔다는 직접 증거입니다.");
        }
        if (timedOut > 0)
        {
            notes.Add($"UP 대기 시간 초과 {timedOut}건 — 그만큼 집계가 늦게 도착했을 수 있습니다.");
        }
        if (untested != null && untested.Count > 0)
        {
            int missed = 0;
            foreach (var u in untested) missed += u.Attempted - u.Landed;
            if (missed > 0) notes.Add($"빗맞은 탭 {missed}건 — 실패가 아니라 미착지입니다.");
        }

        foreach (var n in notes) sb.Append('\n').Append(n);
    }

    private static string Names(List<ConditionResult> list)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < list.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(list[i].Name);
        }
        return sb.ToString();
    }

    /// <summary>조건별 집계를 사람이 읽을 표로 만듭니다. 자동 판정이 틀렸을 때 대조할 원본입니다.</summary>
    public static string Summarize(HarnessInfo info, IReadOnlyList<ConditionResult> results)
    {
        var sb = new StringBuilder();
        sb.Append($"대조군={info.ControlName ?? "?"} 표적={info.TargetName ?? "?"}\n");
        sb.Append($"inertia={(info.Inertia ? 1 : 0)} move={info.MovementType} decel={info.DecelerationRate:F3}")
          .Append($" scale={info.ScaleFactor:F2} h={info.TargetHeight:F0} 문턱={info.FlipThreshold:F0}\n");

        if (results == null || results.Count == 0)
        {
            sb.Append("(집계 없음)");
            return sb.ToString();
        }

        sb.Append("조건       변위  명령v  실측v 착지 UP FIRE over\n");
        foreach (var r in results)
        {
            sb.Append(Pad(r.Name, 10))
              .Append(Pad(r.HasScrollRect ? $"{r.ExpectedDisplacement:F0}" : "-", 6))
              .Append(Pad(r.HasScrollRect ? $"{r.CommandedVelocity:F0}" : "-", 7))
              .Append(Pad(r.MeasuredSamples > 0 ? $"{r.MeasuredVelocityAbsMean:F0}" : "-", 6))
              .Append(Pad($"{r.Landed}/{r.Attempted}", 5))
              .Append(Pad(r.UpReceived.ToString(), 3))
              .Append(Pad(r.Fired.ToString(), 5))
              .Append(r.OverChanged);
            if (r.Contaminated > 0) sb.Append($" 오염{r.Contaminated}");
            if (r.TimedOut > 0) sb.Append($" 지연{r.TimedOut}");
            if (!r.Eligible) sb.Append(" 표본부족");
            sb.Append('\n');
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
        IsRunning = true;   // 코루틴 첫 프레임 전에 잠급니다. 같은 프레임 두 번 눌림 방지.
        StartCoroutine(RunAll());
    }

    private IEnumerator RunAll()
    {
        Verdict = "";
        _results.Clear();
        _info = new HarnessInfo();

#if UNITY_WEBGL && !UNITY_EDITOR
        Progress = "준비 중…";
        PointerTapDiagnostics.Clear();
        yield return null;

        var control = PointerTapDiagnostics.FindByLabel(ControlLabel);
        var target = PointerTapDiagnostics.FindByLabel(TargetLabel);

        // 표적을 라벨로만 찾습니다. "스크롤 안의 첫 프로브" 같은 폴백을 두면 다른 패널의
        // 입력칸을 때리고도 그럴듯한 판정문이 나옵니다.
        if (control == null || target == null)
        {
            Finish($"라벨로 표적을 찾지 못했습니다(대조군 \"{ControlLabel}\"={(control != null ? "OK" : "없음")}, "
                 + $"표적 \"{TargetLabel}\"={(target != null ? "OK" : "없음")}).");
            yield break;
        }

        var scroll = target.OwningScrollRect;
        if (scroll == null || control.OwningScrollRect != null)
        {
            Finish($"표적 배치가 기대와 다릅니다(대조군은 ScrollRect 밖, 표적은 안이어야 합니다). "
                 + $"대조군 scroll={(control.OwningScrollRect != null ? "YES" : "NO")}, "
                 + $"표적 scroll={(scroll != null ? "YES" : "NO")}.");
            yield break;
        }

        var targetRt = target.transform as RectTransform;
        var canvas = target.GetComponentInParent<Canvas>();
        _info = new HarnessInfo
        {
            Resolved = true,
            ControlName = control.Label,
            TargetName = target.Label,
            Inertia = scroll.inertia,
            MovementType = scroll.movementType.ToString(),
            DecelerationRate = scroll.decelerationRate,
            ScaleFactor = canvas != null ? canvas.rootCanvas.scaleFactor : 1f,
            TargetHeight = targetRt != null ? targetRt.rect.height : 0f,
        };
        Debug.Log($"[TapDiag] harness inertia={_info.Inertia} move={_info.MovementType} "
                + $"decel={_info.DecelerationRate} scale={_info.ScaleFactor} h={_info.TargetHeight}");

        yield return RunCondition("A 대조군", control, null, 0f, 0f);
        yield return RunCondition("B 정지", target, scroll, 0f, 0f);

        // 위/아래 양쪽을 봅니다. Elastic 리바운드도 결국 부호가 반대인 관성이라, 경계 밖으로
        // 밀어내는 대신 부호로 대신 덮습니다 — 경계 밖으로 밀면 표적이 뷰포트를 벗어나
        // 빗맞은 탭과 실패한 탭이 섞입니다.
        float hold = HoldMs / 1000f;
        foreach (float d in SweepDisplacements)
        {
            float v = VelocityForDisplacement(d, hold, _info.DecelerationRate);
            yield return RunCondition($"C -{d:F0}", target, scroll, -v, d);
            yield return RunCondition($"C +{d:F0}", target, scroll, v, d);
        }

        scroll.velocity = Vector2.zero;
        Finish(null);
#else
        Progress = "";
        Verdict = "판정: 불가 — 자동 진단은 WebGL 빌드에서만 동작합니다(합성 터치가 필요).";
        IsRunning = false;
        yield break;
#endif
    }

    private void Finish(string problem)
    {
        if (!string.IsNullOrEmpty(problem))
        {
            _info.Resolved = false;
            _info.Problem = problem;
        }
        Verdict = Conclude(_info, _results);
        Progress = "";
        IsRunning = false;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private IEnumerator RunCondition(string name, PointerTapDiagnostics probe, ScrollRect scroll,
        float velocityY, float expectedDisplacement)
    {
        var result = new ConditionResult
        {
            Name = name,
            TargetLabel = probe.Label,
            HasScrollRect = scroll != null,
            CommandedVelocity = velocityY,
            ExpectedDisplacement = expectedDisplacement,
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
                // 속도를 세팅한 프레임에는 아직 콘텐츠가 안 움직입니다. 한 프레임 흘려서
                // 실제로 흐르는 상태를 만든 뒤 조준합니다.
                yield return null;
            }

            // 관성 산수는 캔버스 단위, 조준은 화면 픽셀. 여기서 한 번만 변환합니다.
            // dt를 한 프레임 값 그대로 쓰면 직전 탭의 로그 출력으로 늘어난 프레임이 그대로
            // 과보정이 됩니다. 30~120fps 범위로 묶어 둡니다.
            float dt = Mathf.Clamp(Time.unscaledDeltaTime, 1f / 120f, 1f / 30f);
            float leadCanvas = velocityY * dt * LeadFrames;
            float leadScreen = leadCanvas * _info.ScaleFactor;

            Vector2 point;
            if (!TryGetScreenPoint(rt, scroll, leadScreen, out point))
            {
                // 조준점이 뷰포트 밖이면 보내 봐야 빗맞습니다. 시도는 세되 탭은 생략합니다.
                result.Attempted++;
                continue;
            }

            int before = PointerTapDiagnostics.Records.Count;
            result.Attempted++;
            TAP_Tap(point.x / Screen.width, point.y / Screen.height, HoldMs);

            float busyDeadline = Time.realtimeSinceStartup + 2f;
            while (TAP_DriveBusy() != 0 && Time.realtimeSinceStartup < busyDeadline) yield return null;

            // 고정 시간을 기다리는 대신 이 탭의 기록에 UP이 설 때까지 폴링합니다. 고정 대기는
            // 늦게 온 UP을 "미수신"으로 오집계하고, 그 오집계가 관성 임계로 흡수됩니다.
            bool up = false;
            float upDeadline = Time.realtimeSinceStartup + UpWaitSeconds;
            while (Time.realtimeSinceStartup < upDeadline)
            {
                if (HasUp(before, probe.Label)) { up = true; break; }
                yield return null;
            }
            // OnPointerClick은 OnPointerUp과 같은 프레임에 이어서 실행됩니다(:428 다음 :436).
            yield return null;
            if (!up) result.TimedOut++;

            Tally(ref result, before, probe.Label);
        }

        if (scroll != null) scroll.velocity = Vector2.zero;
        _results.Add(result);
        yield return null;
    }

    private static bool HasUp(int startIndex, string label)
    {
        var records = PointerTapDiagnostics.Records;
        for (int i = startIndex; i < records.Count; i++)
        {
            if (records[i].Label == label && records[i].UpReceived) return true;
        }
        return false;
    }

    /// <summary>이번 탭으로 새로 생긴 기록만 골라 집계에 더합니다.</summary>
    private static void Tally(ref ConditionResult result, int startIndex, string label)
    {
        var records = PointerTapDiagnostics.Records;
        int matched = 0;
        for (int i = startIndex; i < records.Count; i++)
        {
            var r = records[i];
            if (r.Label != label) continue;
            matched++;
            result.Landed++;
            if (r.UpReceived) result.UpReceived++;
            if (r.WillFireClick) result.Fired++;
            if (r.OverChanged) result.OverChanged++;
            if (r.ClickReceived) result.ClickReceived++;
            if (r.DragIsSelf) result.DragWasSelf++;
            result.MeasuredVelocityAbsSum += Mathf.Abs(r.DownVelocityY);
            result.MeasuredSamples++;
        }

        // 합성 탭 하나에 기록이 둘 이상이면 사람 손이 섞인 것입니다. 조건 전체를 판정에서 뺍니다.
        if (matched > 1) result.Contaminated += matched - 1;
    }

    /// <summary>대상을 뷰포트 한가운데로 스크롤합니다. 콘텐츠가 뷰포트보다 짧으면 할 일이 없습니다.</summary>
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
    /// 대상의 화면 좌표(픽셀). 화면 밖이거나 스크롤 뷰포트에 가려져 있으면 false.
    /// leadScreenPixels는 이미 화면 픽셀로 환산된 선행 보정입니다.
    /// </summary>
    private static bool TryGetScreenPoint(RectTransform rt, ScrollRect sr, float leadScreenPixels,
        out Vector2 screenPoint)
    {
        screenPoint = Vector2.zero;
        if (rt == null) return false;

        // Screen Space - Overlay 캔버스라 카메라는 null입니다.
        Vector2 p = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
        p.y += leadScreenPixels;
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
}
