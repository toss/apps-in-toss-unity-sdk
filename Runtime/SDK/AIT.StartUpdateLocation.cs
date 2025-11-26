// -----------------------------------------------------------------------
// <copyright file="AIT.StartUpdateLocation.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - StartUpdateLocation API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - StartUpdateLocation
    /// </summary>
    public static partial class AIT
    {
        public static System.Action StartUpdateLocation(StartUpdateLocationEventParams eventParams)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return __startUpdateLocation_Internal(eventParams);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] StartUpdateLocation called");
            return () => { };
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern System.Action __startUpdateLocation_Internal(StartUpdateLocationEventParams eventParams);
#endif
    }
}
