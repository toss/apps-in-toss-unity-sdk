// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Notification.cs" company="Toss">
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
    public partial class RequestNotificationAgreementOptions
    {
        [Preserve]
        public RequestNotificationAgreementOptionsOptions Options;
        public System.Action<RequestNotificationAgreementOptionsOnEventParam> OnEvent;
        public System.Action<object> OnError;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class RequestNotificationAgreementOptionsOnEventParam
    {
        [Preserve]
        public RequestNotificationAgreementOptionsOnEventParamType Type;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    public enum RequestNotificationAgreementOptionsOnEventParamType
    {
        [EnumMember(Value = "newAgreement")]
        NewAgreement,
        [EnumMember(Value = "alreadyAgreed")]
        AlreadyAgreed,
        [EnumMember(Value = "agreementRejected")]
        AgreementRejected
    }

    [Serializable]
    [Preserve]
    public partial class RequestNotificationAgreementOptionsOptions
    {
        [Preserve]
        public string TemplateCode;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }
}
