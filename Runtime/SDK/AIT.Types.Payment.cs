// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Payment.cs" company="Toss">
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
    [Serializable]
    [Preserve]
    public class CheckoutPaymentOptions
    {
        /// <summary>결제 토큰이에요.</summary>
        [Preserve]
        [JsonProperty("payToken")]
        public string PayToken;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class CheckoutPaymentResult
    {
        /// <summary>인증이 성공했는지 여부예요.</summary>
        [Preserve]
        [JsonProperty("success")]
        public bool Success;
        /// <summary>인증이 실패했을 경우의 이유예요.</summary>
        [Preserve]
        [JsonProperty("reason")]
        public string Reason; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
