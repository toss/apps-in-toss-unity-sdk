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

    public enum ViralGetCampaignResultCampaignStatus
    {
        [EnumMember(Value = "WAITING")]
        WAITING,
        [EnumMember(Value = "IN_PROGRESS")]
        IN_PROGRESS,
        [EnumMember(Value = "ENDED")]
        ENDED
    }

    public enum ViralGetCampaignResultParticipationUnavailableReason
    {
        [EnumMember(Value = "ENDED")]
        ENDED,
        [EnumMember(Value = "INACTIVE")]
        INACTIVE,
        [EnumMember(Value = "NOT_STARTED")]
        NOT_STARTED,
        [EnumMember(Value = "ALL_RULES_ACHIEVED")]
        ALL_RULES_ACHIEVED
    }

    public enum ViralGetCampaignResultRuleStatus
    {
        [EnumMember(Value = "EXPIRED")]
        EXPIRED,
        [EnumMember(Value = "IN_PROGRESS")]
        IN_PROGRESS,
        [EnumMember(Value = "ACHIEVED")]
        ACHIEVED
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
    public class ViralGetCampaignOptions
    {
        [Preserve]
        [JsonProperty("campaignId")]
        public string CampaignId;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class ViralGetCampaignResult
    {
        [Preserve]
        [JsonProperty("campaignId")]
        public string CampaignId;
        [Preserve]
        [JsonProperty("benefitName")]
        public string BenefitName;
        [Preserve]
        [JsonProperty("display")]
        public ViralGetCampaignResultDisplay Display;
        [Preserve]
        [JsonProperty("active")]
        public bool Active;
        [Preserve]
        [JsonProperty("campaignStatus")]
        public ViralGetCampaignResultCampaignStatus CampaignStatus;
        [Preserve]
        [JsonProperty("participationAvailable")]
        public bool ParticipationAvailable;
        [Preserve]
        [JsonProperty("participationUnavailableReason")]
        public ViralGetCampaignResultParticipationUnavailableReason ParticipationUnavailableReason;
        [Preserve]
        [JsonProperty("rules")]
        public ViralGetCampaignResultRule[] Rules;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class ViralGetCampaignResultDisplay
    {
        [Preserve]
        [JsonProperty("title")]
        public string Title;
        [Preserve]
        [JsonProperty("benefitText")]
        public string BenefitText;
        [Preserve]
        [JsonProperty("conditionText")]
        public string ConditionText;
        [Preserve]
        [JsonProperty("ctaText")]
        public string CtaText;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class ViralGetCampaignResultRule
    {
        [Preserve]
        [JsonProperty("ruleId")]
        public string RuleId;
        [Preserve]
        [JsonProperty("ruleKey")]
        public string RuleKey;
        [Preserve]
        [JsonProperty("revision")]
        public double Revision;
        [Preserve]
        [JsonProperty("title")]
        public string Title;
        [Preserve]
        [JsonProperty("currentValue")]
        public double CurrentValue;
        [Preserve]
        [JsonProperty("targetValue")]
        public double TargetValue;
        [Preserve]
        [JsonProperty("status")]
        public ViralGetCampaignResultRuleStatus Status;
        [Preserve]
        [JsonProperty("achievedAt")]
        public string AchievedAt;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class ViralShareOptions
    {
        [Preserve]
        [JsonProperty("campaignId")]
        public string CampaignId;
        [Preserve]
        [JsonProperty("title")]
        public string Title; // optional
        [Preserve]
        [JsonProperty("description")]
        public string Description; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class TrackViralParams
    {
        [Preserve]
        [JsonProperty("eventName")]
        public string EventName;

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
        [JsonProperty("rewardAmount")]
        public double? RewardAmount; // optional
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
