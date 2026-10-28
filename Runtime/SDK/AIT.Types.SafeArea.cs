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
        [JsonProperty("left")]
        public double Left;
        [Preserve]
        [JsonProperty("right")]
        public double Right;
        [Preserve]
        [JsonProperty("bottom")]
        public double Bottom;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
