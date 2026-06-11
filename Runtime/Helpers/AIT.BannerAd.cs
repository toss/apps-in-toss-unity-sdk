// -----------------------------------------------------------------------
// <copyright file="AIT.BannerAd.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Banner Ad Helper
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;

namespace AppsInToss
{
    /// <summary>
    /// 배너가 표시될 화면 위치 프리셋
    /// </summary>
    public enum AITBannerPosition
    {
        /// <summary>화면 상단 (safe area 반영)</summary>
        Top,

        /// <summary>화면 하단 (safe area 반영)</summary>
        Bottom,
    }

    /// <summary>
    /// 배너 테마
    /// </summary>
    public enum AITBannerTheme
    {
        /// <summary>시스템 설정에 따라 자동 결정</summary>
        Auto,

        /// <summary>라이트 테마</summary>
        Light,

        /// <summary>다크 테마</summary>
        Dark,
    }

    /// <summary>
    /// 배너 톤
    /// </summary>
    public enum AITBannerTone
    {
        /// <summary>흑백 톤</summary>
        BlackAndWhite,

        /// <summary>그레이 톤</summary>
        Grey,
    }

    /// <summary>
    /// 배너 변형 (레이아웃)
    /// </summary>
    public enum AITBannerVariant
    {
        /// <summary>카드형</summary>
        Card,

        /// <summary>확장형</summary>
        Expanded,
    }

    /// <summary>
    /// 배너 광고 생명주기 이벤트 종류
    /// </summary>
    public enum AITBannerAdEventKind
    {
        /// <summary>광고 SDK 초기화 완료</summary>
        Initialized,

        /// <summary>광고 SDK 초기화 실패</summary>
        InitializationFailed,

        /// <summary>광고 렌더링 완료</summary>
        Rendered,

        /// <summary>광고가 화면에 보임</summary>
        Viewable,

        /// <summary>광고 클릭</summary>
        Clicked,

        /// <summary>광고 노출 집계</summary>
        Impression,

        /// <summary>광고 렌더링 실패</summary>
        FailedToRender,

        /// <summary>채울 광고 없음 (no-fill)</summary>
        NoFill,
    }

    /// <summary>
    /// 배너 광고 생명주기 이벤트
    /// </summary>
    [Serializable]
    [Preserve]
    public class AITBannerAdEvent
    {
        /// <summary>이벤트 종류</summary>
        public AITBannerAdEventKind Kind;

        /// <summary>광고 그룹 ID</summary>
        public string AdGroupId;

        /// <summary>광고 슬롯 ID (플랫폼 발급)</summary>
        public string SlotId;

        /// <summary>광고 소재 ID</summary>
        public string CreativeId;

        /// <summary>광고 요청 ID</summary>
        public string RequestId;

        /// <summary>오류 코드 (실패 이벤트 전용, 그 외 0)</summary>
        public int ErrorCode;

        /// <summary>오류 메시지 (실패 이벤트 전용)</summary>
        public string ErrorMessage;

        public override string ToString()
        {
            return Kind == AITBannerAdEventKind.InitializationFailed || Kind == AITBannerAdEventKind.FailedToRender
                ? $"{Kind} (adGroupId: {AdGroupId}, code: {ErrorCode}, message: {ErrorMessage})"
                : $"{Kind} (adGroupId: {AdGroupId}, slotId: {SlotId})";
        }
    }

    /// <summary>
    /// 배너 광고를 코드 한 줄로 표시하는 정적 helper
    /// </summary>
    /// <remarks>
    /// HTML/CSS 지식 없이 화면 상단/하단 프리셋 위치에 배너를 표시합니다.
    /// DOM 컨테이너 생성·관리는 SDK가 담당하며, 모든 생명주기 이벤트는
    /// <see cref="OnAdEvent"/>로 전달됩니다. 실제 광고 렌더링은 Toss 앱/샌드박스
    /// 안에서만 가능합니다.
    ///
    /// RectTransform으로 위치를 자유롭게 배치하려면 <see cref="AITBannerAdView"/>
    /// 컴포넌트를 사용하세요. 배너를 동시에 여러 개 띄우려면 AITBannerAdView를
    /// 여러 개 사용하세요 (이 정적 helper는 슬롯 1개를 유지하며 재호출 시 교체).
    ///
    /// 사용 예시:
    /// <code>
    /// AITBannerAd.OnAdEvent += evt => Debug.Log($"배너 이벤트: {evt}");
    /// AITBannerAd.Show("my-ad-group-id", AITBannerPosition.Bottom);
    /// // ...
    /// AITBannerAd.Hide();
    /// </code>
    /// </remarks>
    [Preserve]
    public static class AITBannerAd
    {
        private static int _instanceId = -1;

