// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Advertising.cs" company="Toss">
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
    public enum TossAdsAttachBannerOptionsTheme
    {
        [EnumMember(Value = "light")]
        Light,
        [EnumMember(Value = "dark")]
        Dark,
        [EnumMember(Value = "auto")]
        Auto
    }

    public enum TossAdsAttachBannerOptionsTone
    {
        [EnumMember(Value = "blackAndWhite")]
        BlackAndWhite,
        [EnumMember(Value = "grey")]
        Grey
    }

    public enum TossAdsAttachBannerOptionsVariant
    {
        [EnumMember(Value = "card")]
        Card,
        [EnumMember(Value = "expanded")]
        Expanded
    }

    [Serializable]
    [Preserve]
    public class GoogleAdMobLoadAppsInTossAdMobArgs
    {
        [Preserve]
        [JsonProperty("options")]
        public LoadAdMobOptions Options; // optional
        [JsonIgnore]
        public System.Action<LoadAdMobEvent> OnEvent;
        [JsonIgnore]
        public System.Action<Error> OnError;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class LoadAdMobOptions
    {
        [Preserve]
        [JsonProperty("adGroupId")]
        public string AdGroupId;
        [Preserve]
        [JsonProperty("referrer")]
        public string Referrer; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class Error
    {
        [Preserve]
        [JsonProperty("name")]
        public string Name;
        [Preserve]
        [JsonProperty("message")]
        public string Message;
        [Preserve]
        [JsonProperty("stack")]
        public string Stack; // optional
        [Preserve]
        [JsonProperty("cause")]
        public object Cause; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class GoogleAdMobShowAppsInTossAdMobArgs
    {
        [Preserve]
        [JsonProperty("options")]
        public ShowAdMobOptions Options; // optional
        [JsonIgnore]
        public System.Action<ShowAdMobEvent> OnEvent;
        [JsonIgnore]
        public System.Action<Error> OnError;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class ShowAdMobOptions
    {
        [Preserve]
        [JsonProperty("adGroupId")]
        public string AdGroupId;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class IsAdMobLoadedOptions
    {
        [Preserve]
        [JsonProperty("adGroupId")]
        public string AdGroupId;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class InitializeOptions
    {
        [Preserve]
        [JsonProperty("callbacks")]
        public InitializeOptionsCallbacks Callbacks; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class InitializeOptionsCallbacks
    {
        [JsonIgnore]
        public System.Action OnInitialized; // optional
        [JsonIgnore]
        public System.Action<Error> OnInitializationFailed; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class TossAdsAttachOptions
    {
        [Preserve]
        [JsonProperty("theme")]
        [Obsolete("Use ThemeTyped")]
        public string Theme; // optional

        /// <summary>
        /// <see cref="Theme"/>를 <see cref="NavigationBarTheme"/>로 읽고 쓴다. 정의에 없는 문자열이면 읽을 때 null이고,
        /// 그 문자열은 <see cref="Theme"/>에 그대로 남는다. 값을 쓰면 <see cref="Theme"/>도 함께 바뀐다.
        /// </summary>
        [JsonIgnore]
        public NavigationBarTheme? ThemeTyped
        {
            get => AITCompat.ParseEnum<NavigationBarTheme>(Theme);
            set => Theme = AITCompat.ToWire(value);
        }
        [Preserve]
        [JsonProperty("padding")]
        public string Padding; // optional
        [Preserve]
        [JsonProperty("callbacks")]
        public BannerSlotCallbacks Callbacks; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class BannerSlotCallbacks
    {
        [JsonIgnore]
        public System.Action<BannerSlotEventPayload> OnAdRendered; // optional
        [JsonIgnore]
        public System.Action<BannerSlotEventPayload> OnAdViewable; // optional
        [JsonIgnore]
        public System.Action<BannerSlotEventPayload> OnAdClicked; // optional
        [JsonIgnore]
        public System.Action<BannerSlotEventPayload> OnAdImpression; // optional
        [JsonIgnore]
        public System.Action<BannerSlotErrorPayload> OnAdFailedToRender; // optional
        [JsonIgnore]
        public System.Action<BannerSlotCallbacksOnNoFillParam> OnNoFill; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class BannerSlotEventPayload
    {
        [Preserve]
        [JsonProperty("slotId")]
        public string SlotId;
        [Preserve]
        [JsonProperty("adGroupId")]
        public string AdGroupId;
        [Preserve]
        [JsonProperty("adMetadata")]
        public BannerSlotEventPayloadAdMetadata AdMetadata;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class BannerSlotEventPayloadAdMetadata
    {
        [Preserve]
        [JsonProperty("creativeId")]
        public string CreativeId;
        [Preserve]
        [JsonProperty("requestId")]
        public string RequestId;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class BannerSlotErrorPayload
    {
        [Preserve]
        [JsonProperty("slotId")]
        public string SlotId;
        [Preserve]
        [JsonProperty("adGroupId")]
        public string AdGroupId;
        [Preserve]
        [JsonProperty("adMetadata")]
        public Dictionary<string, object> AdMetadata;
        [Preserve]
        [JsonProperty("error")]
        public BannerSlotErrorPayloadError Error;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class BannerSlotErrorPayloadError
    {
        [Preserve]
        [JsonProperty("code")]
        public double Code;
        [Preserve]
        [JsonProperty("message")]
        public string Message;
        [Preserve]
        [JsonProperty("domain")]
        public string Domain; // optional
        [Preserve]
        [JsonProperty("mediationId")]
        public string MediationId; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class BannerSlotCallbacksOnNoFillParam
    {
        [Preserve]
        [JsonProperty("slotId")]
        public string SlotId;
        [Preserve]
        [JsonProperty("adGroupId")]
        public string AdGroupId;
        [Preserve]
        [JsonProperty("adMetadata")]
        public Dictionary<string, object> AdMetadata;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class TossAdsAttachBannerOptions
    {
        [Preserve]
        [JsonProperty("theme")]
        [Obsolete("Use ThemeTyped")]
        public string Theme; // optional

        /// <summary>
        /// <see cref="Theme"/>를 <see cref="TossAdsAttachBannerOptionsTheme"/>로 읽고 쓴다. 정의에 없는 문자열이면 읽을 때 null이고,
        /// 그 문자열은 <see cref="Theme"/>에 그대로 남는다. 값을 쓰면 <see cref="Theme"/>도 함께 바뀐다.
        /// </summary>
        [JsonIgnore]
        public TossAdsAttachBannerOptionsTheme? ThemeTyped
        {
            get => AITCompat.ParseEnum<TossAdsAttachBannerOptionsTheme>(Theme);
            set => Theme = AITCompat.ToWire(value);
        }
        [Preserve]
        [JsonProperty("tone")]
        [Obsolete("Use ToneTyped")]
        public string Tone; // optional

        /// <summary>
        /// <see cref="Tone"/>를 <see cref="TossAdsAttachBannerOptionsTone"/>로 읽고 쓴다. 정의에 없는 문자열이면 읽을 때 null이고,
        /// 그 문자열은 <see cref="Tone"/>에 그대로 남는다. 값을 쓰면 <see cref="Tone"/>도 함께 바뀐다.
        /// </summary>
        [JsonIgnore]
        public TossAdsAttachBannerOptionsTone? ToneTyped
        {
            get => AITCompat.ParseEnum<TossAdsAttachBannerOptionsTone>(Tone);
            set => Tone = AITCompat.ToWire(value);
        }
        [Preserve]
        [JsonProperty("variant")]
        [Obsolete("Use VariantTyped")]
        public string Variant; // optional

        /// <summary>
        /// <see cref="Variant"/>를 <see cref="TossAdsAttachBannerOptionsVariant"/>로 읽고 쓴다. 정의에 없는 문자열이면 읽을 때 null이고,
        /// 그 문자열은 <see cref="Variant"/>에 그대로 남는다. 값을 쓰면 <see cref="Variant"/>도 함께 바뀐다.
        /// </summary>
        [JsonIgnore]
        public TossAdsAttachBannerOptionsVariant? VariantTyped
        {
            get => AITCompat.ParseEnum<TossAdsAttachBannerOptionsVariant>(Variant);
            set => Variant = AITCompat.ToWire(value);
        }
        [Preserve]
        [JsonProperty("callbacks")]
        public BannerSlotCallbacks Callbacks; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class AttachBannerResult
    {
        [JsonIgnore]
        public System.Action Destroy;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        [Obsolete("오류는 AITException으로 전달돼요. 이 필드는 더 이상 채워지지 않아요.")]
        public string error;


        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class ShowFullScreenAdEvent
    {
        /// <summary>Event type: clicked, dismissed, failedToShow, impression, show, userEarnedReward, requested</summary>
        [Preserve]
        [JsonProperty("type")]
        public string Type;
        /// <summary>Event data (only for userEarnedReward)</summary>
        [Preserve]
        [JsonProperty("data")]
        public ShowFullScreenAdEventData Data; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class ShowFullScreenAdEventData
    {
        [Preserve]
        [JsonProperty("unitType")]
        public string UnitType;
        [Preserve]
        [JsonProperty("unitAmount")]
        public double UnitAmount;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class LoadFullScreenAdEvent
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class LoadFullScreenAdOptions
    {
        [Preserve]
        [JsonProperty("adGroupId")]
        public string AdGroupId;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class ShowFullScreenAdOptions
    {
        [Preserve]
        [JsonProperty("adGroupId")]
        public string AdGroupId;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
