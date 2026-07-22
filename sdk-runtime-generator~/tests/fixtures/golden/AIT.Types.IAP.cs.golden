// -----------------------------------------------------------------------
// <copyright file="AIT.Types.IAP.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Type Definitions
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Scripting;

namespace AppsInToss
{
    public enum CompletedOrRefundedOrdersResultOrderStatus
    {
        [EnumMember(Value = "COMPLETED")]
        COMPLETED,
        [EnumMember(Value = "REFUNDED")]
        REFUNDED
    }

    [Serializable]
    [Preserve]
    public class IapCreateOneTimePurchaseOrderOptions
    {
        [Preserve]
        [JsonProperty("options")]
        public IapCreateOneTimePurchaseOrderOptionsOptions Options;
        [JsonIgnore]
        public System.Action<SuccessEvent> OnEvent;
        [JsonIgnore]
        public System.Action<object> OnError;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class IapCreateOneTimePurchaseOrderOptionsOptions
    {
        [Preserve]
        [JsonProperty("productId")]
        public string ProductId;
        [Preserve]
        [JsonProperty("sku")]
        public string Sku; // optional
        /// <summary>
        /// 결제 완료 시 상품 지급 여부를 결정하는 콜백. 반드시 동기로 값을 반환해야 한다 —
        /// 이 콜백 안에서 <c>await</c>를 쓰면 결제가 완료되지 않는다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// 이 콜백은 네이티브 결제 오버레이가 화면을 덮고 있는 동안 호출된다. 그 구간에는
        /// 브라우저가 <c>visibilityState = hidden</c> 상태라 requestAnimationFrame이 멈추고,
        /// 그것이 유일한 구동원인 Unity WebGL player loop도 함께 멈춘다. player loop가 멈추면
        /// <c>await</c>의 continuation이 재개되지 않는다.
        /// </para>
        /// <para>
        /// 그래서 서버 검증, <c>UnityWebRequest</c>, 코루틴 대기 등을 await하면 순환 교착이 생긴다.
        /// 오버레이는 이 콜백의 응답을 기다리고, 이 콜백은 오버레이가 닫혀야 오는 프레임을 기다린다.
        /// 실기기 실측에서 115초간 정지한 뒤 "문제가 생겼어요. 환불을 신청해주세요" 페이지가
        /// 노출됐다. (<c>Task.Delay</c>는 WebGL에 타이머 스레드가 없어 아예 완료되지 않으므로
        /// 어떤 경우에도 쓸 수 없다.)
        /// </para>
        /// <para>
        /// 서버 검증이 필요하면 결제를 시작하기 전에 끝내고, 그 결과를 캡처해서 반환한다.
        /// </para>
        /// <example>
        /// <code>
        /// // 1) 오버레이가 뜨기 전 — 여기서는 프레임이 정상적으로 돈다
        /// bool authorized = await MyServer.ReserveEntitlement(sku);
        /// if (!authorized) return;   // 결제 자체를 시작하지 않는 선택지가 생긴다
        ///
        /// // 2) 콜백은 이미 결정된 값을 동기로 반환한다 (await 0회)
        /// options.ProcessProductGrant = _ => Task.FromResult(authorized);
        /// </code>
        /// </example>
        /// <para>
        /// 지급 가능 여부를 미리 알 수 없다면 <c>Task.FromResult(false)</c>를 반환한다. 주문이
        /// PAYMENT_COMPLETED 상태로 남아 <c>IAPGetPendingOrders</c>에 계속 보이므로, 검증을 마친
        /// 뒤 <c>IAPCompleteProductGrant</c>로 지급을 완료할 수 있다. 반대로 <c>true</c>는 주문을
        /// PURCHASED로 확정하며 되돌리는 API가 없다 — 확신이 없으면 <c>false</c>가 안전한 방향이다.
        /// </para>
        /// </remarks>
        [JsonIgnore]
        public System.Func<IapCreateOneTimePurchaseOrderOptionsOptionsProcessProductGrantParam, System.Threading.Tasks.Task<bool>> ProcessProductGrant;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class IapCreateOneTimePurchaseOrderOptionsOptionsProcessProductGrantParam
    {
        [Preserve]
        [JsonProperty("orderId")]
        public string OrderId;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class SuccessEvent
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;
        [Preserve]
        [JsonProperty("data")]
        public IapCreateOneTimePurchaseOrderResult Data;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class IapCreateOneTimePurchaseOrderResult
    {
        [Preserve]
        [JsonProperty("orderId")]
        public string OrderId;
        [Preserve]
        [JsonProperty("displayName")]
        public string DisplayName;
        [Preserve]
        [JsonProperty("displayAmount")]
        public string DisplayAmount;
        [Preserve]
        [JsonProperty("amount")]
        public double Amount;
        [Preserve]
        [JsonProperty("currency")]
        public string Currency;
        [Preserve]
        [JsonProperty("fraction")]
        public double Fraction;
        [Preserve]
        [JsonProperty("miniAppIconUrl")]
        public string MiniAppIconUrl;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class CreateSubscriptionPurchaseOrderOptions
    {
        [Preserve]
        [JsonProperty("options")]
        public CreateSubscriptionPurchaseOrderOptionsOptions Options;
        [JsonIgnore]
        public System.Action<SubscriptionSuccessEvent> OnEvent;
        [JsonIgnore]
        public System.Action<object> OnError;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class CreateSubscriptionPurchaseOrderOptionsOptions
    {
        [Preserve]
        [JsonProperty("sku")]
        public string Sku;
        [Preserve]
        [JsonProperty("offerId")]
        public string OfferId; // optional
        /// <summary>
        /// 결제 완료 시 상품 지급 여부를 결정하는 콜백. 반드시 동기로 값을 반환해야 한다 —
        /// 이 콜백 안에서 <c>await</c>를 쓰면 결제가 완료되지 않는다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// 이 콜백은 네이티브 결제 오버레이가 화면을 덮고 있는 동안 호출된다. 그 구간에는
        /// 브라우저가 <c>visibilityState = hidden</c> 상태라 requestAnimationFrame이 멈추고,
        /// 그것이 유일한 구동원인 Unity WebGL player loop도 함께 멈춘다. player loop가 멈추면
        /// <c>await</c>의 continuation이 재개되지 않는다.
        /// </para>
        /// <para>
        /// 그래서 서버 검증, <c>UnityWebRequest</c>, 코루틴 대기 등을 await하면 순환 교착이 생긴다.
        /// 오버레이는 이 콜백의 응답을 기다리고, 이 콜백은 오버레이가 닫혀야 오는 프레임을 기다린다.
        /// 실기기 실측에서 115초간 정지한 뒤 "문제가 생겼어요. 환불을 신청해주세요" 페이지가
        /// 노출됐다. (<c>Task.Delay</c>는 WebGL에 타이머 스레드가 없어 아예 완료되지 않으므로
        /// 어떤 경우에도 쓸 수 없다.)
        /// </para>
        /// <para>
        /// 서버 검증이 필요하면 결제를 시작하기 전에 끝내고, 그 결과를 캡처해서 반환한다.
        /// </para>
        /// <example>
        /// <code>
        /// // 1) 오버레이가 뜨기 전 — 여기서는 프레임이 정상적으로 돈다
        /// bool authorized = await MyServer.ReserveEntitlement(sku);
        /// if (!authorized) return;   // 결제 자체를 시작하지 않는 선택지가 생긴다
        ///
        /// // 2) 콜백은 이미 결정된 값을 동기로 반환한다 (await 0회)
        /// options.ProcessProductGrant = _ => Task.FromResult(authorized);
        /// </code>
        /// </example>
        /// <para>
        /// 지급 가능 여부를 미리 알 수 없다면 <c>Task.FromResult(false)</c>를 반환한다. 주문이
        /// PAYMENT_COMPLETED 상태로 남아 <c>IAPGetPendingOrders</c>에 계속 보이므로, 검증을 마친
        /// 뒤 <c>IAPCompleteProductGrant</c>로 지급을 완료할 수 있다. 반대로 <c>true</c>는 주문을
        /// PURCHASED로 확정하며 되돌리는 API가 없다 — 확신이 없으면 <c>false</c>가 안전한 방향이다.
        /// </para>
        /// </remarks>
        [JsonIgnore]
        public System.Func<CreateSubscriptionPurchaseOrderOptionsOptionsProcessProductGrantParam, System.Threading.Tasks.Task<bool>> ProcessProductGrant;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class CreateSubscriptionPurchaseOrderOptionsOptionsProcessProductGrantParam
    {
        [Preserve]
        [JsonProperty("orderId")]
        public string OrderId;
        [Preserve]
        [JsonProperty("subscriptionId")]
        public string SubscriptionId; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class SubscriptionSuccessEvent
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;
        [Preserve]
        [JsonProperty("data")]
        public IapCreateOneTimePurchaseOrderResult Data;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class IAPGetProductItemListResult
    {
        [Preserve]
        [JsonProperty("products")]
        public IapProductListItem[] Products;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class IAPGetPendingOrdersResult
    {
        [Preserve]
        [JsonProperty("orders")]
        public IAPGetPendingOrdersResultOrder[] Orders;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class IAPGetPendingOrdersResultOrder
    {
        [Preserve]
        [JsonProperty("orderId")]
        public string OrderId;
        [Preserve]
        [JsonProperty("sku")]
        public string Sku;
        [Preserve]
        [JsonProperty("paymentCompletedDate")]
        public string PaymentCompletedDate;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class CompletedOrRefundedOrdersResult
    {
        [Preserve]
        [JsonProperty("hasNext")]
        public bool HasNext;
        [Preserve]
        [JsonProperty("nextKey")]
        public string NextKey; // optional
        [Preserve]
        [JsonProperty("orders")]
        public CompletedOrRefundedOrdersResultOrder[] Orders;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class CompletedOrRefundedOrdersResultOrder
    {
        [Preserve]
        [JsonProperty("orderId")]
        public string OrderId;
        [Preserve]
        [JsonProperty("sku")]
        public string Sku;
        [Preserve]
        [JsonProperty("status")]
        public CompletedOrRefundedOrdersResultOrderStatus Status;
        [Preserve]
        [JsonProperty("date")]
        public string Date;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class IAPCompleteProductGrantArgs_0
    {
        [Preserve]
        [JsonProperty("params")]
        public IAPCompleteProductGrantArgs_0Params Params;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class IAPCompleteProductGrantArgs_0Params
    {
        [Preserve]
        [JsonProperty("orderId")]
        public string OrderId;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class IAPGetSubscriptionInfoArgs_0
    {
        [Preserve]
        [JsonProperty("params")]
        public IAPGetSubscriptionInfoArgs_0Params Params;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class IAPGetSubscriptionInfoArgs_0Params
    {
        [Preserve]
        [JsonProperty("orderId")]
        public string OrderId;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class IapSubscriptionInfoResponse
    {
        [Preserve]
        [JsonProperty("subscription")]
        public IapSubscriptionInfoResult Subscription;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class IapSubscriptionInfoResult
    {
        [Preserve]
        [JsonProperty("catalogId")]
        public double CatalogId;
        [Preserve]
        [JsonProperty("status")]
        public string Status;
        [Preserve]
        [JsonProperty("expiresAt")]
        public string ExpiresAt;
        [Preserve]
        [JsonProperty("isAutoRenew")]
        public bool IsAutoRenew;
        [Preserve]
        [JsonProperty("gracePeriodExpiresAt")]
        public string GracePeriodExpiresAt;
        [Preserve]
        [JsonProperty("isAccessible")]
        public bool IsAccessible;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
