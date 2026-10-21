// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Visibility.cs" company="Toss">
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
    public class OnVisibilityChangedByTransparentServiceWebEventParams
    {
        [Preserve]
        [JsonProperty("options")]
        public OnVisibilityChangedByTransparentServiceWebEventParamsOptions Options;
        [JsonIgnore]
        public System.Action<bool> OnEvent;
        [JsonIgnore]
        public System.Action<object> OnError;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class OnVisibilityChangedByTransparentServiceWebEventParamsOptions
    {
        [Preserve]
        [JsonProperty("callbackId")]
        public string CallbackId;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
