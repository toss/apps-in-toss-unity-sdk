// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Payment.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Type Definitions
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;
using UnityEngine.Scripting;

namespace AppsInToss
{
    [Serializable]
    [Preserve]
    public partial class CheckoutPaymentParams
    {
        [Preserve]
        public string PayToken;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class RequestTossPayPaysBillingParams
    {
        [Preserve]
        public string WrappedToken;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class TossPayCheckoutPaymentOptions
    {
        [Preserve]
        public CheckoutPaymentParams Params;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class TossPayRequestTossPayPaysBillingOptions
    {
        [Preserve]
        public RequestTossPayPaysBillingParams Params;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class CheckoutPaymentOptions
    {
        [Preserve]
        public CheckoutPaymentParams Params;
        /// <summary>결제 토큰이에요.</summary>
        [Obsolete("Use Params.PayToken")]
        public string PayToken
        {
            get => Params?.PayToken;
            set
            {
                if (Params == null) Params = new CheckoutPaymentParams();
                Params.PayToken = value;
            }
        }


        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class RequestTossPayPaysBillingOptions
    {
        [Preserve]
        public RequestTossPayPaysBillingParams Params;
        /// <summary>정기결제 래핑 토큰이에요.</summary>
        [Obsolete("Use Params.WrappedToken")]
        public string WrappedToken
        {
            get => Params?.WrappedToken;
            set
            {
                if (Params == null) Params = new RequestTossPayPaysBillingParams();
                Params.WrappedToken = value;
            }
        }


        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class CheckoutPaymentResult
    {
        /// <summary>인증이 성공했는지 여부예요.</summary>
        [Preserve]
        public bool Success;
        /// <summary>인증이 실패했을 경우의 이유예요.</summary>
        [Preserve]
        public string Reason; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class RequestTossPayPaysBillingResult
    {
        /// <summary>인증이 성공했는지 여부예요.</summary>
        [Preserve]
        public bool Success;
        /// <summary>인증이 실패했을 경우의 이유예요.</summary>
        [Preserve]
        public string Reason; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }
}
