using System;
using System.Collections.Generic;
using UnityEngine;
using AppsInToss;

/// <summary>
/// IAPv2 (인앱결제 v2) 테스터 컴포넌트
/// 인앱결제 v2 API 워크플로우를 테스트할 수 있는 UI 제공
/// OOMTester 패턴을 따라 InteractiveAPITester에서 분리됨
///
/// ## 정상 플로우 (소모품)
/// 1. GetProductItemList() - 상품 목록 조회
/// 2. CreateOneTimePurchaseOrder() - 구매 주문 생성
///    - processProductGrant 콜백에서 상품 지급 후 true 반환
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

    /// <summary>
    /// 마지막 작업 상태 메시지
    /// </summary>
    public string Status => iapStatus;

    /// <summary>
    /// IAP 테스터 UI를 렌더링합니다.
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
        GUILayout.Label("IAPv2 Tester (인앱결제v2)", groupHeaderStyle);
        GUILayout.Label("인앱결제 v2 API 워크플로우 예제입니다.", labelStyle);

        GUILayout.Space(10);

        // 상태 표시
        if (!string.IsNullOrEmpty(iapStatus))
        {
            GUILayout.Label($"Status: {iapStatus}", labelStyle);
        }

        // 이벤트 로그 표시 (최근 5개)
        if (iapEventLog.Count > 0)
        {
            GUILayout.Label("Event Log:", labelStyle);
            int startIndex = Math.Max(0, iapEventLog.Count - 5);
            for (int i = startIndex; i < iapEventLog.Count; i++)
            {
                GUILayout.Label($"  {iapEventLog[i]}", callbackLabelStyle);
            }
        }

        GUILayout.Space(10);

        // Step 1: 상품 목록 조회
        GUILayout.Label("Step 1: Get Product List", fieldLabelStyle);
        if (GUILayout.Button("IAPGetProductItemList()", buttonStyle, GUILayout.Height(40)))
        {
            ExecuteIAPGetProductList();
        }

        // 상품 목록 표시
        if (iapProducts != null && iapProducts.Products != null && iapProducts.Products.Length > 0)
        {
            GUILayout.Label($"Products ({iapProducts.Products.Length}):", labelStyle);
            int displayCount = Math.Min(3, iapProducts.Products.Length);
            for (int i = 0; i < displayCount; i++)
            {
                var product = iapProducts.Products[i];
                GUILayout.Label($"  - {product.DisplayName} ({product.DisplayAmount})", callbackLabelStyle);
            }
        }

        GUILayout.Space(10);

        // Step 2: 구매 주문 생성
        GUILayout.Label("Step 2: Create Purchase Order", fieldLabelStyle);
        GUILayout.BeginHorizontal();
        GUILayout.Label("SKU:", fieldLabelStyle, GUILayout.Width(50));
        iapSku = GUILayout.TextField(iapSku, textFieldStyle, GUILayout.Height(36), GUILayout.ExpandWidth(true));
        GUILayout.EndHorizontal();

        // 상품 목록에서 빠른 선택 버튼
        if (iapProducts != null && iapProducts.Products != null && iapProducts.Products.Length > 0)
        {
            GUILayout.Label("Quick Select:", labelStyle);
            foreach (var product in iapProducts.Products)
            {
                if (GUILayout.Button($"{product.DisplayName} ({product.Sku})", buttonStyle, GUILayout.Height(32)))
                {
                    iapSku = product.Sku;
                }
            }
        }

        if (GUILayout.Button("IAPCreateOneTimePurchaseOrder(...)", buttonStyle, GUILayout.Height(40)))
        {
            ExecuteIAPCreateOrder();
        }

        GUILayout.Space(10);

        // Step 3: 대기 중인 주문 조회
        GUILayout.Label("Step 3: Get Pending Orders", fieldLabelStyle);
        if (GUILayout.Button("IAPGetPendingOrders()", buttonStyle, GUILayout.Height(40)))
        {
            ExecuteIAPGetPendingOrders();
        }

        // Pending Orders 목록 표시 및 선택
#if AIT_SDK_1_7_OR_LATER
        if (iapPendingOrders != null && iapPendingOrders.Orders != null && iapPendingOrders.Orders.Length > 0)
        {
            GUILayout.Label($"Pending Orders ({iapPendingOrders.Orders.Length}):", labelStyle);
            GUILayout.Label("Select to fill Order ID:", callbackLabelStyle);
            foreach (var order in iapPendingOrders.Orders)
            {
                if (!string.IsNullOrEmpty(order.OrderId))
                {
                    if (GUILayout.Button($"→ {order.OrderId} ({order.Sku})", buttonStyle, GUILayout.Height(32)))
                    {
                        iapOrderId = order.OrderId;
                    }
                }
            }
        }
#else
        if (iapPendingOrders != null && iapPendingOrders.Orders != null && iapPendingOrders.Orders.Length > 0)
        {
            GUILayout.Label($"Pending Orders ({iapPendingOrders.Orders.Length}) - SDK 1.7.0+ required for details", labelStyle);
        }
#endif

        GUILayout.Space(10);

        // Step 4: 완료/환불 주문 조회 (복구 플로우용)
        GUILayout.Label("Step 4: Get Completed/Refunded Orders (복구용)", fieldLabelStyle);
        if (GUILayout.Button("IAPGetCompletedOrRefundedOrders()", buttonStyle, GUILayout.Height(40)))
        {
            ExecuteIAPGetCompletedOrRefundedOrders();
        }

        // Completed/Refunded Orders 목록 표시 및 선택
