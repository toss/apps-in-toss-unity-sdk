using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 탭 진단 로그를 화면에 띄우는 패널.
///
/// PointerTapDiagnostics가 모아둔 press/release 기록을 주기적으로 다시 그립니다. 버튼을 눌러야
/// 갱신되는 구조로 만들지 않은 이유가 있습니다 — 진단 대상이 "스크롤 영역 안에서 탭이 안 먹는
/// 증상"이고, 이 패널도 같은 스크롤 영역 안에 있습니다. 로그를 보려고 누르는 버튼이 같은 이유로
/// 안 먹으면 진단 자체를 못 합니다. 그래서 자동 갱신으로 두고, 버튼은 지우기 하나만 둡니다.
///
/// 같은 내용이 Debug.Log와 window.__AIT_TAPLOG(console.log)로도 나가므로, 원격 인스펙터가
/// 붙는 상황이면 화면을 거치지 않고 그쪽에서 읽는 편이 편합니다.
/// </summary>
public class TapDiagnosticsTester : MonoBehaviour
{
    /// <summary>화면에 보여줄 최근 기록 수. 버퍼 전체(PointerTapDiagnostics.MaxLines)는 콘솔로 나갑니다.</summary>
    private const int VisibleEntries = 12;

    private const float RefreshIntervalSeconds = 0.3f;

#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>소프트 키보드가 뜰 때 뷰포트 변화를 콘솔에 남기는 프로브. E2ETestBridge.jslib.</summary>
    [DllImport("__Internal")]
    private static extern void VP_Install();
#endif

    private Text _logText;
    private Text _verdictText;
    private Button _runButton;
    private Text _runButtonLabel;
    private TapAutoProbe _autoProbe;
    private int _lastRenderedRevision = -1;
    private float _lastRefreshTime = -1f;
    private string _lastVerdictRender;

    // Unity가 보는 화면 크기. JS 쪽 프로브(VP_Install)가 찍는 canvas.width/height와 대조해
    // 키보드가 뜰 때 엔진이 리사이즈를 인지하는지 본다.
    private int _lastScreenW = -1;
    private int _lastScreenH = -1;
    private Rect _lastSafeArea;
    private bool _lastKeyboardVisible;

    public void SetupUI(Transform parent)
    {
        _autoProbe = GetComponent<TapAutoProbe>() ?? gameObject.AddComponent<TapAutoProbe>();

#if UNITY_WEBGL && !UNITY_EDITOR
        try { VP_Install(); }
        catch (Exception e) { Debug.LogWarning($"[E2E-VP] VP_Install failed: {e.Message}"); }
#endif

        var section = UIBuilder.CreatePanel(parent, UIBuilder.Theme.SectionBg);
        var vlg = section.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = UIBuilder.Theme.SpacingSmall;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.padding = new RectOffset(12, 12, 12, 12);
        section.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        UIBuilder.CreateText(section, "탭 진단",
            UIBuilder.Theme.FontLarge, UIBuilder.Theme.TextAccent, fontStyle: FontStyle.Bold);

        UIBuilder.CreateText(section,
            "아래 버튼 하나면 A/B/C 전부 자동으로 돕니다. 20초쯤 걸리고, 끝나면 판정이 그대로 뜹니다. "
            + "도는 동안 화면을 만지지 마세요 — 사람 손 입력이 섞이면 집계가 오염됩니다.",
            UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextSecondary);

        _runButton = UIBuilder.CreateButton(section, "자동 진단 실행", onClick: OnAutoRunClick);
        _runButtonLabel = _runButton != null ? _runButton.GetComponentInChildren<Text>() : null;

        _verdictText = UIBuilder.CreateText(section, "(아직 실행하지 않음)",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextAccent);
        _verdictText.horizontalOverflow = HorizontalWrapMode.Wrap;

        UIBuilder.CreateText(section,
            "손으로 확인하려면: 위쪽 검색바는 스크롤 밖(scroll=NO), 이 아래 PlayerPrefs의 Key/Value 칸은 "
            + "스크롤 안(scroll=YES)입니다. 둘을 번갈아 탭해서 UP 줄의 FIRE 값을 비교하세요. "
            + "FIRE=NO거나 UP 줄이 '미수신'이면 그 줄에 적힌 사유가 원인입니다.",
            UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextSecondary);

        UIBuilder.CreateText(section,
            "자동 진단이 답하지 못하는 것 하나: 합성 터치는 iOS가 사용자 제스처로 쳐주지 않아서 "
            + "소프트 키보드가 뜨는지는 못 봅니다. FIRE=YES로 나오면 그때 손으로 한 번 탭해서 "
            + "키보드가 뜨는지 확인해 주세요.",
            UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextSecondary);

        UIBuilder.CreateText(section,
            "키보드가 뜰 때 화면이 밀리거나 줄어드는 증상은 콘솔의 [E2E-VP] 줄에 남습니다. "
            + "입력칸을 탭해 키보드를 띄운 뒤 Dev Console에서 로그를 내보내면 됩니다.",
            UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextSecondary);

        UIBuilder.CreateButton(section, "로그 지우기", onClick: OnClearClick);

        _logText = UIBuilder.CreateText(section, "(아직 탭 기록 없음)",
            UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextPrimary);
        _logText.horizontalOverflow = HorizontalWrapMode.Wrap;
    }

