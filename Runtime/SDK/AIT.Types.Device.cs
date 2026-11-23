// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Device.cs" company="Toss">
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
    public partial class DeviceGetPhotosOptions
    {
        [Preserve]
        public bool? Base64; // optional
        [Preserve]
        public double? MaxCount; // optional
        [Preserve]
        public double? MaxWidth; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class FetchContactsOptions
    {
        [Preserve]
        public double Size;
        [Preserve]
        public double Offset;
        [Preserve]
        public FetchContactsOptionsQuery Query; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class FetchContactsOptionsQuery
    {
        [Preserve]
        public string Contains; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class ContactResult
    {
        [Preserve]
        public ContactEntity[] Result;
        [Preserve]
        public double? NextOffset;
        [Preserve]
        public bool Done;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class DeviceOpenCameraOptions
    {
        [Preserve]
        public bool? Base64; // optional
        [Preserve]
        public double? MaxWidth; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class DeviceGetAlbumItemsOptions
    {
        [Preserve]
        public AlbumItemType[] Types; // optional
        [Preserve]
        public double? MaxCount; // optional
        [Preserve]
        public double? MaxWidth; // optional
        [Preserve]
        public bool? Base64; // optional

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class ScreenSetAwakeModeResult
    {
        [Preserve]
        public bool Enabled;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class ScreenSetSecureResult
    {
        [Preserve]
        public bool Enabled;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class SetScreenAwakeModeResult
    {
        [Preserve]
        public bool Enabled;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        [Obsolete("오류는 AITException으로 전달돼요. 이 필드는 더 이상 채워지지 않아요.")]
        public string error;


        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class SetSecureScreenResult
    {
        [Preserve]
        public bool Enabled;
        /// <summary>에러 발생 시 에러 메시지 (플랫폼 미지원 등)</summary>
        [Obsolete("오류는 AITException으로 전달돼요. 이 필드는 더 이상 채워지지 않아요.")]
        public string error;


        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    public enum SetDeviceOrientationOptionsType
    {
        [EnumMember(Value = "portrait")]
        Portrait,
        [EnumMember(Value = "landscape")]
        Landscape
    }

    [Serializable]
    [Preserve]
    public partial class SetDeviceOrientationOptions
    {
        [Preserve]
        public SetDeviceOrientationOptionsType Type;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class SetIosSwipeGestureEnabledOptions
    {
        [Preserve]
        public bool IsEnabled;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class SetScreenAwakeModeOptions
    {
        [Preserve]
        public bool Enabled;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class SetSecureScreenOptions
    {
        [Preserve]
        public bool Enabled;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }
}