#if AIT_SDK_1_7_OR_LATER
        if (iapCompletedOrders != null && iapCompletedOrders.Orders != null && iapCompletedOrders.Orders.Length > 0)
        {
            GUILayout.Label($"Completed/Refunded Orders ({iapCompletedOrders.Orders.Length}):", labelStyle);
            GUILayout.Label("Select to fill Order ID:", callbackLabelStyle);
            foreach (var order in iapCompletedOrders.Orders)
            {
                if (!string.IsNullOrEmpty(order.OrderId))
                {
                    string status = order.Status == CompletedOrRefundedOrdersResultOrderStatus.REFUNDED ? "Refunded" : "Completed";
                    if (GUILayout.Button($"→ {order.OrderId} ({order.Sku}, {status})", buttonStyle, GUILayout.Height(32)))
                    {
                        iapOrderId = order.OrderId;
                    }
                }
            }
        }
#else
        if (iapCompletedOrders != null && iapCompletedOrders.Orders != null && iapCompletedOrders.Orders.Length > 0)
        {
            GUILayout.Label($"Completed/Refunded Orders ({iapCompletedOrders.Orders.Length}) - SDK 1.7.0+ required for details", labelStyle);
        }
#endif

        GUILayout.Space(10);

        // Step 5: 상품 지급 완료 처리 (복구 플로우용 - 정상 플로우에서는 processProductGrant 콜백에서 자동 처리됨)
        GUILayout.Label("Step 5: Complete Product Grant (복구용)", fieldLabelStyle);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Order ID:", fieldLabelStyle, GUILayout.Width(80));
        iapOrderId = GUILayout.TextField(iapOrderId, textFieldStyle, GUILayout.Height(36), GUILayout.ExpandWidth(true));
        GUILayout.EndHorizontal();
        if (GUILayout.Button("IAPCompleteProductGrant(...)", buttonStyle, GUILayout.Height(40)))
        {
            ExecuteIAPCompleteGrant();
        }

        GUILayout.EndVertical();
    }

    private async void ExecuteIAPGetProductList()
    {
        iapStatus = "Loading products...";
        iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] IAPGetProductItemList()");

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
    }

    private void ExecuteIAPCreateOrder()
    {
        if (string.IsNullOrEmpty(iapSku))
        {
            iapStatus = "Please enter or select a SKU";
            return;
        }

        iapStatus = "Creating purchase order...";
        iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] IAPCreateOneTimePurchaseOrder(sku: {iapSku})");

        try
        {
#if AIT_SDK_1_7_OR_LATER
            // SDK 1.7.0+ 새 API: 콜백 패턴 (onEvent, options, onError)
            var options = new IapCreateOneTimePurchaseOrderOptionsOptions
            {
                Sku = iapSku,
                ProcessProductGrant = (data) =>
                {
                    iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] ProcessProductGrant called: {data}");
                    Debug.Log($"[IAPv2Tester] ProcessProductGrant called with data: {data}");
                    bool grantSuccess = GrantGameProduct(data);
                    iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] ProcessProductGrant result: {grantSuccess}");
                    return grantSuccess;
                }
            };

            var disposer = AIT.IAPCreateOneTimePurchaseOrder(
                onEvent: (successEvent) =>
                {
                    iapStatus = "Purchase completed";
                    iapOrderId = successEvent.Data?.OrderId ?? "";
                    iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] OnEvent: orderId={successEvent.Data?.OrderId}, amount={successEvent.Data?.DisplayAmount}");
                },
                options: options,
                onError: (error) =>
                {
                    iapStatus = "Purchase failed";
                    iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] OnError: {error.ErrorCode} - {error.Message}");
                }
            );
#else
            // SDK 1.6.x 이전 API: async 패턴
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
    }

#if !AIT_SDK_1_7_OR_LATER
    private async void ExecuteIAPCreateOrderLegacy()
    {
        try
        {
            var options = new IapCreateOneTimePurchaseOrderOptions
            {
                OnEvent = (successEvent) =>
                {
                    iapStatus = "Purchase completed (legacy)";
                    iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] OnEvent (legacy): success");
                },
                OnError = (error) =>
                {
                    iapStatus = "Purchase failed (legacy)";
                    iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] OnError (legacy)");
                }
            };

            await AIT.IAPCreateOneTimePurchaseOrder(options);
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
    }

    private async void ExecuteIAPGetCompletedOrRefundedOrders()
    {
        iapStatus = "Loading completed/refunded orders...";
        iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] IAPGetCompletedOrRefundedOrders()");

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
    }

    private async void ExecuteIAPCompleteGrant()
    {
        if (string.IsNullOrEmpty(iapOrderId))
        {
            iapStatus = "Please enter Order ID";
            return;
        }

        iapStatus = "Processing product grant...";
        iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] IAPCompleteProductGrant(orderId: {iapOrderId})");

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
    }

    /// <summary>
    /// 실제 게임 상품 지급 로직 (데모용)
    /// 실제 게임에서는 여기서 코인, 아이템 등을 플레이어에게 지급합니다.
    /// </summary>
    /// <param name="data">결제 데이터 (orderId 등 포함)</param>
    /// <returns>지급 성공 여부</returns>
    private bool GrantGameProduct(object data)
    {
        Debug.Log($"[IAPv2Tester] Granting product: {data}");

        // 실제 게임에서는 여기서 상품 지급 로직 수행
        // 예:
        // - 코인 추가: PlayerData.AddCoins(100);
        // - 아이템 추가: Inventory.AddItem("premium_sword");
        // - 레벨업: PlayerData.SetLevel(10);

        // 데모에서는 항상 성공 반환
        return true;
    }
}
