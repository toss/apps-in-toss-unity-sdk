// -----------------------------------------------------------------------
// <copyright file="AITCore.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Generated from @apps-in-toss/web-framework
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss SDK Core Infrastructure
    /// Callback management and JavaScript bridge
    /// </summary>
    public class AITCore : MonoBehaviour
    {
        private static AITCore _instance;

        /// <summary>
        /// Singleton instance
        /// </summary>
        public static AITCore Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("AITCore");
                    _instance = go.AddComponent<AITCore>();
                    UnityEngine.Object.DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // Callback storage
        private int _callbackIdCounter = 0;
        private Dictionary<string, Delegate> _callbacks = new Dictionary<string, Delegate>();

        /// <summary>
        /// Register a callback and return its ID
        /// </summary>
        public string RegisterCallback<T>(Action<T> callback)
        {
            string id = $"cb_{_callbackIdCounter++}";
            _callbacks[id] = callback;
            return id;
        }

        /// <summary>
        /// Register a void callback (no parameters) and return its ID
        /// </summary>
        public string RegisterCallback(Action callback)
        {
            string id = $"cb_{_callbackIdCounter++}";
            // Wrap Action in Action<object> for storage
            _callbacks[id] = new Action<object>((_) => callback?.Invoke());
            return id;
        }

        /// <summary>
        /// Try to get and remove a callback by ID
        /// </summary>
        public bool TryGetCallback<T>(string callbackId, out Action<T> callback)
        {
            if (_callbacks.TryGetValue(callbackId, out var rawCallback))
            {
                callback = rawCallback as Action<T>;
                _callbacks.Remove(callbackId);
                return callback != null;
            }
            callback = null;
            return false;
        }

        /// <summary>
        /// Remove a callback without invoking it
        /// </summary>
        public void RemoveCallback(string callbackId)
        {
            _callbacks.Remove(callbackId);
        }

        /// <summary>
        /// Called by JavaScript when a callback is triggered
        /// This is the entry point for all JavaScript -> Unity callbacks
        /// </summary>
        public void OnAITCallback(string jsonPayload)
        {
            try
            {
                var callbackData = JsonUtility.FromJson<CallbackData>(jsonPayload);
                RouteCallback(callbackData.CallbackId, callbackData.TypeName, callbackData.Result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AITCore] Failed to process callback: {ex.Message}");
            }
        }

        /// <summary>
        /// Route callback based on type name
        /// </summary>
        private void RouteCallback(string callbackId, string typeName, string resultJson)
        {
            switch (typeName)
            {
                case "CheckoutPaymentResult":
                    if (TryGetCallback<CheckoutPaymentResult>(callbackId, out var callback0) && callback0 != null)
                    {
                        var result0 = JsonUtility.FromJson<CheckoutPaymentResult>(resultJson);
                        callback0(result0);
                    }
                    break;
                case "ContactResult":
                    if (TryGetCallback<ContactResult>(callbackId, out var callback1) && callback1 != null)
                    {
                        var result1 = JsonUtility.FromJson<ContactResult>(resultJson);
                        callback1(result1);
                    }
                    break;
                case "GameCenterGameProfileResponse":
                    if (TryGetCallback<GameCenterGameProfileResponse>(callbackId, out var callback2) && callback2 != null)
                    {
                        var result2 = JsonUtility.FromJson<GameCenterGameProfileResponse>(resultJson);
                        callback2(result2);
                    }
                    break;
                case "GetUserKeyForGameResult":
                    if (TryGetCallback<GetUserKeyForGameResult>(callbackId, out var callback3) && callback3 != null)
                    {
                        var result3 = JsonUtility.FromJson<GetUserKeyForGameResult>(resultJson);
                        callback3(result3);
                    }
                    break;
                case "GrantPromotionRewardForGameResult":
                    if (TryGetCallback<GrantPromotionRewardForGameResult>(callbackId, out var callback4) && callback4 != null)
                    {
                        var result4 = JsonUtility.FromJson<GrantPromotionRewardForGameResult>(resultJson);
                        callback4(result4);
                    }
                    break;
                case "ImageResponse":
                    if (TryGetCallback<ImageResponse>(callbackId, out var callback5) && callback5 != null)
                    {
                        var result5 = JsonUtility.FromJson<ImageResponse>(resultJson);
                        callback5(result5);
                    }
                    break;
                case "ImageResponse[]":
                    if (TryGetCallback<ImageResponse[]>(callbackId, out var callback6) && callback6 != null)
                    {
                        var result6 = JsonUtility.FromJson<ImageResponse[]>(resultJson);
                        callback6(result6);
                    }
                    break;
                case "Location1":
                    if (TryGetCallback<Location1>(callbackId, out var callback7) && callback7 != null)
                    {
                        var result7 = JsonUtility.FromJson<Location1>(resultJson);
                        callback7(result7);
                    }
                    break;
                case "NetworkStatus":
                    if (TryGetCallback<NetworkStatus>(callbackId, out var callback8) && callback8 != null)
                    {
                        var result8 = JsonUtility.FromJson<NetworkStatus>(resultJson);
                        callback8(result8);
                    }
                    break;
                case "SubmitGameCenterLeaderBoardScoreResponse":
                    if (TryGetCallback<SubmitGameCenterLeaderBoardScoreResponse>(callbackId, out var callback9) && callback9 != null)
                    {
                        var result9 = JsonUtility.FromJson<SubmitGameCenterLeaderBoardScoreResponse>(resultJson);
                        callback9(result9);
                    }
                    break;
                case "bool":
                    if (TryGetCallback<bool>(callbackId, out var callback10) && callback10 != null)
                    {
                        var result10 = JsonUtility.FromJson<bool>(resultJson);
                        callback10(result10);
                    }
                    break;
                case "void":
                    if (TryGetCallback<object>(callbackId, out var voidCallback) && voidCallback != null)
                    {
                        voidCallback(null);
                    }
                    break;

                default:
                    Debug.LogWarning($"[AITCore] Unknown callback type: {typeName}");
                    break;
            }
        }
    }

    /// <summary>
    /// Callback payload from JavaScript
    /// </summary>
    [Serializable]
    public class CallbackData
    {
        public string CallbackId = "";
        public string TypeName = "";
        public string Result = "";
    }
}
