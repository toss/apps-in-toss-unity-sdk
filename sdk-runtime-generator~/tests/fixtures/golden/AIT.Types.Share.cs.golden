// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Share.cs" company="Toss">
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
    public partial class ShareSendMessageMessage
    {
        [Preserve]
        public string Message;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class ViralGetCampaignOptions
    {
        [Preserve]
        public string CampaignId;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class ViralGetCampaignResult
    {
        [Preserve]
        public string CampaignId;
        [Preserve]
        public string BenefitName;
        [Preserve]
        public ViralGetCampaignResultDisplay Display;
        [Preserve]
        public bool Active;
        [Preserve]
        public ViralGetCampaignResultCampaignStatus CampaignStatus;
        [Preserve]
        public bool ParticipationAvailable;
        [Preserve]
        public ViralGetCampaignResultParticipationUnavailableReason ParticipationUnavailableReason;
        [Preserve]
        public ViralGetCampaignResultRule[] Rules;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class ViralGetCampaignResultDisplay
    {
        [Preserve]
        public string Title;
        [Preserve]
        public string BenefitText;
        [Preserve]
        public string ConditionText;
        [Preserve]
        public string CtaText;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class ViralGetCampaignResultRule
    {
        [Preserve]
        public string RuleId;
        [Preserve]
        public string RuleKey;
        [Preserve]
        public double Revision;
        [Preserve]
        public string Title;
        [Preserve]
        public double CurrentValue;
        [Preserve]
        public double TargetValue;
        [Preserve]
        public ViralGetCampaignResultRuleStatus Status;
        [Preserve]
        public string AchievedAt;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class ViralShareOptions
    {
        [Preserve]
        public string CampaignId;
        [Preserve]
        public string Title; // optional
        [Preserve]
        public string Description; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class TrackViralParams
    {
        [Preserve]
        public string EventName;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class ShareMessage
    {
        [Preserve]
        public string Message;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    /// <summary>
    /// RewardFromContactsViralEventData | ContactsViralSuccessEventData의 필드를 합친 값. 이벤트 종류에 따라 일부 필드만 채워진다.
    /// </summary>
    [Serializable]
    [Preserve]
    public partial class ContactsViralEventData
    {
        [Preserve]
        [Obsolete("Use RewardAmountTyped")]
        public double RewardAmount;

        /// <summary>와이어에 <see cref="RewardAmount"/> 값이 왔는지 여부. SDK가 JSON을 읽을 때 채운다.</summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public bool RewardAmountSpecified { get; set; }

        /// <summary>
        /// <see cref="RewardAmount"/>를 값이 없으면 null로 돌려준다. 값을 쓰면 <see cref="RewardAmount"/>도 함께 바뀐다.
        /// </summary>
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
        public string RewardUnit; // optional
        [Preserve]
        public ContactsViralEventDataCloseReason? CloseReason; // optional
        [Preserve]
        public double? SentRewardAmount; // optional
        [Preserve]
        public double? SendableRewardsCount; // optional
        [Preserve]
        public double? SentRewardsCount; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class ContactsViralEvent
    {
        [Preserve]
        public string Type;
        [Preserve]
        public ContactsViralEventData Data;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class ContactsViralSuccessEventData
    {
        [Preserve]
        public ContactsViralSuccessEventDataCloseReason CloseReason;
        [Preserve]
        public double? SentRewardAmount; // optional
        [Preserve]
        public double? SendableRewardsCount; // optional
        [Preserve]
        public double SentRewardsCount;
        [Preserve]
        public string RewardUnit; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class ContactsViralParamsOptions
    {
        [Preserve]
        public string ModuleId;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class ContactsViralParams
    {
        [Preserve]
        public ContactsViralParamsOptions Options;
        public System.Action<ContactsViralEvent> OnEvent;
        public System.Action<object> OnError;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }
}