        /// <summary>
        /// 배너 생명주기 이벤트 (초기화/렌더링/노출/클릭/실패 등)
        /// </summary>
        public static event Action<AITBannerAdEvent> OnAdEvent;

        /// <summary>
        /// helper 자체 오류 이벤트 (빈 adGroupId 등 호출 단계 오류)
        /// </summary>
        public static event Action<string> OnError;

        /// <summary>
        /// 배너 표시 중 여부
        /// </summary>
        public static bool IsShowing => _instanceId >= 0;

        /// <summary>
        /// 배너 광고를 화면 프리셋 위치에 표시
        /// </summary>
        /// <param name="adGroupId">광고 그룹 ID</param>
        /// <param name="position">표시 위치 (기본 하단)</param>
        /// <param name="theme">테마 (기본 Auto)</param>
        /// <param name="tone">톤 (기본 BlackAndWhite)</param>
        /// <param name="variant">변형 (기본 Expanded)</param>
        public static void Show(
            string adGroupId,
            AITBannerPosition position = AITBannerPosition.Bottom,
            AITBannerTheme theme = AITBannerTheme.Auto,
            AITBannerTone tone = AITBannerTone.BlackAndWhite,
            AITBannerVariant variant = AITBannerVariant.Expanded)
        {
            if (string.IsNullOrEmpty(adGroupId))
            {
                Debug.LogWarning("[AITBannerAd] adGroupId가 비어 있습니다.");
                OnError?.Invoke("adGroupId가 비어 있습니다.");
                return;
            }

            // 재호출 = 교체: 기존 슬롯만 정리하고 새 슬롯을 연다
            Hide();

            int mode = position == AITBannerPosition.Top
                ? AITBannerAdEngine.ModeTop
                : AITBannerAdEngine.ModeBottom;
            _instanceId = AITBannerAdEngine.Show(adGroupId, mode, theme, tone, variant, evt => OnAdEvent?.Invoke(evt));
        }

        /// <summary>
        /// 표시 중인 배너 제거
        /// </summary>
        public static void Hide()
        {
            if (_instanceId < 0) return;
            AITBannerAdEngine.Hide(_instanceId);
            _instanceId = -1;
        }
    }

    /// <summary>
    /// 배너 광고 내부 엔진 — instanceId 기반 multi-slot 관리
    /// </summary>
    /// <remarks>
    /// <see cref="AITBannerAd"/>(정적 facade)와 <see cref="AITBannerAdView"/>(컴포넌트)가
    /// 공유합니다. 슬롯마다 독립 instanceId를 발급하므로 배너 여러 개를 동시에
    /// 표시할 수 있습니다. jslib의 이벤트는 AITBannerAdBridge GameObject가 수신해
    /// instanceId로 해당 슬롯 핸들러에 dispatch합니다.
    /// </remarks>
    [Preserve]
    internal static class AITBannerAdEngine
    {
        internal const int ModeTop = 0;
        internal const int ModeBottom = 1;
        internal const int ModeRect = 2;

        private const string Tag = "[AITBannerAd]";

        private static readonly Dictionary<int, Action<AITBannerAdEvent>> _handlers =
            new Dictionary<int, Action<AITBannerAdEvent>>();

        private static int _nextInstanceId = 1;
        private static AITBannerAdBridge _bridge;

        /// <summary>
        /// 새 배너 슬롯을 열고 instanceId를 반환
        /// </summary>
        internal static int Show(
            string adGroupId,
            int mode,
            AITBannerTheme theme,
            AITBannerTone tone,
            AITBannerVariant variant,
            Action<AITBannerAdEvent> handler)
        {
            int instanceId = _nextInstanceId++;
            _handlers[instanceId] = handler;

#if UNITY_WEBGL && !UNITY_EDITOR
            EnsureBridge();
            __AITBannerAd_Show(instanceId, adGroupId, mode, ThemeToString(theme), ToneToString(tone), VariantToString(variant));
#else
            Debug.Log($"[AIT Mock] AITBannerAd Show(instanceId: {instanceId}, adGroupId: {adGroupId}, mode: {mode}, " +
                      $"theme: {ThemeToString(theme)}, tone: {ToneToString(tone)}, variant: {VariantToString(variant)})");
#endif
            return instanceId;
        }

