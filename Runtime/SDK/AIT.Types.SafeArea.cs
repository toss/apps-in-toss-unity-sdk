// -----------------------------------------------------------------------
// <copyright file="AIT.Types.SafeArea.cs" company="Toss">
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
    public class SafeAreaInsets
    {
        [Preserve]
        [JsonProperty("top")]
        public double Top;
        [Preserve]
        [JsonProperty("bottom")]
        public double Bottom;
        [Preserve]
        [JsonProperty("left")]
        public double Left;
        [Preserve]
        [JsonProperty("right")]
        public double Right;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class SafeAreaInsetsSubscribe__0
    {
        [JsonIgnore]
        public System.Action<SafeAreaInsets> OnEvent;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
