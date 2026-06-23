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
        [JsonIgnore]
        public System.Func<object, object> ProcessProductGrant;

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
        [JsonIgnore]
        public System.Func<object, object> ProcessProductGrant;

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
