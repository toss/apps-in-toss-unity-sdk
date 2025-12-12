// -----------------------------------------------------------------------
// <copyright file="AITCore.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Generated from @apps-in-toss/web-framework
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using UnityEngine.Scripting;

namespace AppsInToss
{
    /// <summary>
    /// Shared JSON serialization settings for SDK.
    /// Uses StringEnumConverter to serialize enums as strings (matching JavaScript API expectations).
    /// Note: [JsonConverter(typeof(StringEnumConverter))] on enums causes IL2CPP infinite loop,
    /// so we configure it globally here instead.
    /// </summary>
    public static class AITJsonSettings
    {
        private static JsonSerializerSettings _settings;

        public static JsonSerializerSettings Default
        {
            get
            {
                if (_settings == null)
                {
                    _settings = new JsonSerializerSettings();
                    _settings.Converters.Add(new StringEnumConverter());
                }
                return _settings;
            }
        }

        /// <summary>
        /// Serialize object to JSON with enum-as-string support.
        /// </summary>
        public static string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj, Default);
        }
    }

    /// <summary>
    /// Attribute to mark API methods with their category for grouping in UI.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class APICategoryAttribute : Attribute
    {
        /// <summary>
        /// The category name (e.g., "Authentication", "Payment", "Location")
        /// </summary>
        public string Category { get; }

        public APICategoryAttribute(string category)
        {
            Category = category;
        }
    }

    /// <summary>
    /// Exception thrown when an Apps in Toss API call fails.
    /// Use try-catch to handle API errors in a C#-idiomatic way.
    /// </summary>
    public class AITException : Exception
    {
        /// <summary>The API name that failed</summary>
        public string APIName { get; }

        /// <summary>Error code for categorization (if available from platform)</summary>
        public string ErrorCode { get; }

        /// <summary>Whether this error is due to platform unavailability (browser environment)</summary>
        public bool IsPlatformUnavailable { get; }

        public AITException(string message) : base(message)
        {
            APIName = "";
            ErrorCode = "";
            IsPlatformUnavailable = CheckPlatformUnavailable(message);
        }

        public AITException(string apiName, string message) : base(message)
        {
            APIName = apiName;
            ErrorCode = "";
            IsPlatformUnavailable = CheckPlatformUnavailable(message);
        }

        public AITException(string apiName, string message, string errorCode) : base(message)
        {
            APIName = apiName;
            ErrorCode = errorCode;
            IsPlatformUnavailable = CheckPlatformUnavailable(message);
        }

        public AITException(string message, Exception innerException) : base(message, innerException)
        {
            APIName = "";
            ErrorCode = "";
            IsPlatformUnavailable = CheckPlatformUnavailable(message);
        }

        private static bool CheckPlatformUnavailable(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            return message.Contains("__GRANITE_NATIVE_EMITTER") ||
                   message.Contains("ReactNativeWebView") ||
                   message.Contains("is not a constant handler");
        }
    }

    /// <summary>
    /// API response from JavaScript bridge.
    /// Uses explicit success/data/error format to avoid IL2CPP stripping issues.
    /// </summary>
    [Serializable]
    [Preserve]
    public class APIResponse
    {
        [Preserve]
        public bool success;
        [Preserve]
        public string data = "";
        [Preserve]
        public string error = "";
    }

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

        // Callback storage: success callbacks and error callbacks
        private int _callbackIdCounter = 0;
        private Dictionary<string, Delegate> _callbacks = new Dictionary<string, Delegate>();
        private Dictionary<string, Action<AITException>> _errorCallbacks = new Dictionary<string, Action<AITException>>();

        /// <summary>
        /// Register success and error callbacks, return the callback ID.
        /// Used by async API methods with TaskCompletionSource.
        /// </summary>
        public string RegisterCallback<T>(Action<T> onSuccess, Action<AITException> onError)
        {
            string id = $"cb_{_callbackIdCounter++}";
            _callbacks[id] = onSuccess;
            _errorCallbacks[id] = onError;
            return id;
        }

        /// <summary>
        /// Register a void callback (no parameters) and return its ID
        /// </summary>
        public string RegisterVoidCallback(Action onSuccess, Action<AITException> onError)
        {
            string id = $"cb_{_callbackIdCounter++}";
            // Wrap Action in Action<object> for storage
            _callbacks[id] = new Action<object>((_) => onSuccess?.Invoke());
            _errorCallbacks[id] = onError;
            return id;
        }

        /// <summary>
        /// Try to get and remove a success callback by ID
        /// </summary>
        public bool TryGetCallback<T>(string callbackId, out Action<T> callback)
        {
            if (_callbacks.TryGetValue(callbackId, out var rawCallback))
            {
                callback = rawCallback as Action<T>;
                _callbacks.Remove(callbackId);
                _errorCallbacks.Remove(callbackId);
                return callback != null;
            }
            callback = null;
            return false;
        }

        /// <summary>
        /// Try to get and remove an error callback by ID
        /// </summary>
        public bool TryGetErrorCallback(string callbackId, out Action<AITException> callback)
        {
            if (_errorCallbacks.TryGetValue(callbackId, out callback))
            {
                _callbacks.Remove(callbackId);
                _errorCallbacks.Remove(callbackId);
                return callback != null;
            }
            callback = null;
            return false;
        }

        /// <summary>
        /// Remove callbacks without invoking them
        /// </summary>
        public void RemoveCallback(string callbackId)
        {
            _callbacks.Remove(callbackId);
            _errorCallbacks.Remove(callbackId);
        }

        // ===================================================================
        // Event Subscription System (persistent callbacks)
        // ===================================================================

        private int _subscriptionIdCounter = 0;
        private Dictionary<string, Delegate> _subscriptionCallbacks = new Dictionary<string, Delegate>();
        private Dictionary<string, Action<AITException>> _subscriptionErrorCallbacks = new Dictionary<string, Action<AITException>>();

        /// <summary>
        /// Register a subscription callback (not removed after invocation) - for events with data
        /// </summary>
        public string RegisterSubscriptionCallback<T>(Action<T> onEvent, Action<AITException> onError)
        {
            string id = $"sub_{_subscriptionIdCounter++}";
            _subscriptionCallbacks[id] = onEvent;
            if (onError != null) _subscriptionErrorCallbacks[id] = onError;
            return id;
        }

        /// <summary>
        /// Register a void subscription callback - for events without data (backEvent, entryMessageExited)
        /// </summary>
        public string RegisterVoidSubscriptionCallback(Action onEvent, Action<AITException> onError)
        {
            string id = $"sub_{_subscriptionIdCounter++}";
            _subscriptionCallbacks[id] = onEvent;
            if (onError != null) _subscriptionErrorCallbacks[id] = onError;
            return id;
        }

        /// <summary>
        /// Unsubscribe from an event
        /// </summary>
        public void Unsubscribe(string subscriptionId)
        {
            _subscriptionCallbacks.Remove(subscriptionId);
            _subscriptionErrorCallbacks.Remove(subscriptionId);

#if UNITY_WEBGL && !UNITY_EDITOR
            __AITUnsubscribe_Internal(subscriptionId);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void __AITUnsubscribe_Internal(string subscriptionId);
#endif

        /// <summary>
        /// Called by JavaScript when an event is triggered (persistent callback)
        /// </summary>
        public void OnAITEventCallback(string jsonPayload)
        {
            Debug.Log($"[AITCore] OnAITEventCallback received: {jsonPayload}");
            try
            {
                var callbackData = JsonConvert.DeserializeObject<CallbackData>(jsonPayload);
                Debug.Log($"[AITCore] Routing event callback: id={callbackData.CallbackId}, type={callbackData.TypeName}");
                RouteSubscriptionCallback(callbackData.CallbackId, callbackData.TypeName, callbackData.Result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AITCore] Failed to process event callback: {ex.Message}");
            }
        }

        /// <summary>
        /// Route subscription callback (does NOT remove after invocation)
        /// </summary>
        private void RouteSubscriptionCallback(string callbackId, string typeName, string resultJson)
        {
            if (!_subscriptionCallbacks.TryGetValue(callbackId, out var rawCallback))
            {
                Debug.LogWarning($"[AITCore] Unknown subscription: {callbackId}");
                return;
            }

            var apiResponse = JsonConvert.DeserializeObject<APIResponse>(resultJson);

            if (apiResponse.success)
            {
                switch (typeName)
                {
                    case "TdsNavigationAccessoryEventData":
                        var tdsData = JsonConvert.DeserializeObject<TdsNavigationAccessoryEventData>(apiResponse.data);
                        (rawCallback as Action<TdsNavigationAccessoryEventData>)?.Invoke(tdsData);
                        break;
                    case "void":
                        // Void event - call Action directly
                        (rawCallback as Action)?.Invoke();
                        break;
                    default:
                        Debug.LogWarning($"[AITCore] Unknown subscription type: {typeName}");
                        break;
                }
            }
            else
            {
                if (_subscriptionErrorCallbacks.TryGetValue(callbackId, out var errorCallback))
                {
                    errorCallback?.Invoke(new AITException(typeName, apiResponse.error));
                }
            }
        }

        /// <summary>
        /// Called by JavaScript when a callback is triggered
        /// This is the entry point for all JavaScript -> Unity callbacks
        /// </summary>
        public void OnAITCallback(string jsonPayload)
        {
            Debug.Log($"[AITCore] OnAITCallback received: {jsonPayload}");
            try
            {
                var callbackData = JsonConvert.DeserializeObject<CallbackData>(jsonPayload);
                Debug.Log($"[AITCore] Routing callback: id={callbackData.CallbackId}, type={callbackData.TypeName}");
                RouteCallback(callbackData.CallbackId, callbackData.TypeName, callbackData.Result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AITCore] Failed to process callback: {ex.Message}");
            }
        }

        /// <summary>
        /// Route callback based on type name.
        /// On success: calls callback with data.
        /// On error: calls error callback with AITException (causes Task to fault).
        /// </summary>
        private void RouteCallback(string callbackId, string typeName, string resultJson)
        {
            // Parse APIResponse first (success/data/error format)
            var apiResponse = JsonConvert.DeserializeObject<APIResponse>(resultJson);

            switch (typeName)
            {
                case "AppLoginResult":
                    if (apiResponse.success)
                    {
                        if (TryGetCallback<AppLoginResult>(callbackId, out var callback0) && callback0 != null)
                        {
                            var data0 = JsonConvert.DeserializeObject<AppLoginResult>(apiResponse.data);
                            callback0(data0);
                        }
                    }
                    else
                    {
                        if (TryGetErrorCallback(callbackId, out var errorCallback0) && errorCallback0 != null)
                        {
                            errorCallback0(new AITException("AppLoginResult", apiResponse.error));
                        }
                    }
                    break;
                case "AppsInTossGlobals":
                    if (apiResponse.success)
                    {
                        if (TryGetCallback<AppsInTossGlobals>(callbackId, out var callback1) && callback1 != null)
                        {
                            var data1 = JsonConvert.DeserializeObject<AppsInTossGlobals>(apiResponse.data);
                            callback1(data1);
                        }
                    }
                    else
                    {
                        if (TryGetErrorCallback(callbackId, out var errorCallback1) && errorCallback1 != null)
                        {
                            errorCallback1(new AITException("AppsInTossGlobals", apiResponse.error));
                        }
                    }
                    break;
                case "CheckoutPaymentResult":
                    if (apiResponse.success)
                    {
                        if (TryGetCallback<CheckoutPaymentResult>(callbackId, out var callback2) && callback2 != null)
                        {
                            var data2 = JsonConvert.DeserializeObject<CheckoutPaymentResult>(apiResponse.data);
                            callback2(data2);
                        }
                    }
                    else
                    {
                        if (TryGetErrorCallback(callbackId, out var errorCallback2) && errorCallback2 != null)
                        {
                            errorCallback2(new AITException("CheckoutPaymentResult", apiResponse.error));
                        }
                    }
                    break;
                case "CompletedOrRefundedOrdersResult":
                    if (apiResponse.success)
                    {
                        if (TryGetCallback<CompletedOrRefundedOrdersResult>(callbackId, out var callback3) && callback3 != null)
                        {
                            var data3 = JsonConvert.DeserializeObject<CompletedOrRefundedOrdersResult>(apiResponse.data);
                            callback3(data3);
                        }
                    }
                    else
                    {
                        if (TryGetErrorCallback(callbackId, out var errorCallback3) && errorCallback3 != null)
                        {
                            errorCallback3(new AITException("CompletedOrRefundedOrdersResult", apiResponse.error));
                        }
                    }
                    break;
                case "ContactResult":
                    if (apiResponse.success)
                    {
                        if (TryGetCallback<ContactResult>(callbackId, out var callback4) && callback4 != null)
                        {
                            var data4 = JsonConvert.DeserializeObject<ContactResult>(apiResponse.data);
                            callback4(data4);
                        }
                    }
                    else
                    {
                        if (TryGetErrorCallback(callbackId, out var errorCallback4) && errorCallback4 != null)
                        {
                            errorCallback4(new AITException("ContactResult", apiResponse.error));
                        }
                    }
                    break;
                case "GameCenterGameProfileResponse":
                    if (apiResponse.success)
                    {
                        if (TryGetCallback<GameCenterGameProfileResponse>(callbackId, out var callback5) && callback5 != null)
                        {
                            var data5 = JsonConvert.DeserializeObject<GameCenterGameProfileResponse>(apiResponse.data);
                            callback5(data5);
                        }
                    }
                    else
                    {
                        if (TryGetErrorCallback(callbackId, out var errorCallback5) && errorCallback5 != null)
                        {
                            errorCallback5(new AITException("GameCenterGameProfileResponse", apiResponse.error));
                        }
                    }
                    break;
                case "GetUserKeyForGameResult":
                    if (apiResponse.success)
                    {
                        if (TryGetCallback<GetUserKeyForGameResult>(callbackId, out var callback6) && callback6 != null)
                        {
                            var data6 = JsonConvert.DeserializeObject<GetUserKeyForGameResult>(apiResponse.data);
                            callback6(data6);
                        }
                    }
                    else
                    {
                        if (TryGetErrorCallback(callbackId, out var errorCallback6) && errorCallback6 != null)
                        {
                            errorCallback6(new AITException("GetUserKeyForGameResult", apiResponse.error));
                        }
                    }
                    break;
                case "GrantPromotionRewardForGameResult":
                    if (apiResponse.success)
                    {
                        if (TryGetCallback<GrantPromotionRewardForGameResult>(callbackId, out var callback7) && callback7 != null)
                        {
                            var data7 = JsonConvert.DeserializeObject<GrantPromotionRewardForGameResult>(apiResponse.data);
                            callback7(data7);
                        }
                    }
                    else
                    {
                        if (TryGetErrorCallback(callbackId, out var errorCallback7) && errorCallback7 != null)
                        {
                            errorCallback7(new AITException("GrantPromotionRewardForGameResult", apiResponse.error));
                        }
                    }
                    break;
                case "IAPGetPendingOrdersResult":
                    if (apiResponse.success)
                    {
                        if (TryGetCallback<IAPGetPendingOrdersResult>(callbackId, out var callback8) && callback8 != null)
                        {
                            var data8 = JsonConvert.DeserializeObject<IAPGetPendingOrdersResult>(apiResponse.data);
                            callback8(data8);
                        }
                    }
                    else
                    {
                        if (TryGetErrorCallback(callbackId, out var errorCallback8) && errorCallback8 != null)
                        {
                            errorCallback8(new AITException("IAPGetPendingOrdersResult", apiResponse.error));
                        }
                    }
                    break;
                case "IAPGetProductItemListResult":
                    if (apiResponse.success)
                    {
                        if (TryGetCallback<IAPGetProductItemListResult>(callbackId, out var callback9) && callback9 != null)
                        {
                            var data9 = JsonConvert.DeserializeObject<IAPGetProductItemListResult>(apiResponse.data);
                            callback9(data9);
                        }
                    }
                    else
                    {
                        if (TryGetErrorCallback(callbackId, out var errorCallback9) && errorCallback9 != null)
                        {
                            errorCallback9(new AITException("IAPGetProductItemListResult", apiResponse.error));
                        }
                    }
                    break;
                case "ImageResponse":
                    if (apiResponse.success)
                    {
                        if (TryGetCallback<ImageResponse>(callbackId, out var callback10) && callback10 != null)
                        {
                            var data10 = JsonConvert.DeserializeObject<ImageResponse>(apiResponse.data);
                            callback10(data10);
                        }
                    }
                    else
                    {
                        if (TryGetErrorCallback(callbackId, out var errorCallback10) && errorCallback10 != null)
                        {
                            errorCallback10(new AITException("ImageResponse", apiResponse.error));
                        }
                    }
                    break;
                case "ImageResponse[]":
                    if (apiResponse.success)
                    {
                        if (TryGetCallback<ImageResponse[]>(callbackId, out var callback11) && callback11 != null)
                        {
                            var data11 = JsonConvert.DeserializeObject<ImageResponse[]>(apiResponse.data);
                            callback11(data11);
                        }
                    }
                    else
                    {
                        if (TryGetErrorCallback(callbackId, out var errorCallback11) && errorCallback11 != null)
                        {
                            errorCallback11(new AITException("ImageResponse[]", apiResponse.error));
                        }
                    }
                    break;
                case "Location":
                    if (apiResponse.success)
                    {
                        if (TryGetCallback<Location>(callbackId, out var callback12) && callback12 != null)
                        {
                            var data12 = JsonConvert.DeserializeObject<Location>(apiResponse.data);
                            callback12(data12);
                        }
                    }
                    else
                    {
                        if (TryGetErrorCallback(callbackId, out var errorCallback12) && errorCallback12 != null)
                        {
                            errorCallback12(new AITException("Location", apiResponse.error));
                        }
                    }
                    break;
                case "SafeAreaInsetsGetResult":
                    if (apiResponse.success)
                    {
                        if (TryGetCallback<SafeAreaInsetsGetResult>(callbackId, out var callback13) && callback13 != null)
                        {
                            var data13 = JsonConvert.DeserializeObject<SafeAreaInsetsGetResult>(apiResponse.data);
                            callback13(data13);
                        }
                    }
                    else
                    {
                        if (TryGetErrorCallback(callbackId, out var errorCallback13) && errorCallback13 != null)
                        {
                            errorCallback13(new AITException("SafeAreaInsetsGetResult", apiResponse.error));
                        }
                    }
                    break;
                case "SetScreenAwakeModeResult":
                    if (apiResponse.success)
                    {
                        if (TryGetCallback<SetScreenAwakeModeResult>(callbackId, out var callback14) && callback14 != null)
                        {
                            var data14 = JsonConvert.DeserializeObject<SetScreenAwakeModeResult>(apiResponse.data);
                            callback14(data14);
                        }
                    }
                    else
                    {
                        if (TryGetErrorCallback(callbackId, out var errorCallback14) && errorCallback14 != null)
                        {
                            errorCallback14(new AITException("SetScreenAwakeModeResult", apiResponse.error));
                        }
                    }
                    break;
                case "SetSecureScreenResult":
                    if (apiResponse.success)
                    {
                        if (TryGetCallback<SetSecureScreenResult>(callbackId, out var callback15) && callback15 != null)
                        {
                            var data15 = JsonConvert.DeserializeObject<SetSecureScreenResult>(apiResponse.data);
                            callback15(data15);
                        }
                    }
                    else
                    {
                        if (TryGetErrorCallback(callbackId, out var errorCallback15) && errorCallback15 != null)
                        {
                            errorCallback15(new AITException("SetSecureScreenResult", apiResponse.error));
                        }
                    }
                    break;
                case "SubmitGameCenterLeaderBoardScoreResponse":
                    if (apiResponse.success)
                    {
                        if (TryGetCallback<SubmitGameCenterLeaderBoardScoreResponse>(callbackId, out var callback16) && callback16 != null)
                        {
                            var data16 = JsonConvert.DeserializeObject<SubmitGameCenterLeaderBoardScoreResponse>(apiResponse.data);
                            callback16(data16);
                        }
                    }
                    else
                    {
                        if (TryGetErrorCallback(callbackId, out var errorCallback16) && errorCallback16 != null)
                        {
                            errorCallback16(new AITException("SubmitGameCenterLeaderBoardScoreResponse", apiResponse.error));
                        }
                    }
                    break;
                case "NetworkStatus":
                    if (apiResponse.success)
                    {
                        if (TryGetCallback<NetworkStatus>(callbackId, out var enumCb_NetworkStatus) && enumCb_NetworkStatus != null)
                        {
                            // enum은 JsonUtility가 파싱 불가. Enum.TryParse 사용
                            var enumStr_NetworkStatus = apiResponse.data.Trim().Trim('"');
                            if (Enum.TryParse<NetworkStatus>(enumStr_NetworkStatus, true, out var enumVal_NetworkStatus))
                            {
                                enumCb_NetworkStatus(enumVal_NetworkStatus);
                            }
                            else
                            {
                                Debug.LogWarning("[AITCore] Failed to parse enum NetworkStatus: " + enumStr_NetworkStatus);
                            }
                        }
                    }
                    else
                    {
                        if (TryGetErrorCallback(callbackId, out var enumErrCb_NetworkStatus) && enumErrCb_NetworkStatus != null)
                        {
                            enumErrCb_NetworkStatus(new AITException("NetworkStatus", apiResponse.error));
                        }
                    }
                    break;
                case "PermissionStatus":
                    if (apiResponse.success)
                    {
                        if (TryGetCallback<PermissionStatus>(callbackId, out var enumCb_PermissionStatus) && enumCb_PermissionStatus != null)
                        {
                            // enum은 JsonUtility가 파싱 불가. Enum.TryParse 사용
                            var enumStr_PermissionStatus = apiResponse.data.Trim().Trim('"');
                            if (Enum.TryParse<PermissionStatus>(enumStr_PermissionStatus, true, out var enumVal_PermissionStatus))
                            {
                                enumCb_PermissionStatus(enumVal_PermissionStatus);
                            }
                            else
                            {
                                Debug.LogWarning("[AITCore] Failed to parse enum PermissionStatus: " + enumStr_PermissionStatus);
                            }
                        }
                    }
                    else
                    {
                        if (TryGetErrorCallback(callbackId, out var enumErrCb_PermissionStatus) && enumErrCb_PermissionStatus != null)
                        {
                            enumErrCb_PermissionStatus(new AITException("PermissionStatus", apiResponse.error));
                        }
                    }
                    break;
                case "string":
                    if (apiResponse.success)
                    {
                        if (TryGetCallback<string>(callbackId, out var stringCallback) && stringCallback != null)
                        {
                            // string은 JsonUtility가 파싱 불가. data가 JSON 문자열이면 따옴표 제거
                            var stringData = apiResponse.data;
                            if (stringData.StartsWith("\"") && stringData.EndsWith("\""))
                            {
                                stringData = stringData.Substring(1, stringData.Length - 2);
                            }
                            stringCallback(stringData);
                        }
                    }
                    else
                    {
                        if (TryGetErrorCallback(callbackId, out var stringErrorCallback) && stringErrorCallback != null)
                        {
                            stringErrorCallback(new AITException("string", apiResponse.error));
                        }
                    }
                    break;
                case "bool":
                    if (apiResponse.success)
                    {
                        if (TryGetCallback<bool>(callbackId, out var boolCallback) && boolCallback != null)
                        {
                            // bool은 JsonUtility가 파싱 불가. 직접 파싱
                            var boolData = apiResponse.data.Trim().ToLower() == "true";
                            boolCallback(boolData);
                        }
                    }
                    else
                    {
                        if (TryGetErrorCallback(callbackId, out var boolErrorCallback) && boolErrorCallback != null)
                        {
                            boolErrorCallback(new AITException("bool", apiResponse.error));
                        }
                    }
                    break;
                case "double":
                    if (apiResponse.success)
                    {
                        if (TryGetCallback<double>(callbackId, out var doubleCallback) && doubleCallback != null)
                        {
                            // double은 JsonUtility가 파싱 불가. 직접 파싱
                            if (double.TryParse(apiResponse.data, out var doubleData))
                            {
                                doubleCallback(doubleData);
                            }
                        }
                    }
                    else
                    {
                        if (TryGetErrorCallback(callbackId, out var doubleErrorCallback) && doubleErrorCallback != null)
                        {
                            doubleErrorCallback(new AITException("double", apiResponse.error));
                        }
                    }
                    break;
                case "void":
                    if (apiResponse.success)
                    {
                        if (TryGetCallback<object>(callbackId, out var voidCallback) && voidCallback != null)
                        {
                            voidCallback(null);
                        }
                    }
                    else
                    {
                        if (TryGetErrorCallback(callbackId, out var voidErrorCallback) && voidErrorCallback != null)
                        {
                            voidErrorCallback(new AITException("void", apiResponse.error));
                        }
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
    [Preserve]
    public class CallbackData
    {
        [Preserve]
        public string CallbackId = "";
        [Preserve]
        public string TypeName = "";
        [Preserve]
        public string Result = "";
    }

    // ===================================================================
    // Event Data Types
    // ===================================================================

    /// <summary>
    /// TdsEvent navigationAccessoryEvent 데이터
    /// </summary>
    [Serializable]
    [Preserve]
    public class TdsNavigationAccessoryEventData
    {
        [Preserve]
        [JsonProperty("id")]
        public string Id;
    }
}
