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
        /// 결제 완료 시 상품 지급 여부를 <c>bool</c>로 즉시 반환하는 콜백. 여기서 서버 검증을
        /// 하지 말고 <c>true</c>를 반환한 뒤, 검증·지급은 <c>onEvent</c>에서 한다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// 이 콜백은 네이티브 결제 오버레이가 화면을 덮고 player loop가 멈춘 동안 호출되므로,
        /// 반드시 이미 메모리에 있는 값으로 동기 판정해야 한다 (그래서 반환형이 <c>bool</c>이다 —
        /// async로 서버를 기다리면 오버레이가 닫힐 때까지 프레임이 오지 않아 교착이 된다).
        /// 전달되는 정보도 OrderId 하나뿐이라 여기서 새로 검증할 수 있는 것도 없다.
        /// </para>
        /// <para>
        /// nullable 필드지만 사실상 필수다. 지정하지 않으면 브릿지는 플랫폼에 콜백을 넘기는데
        /// C# 쪽에 등록된 핸들러가 없어, 결제가 완료될 때마다 SDK가 자동으로 false를 응답한다
        /// (Console에 "Nested callback 'processProductGrant' is not registered" 에러). 결과적으로
        /// 모든 결제가 지급 실패로 처리되므로, 결제 흐름을 붙일 때 이 필드부터 채워야 한다.
        /// </para>
        /// <example>
        /// <code>
        /// // 1) 콜백은 즉시 승인한다
        /// options.ProcessProductGrant = _ => true;
        ///
        /// // 2) 검증·지급은 onEvent에서. 오버레이가 닫혀 player loop가 살아난 뒤라 await가 안전하다.
        /// //    검증은 개발사 서버가 Toss 주문 상태 조회 API(mTLS)로 OrderId를 확인하는 것이며,
        /// //    클라이언트가 보고한 OrderId를 그대로 신뢰하지 않는다.
        /// onEvent: e => { ShowPurchaseSuccess(); _ = MyServer.VerifyAndDeliver(e.Data.OrderId); }
        ///
        /// // 3) 앱 시작 시 미배달 대사 — 2)가 실행되기 전에 앱이 죽은 경우를 회수한다.
        /// var completed = await AIT.IAPGetCompletedOrRefundedOrders();
        /// </code>
        /// </example>
        /// <para>
        /// 3단계가 빠지면 1단계가 위험해진다. <c>true</c>는 주문을 PURCHASED로 확정하고 되돌리는
        /// API가 없어서, 승인 직후 앱이 종료되면 그 주문은 <c>IAPGetPendingOrders</c>에도 나타나지
        /// 않는다. 회수 창구는 <c>IAPGetCompletedOrRefundedOrders</c>뿐이며, 배달 여부의 기준은
        /// 재설치·기기 변경에도 남는 서버 기록이어야 한다 (PlayerPrefs 등 로컬 기록은 안 된다).
        /// </para>
        /// <para>
        /// <c>false</c>는 정말로 이 상품을 줄 수 없을 때만 반환한다 — true가 아닌 응답은 사용자에게
        /// 환불 안내 페이지를 띄우므로 "확신이 없으니 일단 false"는 매 결제마다 환불 안내가 뜨는
        /// 앱이 된다. 판정 근거는 이미 메모리에 있어야 한다.
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
        /// 결제 완료 시 상품 지급 여부를 <c>bool</c>로 즉시 반환하는 콜백. 여기서 서버 검증을
        /// 하지 말고 <c>true</c>를 반환한 뒤, 검증·지급은 <c>onEvent</c>에서 한다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// 이 콜백은 네이티브 결제 오버레이가 화면을 덮고 player loop가 멈춘 동안 호출되므로,
        /// 반드시 이미 메모리에 있는 값으로 동기 판정해야 한다 (그래서 반환형이 <c>bool</c>이다 —
        /// async로 서버를 기다리면 오버레이가 닫힐 때까지 프레임이 오지 않아 교착이 된다).
        /// 전달되는 정보도 OrderId 하나뿐이라 여기서 새로 검증할 수 있는 것도 없다.
        /// </para>
        /// <para>
        /// nullable 필드지만 사실상 필수다. 지정하지 않으면 브릿지는 플랫폼에 콜백을 넘기는데
        /// C# 쪽에 등록된 핸들러가 없어, 결제가 완료될 때마다 SDK가 자동으로 false를 응답한다
        /// (Console에 "Nested callback 'processProductGrant' is not registered" 에러). 결과적으로
        /// 모든 결제가 지급 실패로 처리되므로, 결제 흐름을 붙일 때 이 필드부터 채워야 한다.
        /// </para>
        /// <example>
        /// <code>
        /// // 1) 콜백은 즉시 승인한다
        /// options.ProcessProductGrant = _ => true;
        ///
        /// // 2) 검증·지급은 onEvent에서. 오버레이가 닫혀 player loop가 살아난 뒤라 await가 안전하다.
        /// //    검증은 개발사 서버가 Toss 주문 상태 조회 API(mTLS)로 OrderId를 확인하는 것이며,
        /// //    클라이언트가 보고한 OrderId를 그대로 신뢰하지 않는다.
        /// onEvent: e => { ShowPurchaseSuccess(); _ = MyServer.VerifyAndDeliver(e.Data.OrderId); }
        ///
        /// // 3) 앱 시작 시 미배달 대사 — 2)가 실행되기 전에 앱이 죽은 경우를 회수한다.
        /// var completed = await AIT.IAPGetCompletedOrRefundedOrders();
        /// </code>
        /// </example>
        /// <para>
        /// 3단계가 빠지면 1단계가 위험해진다. <c>true</c>는 주문을 PURCHASED로 확정하고 되돌리는
        /// API가 없어서, 승인 직후 앱이 종료되면 그 주문은 <c>IAPGetPendingOrders</c>에도 나타나지
        /// 않는다. 회수 창구는 <c>IAPGetCompletedOrRefundedOrders</c>뿐이며, 배달 여부의 기준은
        /// 재설치·기기 변경에도 남는 서버 기록이어야 한다 (PlayerPrefs 등 로컬 기록은 안 된다).
        /// </para>
        /// <para>
        /// <c>false</c>는 정말로 이 상품을 줄 수 없을 때만 반환한다 — true가 아닌 응답은 사용자에게
        /// 환불 안내 페이지를 띄우므로 "확신이 없으니 일단 false"는 매 결제마다 환불 안내가 뜨는
        /// 앱이 된다. 판정 근거는 이미 메모리에 있어야 한다.
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
