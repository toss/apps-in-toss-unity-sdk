using System;
using Sentry;
using Sentry.Unity;
using UnityEngine;
using UnityEngine.UI;

using SentrySdk = Sentry.Unity.SentrySdk;

/// <summary>
/// Sentry 에러 캡처 테스트 컴포넌트
/// Preview 빌드에서 Sentry 에러 캡처가 실제로 동작하는지 확인용
/// defineConstraints로 Sentry 미설치 시 컴파일 제외됨
/// </summary>
public class SentryTester : MonoBehaviour
{
    private string _status = "";
    private bool _throwNextFrame;
    private bool _sentryStateResolved = false;

    // uGUI 참조
    private Text _statusTextUI;
    private Text _sentryStateUI;

    private void Update()
    {
        if (_throwNextFrame)
        {
            _throwNextFrame = false;
            throw new InvalidOperationException("SentryTester: Unhandled Exception 테스트");
        }

        // Sentry 비동기 초기화 완료 감지
        if (!_sentryStateResolved && _sentryStateUI != null && SentrySdk.IsEnabled)
        {
            _sentryStateResolved = true;
            _sentryStateUI.text = "Sentry: 활성";
        }
    }

    /// <summary>
    /// uGUI 기반 UI를 생성합니다.
    /// </summary>
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

        UIBuilder.CreateText(section, "Sentry Tester",
            UIBuilder.Theme.FontLarge, UIBuilder.Theme.TextAccent, fontStyle: FontStyle.Bold);

        bool isEnabled = SentrySdk.IsEnabled;
        _sentryStateResolved = isEnabled;
        _sentryStateUI = UIBuilder.CreateText(section, $"Sentry: {(isEnabled ? "활성" : "비활성")}",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);

        _statusTextUI = UIBuilder.CreateText(section, "",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextPrimary);
        _statusTextUI.gameObject.SetActive(false);

        UIBuilder.CreateButton(section, "CaptureException", onClick: () =>
        {
            var ex = new Exception("SentryTester: 테스트 예외 (CaptureException)");
            SentrySdk.CaptureException(ex);
            SetStatus("CaptureException 전송됨");
        }, style: UIBuilder.ButtonStyle.Danger);

        UIBuilder.CreateButton(section, "CaptureMessage", onClick: () =>
        {
            SentrySdk.CaptureMessage("SentryTester: 테스트 메시지");
            SetStatus("CaptureMessage 전송됨");
        }, style: UIBuilder.ButtonStyle.Danger);

        UIBuilder.CreateButton(section, "Debug.LogException", onClick: () =>
        {
            Debug.LogException(new Exception("SentryTester: Debug.LogException 테스트"));
            SetStatus("Debug.LogException 호출됨");
        }, style: UIBuilder.ButtonStyle.Danger);

        UIBuilder.CreateButton(section, "Debug.LogError", onClick: () =>
        {
            Debug.LogError("SentryTester: Debug.LogError 테스트");
            SetStatus("Debug.LogError 호출됨");
        });

        UIBuilder.CreateButton(section, "Unhandled Exception", onClick: () =>
        {
            _throwNextFrame = true;
        }, style: UIBuilder.ButtonStyle.Danger);
    }

    private void SetStatus(string status)
    {
        _status = status;
        if (_statusTextUI != null)
        {
            _statusTextUI.gameObject.SetActive(true);
            _statusTextUI.text = status;
        }
    }
}
