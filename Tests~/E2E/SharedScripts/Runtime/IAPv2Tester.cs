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
///    - processProductGrant 비동기 콜백(Task&lt;bool&gt;)에서 서버 영수증 검증을 await한 뒤 지급, true 반환
///    - SDK가 자동으로 CompleteProductGrant 호출하여 주문 완료 처리
///
/// ## 복구 플로우 (앱 크래시/네트워크 끊김 등으로 processProductGrant 콜백 미호출 시)
/// 1. GetCompletedOrRefundedOrders() - 미처리 완료 주문 조회
/// 2. 각 주문에 대해 상품 지급 수행
/// 3. CompleteProductGrant() - 수동으로 주문 완료 처리
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
        iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] IAPGetProductItemList()");
        UpdateStatus();
        UpdateEventLog();

        try
        {
            iapProducts = await AIT.IAPGetProductItemList();
            int count = iapProducts?.Products?.Length ?? 0;
            iapStatus = $"Found {count} products";
            iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Success: {count} products");
        }
        catch (AITException ex)
        {
            iapStatus = $"Error: {ex.Message}";
            iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Error: {ex.ErrorCode} - {ex.Message}");
        }
        catch (Exception ex)
        {
            iapStatus = $"Error: {ex.Message}";
            iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Exception: {ex.Message}");
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
        iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] IAPCreateOneTimePurchaseOrder(sku: {iapSku})");
        ArmPlayerLoopProbe();
        UpdateStatus();
        UpdateEventLog();

        try
        {
#if AIT_SDK_1_7_OR_LATER
            var options = new IapCreateOneTimePurchaseOrderOptionsOptions
            {
                Sku = iapSku,
                // 비동기 콜백: 개발사 서버의 영수증 검증 API 호출을 기다린 뒤 지급 여부를 결정한다.
                // SDK는 이 Task<bool>이 완료될 때까지 주문 완료 처리를 보류한다 (true → 자동 지급 완료).
                ProcessProductGrant = async (data) =>
                {
                    // [PLP] 진입 시각/프레임은 동기 기록 — loop가 멈춰 있어도 유실 없음
                    var t0 = DateTime.UtcNow;
                    int f0 = _plpFrames;
                    iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] ProcessProductGrant called: {data}");
                    Debug.Log($"[IAPv2Tester] ProcessProductGrant called with data: {data}");

                    // [PLP] continuation 재개 프로브 1: player loop가 멈춰 있으면 3초 Delay가 3초에 안 끝난다
                    await Task.Delay(3000);
                    var t1 = DateTime.UtcNow;
                    int f1 = _plpFrames;

                    // [PLP] continuation 재개 프로브 2: 코루틴(WaitForSecondsRealtime 0.2s) 경로
                    bool grantSuccess = await GrantGameProduct(data);
                    var t2 = DateTime.UtcNow;
                    int f2 = _plpFrames;

                    iapEventLog.Add(
                        $"[{DateTime.Now:HH:mm:ss}] [PLP] delay(3000ms): actual={(t1 - t0).TotalMilliseconds:F0}ms (frames {f0}->{f1}), " +
                        $"coroutine(200ms): actual={(t2 - t1).TotalMilliseconds:F0}ms (frames {f1}->{f2})");
                    iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] ProcessProductGrant result: {grantSuccess}");
                    UpdateEventLog();
                    return grantSuccess;
                }
            };

            _purchaseDisposer?.Invoke();
            _purchaseDisposer = AIT.IAPCreateOneTimePurchaseOrder(
                onEvent: (successEvent) =>
                {
                    iapStatus = "Purchase completed";
                    iapOrderId = successEvent.Data?.OrderId ?? "";
                    if (_orderIdInput != null) _orderIdInput.text = iapOrderId;
                    iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] OnEvent: orderId={successEvent.Data?.OrderId}, amount={successEvent.Data?.DisplayAmount}");
                    ReportPlayerLoopProbe("onEvent");
                    UpdateStatus();
                    UpdateEventLog();
                },
                options: options,
                onError: (error) =>
                {
                    iapStatus = "Purchase failed";
                    iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] OnError: {error.ErrorCode} - {error.Message}");
                    ReportPlayerLoopProbe("onError");
                    UpdateStatus();
                    UpdateEventLog();
                }
            );
#else
            ExecuteIAPCreateOrderLegacy();
#endif
            iapStatus = "Purchase order created";
            iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Order created successfully");
        }
        catch (AITException ex)
        {
            iapStatus = $"Error: {ex.Message}";
            iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Error: {ex.ErrorCode} - {ex.Message}");
        }
        catch (Exception ex)
        {
            iapStatus = $"Error: {ex.Message}";
            iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Exception: {ex.Message}");
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
                    iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] OnEvent (legacy): success");
                    UpdateStatus();
                    UpdateEventLog();
                },
                options: options,
                onError: (error) =>
                {
                    iapStatus = "Purchase failed (legacy)";
                    iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] OnError (legacy): {error?.Message}");
                    UpdateStatus();
                    UpdateEventLog();
                }
            );
        }
        catch (Exception ex)
        {
            iapStatus = $"Error: {ex.Message}";
            iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Exception (legacy): {ex.Message}");
        }
    }
