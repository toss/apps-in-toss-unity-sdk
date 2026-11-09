// -----------------------------------------------------------------------
// <copyright file="AIT.Types.Migration.cs" company="Toss">
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
    public partial class MigrationGetOriginStorageResult
    {
        [Preserve]
        public OriginStorageDump Previous;
        [Preserve]
        public OriginStorageDump Current;

        [Preserve]
        public System.Collections.Generic.IDictionary<string, JsonValue> _extensionData;
    }
}
