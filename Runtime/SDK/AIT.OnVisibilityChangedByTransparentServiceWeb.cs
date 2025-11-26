// -----------------------------------------------------------------------
// <copyright file="AIT.OnVisibilityChangedByTransparentServiceWeb.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - OnVisibilityChangedByTransparentServiceWeb API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - OnVisibilityChangedByTransparentServiceWeb
    /// </summary>
    public static partial class AIT
    {
        public static System.Action OnVisibilityChangedByTransparentServiceWeb(System.Action eventParams)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return __onVisibilityChangedByTransparentServiceWeb_Internal(eventParams);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] OnVisibilityChangedByTransparentServiceWeb called");
            return () => { };
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern System.Action __onVisibilityChangedByTransparentServiceWeb_Internal(System.Action eventParams);
#endif
    }
}
