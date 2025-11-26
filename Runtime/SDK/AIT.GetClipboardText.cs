// -----------------------------------------------------------------------
// <copyright file="AIT.GetClipboardText.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - GetClipboardText API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - GetClipboardText
    /// </summary>
    public static partial class AIT
    {
        public static Task<string> GetClipboardText()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            string callbackId = AITCore.Instance.RegisterCallback<string>(result => tcs.SetResult(result));
            __getClipboardText_Internal(callbackId, "string");
            return tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] GetClipboardText called");
            return Task.FromResult("");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __getClipboardText_Internal(string callbackId, string typeName);
#endif
    }
}
