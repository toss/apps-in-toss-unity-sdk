// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Media.cs" company="Toss">
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
    public class FetchAlbumPhotosOptions
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
    public class OpenCameraOptions
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
    public class FetchAlbumItemsOptions
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
    public class OpenPDFViewerParams
    {
        [Preserve]
        [JsonProperty("data")]
        public string Data;
        [Preserve]
        [JsonProperty("filename")]
        public string Filename; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class SaveBase64DataParams
    {
        [Preserve]
        [JsonProperty("data")]
        public string Data;
        [Preserve]
        [JsonProperty("fileName")]
        public string FileName;
        [Preserve]
        [JsonProperty("mimeType")]
        public string MimeType;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
