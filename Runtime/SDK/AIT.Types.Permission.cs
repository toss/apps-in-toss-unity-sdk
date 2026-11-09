// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Permission.cs" company="Toss">
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
    public partial class GetPermissionPermission
    {
        [Preserve]
        public PermissionName Name;
        [Preserve]
        public PermissionAccess Access;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class OpenPermissionDialogPermission
    {
        [Preserve]
        public PermissionName Name;
        [Preserve]
        public PermissionAccess Access;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }

    [Serializable]
    [Preserve]
    public partial class RequestPermissionPermission
    {
        [Preserve]
        public PermissionName Name;
        [Preserve]
        public PermissionAccess Access;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }
}
