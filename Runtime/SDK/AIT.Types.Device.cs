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
    public class SetScreenAwakeModeResult
    {
        [Preserve]
        [JsonProperty("enabled")]
        public bool Enabled;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;

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

    [Serializable]
    [Preserve]
    public class SetSecureScreenResult
    {
        [Preserve]
        [JsonProperty("enabled")]
        public bool Enabled;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
