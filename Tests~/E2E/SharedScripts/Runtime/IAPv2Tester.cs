using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using AppsInToss;

/// <summary>
/// IAPv2 (인앱결제 v2) 테스터 컴포넌트
/// 인앱결제 v2 API 워크플로우를 테스트할 수 있는 UI 제공
/// OOMTester 패턴을 따라 InteractiveAPITester에서 분리됨
///
/// ## ⚠️ processProductGrant 안에서 await 하지 말 것 (Unity WebGL 제약)
/// 업스트림 계약은 Promise 반환을 허용하지만, 그건 JS 미니앱 기준이다. 결제 native
/// 오버레이가 떠 있는 동안 웹뷰는 visibilityState=hidden이 되고 rAF가 스펙대로 멈춘다.
/// Unity WebGL의 player loop는 rAF로 구동되므로 함께 멈추고, C# await continuation도
/// 재개되지 않는다. 그런데 오버레이는 이 콜백이 끝나야 닫히고, 루프는 오버레이가 닫혀야
/// 도므로 자력 탈출이 불가능한 교착이 된다 (2026-07-21 실기기 실측: rAF 갭 27.5s ≡
/// hidden 구간 27.5s ≡ C# 프레임 갭 27.4s).
///
/// 콜백 "진입"은 정지 중에도 된다 — SendMessage가 JS 스택에서 동기로 C#을 부르기
/// 때문이다. 막히는 건 await 이후의 continuation뿐이다. 따라서:
///   - 동기 반환(Task.FromResult(true))만 안전하다. JS 스택 위에서 끝나 즉시 응답한다.
///   - await(코루틴/UnityWebRequest/Task.Delay 무엇이든)은 결제 완료 시 영구 교착이다.
/// 추가로 Task.Delay는 WebGL에 타이머 스레드가 없어 애초에 완료되지 않는다.
///
/// ## 정상 플로우 (소모품)
/// 1. GetProductItemList() - 상품 목록 조회
/// 2. CreateOneTimePurchaseOrder() - 구매 주문 생성
///    - processProductGrant는 동기로 true 반환 (서버 검증을 여기서 기다리지 않는다)
///    - SDK가 자동으로 CompleteProductGrant 호출하여 주문 완료 처리
///
/// ## 서버 영수증 검증이 필요하면 — 복구 플로우에서 (오버레이가 닫힌 뒤라 루프가 살아있다)
/// 1. GetCompletedOrRefundedOrders() - 미처리 완료 주문 조회
/// 2. 각 주문에 대해 서버 검증 후 상품 지급 수행
/// 3. CompleteProductGrant() - 수동으로 주문 완료 처리
/// 앱 크래시/네트워크 끊김으로 콜백이 미호출된 경우의 복구 경로이기도 하다.
///
/// ## 비소모품
/// - 한 번 구매하면 영구 소유, CompleteProductGrant 불필요
/// </summary>
public class IAPv2Tester : MonoBehaviour
{
    // IAP 테스트 상태
    private string iapSku = "";
    private string iapOrderId = "";
    private string iapStatus = "";
    private IAPGetProductItemListResult iapProducts = null;
    private IAPGetPendingOrdersResult iapPendingOrders = null;
    private CompletedOrRefundedOrdersResult iapCompletedOrders = null;
    private List<string> iapEventLog = new List<string>();
    private int _lastRenderedLogCount = 0;

    /// <summary>화면 이벤트 로그(iapEventLog) 상한. 초과 시 오래된 항목을 트리밍한다.</summary>
    private const int MaxIapEventLogCount = 300;

    /// <summary>
    /// [PLP] 하드닝(AITCore.NestedCallbackTimeoutMs) opt-in 값. UI 토글로 0 ↔ 이 값을 오간다.
    /// 한 빌드로 "교착 재현"(0)과 "하드닝 검증"(ON)을 모두 측정하기 위한 스위치.
    ///
    /// 측정용 10초 — round 2 실측 오버레이 지속이 13.6s/27.5s였고 30s로는 오버레이가
    /// 닫히기 전에 발화하지 못했다. 프로덕션 권장값이 아니라 "타임아웃이 실제로 오버레이를
    /// 닫는가"를 관찰 가능한 창 안에서 확인하기 위한 값이다.
    /// </summary>
    private const int PlpHardeningTimeoutMs = 10000;

    private Text _plpToggleLabel;

    /// <summary>
    /// [실험 1] ProcessProductGrant가 응답을 만드는 방식.
    ///
    /// SyncTrue  — await 0회로 true. 주문이 PURCHASED로 확정되며 되돌릴 수 없다(un-grant API 없음).
    /// SyncFalse — await 0회로 false. 주문이 PAYMENT_COMPLETED로 남아 getPendingOrders +
    ///             IAPCompleteProductGrant로 복구 가능하다. 실패해도 안전한 방향.
    /// Async     — 서버 검증을 await. 교착 재현 arm.
    /// </summary>
    private enum GrantMode
    {
        SyncTrue,
        SyncFalse,
        Async
    }

    // 실험 1의 대상이 SyncTrue이므로 기본값으로 둔다.
    private GrantMode _grantMode = GrantMode.SyncTrue;
    private Text _grantModeLabel;

    // 구독 해제 액션
    private Action _purchaseDisposer;

