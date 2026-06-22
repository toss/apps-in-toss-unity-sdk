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
    public class Location
    {
        [Preserve]
        [JsonProperty("accessLocation")]
        public string AccessLocation; // optional
        [Preserve]
        [JsonProperty("timestamp")]
        public double Timestamp;
        [Preserve]
        [JsonProperty("coords")]
        public LocationCoords Coords;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        public string error;

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
    public class GetCurrentLocationOptions
    {
        /// <summary>위치 정보를 가져올 정확도 수준이에요.</summary>
        [Preserve]
        [JsonProperty("accuracy")]
        public Accuracy Accuracy;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }

    [Serializable]
    [Preserve]
    public class StartUpdateLocationOptions
    {
        /// <summary>위치 정확도를 설정해요.</summary>
        [Preserve]
        [JsonProperty("accuracy")]
        public Accuracy Accuracy;
        /// <summary>위치 업데이트 주기를 밀리초(ms) 단위로 설정해요.</summary>
        [Preserve]
        [JsonProperty("timeInterval")]
        public double TimeInterval;
        /// <summary>위치 변경 거리를 미터(m) 단위로 설정해요.</summary>
        [Preserve]
        [JsonProperty("distanceInterval")]
        public double DistanceInterval;

        [Preserve]
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> _extensionData;
    }
}
