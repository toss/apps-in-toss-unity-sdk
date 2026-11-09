// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Advertising.cs" company="Toss">
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
    public partial class GoogleAdMobLoadAppsInTossAdMobArgs
    {
        [Preserve]
        public LoadAdMobOptions Options; // optional
        public System.Action<LoadAdMobEvent> OnEvent;
        public System.Action<Error> OnError;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class LoadAdMobOptions
    {
        [Preserve]
        public string AdGroupId;
        [Preserve]
        public string Referrer; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class Error
    {
        [Preserve]
        public string Name;
        [Preserve]
        public string Message;
        [Preserve]
        public string Stack; // optional
        [Preserve]
        public object Cause; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class GoogleAdMobShowAppsInTossAdMobArgs
    {
        [Preserve]
        public ShowAdMobOptions Options; // optional
        public System.Action<ShowAdMobEvent> OnEvent;
        public System.Action<Error> OnError;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class ShowAdMobOptions
    {
        [Preserve]
        public string AdGroupId;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class IsAdMobLoadedOptions
    {
        [Preserve]
        public string AdGroupId;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class InitializeOptions
    {
        [Preserve]
        public InitializeOptionsCallbacks Callbacks; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class InitializeOptionsCallbacks
    {
        public System.Action OnInitialized; // optional
        public System.Action<Error> OnInitializationFailed; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class TossAdsAttachOptions
    {
        [Preserve]
        [Obsolete("Use ThemeTyped")]
        public string Theme; // optional

        /// <summary>
        /// <see cref="Theme"/>를 <see cref="NavigationBarTheme"/>로 읽고 쓴다. 정의에 없는 문자열이면 읽을 때 null이고,
        /// 그 문자열은 <see cref="Theme"/>에 그대로 남는다. 값을 쓰면 <see cref="Theme"/>도 함께 바뀐다.
        /// </summary>
        public NavigationBarTheme? ThemeTyped
        {
            get => AITCompat.ParseEnum<NavigationBarTheme>(Theme);
            set => Theme = AITCompat.ToWire(value);
        }
        [Preserve]
        public string Padding; // optional
        [Preserve]
        public BannerSlotCallbacks Callbacks; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class BannerSlotCallbacks
    {
        public System.Action<BannerSlotEventPayload> OnAdRendered; // optional
        public System.Action<BannerSlotEventPayload> OnAdViewable; // optional
        public System.Action<BannerSlotEventPayload> OnAdClicked; // optional
        public System.Action<BannerSlotEventPayload> OnAdImpression; // optional
        public System.Action<BannerSlotErrorPayload> OnAdFailedToRender; // optional
        public System.Action<BannerSlotCallbacksOnNoFillParam> OnNoFill; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class BannerSlotEventPayload
    {
        [Preserve]
        public string SlotId;
        [Preserve]
        public string AdGroupId;
        [Preserve]
        public BannerSlotEventPayloadAdMetadata AdMetadata;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class BannerSlotEventPayloadAdMetadata
    {
        [Preserve]
        public string CreativeId;
        [Preserve]
        public string RequestId;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class BannerSlotErrorPayload
    {
        [Preserve]
        public string SlotId;
        [Preserve]
        public string AdGroupId;
        [Preserve]
        public Dictionary<string, object> AdMetadata;
        [Preserve]
        public BannerSlotErrorPayloadError Error;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class BannerSlotErrorPayloadError
    {
        [Preserve]
        public double Code;
        [Preserve]
        public string Message;
        [Preserve]
        public string Domain; // optional
        [Preserve]
        public string MediationId; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class BannerSlotCallbacksOnNoFillParam
    {
        [Preserve]
        public string SlotId;
        [Preserve]
        public string AdGroupId;
        [Preserve]
        public Dictionary<string, object> AdMetadata;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class TossAdsAttachBannerOptions
    {
        [Preserve]
        [Obsolete("Use ThemeTyped")]
        public string Theme; // optional

        /// <summary>
        /// <see cref="Theme"/>를 <see cref="TossAdsAttachBannerOptionsTheme"/>로 읽고 쓴다. 정의에 없는 문자열이면 읽을 때 null이고,
        /// 그 문자열은 <see cref="Theme"/>에 그대로 남는다. 값을 쓰면 <see cref="Theme"/>도 함께 바뀐다.
        /// </summary>
        public TossAdsAttachBannerOptionsTheme? ThemeTyped
        {
            get => AITCompat.ParseEnum<TossAdsAttachBannerOptionsTheme>(Theme);
            set => Theme = AITCompat.ToWire(value);
        }
        [Preserve]
        [Obsolete("Use ToneTyped")]
        public string Tone; // optional

        /// <summary>
        /// <see cref="Tone"/>를 <see cref="TossAdsAttachBannerOptionsTone"/>로 읽고 쓴다. 정의에 없는 문자열이면 읽을 때 null이고,
        /// 그 문자열은 <see cref="Tone"/>에 그대로 남는다. 값을 쓰면 <see cref="Tone"/>도 함께 바뀐다.
        /// </summary>
        public TossAdsAttachBannerOptionsTone? ToneTyped
        {
            get => AITCompat.ParseEnum<TossAdsAttachBannerOptionsTone>(Tone);
            set => Tone = AITCompat.ToWire(value);
        }
        [Preserve]
        [Obsolete("Use VariantTyped")]
        public string Variant; // optional

        /// <summary>
        /// <see cref="Variant"/>를 <see cref="TossAdsAttachBannerOptionsVariant"/>로 읽고 쓴다. 정의에 없는 문자열이면 읽을 때 null이고,
        /// 그 문자열은 <see cref="Variant"/>에 그대로 남는다. 값을 쓰면 <see cref="Variant"/>도 함께 바뀐다.
        /// </summary>
        public TossAdsAttachBannerOptionsVariant? VariantTyped
        {
            get => AITCompat.ParseEnum<TossAdsAttachBannerOptionsVariant>(Variant);
            set => Variant = AITCompat.ToWire(value);
        }
        [Preserve]
        public BannerSlotCallbacks Callbacks; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class AttachBannerResult
    {
        public System.Action Destroy;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        [Obsolete("오류는 AITException으로 전달돼요. 이 필드는 더 이상 채워지지 않아요.")]
        public string error;


        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class ShowFullScreenAdEvent
    {
        /// <summary>Event type: clicked, dismissed, failedToShow, impression, show, userEarnedReward, requested</summary>
        [Preserve]
        public string Type;
        /// <summary>Event data (only for userEarnedReward)</summary>
        [Preserve]
        public ShowFullScreenAdEventData Data; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class ShowFullScreenAdEventData
    {
        [Preserve]
        public string UnitType;
        [Preserve]
        public double UnitAmount;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class LoadFullScreenAdEvent
    {
        [Preserve]
        public string Type;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class LoadFullScreenAdOptions
    {
        [Preserve]
        public string AdGroupId;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class ShowFullScreenAdOptions
    {
        [Preserve]
        public string AdGroupId;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }
}
