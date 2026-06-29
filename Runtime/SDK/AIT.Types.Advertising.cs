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
    [Serializable]
    [Preserve]
    public class GoogleAdMobLoadAppsInTossAdMobArgs
    {
        [JsonIgnore]
        public System.Action<LoadAdMobEvent> OnEvent;
        [JsonIgnore]
        public System.Action<Error> OnError;
        [Preserve]
        [JsonProperty("options")]
        public LoadAdMobOptions Options; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class LoadAdMobEvent
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;
        [Preserve]
        [JsonProperty("data")]
        public AdMobLoadResult Data;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class AdMobLoadResult
    {
        [Preserve]
        [JsonProperty("adGroupId")]
        public string AdGroupId;
        [Preserve]
        [JsonProperty("adUnitId")]
        public string AdUnitId;
        [Preserve]
        [JsonProperty("responseInfo")]
        public ResponseInfo ResponseInfo;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class ResponseInfo
    {
        [Preserve]
        [JsonProperty("adNetworkInfoArray")]
        public AdNetworkResponseInfo[] AdNetworkInfoArray;
        [Preserve]
        [JsonProperty("loadedAdNetworkInfo")]
        public AdNetworkResponseInfo LoadedAdNetworkInfo;
        [Preserve]
        [JsonProperty("responseId")]
        public string ResponseId;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class AdNetworkResponseInfo
    {
        [Preserve]
        [JsonProperty("adSourceId")]
        public string AdSourceId;
        [Preserve]
        [JsonProperty("adSourceName")]
        public string AdSourceName;
        [Preserve]
        [JsonProperty("adSourceInstanceId")]
        public string AdSourceInstanceId;
        [Preserve]
        [JsonProperty("adSourceInstanceName")]
        public string AdSourceInstanceName;
        [Preserve]
        [JsonProperty("adNetworkClassName")]
        public string AdNetworkClassName;

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
        [JsonIgnore]
        public System.Action<ShowAdMobEvent> OnEvent;
        [JsonIgnore]
        public System.Action<Error> OnError;
        [Preserve]
        [JsonProperty("options")]
        public ShowAdMobOptions Options; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class ShowAdMobEvent
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;
        [Preserve]
        [JsonProperty("data")]
        public ShowAdMobEventData Data;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class ShowAdMobEventData
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
    public class AdUserEarnedReward
    {
        [Preserve]
        [JsonProperty("type")]
        public string Type;
        [Preserve]
        [JsonProperty("data")]
        public AdUserEarnedRewardData Data;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class AdUserEarnedRewardData
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
        [Preserve]
        [JsonProperty("onInitialized")]
        public object OnInitialized; // optional
        [Preserve]
        [JsonProperty("onInitializationFailed")]
        public object OnInitializationFailed; // optional

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
        public string error;

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
        public string Theme; // optional
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
    public class TossAdsAttachBannerOptions
    {
        [Preserve]
        [JsonProperty("theme")]
        public string Theme; // optional
        [Preserve]
        [JsonProperty("tone")]
        public string Tone; // optional
        [Preserve]
        [JsonProperty("variant")]
        public string Variant; // optional
        [Preserve]
        [JsonProperty("callbacks")]
        public BannerSlotCallbacks Callbacks; // optional

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
    public class ShowFullScreenAdOptions
    {
        [Preserve]
        [JsonProperty("adGroupId")]
        public string AdGroupId;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    /// <summary>
    /// Data for userEarnedReward event
    /// </summary>
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

    /// <summary>
    /// Full screen ad event (discriminated union)
    /// </summary>
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
}
