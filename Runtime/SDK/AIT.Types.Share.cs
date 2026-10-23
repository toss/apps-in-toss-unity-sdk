// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Share.cs" company="Toss">
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
    public enum ContactsViralSuccessEventDataCloseReason
    {
        [EnumMember(Value = "clickBackButton")]
        ClickBackButton,
        [EnumMember(Value = "noReward")]
        NoReward
    }

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
    public class RewardFromContactsViralEventData
    {
        [Preserve]
        [JsonProperty("rewardAmount")]
        public double RewardAmount;
        [Preserve]
        [JsonProperty("rewardUnit")]
        public string RewardUnit;

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
    public class ContactsViralSuccessEventData
    {
        [Preserve]
        [JsonProperty("closeReason")]
        public ContactsViralSuccessEventDataCloseReason CloseReason;
        [Preserve]
        [JsonProperty("sentRewardAmount")]
        public double? SentRewardAmount; // optional
        [Preserve]
        [JsonProperty("sendableRewardsCount")]
        public double? SendableRewardsCount; // optional
        [Preserve]
        [JsonProperty("sentRewardsCount")]
        public double SentRewardsCount;
        [Preserve]
        [JsonProperty("rewardUnit")]
        public string RewardUnit; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class ShareMessage
    {
        [Preserve]
        [JsonProperty("message")]
        public string Message;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class ContactsViralEvent
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;
        [Preserve]
        [JsonProperty("data")]
        public ContactsViralEventData Data;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class ContactsViralEventData
    {
        [Preserve]
        [JsonProperty("rewardAmount")]
        public double RewardAmount;
        [Preserve]
        [JsonProperty("rewardUnit")]
        public string RewardUnit;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class ContactsViralParams
    {
        [Preserve]
        [JsonProperty("options")]
        public ContactsViralParamsOptions Options;
        [JsonIgnore]
        public System.Action<ContactsViralEvent> OnEvent;
        [JsonIgnore]
        public System.Action<object> OnError;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class ContactsViralParamsOptions
    {
        [Preserve]
        [JsonProperty("moduleId")]
        public string ModuleId;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class FetchContactsOptions
    {
        [Preserve]
        [JsonProperty("size")]
        public double Size;
        [Preserve]
        [JsonProperty("offset")]
        public double Offset;
        [Preserve]
        [JsonProperty("query")]
        public FetchContactsOptionsQuery Query; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class FetchContactsOptionsQuery
    {
        [Preserve]
        [JsonProperty("contains")]
        public string Contains; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
