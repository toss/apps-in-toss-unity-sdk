using System;
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

    private Text _logText;
    private int _lastRenderedRevision = -1;
    private float _lastRefreshTime = -1f;

    public void SetupUI(Transform parent)
    {
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
            "InputField를 탭하면 press/release 시점의 포인터 상태가 아래에 쌓입니다. "
            + "위쪽 검색바는 스크롤 밖(scroll=NO), 이 아래 PlayerPrefs의 Key/Value 칸은 스크롤 안(scroll=YES)입니다. "
            + "둘을 번갈아 탭해서 UP 줄의 FIRE 값을 비교하세요.",
            UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextSecondary);

        UIBuilder.CreateText(section,
            "FIRE=YES면 클릭이 발화합니다. FIRE=NO거나 UP 줄 자체가 '미수신'이면 그 줄에 적힌 사유가 원인입니다. "
            + "스크롤을 세게 튕긴 직후(vel이 0이 아닐 때) 탭하면 재현 확률이 올라갑니다.",
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

    private void Update()
    {
        PointerTapDiagnostics.PollPendingTaps();

        if (_logText == null) return;
        if (_lastRefreshTime >= 0f && Time.realtimeSinceStartup - _lastRefreshTime < RefreshIntervalSeconds) return;
        _lastRefreshTime = Time.realtimeSinceStartup;

        if (_lastRenderedRevision == PointerTapDiagnostics.Revision) return;
        _lastRenderedRevision = PointerTapDiagnostics.Revision;
        _logText.text = BuildVisibleLog();
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
