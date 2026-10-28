// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Location.cs" company="Toss">
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
    public class GetCurrentLocationOptions
    {
        [Preserve]
        [JsonProperty("accuracy")]
        public Accuracy Accuracy;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class StartUpdateLocationEventParams
    {
        [JsonIgnore]
        public System.Action<Location> OnEvent;
        [JsonIgnore]
        public System.Action<object> OnError;
        [Preserve]
        [JsonProperty("options")]
        public StartUpdateLocationOptions Options;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class StartUpdateLocationOptions
    {
        [Preserve]
        [JsonProperty("accuracy")]
        public Accuracy Accuracy;
        [Preserve]
        [JsonProperty("timeInterval")]
        public double TimeInterval;
        [Preserve]
        [JsonProperty("distanceInterval")]
        public double DistanceInterval;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
