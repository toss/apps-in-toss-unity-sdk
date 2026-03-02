using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using AppsInToss;

/// <summary>
/// 대화형 SDK API 테스터 - 사용자가 API를 선택하고 파라미터를 입력하여 실행할 수 있는 UI 제공
/// uGUI(Canvas + ScrollRect + InputField)를 통해 API를 테스트하고 결과를 확인할 수 있음
/// 카테고리별 그룹핑과 접기/펼치기 기능 지원
/// </summary>
public class InteractiveAPITester : MonoBehaviour
{
    private List<APIMethodInfo> allMethods;
    private Dictionary<string, List<APIMethodInfo>> groupedMethods;
    private APIMethodInfo selectedMethod;

    // uGUI UI 관리자
    private InteractiveAPITesterUI _ui;

    // 분리된 컴포넌트 참조
    private OOMTester _oomTester;
    private Component _sentryTester;
    private MethodInfo _sentrySetupUI;
    private IAPv2Tester _iapTester;
    private AdV2Tester _adV2Tester;
    private ContactsViralTester _contactsViralTester;
    private VisibilityBGMTester _visibilityBGMTester;
    private MetricEventTester _metricEventTester;

    void Start()
    {
        Debug.Log("[InteractiveAPITester] Loading SDK APIs...");

        // 서브 테스터 컴포넌트 초기화
        _oomTester = GetComponent<OOMTester>() ?? gameObject.AddComponent<OOMTester>();

        // SentryTester는 별도 어셈블리(AppsInTossTestScripts.Sentry)에 있으므로 리플렉션으로 로드
        var sentryTesterType = Type.GetType("SentryTester, AppsInTossTestScripts.Sentry");
        if (sentryTesterType != null)
        {
            _sentryTester = GetComponent(sentryTesterType) ?? gameObject.AddComponent(sentryTesterType);
            _sentrySetupUI = sentryTesterType.GetMethod("SetupUI");
        }

        _iapTester = GetComponent<IAPv2Tester>() ?? gameObject.AddComponent<IAPv2Tester>();
        _adV2Tester = GetComponent<AdV2Tester>() ?? gameObject.AddComponent<AdV2Tester>();
        _contactsViralTester = GetComponent<ContactsViralTester>() ?? gameObject.AddComponent<ContactsViralTester>();
        _visibilityBGMTester = GetComponent<VisibilityBGMTester>() ?? gameObject.AddComponent<VisibilityBGMTester>();
        _metricEventTester = GetComponent<MetricEventTester>() ?? gameObject.AddComponent<MetricEventTester>();

        // API 목록 로드
        allMethods = APIParameterInspector.GetAllAPIMethods();
        groupedMethods = APIParameterInspector.GroupByCategory(allMethods);

        Debug.Log($"[InteractiveAPITester] Found {allMethods.Count} API methods in {groupedMethods.Count} categories");

        // uGUI UI 구축
        _ui = new InteractiveAPITesterUI();
        _ui.OnAPISelected = SelectAPI;
        _ui.OnExecuteRequested = ExecuteAPI;
        _ui.OnBackToList = BackToList;
        _ui.OnRetry = () => _ui.ShowParameterInput(selectedMethod);
        _ui.Build(allMethods, groupedMethods);

        // 서브 테스터 UI 설정
        var subTesterContainer = _ui.GetSubTesterContainer();
        _visibilityBGMTester?.SetupUI(subTesterContainer);
        _oomTester?.SetupUI(subTesterContainer);

        // SentryTester - 리플렉션으로 SetupUI 호출
        if (_sentryTester != null && _sentrySetupUI != null)
        {
            _sentrySetupUI.Invoke(_sentryTester, new object[] { subTesterContainer });
        }

        _iapTester?.SetupUI(subTesterContainer);
        _adV2Tester?.SetupUI(subTesterContainer);
        _contactsViralTester?.SetupUI(subTesterContainer);
        _metricEventTester?.SetupUI(subTesterContainer);

        // Safe Area Insets 로드 (Apps in Toss 플랫폼) - 로깅 목적
#if AIT_SDK_1_7_1_OR_LATER
        LogSafeAreaInsetsAsync();
#endif
    }

    void Update()
    {
        // DPI debug 텍스트 업데이트
        _ui?.UpdateDpiDebug();
    }

    void OnDestroy()
    {
        _ui?.Destroy();
    }

#if AIT_SDK_1_7_1_OR_LATER
    private async void LogSafeAreaInsetsAsync()
    {
        Rect unitySafeArea = Screen.safeArea;
        Debug.Log($"[InteractiveAPITester] Unity Screen.safeArea: x={unitySafeArea.x}, y={unitySafeArea.y}, width={unitySafeArea.width}, height={unitySafeArea.height}");
        Debug.Log($"[InteractiveAPITester] Screen size: width={Screen.width}, height={Screen.height}");

        try
        {
            var insets = await AIT.SafeAreaInsetsGet();
            Debug.Log($"[InteractiveAPITester] AIT SafeAreaInsetsGet (CSS px): top={insets.Top}, bottom={insets.Bottom}, left={insets.Left}, right={insets.Right}");

            double dpr = AIT.GetDevicePixelRatio();
            Debug.Log($"[InteractiveAPITester] DevicePixelRatio: {dpr}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[InteractiveAPITester] SafeAreaInsetsGet failed: {ex.Message}, using Unity Screen.safeArea as fallback");
        }
    }
#endif

    // ─── API 선택 / 실행 ───

    private void SelectAPI(APIMethodInfo method)
    {
        selectedMethod = method;
        _ui.ShowParameterInput(method);
        Debug.Log($"[InteractiveAPITester] Selected API: {method.Name} ({method.Category})");
    }

