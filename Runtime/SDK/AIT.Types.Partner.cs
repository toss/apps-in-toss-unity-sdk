// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Partner.cs" company="Toss">
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
    public class AddAccessoryButtonOptions
    {
        [Preserve]
        [JsonProperty("id")]
        public string Id;
        [Preserve]
        [JsonProperty("title")]
        public string Title;
        [Preserve]
        [JsonProperty("icon")]
        public AddAccessoryButtonOptionsIcon Icon;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class AddAccessoryButtonOptionsIcon
    {
        [Preserve]
        [JsonProperty("name")]
        public string Name;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
