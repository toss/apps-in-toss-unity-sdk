// -----------------------------------------------------------------------
// <copyright file="AIT.SetScreenAwakeMode.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - SetScreenAwakeMode API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - SetScreenAwakeMode
    /// </summary>
    public static partial class AIT
    {
        /// <param name="options">화면 항상 켜짐 모드의 설정 값이에요.</param>
        /// <returns>현재 화면 항상 켜짐 모드의 설정 상태를 반환해요.</returns>
        public static Task<SetScreenAwakeModeResult> SetScreenAwakeMode(SetScreenAwakeModeOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<SetScreenAwakeModeResult>();
            string callbackId = AITCore.Instance.RegisterCallback<SetScreenAwakeModeResult>(result => tcs.SetResult(result));
            __setScreenAwakeMode_Internal(options, callbackId, "SetScreenAwakeModeResult");
            return tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SetScreenAwakeMode called");
            return Task.FromResult(default(SetScreenAwakeModeResult));
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __setScreenAwakeMode_Internal(SetScreenAwakeModeOptions options, string callbackId, string typeName);
#endif
    }
}