    private void BackToList()
    {
        selectedMethod = null;
        _ui.ShowView(InteractiveAPITesterUI.ViewState.APIList);
    }

    private async void ExecuteAPI()
    {
        Debug.Log($"[InteractiveAPITester] Executing API: {selectedMethod.Name}");

        try
        {
            // 파라미터 조합
            object[] parameters = new object[selectedMethod.Parameters.Count];
            for (int i = 0; i < selectedMethod.Parameters.Count; i++)
            {
                var param = selectedMethod.Parameters[i];
                parameters[i] = _ui.BuildParameterObject(param.Name, param.Type);
                Debug.Log($"[InteractiveAPITester] Parameter {param.Name}: {parameters[i]}");
            }

            // API 호출
            object result = selectedMethod.Method.Invoke(null, parameters);

            // Task 또는 Awaitable 대기
            var resultType = result.GetType();
            var resultTypeName = resultType.Name;
            Debug.Log($"[InteractiveAPITester] Return type: {resultTypeName}");

            if (result is Task task)
            {
                await task;

                Debug.Log($"[InteractiveAPITester] Task type: {resultType.FullName}");

                if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    var resultProperty = resultType.GetProperty("Result");
                    if (resultProperty != null)
                    {
                        object taskResult = resultProperty.GetValue(task);
                        Debug.Log($"[InteractiveAPITester] Task result: {taskResult}");
                        ShowResult(taskResult, true);
                    }
                    else
                    {
                        var awaiter = resultType.GetMethod("GetAwaiter").Invoke(task, null);
                        var getResultMethod = awaiter.GetType().GetMethod("GetResult");
                        var taskResult = getResultMethod.Invoke(awaiter, null);
                        Debug.Log($"[InteractiveAPITester] Task result via GetAwaiter: {taskResult}");
                        ShowResult(taskResult, true);
                    }
                }
                else
                {
                    ShowResult("Success (void)", true);
                }
            }
#if UNITY_6000_0_OR_NEWER
            else if (resultTypeName.StartsWith("Awaitable"))
            {
                Debug.Log($"[InteractiveAPITester] Awaitable type: {resultType.FullName}");

                object awaitableResult = await AwaitAndGetResult(result, resultType);

                if (resultType.IsGenericType)
                {
                    Debug.Log($"[InteractiveAPITester] Awaitable result: {awaitableResult}");
                    ShowResult(awaitableResult, true);
                }
                else
                {
                    ShowResult("Success (void)", true);
                }
            }
#endif
            else
            {
                ShowResult($"Unexpected return type: {resultTypeName}", false);
            }
        }
        catch (TargetInvocationException tie) when (tie.InnerException is AITException aitEx)
        {
            Debug.LogError($"[InteractiveAPITester] AITException: {aitEx.Message}");
            ShowAITException(aitEx);
        }
        catch (AITException aitEx)
        {
            Debug.LogError($"[InteractiveAPITester] AITException: {aitEx.Message}");
            ShowAITException(aitEx);
        }
        catch (AggregateException ae)
        {
            var innerEx = ae.Flatten().InnerException;
            Debug.LogError($"[InteractiveAPITester] AggregateException: {innerEx}");
            if (innerEx is AITException aitEx)
            {
                ShowAITException(aitEx);
            }
            else
            {
                ShowResult($"Error: {innerEx?.Message ?? ae.Message}\n\nStack Trace:\n{innerEx?.StackTrace ?? ae.StackTrace}", false);
            }
        }
        catch (Exception ex)
        {
            var innerEx = ex.InnerException ?? ex;
            Debug.LogError($"[InteractiveAPITester] API execution failed: {innerEx}");
            if (innerEx is AITException aitEx)
            {
                ShowAITException(aitEx);
            }
            else
            {
                ShowResult($"Error: {innerEx.Message}\n\nStack Trace:\n{innerEx.StackTrace}", false);
            }
        }
    }

    private void ShowAITException(AITException ex)
    {
        string errorInfo = $"API Error: {ex.APIName}\n\n";
        errorInfo += $"Message: {ex.Message}\n";

        if (!string.IsNullOrEmpty(ex.ErrorCode))
        {
            errorInfo += $"Error Code: {ex.ErrorCode}\n";
        }

        if (ex.IsPlatformUnavailable)
        {
            errorInfo += "\nPlatform Unavailable\n";
            errorInfo += "This API requires the Apps in Toss platform environment.\n";
            errorInfo += "It will not work in browser or Unity Editor.";
        }

        ShowResult(errorInfo, false);
    }

#if UNITY_6000_0_OR_NEWER
    private async Awaitable<object> AwaitAndGetResult(object awaitable, Type awaitableType)
    {
        if (!awaitableType.IsGenericType)
        {
            await (Awaitable)awaitable;
            return null;
        }

        var getAwaiterMethod = awaitableType.GetMethod("GetAwaiter");
        var awaiter = getAwaiterMethod.Invoke(awaitable, null);
        var awaiterType = awaiter.GetType();

        var isCompletedProperty = awaiterType.GetProperty("IsCompleted");
        while (!(bool)isCompletedProperty.GetValue(awaiter))
        {
            await Awaitable.NextFrameAsync();
        }

        var getResultMethod = awaiterType.GetMethod("GetResult");
        return getResultMethod.Invoke(awaiter, null);
    }
#endif

    private void ShowResult(object result, bool success)
    {
        string resultText;
        if (result == null)
            resultText = "null";
        else if (result is string s)
            resultText = s;
        else
            resultText = APIParameterInspector.SerializeToJson(result);

        Debug.Log($"[InteractiveAPITester] Result: {resultText}");

        _ui.ShowResult(selectedMethod.Name, result, success);
    }
}
