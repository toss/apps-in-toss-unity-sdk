// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Promotion.cs" company="Toss">
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
    public class RewardFromContactsViralEvent
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;
        [Preserve]
        [JsonProperty("data")]
        public RewardFromContactsViralEventData Data;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class ContactsViralSuccessEvent
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;
        [Preserve]
        [JsonProperty("data")]
        public ContactsViralSuccessEventData Data;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class GrantPromotionRewardErrorResponse
    {
        [Preserve]
        [JsonProperty("code")]
        public string Code;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class GrantPromotionRewardErrorResult
    {
        [Preserve]
        [JsonProperty("errorCode")]
        public string ErrorCode;
        [Preserve]
        [JsonProperty("message")]
        public string Message;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class GrantPromotionRewardParams
    {
        [Preserve]
        [JsonProperty("params")]
        public GrantPromotionRewardParamsParams Params;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class GrantPromotionRewardParamsParams
    {
        [Preserve]
        [JsonProperty("promotionCode")]
        public string PromotionCode;
        [Preserve]
        [JsonProperty("amount")]
        public double Amount;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class GrantPromotionRewardResponse
    {
        [Preserve]
        [JsonProperty("key")]
        public string Key;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