    // uGUI 참조
    private Text _statusText;
    private InputField _skuInput;
    private InputField _orderIdInput;
    private GameObject _eventLogContainer;
    private GameObject _productListContainer;
    private GameObject _quickSelectContainer;
    private GameObject _pendingOrdersContainer;
    private GameObject _completedOrdersContainer;

    /// <summary>
    /// 마지막 작업 상태 메시지
    /// </summary>
    public string Status => iapStatus;

    private void Awake()
    {
        // WebGL 빌드에서는 Debug.Log/Warning 한 줄마다 스택트레이스가 함께 캡처되어
        // vConsole 노이즈가 커지고, 문자열 생성 비용 때문에 프레임 성능도 저하된다.
        // 진단 가치가 큰 Error/Exception은 스택트레이스를 그대로 유지한다.
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
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

        UIBuilder.CreateText(section, "IAPv2 Tester (인앱결제v2)",
            UIBuilder.Theme.FontLarge, UIBuilder.Theme.TextAccent, fontStyle: FontStyle.Bold);
        UIBuilder.CreateText(section, "인앱결제 v2 API 워크플로우 예제입니다.",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);

        // 상태
        _statusText = UIBuilder.CreateText(section, "",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextPrimary);
        _statusText.gameObject.SetActive(false);

        // 이벤트 로그
        _eventLogContainer = CreateEventLogContainer(section);

        // Step 1: 상품 목록
        UIBuilder.CreateText(section, "Step 1: Get Product List",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary, fontStyle: FontStyle.Bold);
        UIBuilder.CreateButton(section, "IAPGetProductItemList()", onClick: ExecuteIAPGetProductList);

        _productListContainer = CreateDynamicContainer(section, "ProductList");
        _productListContainer.SetActive(false);

        // Step 2: 구매 주문 생성
        UIBuilder.CreateText(section, "Step 2: Create Purchase Order",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary, fontStyle: FontStyle.Bold);

        var skuRow = UIBuilder.CreateHorizontalLayout(section, 8);
        skuRow.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var skuLabel = UIBuilder.CreateText(skuRow, "SKU:",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        UIBuilder.SetLayout(skuLabel.gameObject, minWidth: 50, preferredWidth: 50);
        _skuInput = UIBuilder.CreateInputField(skuRow, "",
            onValueChanged: (v) => iapSku = v);
        UIBuilder.SetLayout(_skuInput.gameObject, flexibleWidth: 1);

        _quickSelectContainer = CreateDynamicContainer(section, "QuickSelect");
        _quickSelectContainer.SetActive(false);

        UIBuilder.CreateButton(section, "IAPCreateOneTimePurchaseOrder(...)", onClick: ExecuteIAPCreateOrder);

        // Step 3: Pending Orders
        UIBuilder.CreateText(section, "Step 3: Get Pending Orders",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary, fontStyle: FontStyle.Bold);
        UIBuilder.CreateButton(section, "IAPGetPendingOrders()", onClick: ExecuteIAPGetPendingOrders);

        _pendingOrdersContainer = CreateDynamicContainer(section, "PendingOrders");
        _pendingOrdersContainer.SetActive(false);

        // Step 4: Completed/Refunded Orders
        UIBuilder.CreateText(section, "Step 4: Get Completed/Refunded Orders (복구용)",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary, fontStyle: FontStyle.Bold);
        UIBuilder.CreateButton(section, "IAPGetCompletedOrRefundedOrders()", onClick: ExecuteIAPGetCompletedOrRefundedOrders);

        _completedOrdersContainer = CreateDynamicContainer(section, "CompletedOrders");
        _completedOrdersContainer.SetActive(false);

        // Step 5: Complete Product Grant
        UIBuilder.CreateText(section, "Step 5: Complete Product Grant (복구용)",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary, fontStyle: FontStyle.Bold);

        var orderIdRow = UIBuilder.CreateHorizontalLayout(section, 8);
        orderIdRow.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var orderIdLabel = UIBuilder.CreateText(orderIdRow, "Order ID:",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        UIBuilder.SetLayout(orderIdLabel.gameObject, minWidth: 80, preferredWidth: 80);
        _orderIdInput = UIBuilder.CreateInputField(orderIdRow, "",
            onValueChanged: (v) => iapOrderId = v);
        UIBuilder.SetLayout(_orderIdInput.gameObject, flexibleWidth: 1);

        UIBuilder.CreateButton(section, "IAPCompleteProductGrant(...)", onClick: ExecuteIAPCompleteGrant);

        // [PLP] 교착 방지 하드닝 토글 — 같은 빌드로 재현(OFF)과 검증(ON)을 번갈아 측정한다.
        UIBuilder.CreateText(section, "진단: Player Loop Probe (techchat 4377)",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary, fontStyle: FontStyle.Bold);
        _plpToggleLabel = UIBuilder.CreateText(section, "",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        UIBuilder.CreateButton(section, "NestedCallbackTimeoutMs 토글 (OFF ↔ 30s)", onClick: TogglePlpHardening);

        // [실험 1] grant 응답 방식 — 결제 시작 전에 눌러 arm을 고른다.
        _grantModeLabel = UIBuilder.CreateText(section, "",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        UIBuilder.CreateButton(section, "ProcessProductGrant 모드 토글", onClick: ToggleGrantMode);

        // 교착이 완전하면 onEvent/onError가 끝내 오지 않아 자동 리포트가 안 나온다.
        // 화면이 다시 움직이기 시작하면 이 버튼으로 수집분을 강제로 덤프한다.
        UIBuilder.CreateButton(section, "PLP 리포트 강제 출력", onClick: () => ReportPlayerLoopProbe("manual"));
        UpdatePlpToggleLabel();
        UpdateGrantModeLabel();
    }

    /// <summary>[실험 1] grant 응답 방식을 순환시킨다. 결제 시작 전에 눌러 모드를 정한다.</summary>
    private void ToggleGrantMode()
    {
        _grantMode = _grantMode == GrantMode.SyncTrue ? GrantMode.SyncFalse
                   : _grantMode == GrantMode.SyncFalse ? GrantMode.Async
                   : GrantMode.SyncTrue;
        UpdateGrantModeLabel();
        LogIap($"[실험1] ProcessProductGrant 모드 = {_grantMode}");
        UpdateEventLog();
    }

    private void UpdateGrantModeLabel()
    {
        if (_grantModeLabel == null) return;
        _grantModeLabel.text =
            _grantMode == GrantMode.SyncTrue ? "SyncTrue — 동기 true (지급 확정, 되돌릴 수 없음)"
          : _grantMode == GrantMode.SyncFalse ? "SyncFalse — 동기 false (주문 pending 유지, 복구 가능)"
          : "Async — 서버 검증 await (교착 재현)";
    }

    /// <summary>[PLP] 하드닝 opt-in을 켜고 끈다. 결제 시작 전에 눌러 모드를 정한다.</summary>
    private void TogglePlpHardening()
    {
        AITCore.NestedCallbackTimeoutMs =
            AITCore.NestedCallbackTimeoutMs > 0 ? 0 : PlpHardeningTimeoutMs;
        UpdatePlpToggleLabel();
        LogIap($"[PLP] NestedCallbackTimeoutMs = {AITCore.NestedCallbackTimeoutMs}ms " +
               $"({(AITCore.NestedCallbackTimeoutMs > 0 ? "하드닝 ON" : "하드닝 OFF — 교착 재현 모드")})");
        UpdateEventLog();
    }

    private void UpdatePlpToggleLabel()
    {
        if (_plpToggleLabel == null) return;
        int ms = AITCore.NestedCallbackTimeoutMs;
        _plpToggleLabel.text = ms > 0
            ? $"하드닝 ON — {ms}ms 후 자동 false 응답 (오버레이 해제 시도)"
            : "하드닝 OFF — 교착 재현 모드 (타임아웃 없음)";
    }

    private GameObject CreateDynamicContainer(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>().SetParent(parent, false);
        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 2;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return go;
    }

    private GameObject CreateEventLogContainer(Transform parent)
    {
        var go = CreateDynamicContainer(parent, "EventLog");
        go.SetActive(false);
        return go;
    }

    private void ClearContainer(GameObject container)
    {
        for (int i = container.transform.childCount - 1; i >= 0; i--)
            Destroy(container.transform.GetChild(i).gameObject);
    }

    private void UpdateStatus()
    {
        if (_statusText != null)
        {
            _statusText.text = $"Status: {iapStatus}";
            _statusText.gameObject.SetActive(!string.IsNullOrEmpty(iapStatus));
        }
    }

    /// <summary>
    /// IAP 이벤트를 화면 로그(iapEventLog)와 콘솔(Debug.Log)에 동일한 타임스탬프로 1회씩 기록한다.
    /// 실기기 콘솔 로그에도 발생 시각을 남기기 위해 "HH:mm:ss.fff" 타임스탬프를 공유한다.
    /// </summary>
    /// <param name="msg">기록할 메시지 (타임스탬프/프리픽스 제외한 본문)</param>
    /// <param name="toConsole">false면 화면(iapEventLog)에만 남기고 콘솔에는 기록하지 않는다</param>
    private void LogIap(string msg, bool toConsole = true)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        iapEventLog.Add($"[{timestamp}] {msg}");

        if (iapEventLog.Count > MaxIapEventLogCount)
        {
            // _lastRenderedLogCount 기반 증분 렌더링은 트리밍하면 인덱스가 어긋난다.
            // 렌더 카운트를 리셋해 다음 UpdateEventLog() 호출이 전체 재구축(단순·정확한 경로)을
            // 타도록 강제하는 방식으로 정합을 보장한다.
            iapEventLog.RemoveRange(0, iapEventLog.Count - MaxIapEventLogCount);
            _lastRenderedLogCount = 0;
        }

        if (toConsole)
        {
            Debug.Log($"[IAPv2Tester] [{timestamp}] {msg}");
        }
    }

    private void UpdateEventLog()
    {
        if (_eventLogContainer == null) return;

        if (iapEventLog.Count == 0)
        {
            _eventLogContainer.SetActive(false);
            _lastRenderedLogCount = 0;
            ClearContainer(_eventLogContainer);
            return;
        }

        _eventLogContainer.SetActive(true);

        int displayStart = Math.Max(0, iapEventLog.Count - 5);
        int prevDisplayStart = Math.Max(0, _lastRenderedLogCount - 5);

        if (_lastRenderedLogCount == 0 || displayStart != prevDisplayStart)
        {
            // 전체 재구축
            ClearContainer(_eventLogContainer);
            UIBuilder.CreateText(_eventLogContainer.transform, "Event Log:",
                UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
            for (int i = displayStart; i < iapEventLog.Count; i++)
            {
                UIBuilder.CreateText(_eventLogContainer.transform, $"  {iapEventLog[i]}",
                    UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextCallback);
            }
        }
        else
        {
            // 새 항목만 추가
            for (int i = _lastRenderedLogCount; i < iapEventLog.Count; i++)
            {
                UIBuilder.CreateText(_eventLogContainer.transform, $"  {iapEventLog[i]}",
                    UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextCallback);
            }
        }

        _lastRenderedLogCount = iapEventLog.Count;
    }

    private void UpdateProductList()
    {
        if (_productListContainer == null) return;
        ClearContainer(_productListContainer);

        if (iapProducts != null && iapProducts.Products != null && iapProducts.Products.Length > 0)
        {
            _productListContainer.SetActive(true);
            UIBuilder.CreateText(_productListContainer.transform,
                $"Products ({iapProducts.Products.Length}):",
                UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextPrimary);
            int displayCount = Math.Min(3, iapProducts.Products.Length);
            for (int i = 0; i < displayCount; i++)
            {
                var product = iapProducts.Products[i];
                UIBuilder.CreateText(_productListContainer.transform,
                    $"  - {product.DisplayName} ({product.DisplayAmount})",
                    UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextCallback);
            }

            // Quick select 버튼
            ClearContainer(_quickSelectContainer);
            _quickSelectContainer.SetActive(true);
            UIBuilder.CreateText(_quickSelectContainer.transform, "Quick Select:",
                UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
            foreach (var product in iapProducts.Products)
            {
                var sku = product.Sku;
                UIBuilder.CreateButton(_quickSelectContainer.transform,
                    $"{product.DisplayName} ({product.Sku})",
                    onClick: () =>
                    {
                        iapSku = sku;
                        if (_skuInput != null) _skuInput.text = sku;
                    });
            }
        }
        else
        {
            _productListContainer.SetActive(false);
            _quickSelectContainer.SetActive(false);
        }
    }

    private void UpdatePendingOrders()
    {
        if (_pendingOrdersContainer == null) return;
        ClearContainer(_pendingOrdersContainer);

#if AIT_SDK_1_7_OR_LATER
        if (iapPendingOrders != null && iapPendingOrders.Orders != null && iapPendingOrders.Orders.Length > 0)
        {
            _pendingOrdersContainer.SetActive(true);
            UIBuilder.CreateText(_pendingOrdersContainer.transform,
                $"Pending Orders ({iapPendingOrders.Orders.Length}):",
                UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextPrimary);
            UIBuilder.CreateText(_pendingOrdersContainer.transform,
                "Select to fill Order ID:",
                UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextCallback);
            foreach (var order in iapPendingOrders.Orders)
            {
                if (!string.IsNullOrEmpty(order.OrderId))
                {
                    var orderId = order.OrderId;
                    UIBuilder.CreateButton(_pendingOrdersContainer.transform,
                        $"→ {order.OrderId} ({order.Sku})",
                        onClick: () =>
                        {
                            iapOrderId = orderId;
                            if (_orderIdInput != null) _orderIdInput.text = orderId;
                        });
                }
            }
        }
        else
        {
            _pendingOrdersContainer.SetActive(false);
        }
#else
        if (iapPendingOrders != null && iapPendingOrders.Orders != null && iapPendingOrders.Orders.Length > 0)
        {
            _pendingOrdersContainer.SetActive(true);
            UIBuilder.CreateText(_pendingOrdersContainer.transform,
                $"Pending Orders ({iapPendingOrders.Orders.Length}) - SDK 1.7.0+ required for details",
                UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextPrimary);
        }
        else
        {
            _pendingOrdersContainer.SetActive(false);
        }
#endif
    }

    private void UpdateCompletedOrders()
    {
        if (_completedOrdersContainer == null) return;
        ClearContainer(_completedOrdersContainer);

#if AIT_SDK_1_7_OR_LATER
        if (iapCompletedOrders != null && iapCompletedOrders.Orders != null && iapCompletedOrders.Orders.Length > 0)
        {
            _completedOrdersContainer.SetActive(true);
            UIBuilder.CreateText(_completedOrdersContainer.transform,
                $"Completed/Refunded Orders ({iapCompletedOrders.Orders.Length}):",
                UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextPrimary);
            UIBuilder.CreateText(_completedOrdersContainer.transform,
                "Select to fill Order ID:",
                UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextCallback);
            foreach (var order in iapCompletedOrders.Orders)
            {
                if (!string.IsNullOrEmpty(order.OrderId))
                {
                    var orderId = order.OrderId;
                    string orderStatus = order.Status == CompletedOrRefundedOrdersResultOrderStatus.REFUNDED ? "Refunded" : "Completed";
                    UIBuilder.CreateButton(_completedOrdersContainer.transform,
                        $"→ {order.OrderId} ({order.Sku}, {orderStatus})",
                        onClick: () =>
                        {
                            iapOrderId = orderId;
                            if (_orderIdInput != null) _orderIdInput.text = orderId;
                        });
                }
            }
        }
        else
        {
            _completedOrdersContainer.SetActive(false);
        }
#else
        if (iapCompletedOrders != null && iapCompletedOrders.Orders != null && iapCompletedOrders.Orders.Length > 0)
        {
            _completedOrdersContainer.SetActive(true);
            UIBuilder.CreateText(_completedOrdersContainer.transform,
                $"Completed/Refunded Orders ({iapCompletedOrders.Orders.Length}) - SDK 1.7.0+ required for details",
                UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextPrimary);
        }
        else
        {
            _completedOrdersContainer.SetActive(false);
        }
#endif
    }

    private async void ExecuteIAPGetProductList()
    {
        iapStatus = "Loading products...";
        LogIap("IAPGetProductItemList()");
        UpdateStatus();
        UpdateEventLog();

        try
        {
            iapProducts = await AIT.IAPGetProductItemList();
            int count = iapProducts?.Products?.Length ?? 0;
            iapStatus = $"Found {count} products";
            LogIap($"Success: {count} products");
        }
        catch (AITException ex)
        {
            iapStatus = $"Error: {ex.Message}";
            LogIap($"Error: {ex.ErrorCode} - {ex.Message}");
        }
        catch (Exception ex)
        {
            iapStatus = $"Error: {ex.Message}";
            LogIap($"Exception: {ex.Message}");
        }

        UpdateStatus();
        UpdateEventLog();
        UpdateProductList();
    }

    private void ExecuteIAPCreateOrder()
    {
        if (string.IsNullOrEmpty(iapSku))
        {
            iapStatus = "Please enter or select a SKU";
            UpdateStatus();
            return;
        }

        iapStatus = "Creating purchase order...";
        LogIap($"IAPCreateOneTimePurchaseOrder(sku: {iapSku})");
        ArmPlayerLoopProbe();
        UpdateStatus();
        UpdateEventLog();

        try
        {
#if AIT_SDK_1_7_OR_LATER
            var options = new IapCreateOneTimePurchaseOrderOptionsOptions
            {
                Sku = iapSku,
                // [실험 1] 동기 응답이 실제로 네이티브 오버레이를 닫는가?
                // 오버레이가 webview를 덮는 동안 player loop가 멈춰 await continuation이 재개되지
                // 않는다(115s 실측). 그래서 남는 유일한 경로가 "await 0회 + 이미 결정된 값 반환"인데,
                // 그게 실제로 통하는지는 한 번도 측정된 적이 없다 — hidden 상태에서 JS→native
                // postMessage가 전달되는지 자체가 미검증이기 때문이다. GrantMode로 같은 빌드에서
                // SyncTrue/SyncFalse/Async를 번갈아 눌러 비교한다.
                ProcessProductGrant = (data) =>
                {
                    // 콜백 진입은 SendMessage 동기 스택이라 loop가 멈춰 있어도 실행된다.
                    LogIap($"ProcessProductGrant called [{_grantMode}]: {data}");

                    // [PLP] Task.Delay가 WebGL에서 완료되는가? 어느 모드에서든 관찰만 한다.
                    _ = ProbeTaskDelayAsync();

                    if (_grantMode == GrantMode.Async)
                    {
                        return GrantViaAsyncVerificationAsync(data);
                    }

                    // [PLP] 동기 모드에서도 프리즈 구간 길이는 재고 싶으므로 fire-and-forget으로
                    // 띄운다. 응답은 이 아래에서 즉시 나가므로 크리티컬 패스에 없다.
                    _ = VerifyReceiptViaWebRequest(data);

                    bool grantSuccess = _grantMode == GrantMode.SyncTrue;
                    MarkGrantResponded();
                    LogIap($"ProcessProductGrant result: {grantSuccess} (await 0회 — 동기 반환)");
                    UpdateEventLog();
                    return Task.FromResult(grantSuccess);
                }
            };

            _purchaseDisposer?.Invoke();
            _purchaseDisposer = AIT.IAPCreateOneTimePurchaseOrder(
                onEvent: (successEvent) =>
                {
                    iapStatus = "Purchase completed";
                    iapOrderId = successEvent.Data?.OrderId ?? "";
                    if (_orderIdInput != null) _orderIdInput.text = iapOrderId;
                    LogIap($"OnEvent: orderId={successEvent.Data?.OrderId}, amount={successEvent.Data?.DisplayAmount}");
                    ReportPlayerLoopProbe("onEvent");
                    UpdateStatus();
                    UpdateEventLog();
                },
                options: options,
                onError: (error) =>
                {
                    iapStatus = "Purchase failed";
                    LogIap($"OnError: {error.ErrorCode} - {error.Message}");
                    ReportPlayerLoopProbe("onError");
                    UpdateStatus();
                    UpdateEventLog();
                }
            );
#else
            ExecuteIAPCreateOrderLegacy();
#endif
            iapStatus = "Purchase order created";
            LogIap("Order created successfully");
        }
        catch (AITException ex)
        {
            iapStatus = $"Error: {ex.Message}";
            LogIap($"Error: {ex.ErrorCode} - {ex.Message}");
        }
        catch (Exception ex)
        {
            iapStatus = $"Error: {ex.Message}";
            LogIap($"Exception: {ex.Message}");
        }

        UpdateStatus();
        UpdateEventLog();
    }

#if !AIT_SDK_1_7_OR_LATER
    private void ExecuteIAPCreateOrderLegacy()
    {
        try
        {
            var options = new IapCreateOneTimePurchaseOrderOptionsOptions
            {
                Sku = iapSku
            };

            _purchaseDisposer?.Invoke();
            _purchaseDisposer = AIT.IAPCreateOneTimePurchaseOrder(
                onEvent: (successEvent) =>
                {
                    iapStatus = "Purchase completed (legacy)";
                    LogIap("OnEvent (legacy): success");
                    UpdateStatus();
                    UpdateEventLog();
                },
                options: options,
                onError: (error) =>
                {
                    iapStatus = "Purchase failed (legacy)";
                    LogIap($"OnError (legacy): {error?.Message}");
                    UpdateStatus();
                    UpdateEventLog();
                }
            );
        }
        catch (Exception ex)
        {
            iapStatus = $"Error: {ex.Message}";
            LogIap($"Exception (legacy): {ex.Message}");
        }
    }
#endif

    private async void ExecuteIAPGetPendingOrders()
    {
        iapStatus = "Loading pending orders...";
        LogIap("IAPGetPendingOrders()");
        UpdateStatus();
        UpdateEventLog();

        try
        {
            iapPendingOrders = await AIT.IAPGetPendingOrders();
            int count = iapPendingOrders?.Orders?.Length ?? 0;
            iapStatus = $"Found {count} pending orders";
            LogIap($"Success: {count} orders");
        }
        catch (AITException ex)
        {
            iapPendingOrders = null;
            iapStatus = $"Error: {ex.Message}";
            LogIap($"Error: {ex.ErrorCode} - {ex.Message}");
        }
        catch (Exception ex)
        {
            iapPendingOrders = null;
            iapStatus = $"Error: {ex.Message}";
            LogIap($"Exception: {ex.Message}");
        }

        UpdateStatus();
        UpdateEventLog();
        UpdatePendingOrders();
    }

    private async void ExecuteIAPGetCompletedOrRefundedOrders()
    {
        iapStatus = "Loading completed/refunded orders...";
        LogIap("IAPGetCompletedOrRefundedOrders()");
        UpdateStatus();
        UpdateEventLog();

        try
        {
            iapCompletedOrders = await AIT.IAPGetCompletedOrRefundedOrders();
            int count = iapCompletedOrders?.Orders?.Length ?? 0;
            iapStatus = $"Found {count} completed/refunded orders";
            LogIap($"Success: {count} orders, HasNext={iapCompletedOrders?.HasNext}");
        }
        catch (AITException ex)
        {
            iapCompletedOrders = null;
            iapStatus = $"Error: {ex.Message}";
            LogIap($"Error: {ex.ErrorCode} - {ex.Message}");
        }
        catch (Exception ex)
        {
            iapCompletedOrders = null;
            iapStatus = $"Error: {ex.Message}";
            LogIap($"Exception: {ex.Message}");
        }

        UpdateStatus();
        UpdateEventLog();
        UpdateCompletedOrders();
    }

    private async void ExecuteIAPCompleteGrant()
    {
        if (string.IsNullOrEmpty(iapOrderId))
        {
            iapStatus = "Please enter Order ID";
            UpdateStatus();
            return;
        }

        iapStatus = "Processing product grant...";
        LogIap($"IAPCompleteProductGrant(orderId: {iapOrderId})");
        UpdateStatus();
        UpdateEventLog();

        try
        {
            var args = new IAPCompleteProductGrantArgs_0
            {
                Params = new IAPCompleteProductGrantArgs_0Params
                {
                    OrderId = iapOrderId
                }
            };

            bool success = await AIT.IAPCompleteProductGrant(args);
            iapStatus = success ? "Product grant completed" : "Product grant failed";
            LogIap($"Result: {success}");
        }
        catch (AITException ex)
        {
            iapStatus = $"Error: {ex.Message}";
            LogIap($"Error: {ex.ErrorCode} - {ex.Message}");
        }
        catch (Exception ex)
        {
            iapStatus = $"Error: {ex.Message}";
            LogIap($"Exception: {ex.Message}");
        }

        UpdateStatus();
        UpdateEventLog();
    }

    private void OnDestroy()
    {
        _purchaseDisposer?.Invoke();
    }

    /// <summary>
    /// 실제 게임 상품 지급 로직 (데모용)
    /// 개발사 서버의 영수증 검증 API 왕복을 코루틴 + <see cref="TaskCompletionSource{TResult}"/>로 시뮬레이션한다.
    /// (WebGL은 단일 스레드라 Task.Delay 대신 플레이어 루프가 구동하는 코루틴으로 대기한다 — SDK 관용.)
    /// 실제 구현에서는 이 자리에서 UnityWebRequest/HttpClient로 서버에 영수증을 전송해 검증 결과를 반환한다.
    /// </summary>
    private Task<bool> GrantGameProduct(object data)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(ValidateReceiptOnServer(data, tcs));
        return tcs.Task;
    }

    /// <summary>
    /// 서버 영수증 검증 왕복 시뮬레이션 (WebGL-safe: WaitForSecondsRealtime 코루틴).
    /// </summary>
    private System.Collections.IEnumerator ValidateReceiptOnServer(object data, TaskCompletionSource<bool> tcs)
    {
        LogIap($"Validating receipt on server: {data}");
        // 동기 스텁으로는 표현 불가했던 비동기 서버 왕복 경로
        yield return new WaitForSecondsRealtime(0.2f);
        LogIap($"Receipt validated, granting product: {data}");
        tcs.SetResult(true);
    }

    // =====================================================
    // Player loop freeze 진단 프로브 (techchat 4377 검증용) — round 2
    //
    // 결제 native 오버레이 구간에서 실측으로 판별한다:
    //  (1) player loop(Update) 정지 여부  — C# 프레임 하트비트의 최대 gap
    //  (2) rAF / 타이머 / 매크로태스크     — jslib 3종 하트비트 (무엇이 멈추는지 분리)
    //  (3) C# await continuation 재개 지연 — 코루틴 기반 대기 실측 (+ Task.Delay 완료 여부 관찰)
    //  (4) 실제 서버 검증 완료 가능 여부    — UnityWebRequest 실 왕복 (hugh의 원래 요구사항)
    //
    // 모든 기록은 메모리에 남겼다가 루프 재개 후 이벤트 로그로 출력한다
    // (loop가 멈춰도 SendMessage 진입·메모리 기록은 동기 경로라 유실되지 않음).
    // =====================================================
#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void PLP_StartJsProbe();

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern string PLP_GetJsReport();
#else
    private static void PLP_StartJsProbe() { }
    private static string PLP_GetJsReport() { return "{\"raf\":{},\"timer\":{},\"visibility\":[]}"; }
#endif

    /// <summary>
    /// [실험 1] Async 모드 — 서버 검증을 await한 뒤 지급 여부를 정하는 원래 요구사항 그대로.
    /// 교착 재현 arm이며, 동기 모드와 같은 빌드에서 비교하기 위해 남겨둔다.
    /// </summary>
    private async Task<bool> GrantViaAsyncVerificationAsync(object data)
    {
        var t0 = DateTime.UtcNow;
        int f0 = _plpFrames;

        // [PLP] await continuation 재개. 코루틴 대기(WaitForSecondsRealtime)는 player loop가
        // 구동하므로, loop가 멈춰 있으면 3초가 3초에 끝나지 않는다.
        await DelayViaCoroutine(3000);
        var t1 = DateTime.UtcNow;
        int f1 = _plpFrames;

        // [PLP] 진짜 네트워크 왕복이 오버레이 구간에 완료될 수 있는지. 완료 콜백은 loop가 전달한다.
        bool grantSuccess = await VerifyReceiptViaWebRequest(data);
        var t2 = DateTime.UtcNow;
        int f2 = _plpFrames;

        LogIap($"[PLP] coroutine(3000ms) actual={(t1 - t0).TotalMilliseconds:F0}ms (frames {f0}->{f1}) | " +
               $"UnityWebRequest actual={(t2 - t1).TotalMilliseconds:F0}ms (frames {f1}->{f2}) | " +
               $"Task.Delay(3000ms)={_taskDelayReport}");
        MarkGrantResponded();
        LogIap($"ProcessProductGrant result: {grantSuccess}");
        UpdateEventLog();
        return grantSuccess;
    }

    /// <summary>
    /// [실험 1] grant 응답 시각을 동기로 못박는다. 이 시점 이후 도는 첫 Update()가
    /// 곧 "오버레이가 실제로 해제되어 player loop가 재개된 순간"이다.
    /// </summary>
    private void MarkGrantResponded()
    {
        _grantRespondedAtUtc = DateTime.UtcNow;
        _grantRespondedAtFrames = _plpFrames;
        _grantResumeReported = false;
    }

    private DateTime _grantRespondedAtUtc;
    private int _grantRespondedAtFrames;
    private bool _grantResumeReported = true;

    /// <summary>
    /// [PLP] Task.Delay(3000) 관찰 결과. round 2에서 이 await가 영영 완료되지 않아
    /// grant 콜백 이후 계측이 전부 막혔다 — WebGL은 스레드가 없어 Task의 타이머가
    /// 발화하지 않는 것으로 보이며, 이번 라운드에서 확증한다.
    /// </summary>
    private string _taskDelayReport = "미완료";

    /// <summary>[PLP] Task.Delay 완료 여부를 크리티컬 패스 밖에서 관찰한다.</summary>
    private async Task ProbeTaskDelayAsync()
    {
        _taskDelayReport = "미완료";
        var startedAt = DateTime.UtcNow;
        int startFrames = _plpFrames;
        try
        {
            await Task.Delay(3000);
            _taskDelayReport = $"완료 {(DateTime.UtcNow - startedAt).TotalMilliseconds:F0}ms " +
                               $"(frames {startFrames}->{_plpFrames})";
        }
        catch (Exception ex)
        {
            _taskDelayReport = $"예외 {ex.GetType().Name}";
        }
    }

    /// <summary>
    /// [PLP] 코루틴 기반 대기. WebGL에서 신뢰할 수 있는 유일한 지연 수단이며
    /// player loop가 구동하므로 loop 정지 시 그만큼 늘어난다(= 재개 지연 측정기).
    /// </summary>
    private Task DelayViaCoroutine(int milliseconds)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(DelayViaCoroutineRoutine(milliseconds, tcs));
        return tcs.Task;
    }

    private System.Collections.IEnumerator DelayViaCoroutineRoutine(int milliseconds, TaskCompletionSource<bool> tcs)
    {
        yield return new WaitForSecondsRealtime(milliseconds / 1000f);
        tcs.SetResult(true);
    }

    private bool _plpArmed;
    private int _plpFrames;
    private float _plpLastFrameRealtime;
    private float _plpMaxFrameGapSec;
    private string _plpMaxGapAt = "";

    private void Update()
    {
        // [실험 1] grant 응답 후 처음 도는 프레임 = 오버레이가 해제되어 loop가 재개된 시점.
        // 이게 이번 실험의 핵심 수치다. 동기 응답이 오버레이를 즉시 닫으면 수백 ms,
        // 응답이 무시되고 네이티브 30초 타임아웃에 걸리면 30000ms+로 찍힌다.
        // 자동으로 찍으므로 버튼을 누르느라 측정이 오염되지 않는다.
        if (!_grantResumeReported)
        {
            _grantResumeReported = true;
            LogIap($"[PLP:resume] grant 응답 → 첫 프레임 = " +
                   $"{(DateTime.UtcNow - _grantRespondedAtUtc).TotalMilliseconds:F0}ms " +
                   $"(frames {_grantRespondedAtFrames}->{_plpFrames})");
            UpdateEventLog();
        }

        if (!_plpArmed) return;
        float now = Time.realtimeSinceStartup;
        if (_plpLastFrameRealtime > 0f)
        {
            float gap = now - _plpLastFrameRealtime;
            if (gap > _plpMaxFrameGapSec)
            {
                _plpMaxFrameGapSec = gap;
                _plpMaxGapAt = DateTime.Now.ToString("HH:mm:ss.fff");
            }
        }
        _plpLastFrameRealtime = now;
        _plpFrames++;
    }

    private void ArmPlayerLoopProbe()
    {
        _plpArmed = true;
        _plpFrames = 0;
        _plpLastFrameRealtime = 0f;
        _plpMaxFrameGapSec = 0f;
        _plpMaxGapAt = "";
        PLP_StartJsProbe();
        LogIap($"[PLP] probe armed (C# Update + JS raf/timer/task) — " +
               $"하드닝 {(AITCore.NestedCallbackTimeoutMs > 0 ? AITCore.NestedCallbackTimeoutMs + "ms ON" : "OFF")}");
    }

    private void ReportPlayerLoopProbe(string phase)
    {
        if (!_plpArmed) return;
        _plpArmed = false;
        string jsReport = PLP_GetJsReport();
        LogIap($"[PLP:{phase}] frames={_plpFrames}, maxFrameGap={_plpMaxFrameGapSec:F2}s@{_plpMaxGapAt}, " +
               $"Task.Delay={_taskDelayReport}");
        LogIap($"[PLP:{phase}] js={jsReport}");
        UpdateEventLog();
    }

    /// <summary>
    /// [PLP] 실제 UnityWebRequest 왕복으로 영수증 검증을 시뮬레이션한다.
    ///
    /// hugh가 막힌 지점이 정확히 이것이다 — "결제 완료 후 개발사 서버에 영수증 검증 API를
    /// 호출하고 그 결과로 지급 여부를 결정"하려면 UnityWebRequest가 오버레이 구간에 완료돼야 한다.
    /// UnityWebRequest의 완료 통지는 player loop가 전달하므로, loop가 멈춰 있으면 요청이
    /// 네트워크적으로 끝나도 C#은 그 사실을 알 수 없다. 이 프로브가 그 지연을 실측한다.
    ///
    /// 엔드포인트는 배포본 자신의 정적 파일(same-origin, 캐시버스터 부착)이다 — 외부 서비스
    /// 가용성·CORS에 의존하지 않으면서 "요청 완료를 loop가 전달하는가"라는 메커니즘만 격리해 잰다.
    /// </summary>
    private Task<bool> VerifyReceiptViaWebRequest(object data)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(VerifyReceiptViaWebRequestRoutine(data, tcs));
        return tcs.Task;
    }

    private System.Collections.IEnumerator VerifyReceiptViaWebRequestRoutine(object data, TaskCompletionSource<bool> tcs)
    {
        string url = $"index.html?receiptCheck={DateTime.UtcNow.Ticks}";
        LogIap($"[PLP] UnityWebRequest 시작: {url}");
        var startedAt = DateTime.UtcNow;
        int startFrames = _plpFrames;

        using (var req = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            req.timeout = 30;
            yield return req.SendWebRequest();

            var elapsed = (DateTime.UtcNow - startedAt).TotalMilliseconds;
#if UNITY_2020_1_OR_NEWER
            bool ok = req.result == UnityEngine.Networking.UnityWebRequest.Result.Success;
#else
            bool ok = !req.isNetworkError && !req.isHttpError;
#endif
            LogIap($"[PLP] UnityWebRequest 완료: ok={ok}, {elapsed:F0}ms, " +
                   $"frames {startFrames}->{_plpFrames}, code={req.responseCode}");

            // 네트워크 실패는 지급 실패로 보지 않는다 — 이 프로브의 관심사는 "완료 통지가
            // 오는가"이지 응답 내용이 아니다. 지급은 true로 진행해 결제 흐름을 끝까지 관찰한다.
            tcs.SetResult(true);
        }
    }
}
