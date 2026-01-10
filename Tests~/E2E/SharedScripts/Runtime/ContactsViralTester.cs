using System;
using System.Collections.Generic;
using UnityEngine;
using AppsInToss;

/// <summary>
/// ContactsViral 테스터 컴포넌트
/// contactsViral API를 테스트합니다.
/// 공유 완료 후 onEvent/onError 콜백이 Unity로 전달되는지 확인합니다.
/// </summary>
public class ContactsViralTester : MonoBehaviour
{
    // 테스트용 moduleId (실제 환경에서는 유효한 ID 필요)
    private string moduleId = "test-module-id";

    // 상태
    private string status = "";
    private bool isActive = false;
    private List<string> eventLog = new List<string>();

    // 구독 해제 액션
    private Action _unsubscribe;

    /// <summary>
    /// ContactsViral 테스터 UI를 렌더링합니다.
    /// </summary>
    public void DrawUI(
        GUIStyle boxStyle,
        GUIStyle groupHeaderStyle,
        GUIStyle labelStyle,
        GUIStyle buttonStyle,
        GUIStyle textFieldStyle,
        GUIStyle fieldLabelStyle,
        GUIStyle callbackLabelStyle)
    {
        GUILayout.BeginVertical(boxStyle);

        // 섹션 헤더
        GUILayout.Label("ContactsViral Tester", groupHeaderStyle);
        GUILayout.Label("contactsViral API를 테스트합니다.", labelStyle);
        GUILayout.Label("공유 창에서 공유 완료 후 콜백 이벤트를 확인합니다.", labelStyle);

        GUILayout.Space(10);

        // 상태 표시
        string activeStatus = isActive ? "Active (구독 중)" : "Inactive";
        GUILayout.Label($"Status: {activeStatus}", labelStyle);
        if (!string.IsNullOrEmpty(status))
        {
            GUILayout.Label($"  {status}", callbackLabelStyle);
        }

        // 이벤트 로그 표시 (최근 10개)
        if (eventLog.Count > 0)
        {
            GUILayout.Label("Event Log:", labelStyle);
            int startIndex = Math.Max(0, eventLog.Count - 10);
            for (int i = startIndex; i < eventLog.Count; i++)
            {
                GUILayout.Label($"  {eventLog[i]}", callbackLabelStyle);
            }
        }

        GUILayout.Space(10);

        // moduleId 입력
        GUILayout.Label("Module ID:", fieldLabelStyle);
        moduleId = GUILayout.TextField(moduleId, textFieldStyle, GUILayout.Height(36));

        GUILayout.Space(10);

        // ContactsViral 호출 버튼
        if (!isActive)
        {
            if (GUILayout.Button("contactsViral(...) 호출", buttonStyle, GUILayout.Height(44)))
            {
                ExecuteContactsViral();
            }
        }
        else
        {
            // 구독 해제 버튼
            if (GUILayout.Button("구독 해제 (Unsubscribe)", buttonStyle, GUILayout.Height(44)))
            {
                Unsubscribe();
            }
        }

        GUILayout.Space(10);

        // 로그 초기화
        if (eventLog.Count > 0)
        {
            if (GUILayout.Button("Clear Log", buttonStyle, GUILayout.Height(32)))
            {
                eventLog.Clear();
                status = "";
            }
        }

        GUILayout.EndVertical();
    }

    private void ExecuteContactsViral()
    {
        status = "공유 창 열기 중...";
        eventLog.Add($"[{DateTime.Now:HH:mm:ss}] contactsViral(moduleId: {moduleId}) 호출");

        try
        {
            // ContactsViral API 호출
            _unsubscribe = AIT.ContactsViral(
                onEvent: (evt) =>
                {
                    Debug.Log($"[ContactsViralTester] onEvent: {evt?.Type}");
                    eventLog.Add($"[{DateTime.Now:HH:mm:ss}] onEvent: Type={evt?.Type}");

                    if (evt?.Data != null)
                    {
                        eventLog.Add($"[{DateTime.Now:HH:mm:ss}]   Data: {JsonUtility.ToJson(evt.Data)}");
                    }

                    // 특정 이벤트 타입에 따른 상태 업데이트
                    if (evt?.Type == "success" || evt?.Type == "completed")
                    {
                        status = "공유 완료!";
                        eventLog.Add($"[{DateTime.Now:HH:mm:ss}] ✓ 공유 완료 이벤트 수신됨");
                    }
                    else if (evt?.Type == "reward")
                    {
                        status = "리워드 수신!";
                        eventLog.Add($"[{DateTime.Now:HH:mm:ss}] ✓ 리워드 이벤트 수신됨");
                    }
                    else if (evt?.Type == "closed" || evt?.Type == "dismissed")
                    {
                        status = "공유 창 닫힘";
                        eventLog.Add($"[{DateTime.Now:HH:mm:ss}] 공유 창이 닫혔습니다");
                    }
                },
                options: new ContactsViralParamsOptions { ModuleId = moduleId },
                onError: (error) =>
                {
                    Debug.LogError($"[ContactsViralTester] onError: {error?.Message}");
                    status = $"Error: {error?.Message}";
                    eventLog.Add($"[{DateTime.Now:HH:mm:ss}] ✗ onError: {error?.ErrorCode} - {error?.Message}");
                    isActive = false;
                }
            );

            isActive = true;
            status = "공유 창 열림 (이벤트 대기 중...)";
            eventLog.Add($"[{DateTime.Now:HH:mm:ss}] 구독 시작됨");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ContactsViralTester] Exception: {ex}");
            status = $"Exception: {ex.Message}";
            eventLog.Add($"[{DateTime.Now:HH:mm:ss}] ✗ Exception: {ex.Message}");
            isActive = false;
        }
    }

    private void Unsubscribe()
    {
        if (_unsubscribe != null)
        {
            _unsubscribe.Invoke();
            _unsubscribe = null;
            eventLog.Add($"[{DateTime.Now:HH:mm:ss}] 구독 해제됨");
        }
        isActive = false;
        status = "구독 해제됨";
    }

    private void OnDestroy()
    {
        // 컴포넌트 제거 시 구독 해제
        _unsubscribe?.Invoke();
    }
}
