// -----------------------------------------------------------------------
// <copyright file="AIT.SaveBase64Data.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - SaveBase64Data API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - SaveBase64Data
    /// </summary>
    public static partial class AIT
    {
        /// <param name="paramsParam">저장할 데이터와 파일 정보를 담은 객체예요.</param>
        public static Task SaveBase64Data(SaveBase64DataParams paramsParam)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<bool>();
            string callbackId = AITCore.Instance.RegisterCallback<object>(_ => tcs.SetResult(true));
            __saveBase64Data_Internal(paramsParam, callbackId, "void");
            return tcs.Task;
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] SaveBase64Data called");
            return Task.CompletedTask;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __saveBase64Data_Internal(SaveBase64DataParams paramsParam, string callbackId, string typeName);
#endif
    }
}
