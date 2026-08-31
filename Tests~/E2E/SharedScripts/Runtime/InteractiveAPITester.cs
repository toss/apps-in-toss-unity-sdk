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

    // Action(구독) 반환 API 실행 시 살아있는 구독 1개를 추적 (동시에 여러 구독을 열지 않음 - 재진입/누수 방지)
    //
    // 단일 슬롯 정책의 적용 범위: 이 필드는 특정 API 몇 개가 아니라 Action을 반환하는
    // SDK API 13종 전부(GoogleAdMobLoadAppsInTossAdMob/GoogleAdMobShowAppsInTossAdMob,
    // LoadFullScreenAd/ShowFullScreenAd, GraniteEventSubscribeBackEvent/HomeEvent,
    // TdsEventSubscribeNavigationAccessoryEvent, StartUpdateLocation, ContactsViral,
    // IAPCreateOneTimePurchaseOrder/IAPCreateSubscriptionPurchaseOrder,
    // RequestNotificationAgreement, OnVisibilityChangedByTransparentServiceWeb)에 공통 적용된다 —
    // ExecuteAPI()가 반환 타입만으로 분기해 HandleSubscriptionResult()를 호출하기 때문에
    // 특정 API를 예외 처리하지 않는 한 전부 이 슬롯을 공유한다.
    // 이 인터랙티브 테스터는 임의 API를 하나씩 탐색/스모크 테스트하는 용도라 동시 1개 구독으로
    // 충분하며, 여러 구독을 동시에 유지하며 상호작용을 검증해야 하는 시나리오(광고 로드+표시
    // 동시 진행, IAP 주문 플로우 등)는 전용 테스터(AdV2Tester, IAPv2Tester, FullScreenAdTester 등)가
    // 정식 경로다 — 그쪽은 각자 독립된 구독 핸들을 관리한다.
    private Action _activeSubscriptionDisposer;
    private string _activeSubscriptionMethodName;

    // uGUI UI 관리자
    private InteractiveAPITesterUI _ui;

    // 분리된 컴포넌트 참조
    private OOMTester _oomTester;
#if AIT_SENTRY_AVAILABLE
    private SentryTester _sentryTester;
#endif
    private IAPv2Tester _iapTester;
    private AdV2Tester _adV2Tester;
    private FullScreenAdTester _fullScreenAdTester;
    private BannerAdTester _bannerAdTester;
    private ContactsViralTester _contactsViralTester;
    private VisibilityBGMTester _visibilityBGMTester;
    private MetricEventTester _metricEventTester;
    private PlayerPrefsTester _playerPrefsTester;
    private TapDiagnosticsTester _tapDiagnosticsTester;

    void Start()
    {
        Debug.Log("[InteractiveAPITester] Loading SDK APIs...");

        // 서브 테스터 컴포넌트 초기화
        _oomTester = GetComponent<OOMTester>() ?? gameObject.AddComponent<OOMTester>();

        // SentryTester — AIT_SENTRY_AVAILABLE 매크로로 직접 참조
#if AIT_SENTRY_AVAILABLE
        _sentryTester = GetComponent<SentryTester>() ?? gameObject.AddComponent<SentryTester>();
#endif

        _iapTester = GetComponent<IAPv2Tester>() ?? gameObject.AddComponent<IAPv2Tester>();
        _adV2Tester = GetComponent<AdV2Tester>() ?? gameObject.AddComponent<AdV2Tester>();
        _fullScreenAdTester = GetComponent<FullScreenAdTester>() ?? gameObject.AddComponent<FullScreenAdTester>();
        _bannerAdTester = GetComponent<BannerAdTester>() ?? gameObject.AddComponent<BannerAdTester>();
        _contactsViralTester = GetComponent<ContactsViralTester>() ?? gameObject.AddComponent<ContactsViralTester>();
        _visibilityBGMTester = GetComponent<VisibilityBGMTester>() ?? gameObject.AddComponent<VisibilityBGMTester>();
        _metricEventTester = GetComponent<MetricEventTester>() ?? gameObject.AddComponent<MetricEventTester>();
        _playerPrefsTester = GetComponent<PlayerPrefsTester>() ?? gameObject.AddComponent<PlayerPrefsTester>();
        _tapDiagnosticsTester = GetComponent<TapDiagnosticsTester>() ?? gameObject.AddComponent<TapDiagnosticsTester>();

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
        _ui.OnUnsubscribeRequested = DisposeActiveSubscription;
        _ui.Build(allMethods, groupedMethods);

        // 서브 테스터 UI 설정
        var subTesterContainer = _ui.GetSubTesterContainer();
        _visibilityBGMTester?.SetupUI(subTesterContainer);
        _oomTester?.SetupUI(subTesterContainer);

#if AIT_SENTRY_AVAILABLE
        _sentryTester?.SetupUI(subTesterContainer);
#endif

        _iapTester?.SetupUI(subTesterContainer);
        _adV2Tester?.SetupUI(subTesterContainer);
        _fullScreenAdTester?.SetupUI(subTesterContainer);
        _bannerAdTester?.SetupUI(subTesterContainer);
        _contactsViralTester?.SetupUI(subTesterContainer);
        _metricEventTester?.SetupUI(subTesterContainer);
        _playerPrefsTester?.SetupUI(subTesterContainer);

        // 탭 진단 패널은 PlayerPrefs의 Key/Value 입력칸 바로 아래에 둡니다 — 재현 대상을 탭한
        // 직후 같은 화면에서 결과를 읽을 수 있어야 합니다.
        _tapDiagnosticsTester?.SetupUI(subTesterContainer);

        // Safe Area Insets 적용 (Apps in Toss 플랫폼)
#if AIT_SDK_1_7_1_OR_LATER
        ApplySafeAreaInsetsAsync();
#endif
    }

    void Update()
    {
        // DPI debug 텍스트 업데이트
        _ui?.UpdateDpiDebug();
    }

    void OnDestroy()
    {
        DisposeActiveSubscription();
        _ui?.Destroy();
    }

