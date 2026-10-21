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
    /// <summary>
    /// 앨범 사진을 조회할 때 사용하는 옵션 타입이에요.
    /// </summary>
    [Serializable]
    [Preserve]
    public class FetchAlbumPhotosOptions
    {
        /// <summary>가져올 사진의 최대 개수를 설정해요. 숫자를 입력하고 기본값은 10이에요.</summary>
        [Preserve]
        [JsonProperty("maxCount")]
        public double? MaxCount; // optional
        /// <summary>사진의 최대 폭을 제한해요. 단위는 픽셀이고 기본값은 1024이에요.</summary>
        [Preserve]
        [JsonProperty("maxWidth")]
        public double? MaxWidth; // optional
        /// <summary>이미지를 base64 형식으로 반환할지 설정해요. 기본값은 false예요.</summary>
        [Preserve]
        [JsonProperty("base64")]
        public bool? Base64; // optional

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class OpenCameraOptions
    {
        /// <summary>이미지를 Base64 형식으로 반환할지 여부를 나타내는 불리언 값이에요. 기본값: false.</summary>
        [Preserve]
        [JsonProperty("base64")]
        public bool? Base64; // optional
        /// <summary>이미지의 최대 너비를 나타내는 숫자 값이에요. 기본값: 1024.</summary>
        [Preserve]
        [JsonProperty("maxWidth")]
        public double? MaxWidth; // optional

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
