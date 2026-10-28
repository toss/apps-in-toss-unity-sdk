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
    public enum EventLogParamsLog_type
    {
        [EnumMember(Value = "impression")]
        Impression,
        [EnumMember(Value = "error")]
        Error,
        [EnumMember(Value = "debug")]
        Debug,
        [EnumMember(Value = "info")]
        Info,
        [EnumMember(Value = "warn")]
        Warn,
        [EnumMember(Value = "event")]
        Event,
        [EnumMember(Value = "screen")]
        Screen,
        [EnumMember(Value = "click")]
        Click,
        [EnumMember(Value = "popup")]
        Popup
    }

    /// <summary>
    /// 와이어 계약(log_type: string)은 넓게 유지하고, 공개 표면은 구 SDK와 동일하게
    /// log_type 유니온으로 좁혀서 제공해요.
    /// </summary>
    [Serializable]
    [Preserve]
    public class EventLogParams
    {
        [Preserve]
        [JsonProperty("log_type")]
        public EventLogParamsLog_type Log_type;
        [Preserve]
        [JsonProperty("log_name")]
        public string Log_name;
        [Preserve]
        [JsonProperty("params")]
        public Dictionary<string, object> Params;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
