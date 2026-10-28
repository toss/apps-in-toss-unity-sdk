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
        public NavigationBarTheme? Theme; // optional
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
        public TossAdsAttachBannerOptionsTheme? Theme; // optional
        [Preserve]
        [JsonProperty("tone")]
        public TossAdsAttachBannerOptionsTone? Tone; // optional
        [Preserve]
        [JsonProperty("variant")]
        public TossAdsAttachBannerOptionsVariant? Variant; // optional
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

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class ShowFullScreenAdEvent
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;
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
    public class LoadFullScreenAdParams
    {
        [Preserve]
        [JsonProperty("options")]
        public LoadFullScreenAdOptions Options;
        [JsonIgnore]
        public System.Action<LoadFullScreenAdEvent> OnEvent;
        [JsonIgnore]
        public System.Action<object> OnError;

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

    [Serializable]
    [Preserve]
    public class ShowFullScreenAdParams
    {
        [Preserve]
        [JsonProperty("options")]
        public ShowFullScreenAdOptions Options;
        [JsonIgnore]
        public System.Action<ShowFullScreenAdEvent> OnEvent;
        [JsonIgnore]
        public System.Action<object> OnError;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class TossAdsAttachBannerResult
    {
        [JsonIgnore]
        public System.Action Destroy;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class TossAdsInitializeOptions
    {
        [Preserve]
        [JsonProperty("callbacks")]
        public TossAdsInitializeOptionsCallbacks Callbacks; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class TossAdsInitializeOptionsCallbacks
    {
        [JsonIgnore]
        public System.Action OnInitialized; // optional
        [JsonIgnore]
        public System.Action<Error> OnInitializationFailed; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
