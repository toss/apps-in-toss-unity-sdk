// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Device.cs" company="Toss">
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
    public class DeviceGetPhotosOptions
    {
        [Preserve]
        [JsonProperty("base64")]
        public bool? Base64; // optional
        [Preserve]
        [JsonProperty("maxCount")]
        public double? MaxCount; // optional
        [Preserve]
        [JsonProperty("maxWidth")]
        public double? MaxWidth; // optional

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

    [Serializable]
    [Preserve]
    public class ContactResult
    {
        [Preserve]
        [JsonProperty("result")]
        public ContactEntity[] Result;
        [Preserve]
        [JsonProperty("nextOffset")]
        public double? NextOffset;
        [Preserve]
        [JsonProperty("done")]
        public bool Done;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class DeviceOpenCameraOptions
    {
        [Preserve]
        [JsonProperty("base64")]
        public bool? Base64; // optional
        [Preserve]
        [JsonProperty("maxWidth")]
        public double? MaxWidth; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class DeviceGetAlbumItemsOptions
    {
        [Preserve]
        [JsonProperty("types")]
        public AlbumItemType[] Types; // optional
        [Preserve]
        [JsonProperty("maxCount")]
        public double? MaxCount; // optional
        [Preserve]
        [JsonProperty("maxWidth")]
        public double? MaxWidth; // optional
        [Preserve]
        [JsonProperty("base64")]
        public bool? Base64; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class ScreenSetAwakeModeResult
    {
        [Preserve]
        [JsonProperty("enabled")]
        public bool Enabled;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class ScreenSetSecureResult
    {
        [Preserve]
        [JsonProperty("enabled")]
        public bool Enabled;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class SetScreenAwakeModeResult
    {
        [Preserve]
        [JsonProperty("enabled")]
        public bool Enabled;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        [Obsolete("오류는 AITException으로 전달돼요. 이 필드는 더 이상 채워지지 않아요.")]
        public string error;


        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class SetSecureScreenResult
    {
        [Preserve]
        [JsonProperty("enabled")]
        public bool Enabled;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        [Obsolete("오류는 AITException으로 전달돼요. 이 필드는 더 이상 채워지지 않아요.")]
        public string error;


        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    public enum SetDeviceOrientationOptionsType
    {
        [EnumMember(Value = "portrait")]
        Portrait,
        [EnumMember(Value = "landscape")]
        Landscape
    }

    [Serializable]
    [Preserve]
    public class SetDeviceOrientationOptions
    {
        [Preserve]
        [JsonProperty("type")]
        public SetDeviceOrientationOptionsType Type;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class SetIosSwipeGestureEnabledOptions
    {
        [Preserve]
        [JsonProperty("isEnabled")]
        public bool IsEnabled;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class SetScreenAwakeModeOptions
    {
        [Preserve]
        [JsonProperty("enabled")]
        public bool Enabled;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class SetSecureScreenOptions
    {
        [Preserve]
        [JsonProperty("enabled")]
        public bool Enabled;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
