using System;
using System.Collections.Generic;
using UnityEngine;
using AppsInToss;

/// <summary>
/// IAP (In-App Purchase) 테스터 컴포넌트
/// 인앱결제 v2 API 워크플로우를 테스트할 수 있는 UI 제공
/// OOMTester 패턴을 따라 InteractiveAPITester에서 분리됨
/// </summary>
public class IAPTester : MonoBehaviour
{
    // IAP 테스트 상태
    private string iapSku = "";
    private string iapOrderId = "";
    private string iapStatus = "";
    private IAPGetProductItemListResult iapProducts = null;
    private IAPGetPendingOrdersResult iapPendingOrders = null;
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
        GUILayout.Label("IAP Example", groupHeaderStyle);
        GUILayout.Label("인앱결제 v2 API 워크플로우를 테스트합니다.", labelStyle);

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

        GUILayout.Space(10);

        // Step 4: 완료/환불 주문 조회
        GUILayout.Label("Step 4: Get Completed/Refunded Orders", fieldLabelStyle);
        if (GUILayout.Button("IAPGetCompletedOrRefundedOrders()", buttonStyle, GUILayout.Height(40)))
        {
            ExecuteIAPGetCompletedOrRefundedOrders();
        }

        GUILayout.Space(10);

        // Step 5: 상품 지급 완료 처리
        GUILayout.Label("Step 5: Complete Product Grant", fieldLabelStyle);
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
            var options = new IapCreateOneTimePurchaseOrderOptions
            {
                // Options에 SKU와 processProductGrant 콜백 전달
                Options = new IapCreateOneTimePurchaseOrderOptionsOptions
                {
                    Sku = iapSku,
                    // processProductGrant 콜백 - 상품 지급 로직 수행 후 결과 반환
                    // JS에서 이 함수가 호출되면 C#에서 실행되고 결과가 JS로 반환됨
                    ProcessProductGrant = (data) =>
                    {
                        iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] ProcessProductGrant called: {data}");
                        Debug.Log($"[IAPTester] ProcessProductGrant called with data: {data}");

                        // 여기서 실제 상품 지급 로직 수행
                        // 예: 코인 추가, 아이템 지급 등
                        bool grantSuccess = GrantGameProduct(data);

                        iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] ProcessProductGrant result: {grantSuccess}");
                        return grantSuccess;
                    }
                },
                OnEvent = (successEvent) =>
                {
                    iapStatus = "Purchase completed";
                    iapOrderId = successEvent.Data?.OrderId ?? "";
                    iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] OnEvent: orderId={successEvent.Data?.OrderId}, amount={successEvent.Data?.DisplayAmount}");
                },
                OnError = (error) =>
                {
                    iapStatus = "Purchase failed";
                    iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] OnError: {error}");
                }
            };

            // IAPCreateOneTimePurchaseOrder는 동기 함수로 cleanup Action을 반환
            var disposer = AIT.IAPCreateOneTimePurchaseOrder(options);
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
            var result = await AIT.IAPGetCompletedOrRefundedOrders();
            int count = result?.Orders?.Length ?? 0;
            iapStatus = $"Found {count} completed/refunded orders";
            iapEventLog.Add($"[{DateTime.Now:HH:mm:ss}] Success: {count} orders, HasNext={result?.HasNext}");
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
        Debug.Log($"[IAPTester] Granting product: {data}");

        // 실제 게임에서는 여기서 상품 지급 로직 수행
        // 예:
        // - 코인 추가: PlayerData.AddCoins(100);
        // - 아이템 추가: Inventory.AddItem("premium_sword");
        // - 레벨업: PlayerData.SetLevel(10);

        // 데모에서는 항상 성공 반환
        return true;
    }
}
