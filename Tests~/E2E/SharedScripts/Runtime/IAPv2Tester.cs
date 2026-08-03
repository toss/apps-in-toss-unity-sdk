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
/// ## 정상 플로우 (소모품)
/// 1. GetProductItemList() - 상품 목록 조회
/// 2. CreateOneTimePurchaseOrder() - 구매 주문 생성
///    - processProductGrant 콜백은 동기 bool — 즉시 true를 반환한다 (아래 ExecuteIAPCreateOrder 주석 참조)
///    - SDK가 자동으로 CompleteProductGrant 호출하여 주문 완료 처리
/// 3. 서버 영수증 검증과 실제 상품 지급은 onEvent에서 — 오버레이가 닫힌 뒤라 await가 안전하다
///
/// ## 복구 플로우 (지급 전에 앱이 종료되는 등으로 3단계가 실행되지 못한 경우)
/// 1. GetCompletedOrRefundedOrders() - 승인은 됐지만 배달 기록이 없는 주문 조회
/// 2. 각 주문에 대해 상품 지급 수행
/// 3. CompleteProductGrant() - 수동으로 주문 완료 처리
///
/// 배달 여부의 기준은 재설치·기기 변경에도 남는 서버 기록이어야 한다 (PlayerPrefs 등 로컬 기록은 안 된다).
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
        UpdateStatus();
        UpdateEventLog();

        try
        {
#if AIT_SDK_1_7_OR_LATER
            var options = new IapCreateOneTimePurchaseOrderOptionsOptions
            {
                Sku = iapSku,
                // [1단계] 콜백은 즉시 승인한다. 오버레이가 player loop를 멈춘 동안 호출되는
                // 자리라 반환형이 동기 bool이다(async는 컴파일 불가). 여기서 검증할 것도
                // 없다(정보는 OrderId뿐) — 검증과 지급은 아래 onEvent(2단계)에서 한다.
                ProcessProductGrant = _ =>
                {
                    LogIap("ProcessProductGrant: 즉시 true 반환 (동기)");
                    UpdateEventLog();
                    return true;
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

                    // [2단계] 검증과 지급은 여기서. 오버레이가 닫혀 player loop가 살아난 뒤라
                    // 서버 왕복을 기다려도 안전하다 — OrderId와 살아있는 프레임을 동시에 갖는
                    // 첫 순간이다. (실측: 오버레이가 닫히고 71ms 뒤 도착)
                    GrantGameProduct(iapOrderId);

                    UpdateStatus();
                    UpdateEventLog();
                },
                options: options,
                onError: (error) =>
                {
                    iapStatus = "Purchase failed";
                    LogIap($"OnError: {error.ErrorCode} - {error.Message}");
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
    /// [2단계] 서버 영수증 검증 + 실제 상품 지급 (데모용).
    ///
    /// onEvent에서 fire-and-forget으로 호출한다. 이 자리는 오버레이가 이미 닫혀 player loop가
    /// 돌고 있으므로 await가 정상 동작한다.
    ///
    /// 실제 구현에서는 개발사 서버에 OrderId를 보내고, 서버가 Toss의 주문 상태 조회 API
    /// (mTLS, 서버 간 통신)로 결제를 확인한 뒤 지급을 기록한다. 클라이언트가 보고한 OrderId를
    /// 그대로 신뢰해서는 안 된다.
    /// </summary>
    private async void GrantGameProduct(string orderId)
    {
        try
        {
            bool granted = await VerifyReceiptOnServerAsync(orderId);
            LogIap($"[2단계] 지급 {(granted ? "완료" : "보류")}: {orderId}");
        }
        catch (Exception ex)
        {
            // 여기서 실패해도 결제는 이미 승인(PURCHASED)됐고 되돌릴 수 없다.
            // 회수는 IAPGetCompletedOrRefundedOrders() 버튼의 복구 플로우로 한다.
            LogIap($"[2단계] 지급 실패 — 복구 플로우 필요: {ex.Message}");
        }
        UpdateEventLog();
    }

    /// <summary>
    /// 서버 영수증 검증 왕복 시뮬레이션 (WebGL-safe: WaitForSecondsRealtime 코루틴).
    /// WebGL에는 타이머 스레드가 없어 <c>Task.Delay</c>가 완료되지 않으므로, player loop가
    /// 구동하는 코루틴으로 대기하고 <see cref="TaskCompletionSource{TResult}"/>로 Task 경계를
    /// 만든다 — SDK 관용.
    /// </summary>
    private Task<bool> VerifyReceiptOnServerAsync(string orderId)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(VerifyReceiptRoutine(orderId, tcs));
        return tcs.Task;
    }

    private System.Collections.IEnumerator VerifyReceiptRoutine(string orderId, TaskCompletionSource<bool> tcs)
    {
        LogIap($"[2단계] 서버 영수증 검증 중: {orderId}");
        yield return new WaitForSecondsRealtime(0.2f);
        LogIap($"[2단계] 검증 통과, 상품 지급: {orderId}");
        tcs.SetResult(true);
    }
}
