// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Navigation.cs" company="Toss">
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
    public class AddNavigationBarAccessoryButtonOptions
    {
        [Preserve]
        [JsonProperty("accessibilityLabel")]
        public string AccessibilityLabel;
        [Preserve]
        [JsonProperty("icon")]
        public AddNavigationBarAccessoryButtonOptionsIcon Icon;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class AddNavigationBarAccessoryButtonOptionsIcon
    {
        [Preserve]
        [JsonProperty("name")]
        public string Name;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
