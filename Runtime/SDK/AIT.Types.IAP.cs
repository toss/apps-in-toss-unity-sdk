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
        /// 결제 완료 시 상품 지급 여부를 <c>bool</c>로 즉시 반환하는 콜백. 결제 오버레이가
        /// player loop를 멈춘 동안 호출되므로 async로 서버를 기다리면 교착이 된다 — 그래서
        /// 반환형이 동기 bool이다. 서버 검증·지급은 오버레이가 닫힌 뒤 <c>onEvent</c>에서 한다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// nullable 필드지만 사실상 필수다. 지정하지 않으면 결제가 완료될 때마다 SDK가 자동으로
        /// false를 응답해("Nested callback 'processProductGrant' is not registered" 에러)
        /// 모든 결제가 지급 실패로 처리된다.
        /// </para>
        /// <example>
        /// <code>
        /// options.ProcessProductGrant = _ => true;                          // 1) 즉시 승인
        /// onEvent: e => { _ = MyServer.VerifyAndDeliver(e.Data.OrderId); }  // 2) 검증·지급
        /// var completed = await AIT.IAPGetCompletedOrRefundedOrders();      // 3) 앱 시작 시 미배달 대사
        /// </code>
        /// </example>
        /// <para>
        /// <c>true</c>는 비가역이다 — 주문이 PURCHASED로 확정되고 되돌리는 API가 없어, 승인 직후
        /// 앱이 죽으면 <c>IAPGetPendingOrders</c>에도 남지 않는다. 회수 창구는
        /// <c>IAPGetCompletedOrRefundedOrders</c>뿐이며, 배달 여부의 기준은 서버 기록이어야
        /// 한다(PlayerPrefs 등 로컬 기록은 재설치·기기 변경에 사라진다).
        /// </para>
        /// <para>
        /// <c>false</c>는 정말로 이 상품을 줄 수 없을 때만 반환한다 — true가 아닌 응답은
        /// 사용자에게 환불 안내 페이지를 띄운다. 자세한 절차는 Docs~/APIUsagePatterns.md의
        /// "인앱결제: 지급 승인과 서버 검증" 절 참고.
        /// </para>
        /// </remarks>
        [JsonIgnore]
        public System.Func<IapCreateOneTimePurchaseOrderOptionsOptionsProcessProductGrantParam, bool> ProcessProductGrant;

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
        /// 결제 완료 시 상품 지급 여부를 <c>bool</c>로 즉시 반환하는 콜백. 결제 오버레이가
        /// player loop를 멈춘 동안 호출되므로 async로 서버를 기다리면 교착이 된다 — 그래서
        /// 반환형이 동기 bool이다. 서버 검증·지급은 오버레이가 닫힌 뒤 <c>onEvent</c>에서 한다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// nullable 필드지만 사실상 필수다. 지정하지 않으면 결제가 완료될 때마다 SDK가 자동으로
        /// false를 응답해("Nested callback 'processProductGrant' is not registered" 에러)
        /// 모든 결제가 지급 실패로 처리된다.
        /// </para>
        /// <example>
        /// <code>
        /// options.ProcessProductGrant = _ => true;                          // 1) 즉시 승인
        /// onEvent: e => { _ = MyServer.VerifyAndDeliver(e.Data.OrderId); }  // 2) 검증·지급
        /// var completed = await AIT.IAPGetCompletedOrRefundedOrders();      // 3) 앱 시작 시 미배달 대사
        /// </code>
        /// </example>
        /// <para>
        /// <c>true</c>는 비가역이다 — 주문이 PURCHASED로 확정되고 되돌리는 API가 없어, 승인 직후
        /// 앱이 죽으면 <c>IAPGetPendingOrders</c>에도 남지 않는다. 회수 창구는
        /// <c>IAPGetCompletedOrRefundedOrders</c>뿐이며, 배달 여부의 기준은 서버 기록이어야
        /// 한다(PlayerPrefs 등 로컬 기록은 재설치·기기 변경에 사라진다).
        /// </para>
        /// <para>
        /// <c>false</c>는 정말로 이 상품을 줄 수 없을 때만 반환한다 — true가 아닌 응답은
        /// 사용자에게 환불 안내 페이지를 띄운다. 자세한 절차는 Docs~/APIUsagePatterns.md의
        /// "인앱결제: 지급 승인과 서버 검증" 절 참고.
        /// </para>
        /// </remarks>
        [JsonIgnore]
        public System.Func<CreateSubscriptionPurchaseOrderOptionsOptionsProcessProductGrantParam, bool> ProcessProductGrant;

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