#if AIT_SDK_1_7_1_OR_LATER
    private async void ApplySafeAreaInsetsAsync()
    {
        Rect unitySafeArea = Screen.safeArea;
        Debug.Log($"[InteractiveAPITester] Unity Screen.safeArea: x={unitySafeArea.x}, y={unitySafeArea.y}, width={unitySafeArea.width}, height={unitySafeArea.height}");
        Debug.Log($"[InteractiveAPITester] Screen size: width={Screen.width}, height={Screen.height}");

        try
        {
            var insets = await AIT.SafeAreaInsetsGet();

            // await 이후 오브젝트가 Destroy되었을 수 있음
            if (this == null) return;

            Debug.Log($"[InteractiveAPITester] AIT SafeAreaInsetsGet (CSS px): top={insets.Top}, bottom={insets.Bottom}, left={insets.Left}, right={insets.Right}");

            double dpr = AIT.GetDevicePixelRatio();
            Debug.Log($"[InteractiveAPITester] DevicePixelRatio: {dpr}");

            _ui.ApplySafeAreaInsets(new AITSafeAreaInsets(insets.Top, insets.Bottom, insets.Left, insets.Right, dpr));
        }
        catch (Exception ex)
        {
            if (this == null) return;
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
            // 콜백(Action/Action<T>) 타입 파라미터는 UI 입력값으로 만들 수 없으므로(Activator.CreateInstance가
            // delegate 타입에서 실패함) 여기서 리플렉션으로 로깅 델리게이트를 구성해 대신 채운다.
            object[] parameters = new object[selectedMethod.Parameters.Count];
            for (int i = 0; i < selectedMethod.Parameters.Count; i++)
            {
                var param = selectedMethod.Parameters[i];
                if (typeof(Delegate).IsAssignableFrom(param.Type))
                {
                    parameters[i] = BuildCallbackDelegate(param.Type, param.Name);
                    Debug.Log($"[InteractiveAPITester] Parameter {param.Name}: <callback: {param.Type.Name}>");
                }
                else
                {
                    parameters[i] = _ui.BuildParameterObject(param.Name, param.Type);
                    Debug.Log($"[InteractiveAPITester] Parameter {param.Name}: {parameters[i]}");
                }
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
            else if (result is Action unsubscribeAction)
            {
                // 구독형 API (onEvent/onError 콜백을 등록하고, 구독 해제용 Action을 반환)
                HandleSubscriptionResult(unsubscribeAction);
            }
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

    // ─── 구독형 API (Action 반환) 처리 ───

    /// <summary>
    /// 구독 등록 성공 시 호출됨. 재진입(같은/다른 API를 다시 실행)에 대비해 기존 구독을 먼저 정리하고
    /// 새 구독의 해제 Action을 보관한 뒤, UI에 구독 중 상태를 표시한다.
    ///
    /// 단일 슬롯 정책: Action을 반환하는 API 13종(GoogleAdMob Load/Show, FullScreenAd Load/Show,
    /// IAP 주문 생성 2종 등 - 상세는 _activeSubscriptionDisposer 필드 주석 참조) 중 무엇을 실행하든
    /// 이 메서드를 거치며, 새 구독을 시작하면 기존에 살아있던 구독은(대상 API와 무관하게) 자동
    /// 해제된다. 자동 해제가 발생하면 어떤 API의 구독이 대체됐는지 새 구독의 UI 로그 첫 줄에 남긴다.
    /// </summary>
    private void HandleSubscriptionResult(Action unsubscribeAction)
    {
        string previousMethodName = _activeSubscriptionMethodName;
        bool hadActiveSubscription = _activeSubscriptionDisposer != null;

        DisposeActiveSubscription();

        _activeSubscriptionDisposer = unsubscribeAction;
        _activeSubscriptionMethodName = selectedMethod.Name;

        Debug.Log($"[InteractiveAPITester] Subscribed: {selectedMethod.Name}");
        _ui.ShowSubscriptionResult(selectedMethod.Name);

        if (hadActiveSubscription)
        {
            _ui.AppendSubscriptionLog($"[{DateTime.Now:HH:mm:ss}] 이전 구독 자동 해제됨 (단일 슬롯 정책): {previousMethodName}");
        }
    }

    /// <summary>
    /// 현재 활성 구독이 있으면 해제한다. Unsubscribe 버튼, 새 구독 시작(재진입), 씬 파괴(OnDestroy)
    /// 3곳에서 공통으로 호출되어 구독 해제 Action이 누락되지 않도록 한다.
    /// </summary>
    private void DisposeActiveSubscription()
    {
        if (_activeSubscriptionDisposer == null) return;

        try
        {
            _activeSubscriptionDisposer.Invoke();
            Debug.Log($"[InteractiveAPITester] Unsubscribed: {_activeSubscriptionMethodName}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[InteractiveAPITester] 구독 해제 중 예외 발생: {ex.Message}");
        }

        _activeSubscriptionDisposer = null;
        _activeSubscriptionMethodName = null;
        _ui?.SetSubscriptionActive(false);
    }

    /// <summary>
    /// delegateType(Action 또는 Action&lt;T&gt;)에 맞는 로깅용 델리게이트를 리플렉션으로 구성한다.
    /// 페이로드가 있는 콜백(Action&lt;T&gt;)은 <see cref="CreateTypedLogger{T}"/>를 제네릭 메서드로
    /// 바인딩해 생성하고, 페이로드가 없는 콜백(Action)은 클로저로 직접 생성한다.
    /// </summary>
    private Delegate BuildCallbackDelegate(Type delegateType, string paramLabel)
    {
        var invokeMethod = delegateType.GetMethod("Invoke");
        var invokeParams = invokeMethod.GetParameters();

        if (invokeParams.Length == 0)
        {
            Action handler = () => OnSubscriptionEvent(paramLabel, null);
            return handler;
        }

        if (invokeParams.Length == 1)
        {
            Type payloadType = invokeParams[0].ParameterType;
            var genericHelper = typeof(InteractiveAPITester)
                .GetMethod(nameof(CreateTypedLogger), BindingFlags.NonPublic | BindingFlags.Instance)
                .MakeGenericMethod(payloadType);
            return (Delegate)genericHelper.Invoke(this, new object[] { paramLabel });
        }

        // 대상 5종 API는 모두 0~1개 파라미터의 콜백만 사용함. 향후 다중 파라미터 콜백이 추가되면
        // 여기서 null을 반환하고 Invoke 시 ArgumentException으로 드러나 즉시 눈에 띈다.
        Debug.LogWarning($"[InteractiveAPITester] 지원하지 않는 콜백 시그니처: {paramLabel} ({invokeParams.Length}개 파라미터)");
        return null;
    }

    /// <summary>
    /// Action&lt;T&gt;를 위한 제네릭 로깅 델리게이트 생성. BuildCallbackDelegate가 MakeGenericMethod로
    /// 런타임 payload 타입 T를 바인딩해 호출한다.
    /// </summary>
    private Delegate CreateTypedLogger<T>(string paramLabel)
    {
        Action<T> handler = (payload) => OnSubscriptionEvent(paramLabel, payload);
        return handler;
    }

    /// <summary>
    /// 구독 콜백(onEvent/onError)이 발생할 때마다 호출되어 이벤트를 콘솔과 UI 로그에 남긴다.
    /// </summary>
    private void OnSubscriptionEvent(string paramLabel, object payload)
    {
        string message;
        if (payload == null)
        {
            message = "(no payload)";
        }
        else if (payload is AITException aitEx)
        {
            message = $"AITException: {aitEx.Message} (code: {aitEx.ErrorCode})";
        }
        else
        {
            message = APIParameterInspector.SerializeToJson(payload);
        }

        string line = $"[{DateTime.Now:HH:mm:ss}] {paramLabel}: {message}";
        Debug.Log($"[InteractiveAPITester] Subscription event ({_activeSubscriptionMethodName}): {line}");
        _ui.AppendSubscriptionLog(line);
    }
}