        /// <summary>
        /// 슬롯 컨테이너의 화면 위치 갱신 (정규화 0~1, top-left 기준)
        /// </summary>
        internal static void SetRect(int instanceId, float x, float y, float width, float height)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            __AITBannerAd_SetRect(instanceId, x, y, width, height);
#endif
        }

        /// <summary>
        /// 슬롯 제거 (배너 destroy + 컨테이너 제거)
        /// </summary>
        internal static void Hide(int instanceId)
        {
            if (!_handlers.Remove(instanceId)) return;

#if UNITY_WEBGL && !UNITY_EDITOR
            __AITBannerAd_Hide(instanceId);
#else
            Debug.Log($"[AIT Mock] AITBannerAd Hide(instanceId: {instanceId})");
#endif
        }

        /// <summary>
        /// jslib에서 수신한 이벤트 JSON을 해당 슬롯 핸들러에 dispatch
        /// </summary>
        internal static void DispatchEvent(string json)
        {
            BannerAdEventPayload payload;
            try
            {
                payload = JsonUtility.FromJson<BannerAdEventPayload>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} 이벤트 파싱 실패: {ex.Message}");
                return;
            }

            if (payload == null) return;

            // Hide 이후 도착한 이벤트는 무시
            if (!_handlers.TryGetValue(payload.instanceId, out var handler) || handler == null) return;

            var evt = new AITBannerAdEvent
            {
                Kind = ParseKind(payload.kind),
                AdGroupId = payload.adGroupId,
                SlotId = payload.slotId,
                CreativeId = payload.creativeId,
                RequestId = payload.requestId,
                ErrorCode = payload.errorCode,
                ErrorMessage = payload.errorMessage,
            };

            try
            {
                handler(evt);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static void EnsureBridge()
        {
            if (_bridge != null) return;

            var go = new GameObject("AITBannerAdBridge");
            go.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            _bridge = go.AddComponent<AITBannerAdBridge>();
        }

        private static AITBannerAdEventKind ParseKind(string kind)
        {
            switch (kind)
            {
                case "initialized": return AITBannerAdEventKind.Initialized;
                case "initializationFailed": return AITBannerAdEventKind.InitializationFailed;
                case "rendered": return AITBannerAdEventKind.Rendered;
                case "viewable": return AITBannerAdEventKind.Viewable;
                case "clicked": return AITBannerAdEventKind.Clicked;
                case "impression": return AITBannerAdEventKind.Impression;
                case "noFill": return AITBannerAdEventKind.NoFill;
                case "failedToRender": return AITBannerAdEventKind.FailedToRender;
                default:
                    Debug.LogWarning($"{Tag} 알 수 없는 이벤트 종류: {kind}");
                    return AITBannerAdEventKind.FailedToRender;
            }
        }

        private static string ThemeToString(AITBannerTheme theme)
        {
            switch (theme)
            {
                case AITBannerTheme.Light: return "light";
                case AITBannerTheme.Dark: return "dark";
                default: return "auto";
            }
        }

        private static string ToneToString(AITBannerTone tone)
        {
            return tone == AITBannerTone.Grey ? "grey" : "blackAndWhite";
        }

        private static string VariantToString(AITBannerVariant variant)
        {
            return variant == AITBannerVariant.Card ? "card" : "expanded";
        }

        /// <summary>
        /// jslib 이벤트 JSON 페이로드 (JsonUtility 매핑 — 필드명은 JSON 키와 동일해야 함)
        /// </summary>
        [Serializable]
        [Preserve]
        private class BannerAdEventPayload
        {
            public int instanceId;
            public string kind;
            public string adGroupId;
            public string slotId;
            public string creativeId;
            public string requestId;
            public int errorCode;
            public string errorMessage;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void __AITBannerAd_Show(int instanceId, string adGroupId, int mode, string theme, string tone, string variant);

        [DllImport("__Internal")]
        private static extern void __AITBannerAd_SetRect(int instanceId, float x, float y, float width, float height);

        [DllImport("__Internal")]
        private static extern void __AITBannerAd_Hide(int instanceId);
#endif
    }

    /// <summary>
    /// jslib SendMessage 수신용 hidden GameObject 컴포넌트
    /// </summary>
    [Preserve]
    internal class AITBannerAdBridge : MonoBehaviour
    {
        /// <summary>
        /// jslib가 SendMessage('AITBannerAdBridge', 'OnBannerAdEvent', json)으로 호출
        /// </summary>
        [Preserve]
        public void OnBannerAdEvent(string json)
        {
            AITBannerAdEngine.DispatchEvent(json);
        }
    }
}