#endif

    private async void ExecuteIAPGetPendingOrders()
    {
        iapStatus = "Loading pending orders...";
        iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] IAPGetPendingOrders()");
        UpdateStatus();
        UpdateEventLog();

        try
        {
            iapPendingOrders = await AIT.IAPGetPendingOrders();
            int count = iapPendingOrders?.Orders?.Length ?? 0;
            iapStatus = $"Found {count} pending orders";
            iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Success: {count} orders");
        }
        catch (AITException ex)
        {
            iapPendingOrders = null;
            iapStatus = $"Error: {ex.Message}";
            iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Error: {ex.ErrorCode} - {ex.Message}");
        }
        catch (Exception ex)
        {
            iapPendingOrders = null;
            iapStatus = $"Error: {ex.Message}";
            iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Exception: {ex.Message}");
        }

        UpdateStatus();
        UpdateEventLog();
        UpdatePendingOrders();
    }

    private async void ExecuteIAPGetCompletedOrRefundedOrders()
    {
        iapStatus = "Loading completed/refunded orders...";
        iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] IAPGetCompletedOrRefundedOrders()");
        UpdateStatus();
        UpdateEventLog();

        try
        {
            iapCompletedOrders = await AIT.IAPGetCompletedOrRefundedOrders();
            int count = iapCompletedOrders?.Orders?.Length ?? 0;
            iapStatus = $"Found {count} completed/refunded orders";
            iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Success: {count} orders, HasNext={iapCompletedOrders?.HasNext}");
        }
        catch (AITException ex)
        {
            iapCompletedOrders = null;
            iapStatus = $"Error: {ex.Message}";
            iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Error: {ex.ErrorCode} - {ex.Message}");
        }
        catch (Exception ex)
        {
            iapCompletedOrders = null;
            iapStatus = $"Error: {ex.Message}";
            iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Exception: {ex.Message}");
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
        iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] IAPCompleteProductGrant(orderId: {iapOrderId})");
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
            iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Result: {success}");
        }
        catch (AITException ex)
        {
            iapStatus = $"Error: {ex.Message}";
            iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Error: {ex.ErrorCode} - {ex.Message}");
        }
        catch (Exception ex)
        {
            iapStatus = $"Error: {ex.Message}";
            iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Exception: {ex.Message}");
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
        Debug.Log($"[IAPv2Tester] Validating receipt on server: {data}");
        // 동기 스텁으로는 표현 불가했던 비동기 서버 왕복 경로
        yield return new WaitForSecondsRealtime(0.2f);
        Debug.Log($"[IAPv2Tester] Receipt validated, granting product: {data}");
        tcs.SetResult(true);
    }

    // =====================================================
    // Player loop freeze 진단 프로브 (techchat 4377 검증용)
    //
    // 결제 native 오버레이 구간에서 실측으로 판별한다:
    //  (1) player loop(Update) 정지 여부      — C# 프레임 하트비트의 최대 gap
    //  (2) 웹뷰 JS 이벤트 루프 생존 여부       — jslib setInterval(250ms) 하트비트
    //  (3) C# await 재개(continuation) 지연    — ProcessProductGrant 내 Task.Delay/코루틴 실측
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
    private static string PLP_GetJsReport() { return "{\"count\":0,\"maxGapMs\":0,\"spanMs\":0}"; }
#endif

    private bool _plpArmed;
    private int _plpFrames;
    private float _plpLastFrameRealtime;
    private float _plpMaxFrameGapSec;
    private string _plpMaxGapAt = "";

    private void Update()
    {
        if (!_plpArmed) return;
        float now = Time.realtimeSinceStartup;
        if (_plpLastFrameRealtime > 0f)
        {
            float gap = now - _plpLastFrameRealtime;
            if (gap > _plpMaxFrameGapSec)
            {
                _plpMaxFrameGapSec = gap;
                _plpMaxGapAt = DateTime.Now.ToString("HH:mm:ss");
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
        iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] [PLP] probe armed (C# Update + JS setInterval 250ms)");
    }

    private void ReportPlayerLoopProbe(string phase)
    {
        if (!_plpArmed) return;
        _plpArmed = false;
        string jsReport = PLP_GetJsReport();
        iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] [PLP:{phase}] frames={_plpFrames}, maxFrameGap={_plpMaxFrameGapSec:F2}s@{_plpMaxGapAt}");
        iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] [PLP:{phase}] js={jsReport}");
        Debug.Log($"[PLP:{phase}] frames={_plpFrames} maxFrameGap={_plpMaxFrameGapSec:F2}s@{_plpMaxGapAt} js={jsReport}");
        UpdateEventLog();
    }
}
