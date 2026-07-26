// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Events.cs" company="Toss">
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
    public class EventLogParams
    {
        [Preserve]
        [JsonProperty("log_name")]
        public string Log_name;
        [Preserve]
        [JsonProperty("log_type")]
        public string Log_type;
        [Preserve]
        [JsonProperty("params")]
        public Dictionary<string, object> Params;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