    private void OnClearClick()
    {
        PointerTapDiagnostics.Clear();
    }

    private void OnAutoRunClick()
    {
        if (_autoProbe == null) return;
        _autoProbe.StartRun();
    }

    private void Update()
    {
        PointerTapDiagnostics.PollPendingTaps();
        WatchScreen();
        RefreshVerdict();

        if (_logText == null) return;
        // 자동 진단이 도는 동안에는 로그 패널을 다시 그리지 않습니다. 이 패널은 측정 대상과 같은
        // ScrollRect 안에 있어서, 텍스트가 길어지며 일어나는 레이아웃 재구축이 탭이 진행되는
        // 프레임에 끼어듭니다. 어차피 도는 동안 읽을 사람도 없습니다.
        if (_autoProbe != null && _autoProbe.IsRunning) return;
        if (_lastRefreshTime >= 0f && Time.realtimeSinceStartup - _lastRefreshTime < RefreshIntervalSeconds) return;
        _lastRefreshTime = Time.realtimeSinceStartup;

        if (_lastRenderedRevision == PointerTapDiagnostics.Revision) return;
        _lastRenderedRevision = PointerTapDiagnostics.Revision;
        _logText.text = BuildVisibleLog();
    }

    /// <summary>
    /// Screen 크기·safeArea·TouchScreenKeyboard.visible이 바뀔 때만 한 줄 남깁니다.
    /// JS 프로브가 찍는 값과 태그([E2E-VP])를 맞춰서 Dev Console 내보내기에서 한 흐름으로 읽힙니다.
    /// 값이 한 번도 안 바뀌면 "Unity는 키보드를 전혀 인지하지 못한다"는 뜻이고, 그것도 결과입니다.
    /// </summary>
    private void WatchScreen()
    {
        int w = Screen.width, h = Screen.height;
        Rect sa = Screen.safeArea;
        bool kb = TouchScreenKeyboard.visible;
        if (w == _lastScreenW && h == _lastScreenH && sa == _lastSafeArea && kb == _lastKeyboardVisible) return;

        string tag = _lastScreenW < 0 ? "unity-base" : "unity-change";
        _lastScreenW = w; _lastScreenH = h; _lastSafeArea = sa; _lastKeyboardVisible = kb;
        Debug.Log($"[E2E-VP] {tag} Screen={w}x{h} safeArea=({sa.x:F0},{sa.y:F0},{sa.width:F0},{sa.height:F0}) kbVisible={(kb ? 1 : 0)}");
    }

    /// <summary>
    /// 진행 상황과 판정을 다시 그립니다. 실행 중에는 버튼을 잠급니다 — 도는 도중 다시 누르면
    /// 두 번째 실행이 첫 번째 집계 위에 겹쳐 쌓입니다.
    /// </summary>
    private void RefreshVerdict()
    {
        if (_verdictText == null || _autoProbe == null) return;

        string body;
        if (_autoProbe.IsRunning)
        {
            body = $"진행 중 — {_autoProbe.Progress}";
        }
        else if (string.IsNullOrEmpty(_autoProbe.Verdict))
        {
            body = "(아직 실행하지 않음)";
        }
        else
        {
            body = _autoProbe.Verdict + "\n\n" + TapAutoProbe.Summarize(_autoProbe.Info, _autoProbe.Results);
        }

        if (body != _lastVerdictRender)
        {
            _lastVerdictRender = body;
            _verdictText.text = body;
        }

        if (_runButton != null) _runButton.interactable = !_autoProbe.IsRunning;
        if (_runButtonLabel != null)
        {
            string label = _autoProbe.IsRunning ? "실행 중…" : "자동 진단 실행";
            if (_runButtonLabel.text != label) _runButtonLabel.text = label;
        }
    }

    private static string BuildVisibleLog()
    {
        var lines = PointerTapDiagnostics.Lines;
        if (lines.Count == 0) return "(아직 탭 기록 없음)";

        int start = Math.Max(0, lines.Count - VisibleEntries);
        var sb = new StringBuilder();
        if (start > 0) sb.AppendLine($"... 이전 {start}건은 콘솔에만 남아 있습니다");
        for (int i = start; i < lines.Count; i++)
        {
            sb.AppendLine(lines[i]);
        }
        return sb.ToString().TrimEnd();
    }
}
