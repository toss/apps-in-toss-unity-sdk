// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Location.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Type Definitions
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;
using UnityEngine.Scripting;

namespace AppsInToss
{
    [Serializable]
    [Preserve]
    public partial class GetCurrentLocationOptions
    {
        /// <summary>위치 정보를 가져올 정확도 수준이에요.</summary>
        [Preserve]
        public Accuracy Accuracy;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class StartUpdateLocationEventParams
    {
        public System.Action<Location> OnEvent;
        public System.Action<object> OnError;
        [Preserve]
        public StartUpdateLocationOptions Options;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class StartUpdateLocationOptions
    {
        /// <summary>위치 정확도를 설정해요.</summary>
        [Preserve]
        public Accuracy Accuracy;
        /// <summary>위치 업데이트 주기를 밀리초(ms) 단위로 설정해요.</summary>
        [Preserve]
        public double TimeInterval;
        /// <summary>위치 변경 거리를 미터(m) 단위로 설정해요.</summary>
        [Preserve]
        public double DistanceInterval;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }
}
