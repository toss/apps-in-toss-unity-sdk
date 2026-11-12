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
    public enum ContactsViralEventDataCloseReason
    {
        [EnumMember(Value = "clickBackButton")]
        ClickBackButton,
        [EnumMember(Value = "noReward")]
        NoReward
    }

    public enum ContactsViralSuccessEventDataCloseReason
    {
        [EnumMember(Value = "clickBackButton")]
        ClickBackButton,
        [EnumMember(Value = "noReward")]
        NoReward
    }

    [Serializable]
    [Preserve]
    public class ShareSendMessageMessage
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
    public class ShareMessage
    {
        [Preserve]
        [JsonProperty("message")]
        public string Message;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    /// <summary>
    /// RewardFromContactsViralEventData | ContactsViralSuccessEventData의 필드를 합친 값. 이벤트 종류에 따라 일부 필드만 채워진다.
    /// </summary>
    [Serializable]
    [Preserve]
    public class ContactsViralEventData
    {
        [Preserve]
        [JsonProperty("rewardAmount", NullValueHandling = NullValueHandling.Ignore)]
        [Obsolete("Use RewardAmountTyped")]
        public double RewardAmount;

        /// <summary>와이어에 <see cref="RewardAmount"/> 값이 왔는지 여부. Newtonsoft가 역직렬화할 때 채운다.</summary>
        [JsonIgnore]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public bool RewardAmountSpecified { get; set; }

        /// <summary>
        /// <see cref="RewardAmount"/>를 값이 없으면 null로 돌려준다. 값을 쓰면 <see cref="RewardAmount"/>도 함께 바뀐다.
        /// </summary>
        [JsonIgnore]
        public double? RewardAmountTyped
        {
            get => RewardAmountSpecified || RewardAmount != 0 ? RewardAmount : (double?)null;
            set
            {
                RewardAmount = value ?? 0;
                RewardAmountSpecified = value.HasValue;
            }
        }
        [Preserve]
        [JsonProperty("rewardUnit")]
        public string RewardUnit; // optional
        [Preserve]
        [JsonProperty("closeReason")]
        public ContactsViralEventDataCloseReason? CloseReason; // optional
        [Preserve]
        [JsonProperty("sentRewardAmount")]
        public double? SentRewardAmount; // optional
        [Preserve]
        [JsonProperty("sendableRewardsCount")]
        public double? SendableRewardsCount; // optional
        [Preserve]
        [JsonProperty("sentRewardsCount")]
        public double? SentRewardsCount; // optional

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
}
