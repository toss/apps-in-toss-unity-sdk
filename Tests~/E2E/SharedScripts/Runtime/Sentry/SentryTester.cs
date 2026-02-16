using System;
using Sentry;
using Sentry.Unity;
using UnityEngine;

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

    private void Update()
    {
        if (_throwNextFrame)
        {
            _throwNextFrame = false;
            throw new InvalidOperationException("SentryTester: Unhandled Exception 테스트");
        }
    }

    public void DrawUI(GUIStyle boxStyle, GUIStyle headerStyle,
                       GUIStyle labelStyle, GUIStyle buttonStyle,
                       GUIStyle dangerButtonStyle)
    {
        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label("🔴 Sentry Tester", headerStyle);

        bool isEnabled = SentrySdk.IsEnabled;
        GUILayout.Label($"Sentry: {(isEnabled ? "활성" : "비활성")}", labelStyle);

        if (!string.IsNullOrEmpty(_status))
        {
            GUILayout.Label(_status, labelStyle);
        }

        GUILayout.Space(10);

        if (GUILayout.Button("CaptureException", dangerButtonStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(40))))
        {
            var ex = new Exception("SentryTester: 테스트 예외 (CaptureException)");
            SentrySdk.CaptureException(ex);
            _status = "✓ CaptureException 전송됨";
        }

        if (GUILayout.Button("CaptureMessage", dangerButtonStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(40))))
        {
            SentrySdk.CaptureMessage("SentryTester: 테스트 메시지");
            _status = "✓ CaptureMessage 전송됨";
        }

        if (GUILayout.Button("Debug.LogException", dangerButtonStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(40))))
        {
            Debug.LogException(new Exception("SentryTester: Debug.LogException 테스트"));
            _status = "✓ Debug.LogException 호출됨";
        }

        if (GUILayout.Button("Debug.LogError", buttonStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(40))))
        {
            Debug.LogError("SentryTester: Debug.LogError 테스트");
            _status = "✓ Debug.LogError 호출됨";
        }

        if (GUILayout.Button("Unhandled Exception", dangerButtonStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(40))))
        {
            _throwNextFrame = true;
        }

        GUILayout.EndVertical();
    }
}
