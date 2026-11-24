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

        /// <summary>
        /// 렌더된 배너 크기 변경 (소재 로드/리사이즈, 화면 크기 변경 시).
        /// <see cref="AITBannerAdEvent.Width"/>/<see cref="AITBannerAdEvent.Height"/>(CSS px)와
        /// <see cref="AITBannerAdEvent.HeightFraction"/>(캔버스 대비 비율)이 채워집니다.
        /// </summary>
        Resized,
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

        /// <summary>렌더된 배너 너비 (CSS px, Resized 이벤트 전용 / 그 외 0)</summary>
        public float Width;

        /// <summary>렌더된 배너 높이 (CSS px, Resized 이벤트 전용 / 그 외 0)</summary>
        public float Height;

        /// <summary>
        /// 렌더된 배너 높이의 캔버스 대비 비율 (0~1, Resized 이벤트 전용 / 그 외 0).
        /// RectTransform 영역을 배너 실제 높이에 맞추는 자동 높이 조정에 사용됩니다.
        /// </summary>
        public float HeightFraction;

        public override string ToString()
        {
            if (Kind == AITBannerAdEventKind.InitializationFailed || Kind == AITBannerAdEventKind.FailedToRender)
                return $"{Kind} (adGroupId: {AdGroupId}, code: {ErrorCode}, message: {ErrorMessage})";
            if (Kind == AITBannerAdEventKind.Resized)
                return $"{Kind} (adGroupId: {AdGroupId}, {Width:F0}x{Height:F0}px, heightFraction: {HeightFraction:F3})";
            return $"{Kind} (adGroupId: {AdGroupId}, slotId: {SlotId})";
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
    /// 게임이 넓은 화면/멀티 윈도우에서 레터박스(필러박스)를 두면 Top/Bottom 배너는
    /// 게임 렌더 영역의 가로폭을 따릅니다. 기본은 <c>Camera.main.rect</c>에서 자동으로 읽고,
    /// 다른 방식으로 영역을 만드는 게임은 <see cref="SetGameArea(Rect)"/> /
    /// <see cref="SetGameArea(RectTransform)"/>로 직접 알려 주세요. 영역을 지정하지 않고
    /// 카메라도 전체 화면이면 기존처럼 화면 전체 폭으로 표시됩니다.
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

        /// <summary>
        /// 게임 렌더 영역을 직접 지정 (정규화 0~1, 좌상단 원점, 캔버스 기준)
        /// </summary>
        /// <remarks>
        /// Top/Bottom 배너(이 helper와 <see cref="AITBannerAdView"/>의 ScreenTop/ScreenBottom)가
        /// 이 영역의 가로폭과 상/하단에 맞춰 표시됩니다. 폭이나 높이가 0 이하이거나 화면 전체이면
        /// 영역 없음(<see cref="ClearGameArea"/>)과 같습니다. 호출 이후 <c>Camera.main.rect</c>
        /// 자동 감지보다 이 값이 우선합니다.
        /// </remarks>
        /// <param name="normalizedRect">좌상단 원점 기준 정규화 rect (x, y, width, height)</param>
        public static void SetGameArea(Rect normalizedRect)
        {
            AITBannerAdEngine.SetGameArea(normalizedRect);
        }

        /// <summary>
        /// RectTransform의 화면 영역을 게임 렌더 영역으로 지정 (매 프레임 추적)
        /// </summary>
        /// <remarks>
        /// 레터박스를 uGUI 영역으로 표현하는 게임용입니다. RectTransform이 이동·리사이즈하거나
        /// 화면 크기가 바뀌면 자동으로 따라갑니다. 대상이 파괴되면 카메라 자동 감지로 돌아갑니다.
        /// </remarks>
        /// <param name="area">게임 렌더 영역을 나타내는 RectTransform (null이면 <see cref="ClearGameArea"/>)</param>
        public static void SetGameArea(RectTransform area)
        {
            AITBannerAdEngine.SetGameArea(area);
        }

        /// <summary>
        /// 지정한 게임 렌더 영역을 해제하고 <c>Camera.main.rect</c> 자동 감지로 복귀
        /// </summary>
        public static void ClearGameArea()
        {
            AITBannerAdEngine.ClearGameArea();
        }
    }

    /// <summary>
    /// 게임 렌더 영역(정규화 0~1, 좌상단 원점) 계산용 순수 함수 모음
    /// </summary>
    internal static class AITBannerGameArea
    {
        internal const float Epsilon = 0.0005f;

        /// <summary>
        /// 정규화 rect를 0~1로 보정. 크기가 없거나 화면 전체면 false (= 영역 없음)
        /// </summary>
        internal static bool TryNormalize(Rect topLeftRect, out Rect area)
        {
            area = default;

            float left = Mathf.Clamp01(topLeftRect.xMin);
            float top = Mathf.Clamp01(topLeftRect.yMin);
            float right = Mathf.Clamp01(topLeftRect.xMax);
            float bottom = Mathf.Clamp01(topLeftRect.yMax);
            if (float.IsNaN(left + top + right + bottom)) return false;
            if (right - left <= Epsilon || bottom - top <= Epsilon) return false;

            if (left <= Epsilon && top <= Epsilon && right >= 1f - Epsilon && bottom >= 1f - Epsilon) return false;

            area = Rect.MinMaxRect(left, top, right, bottom);
            return true;
        }

        /// <summary>
        /// Camera.rect(정규화, 좌하단 원점)를 좌상단 원점 영역으로 변환. 전체 화면이면 false
        /// </summary>
        internal static bool TryFromCameraRect(Rect cameraRect, out Rect area)
        {
            var topLeft = new Rect(cameraRect.x, 1f - cameraRect.y - cameraRect.height, cameraRect.width, cameraRect.height);
            return TryNormalize(topLeft, out area);
        }

        /// <summary>
        /// 화면 좌표(픽셀, 좌하단 원점) 사각형의 두 모서리를 좌상단 원점 영역으로 변환. 전체 화면이면 false
        /// </summary>
        internal static bool TryFromScreenCorners(Vector2 bottomLeft, Vector2 topRight, float screenWidth, float screenHeight, out Rect area)
        {
            area = default;
            if (screenWidth <= 0f || screenHeight <= 0f) return false;

            float xMin = Mathf.Min(bottomLeft.x, topRight.x) / screenWidth;
            float xMax = Mathf.Max(bottomLeft.x, topRight.x) / screenWidth;
            float yMin = (screenHeight - Mathf.Max(bottomLeft.y, topRight.y)) / screenHeight;
            float yMax = (screenHeight - Mathf.Min(bottomLeft.y, topRight.y)) / screenHeight;
            return TryNormalize(Rect.MinMaxRect(xMin, yMin, xMax, yMax), out area);
        }

        internal static bool ApproximatelyEqual(Rect a, Rect b)
        {
            return Mathf.Abs(a.x - b.x) < Epsilon &&
                   Mathf.Abs(a.y - b.y) < Epsilon &&
                   Mathf.Abs(a.width - b.width) < Epsilon &&
                   Mathf.Abs(a.height - b.height) < Epsilon;
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

        private enum GameAreaSource
        {
            Auto,
            Explicit,
            Tracked,
        }

        private static int _nextInstanceId = 1;
        private static AITBannerAdBridge _bridge;

        // 게임 렌더 영역 — 모든 Top/Bottom 슬롯이 공유한다. 마지막으로 jslib에 보낸 값은 _pushedGameArea
        // (width <= 0이면 영역 없음). 값이 바뀔 때만 보낸다.
        private static GameAreaSource _gameAreaSource = GameAreaSource.Auto;
        private static Rect _explicitGameArea;
        private static RectTransform _trackedGameArea;
        private static Rect _pushedGameArea;

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
            SyncGameArea();
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
        /// 게임 렌더 영역을 직접 지정 (정규화 0~1, 좌상단 원점)
        /// </summary>
        internal static void SetGameArea(Rect normalizedRect)
        {
            _gameAreaSource = GameAreaSource.Explicit;
            _explicitGameArea = normalizedRect;
            _trackedGameArea = null;
            SyncGameArea();
        }

        /// <summary>
        /// RectTransform의 화면 영역을 게임 렌더 영역으로 추적 (null이면 해제)
        /// </summary>
        internal static void SetGameArea(RectTransform area)
        {
            if (area == null)
            {
                ClearGameArea();
                return;
            }

            _gameAreaSource = GameAreaSource.Tracked;
            _trackedGameArea = area;
            SyncGameArea();
        }

        /// <summary>
        /// 지정한 게임 렌더 영역을 해제 (Camera.main.rect 자동 감지로 복귀)
        /// </summary>
        internal static void ClearGameArea()
        {
            _gameAreaSource = GameAreaSource.Auto;
            _trackedGameArea = null;
            SyncGameArea();
        }

        /// <summary>
        /// 현재 게임 렌더 영역을 계산해 값이 바뀌었을 때만 jslib에 전달
        /// </summary>
        /// <remarks>
        /// 표시 중인 슬롯이 없으면 건너뛴다. jslib가 마지막 값을 보관하고 있다가 새 슬롯에 적용하며,
        /// 다음 Show 직후 호출로 최신 값과 다시 맞춘다.
        /// </remarks>
        internal static void SyncGameArea()
        {
            if (_handlers.Count == 0) return;

            Rect area;
            if (!TryResolveGameArea(out area)) area = default;

            if (AITBannerGameArea.ApproximatelyEqual(area, _pushedGameArea)) return;
            _pushedGameArea = area;

#if UNITY_WEBGL && !UNITY_EDITOR
            __AITBannerAd_SetGameArea(area.x, area.y, area.width, area.height);
#else
            Debug.Log($"[AIT Mock] AITBannerAd SetGameArea({area})");
#endif
        }

        private static bool TryResolveGameArea(out Rect area)
        {
            area = default;

            if (_gameAreaSource == GameAreaSource.Explicit)
            {
                return AITBannerGameArea.TryNormalize(_explicitGameArea, out area);
            }

            if (_gameAreaSource == GameAreaSource.Tracked)
            {
                if (_trackedGameArea != null) return TryProjectRectTransform(_trackedGameArea, out area);

                // 대상이 파괴됨 — 자동 감지로 복귀
                _gameAreaSource = GameAreaSource.Auto;
                _trackedGameArea = null;
            }

            var cam = Camera.main;
            if (cam == null) return false;
            return AITBannerGameArea.TryFromCameraRect(cam.rect, out area);
        }

        private static readonly Vector3[] _corners = new Vector3[4];

        private static bool TryProjectRectTransform(RectTransform target, out Rect area)
        {
            area = default;
            if (Screen.width <= 0 || Screen.height <= 0) return false;

            var canvas = target.GetComponentInParent<Canvas>();
            var root = canvas != null ? canvas.rootCanvas : null;
            Camera cam = null;
            if (root != null && root.renderMode != RenderMode.ScreenSpaceOverlay) cam = root.worldCamera;

            target.GetWorldCorners(_corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, _corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, _corners[2]);
            return AITBannerGameArea.TryFromScreenCorners(min, max, Screen.width, Screen.height, out area);
        }

        /// <summary>
        /// 표시 중인 모든 슬롯에 현재 크기로 Resized를 다시 보내도록 요청
        /// </summary>
        internal static void RefreshSizes()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_handlers.Count > 0) __AITBannerAd_RefreshSizes();
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
                Width = payload.width,
                Height = payload.height,
                HeightFraction = payload.heightFraction,
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
                case "resized": return AITBannerAdEventKind.Resized;
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
            public float width;
            public float height;
            // widthFraction은 jslib가 보내지만 C#은 높이만 자동 조정하므로 매핑하지 않음 (가로폭은 호출자 결정)
            public float heightFraction;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void __AITBannerAd_Show(int instanceId, string adGroupId, int mode, string theme, string tone, string variant);

        [DllImport("__Internal")]
        private static extern void __AITBannerAd_SetRect(int instanceId, float x, float y, float width, float height);

        [DllImport("__Internal")]
        private static extern void __AITBannerAd_SetGameArea(float x, float y, float width, float height);

        [DllImport("__Internal")]
        private static extern void __AITBannerAd_RefreshSizes();

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
        private int _screenWidth;
        private int _screenHeight;
        private int _refreshFrames;

        // 창 크기가 바뀌어도 배너 높이(CSS px)는 그대로라 jslib가 스스로 다시 보고하지 않는다.
        // 캔버스 대비 비율(heightFraction)은 달라지므로 새 화면 크기가 반영된 프레임에서 재보고를 요청한다.
        // 레이아웃 그룹 재계산은 LateUpdate보다 늦게 끝날 수 있어 다음 프레임에 한 번 더 요청한다.
        private void LateUpdate()
        {
            // 카메라 rect·추적 RectTransform·화면 크기 변화를 반영 (값이 바뀔 때만 jslib로 전달)
            AITBannerAdEngine.SyncGameArea();

            if (Screen.width != _screenWidth || Screen.height != _screenHeight)
            {
                _screenWidth = Screen.width;
                _screenHeight = Screen.height;
                _refreshFrames = 2;
            }

            if (_refreshFrames == 0) return;
            _refreshFrames--;
            AITBannerAdEngine.RefreshSizes();
        }

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
