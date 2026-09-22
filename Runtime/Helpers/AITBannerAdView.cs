// -----------------------------------------------------------------------
// <copyright file="AITBannerAdView.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Banner Ad View Component
// </copyright>
// -----------------------------------------------------------------------

using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting;

namespace AppsInToss
{
    /// <summary>
    /// 배너 배치 방식
    /// </summary>
    public enum AITBannerAdPlacement
    {
        /// <summary>이 컴포넌트의 RectTransform 영역을 따라 배치 (이동/리사이즈 자동 추적)</summary>
        FollowRectTransform,

        /// <summary>화면 상단 고정 (safe area 반영)</summary>
        ScreenTop,

        /// <summary>화면 하단 고정 (safe area 반영)</summary>
        ScreenBottom,
    }

    /// <summary>
    /// 배너 광고를 uGUI처럼 배치하는 MonoBehaviour 컴포넌트
    /// </summary>
    /// <remarks>
    /// Canvas 아래 RectTransform을 평소처럼 anchor/size로 배치하고 이 컴포넌트를
    /// 붙이면, SDK가 해당 화면 영역 위에 배너 광고 DOM을 오버레이합니다.
    /// HTML/CSS 지식이 필요 없으며 RectTransform의 이동·리사이즈·화면 회전을
    /// 자동으로 추적합니다.
    ///
    /// 각 인스턴스는 독립 슬롯이므로 여러 개를 동시에 표시할 수 있습니다.
    /// 생명주기 이벤트는 Inspector의 <see cref="onAdEvent"/>(UnityEvent)와
    /// C# <see cref="OnAdEvent"/> 양쪽으로 전달됩니다. 실제 광고 렌더링은
    /// Toss 앱/샌드박스 안에서만 가능합니다.
    ///
    /// 사용 예시:
    /// <code>
    /// var view = bannerArea.AddComponent&lt;AITBannerAdView&gt;();
    /// view.AdGroupId = "my-ad-group-id";
    /// view.OnAdEvent += evt => Debug.Log($"배너 이벤트: {evt}");
    /// view.Show();
    /// </code>
    /// </remarks>
    [Preserve]
    [AddComponentMenu("Apps in Toss/AIT Banner Ad View")]
    [RequireComponent(typeof(RectTransform))]
    public class AITBannerAdView : MonoBehaviour
    {
        /// <summary>
        /// Inspector 배선용 배너 이벤트 UnityEvent
        /// </summary>
        [Serializable]
        public class BannerAdUnityEvent : UnityEvent<AITBannerAdEvent>
        {
        }

        [Header("Ad Settings")]
        [Tooltip("광고 그룹 ID")]
        [SerializeField] private string adGroupId;

        [Tooltip("배치 방식 — FollowRectTransform이면 이 RectTransform 영역을 따라감")]
        [SerializeField] private AITBannerAdPlacement placement = AITBannerAdPlacement.FollowRectTransform;

        [Tooltip("OnEnable 시 자동 표시")]
        [SerializeField] private bool showOnEnable = true;

        [Tooltip("렌더된 배너 높이에 맞춰 이 RectTransform 높이를 자동 조정 (FollowRectTransform 모드 전용). " +
                 "레이아웃 그룹 하위에서는 효과가 없으니, RenderedHeightLocal을 읽어 LayoutElement.preferredHeight를 직접 설정하세요.")]
        [SerializeField] private bool autoResizeHeight = true;

        [Header("Appearance")]
        [SerializeField] private AITBannerTheme theme = AITBannerTheme.Auto;
        [SerializeField] private AITBannerTone tone = AITBannerTone.BlackAndWhite;
        [SerializeField] private AITBannerVariant variant = AITBannerVariant.Expanded;

        [Header("Events")]
        [Tooltip("배너 생명주기 이벤트 (초기화/렌더링/노출/클릭/실패 등)")]
        [SerializeField] private BannerAdUnityEvent onAdEvent = new BannerAdUnityEvent();

        private const float RectEpsilon = 0.0005f;

        private int _instanceId = -1;
        private RectTransform _rectTransform;
        private Canvas _rootCanvas;
        private readonly Vector3[] _worldCorners = new Vector3[4];
        private Vector4 _lastRect = new Vector4(-1f, -1f, -1f, -1f);

        // PushRect가 마지막으로 계산한 RectTransform의 화면 높이 비율(0~1).
        // Resized 이벤트의 heightFraction(캔버스 대비)을 로컬 높이로 역산하는 데 사용 (Screen.height 약분 → 렌더모드/DPI 독립).
        private float _lastProjectedHeight = -1f;

        /// <summary>
        /// 배너 생명주기 이벤트 (코드 구독용 — Inspector는 onAdEvent 사용)
        /// </summary>
        public event Action<AITBannerAdEvent> OnAdEvent;

        /// <summary>
        /// 배너 생명주기 UnityEvent (Inspector 배선과 동일 인스턴스 — 코드에서 AddListener 가능)
        /// </summary>
        public BannerAdUnityEvent OnAdEventUnity => onAdEvent;

        /// <summary>
        /// 광고 그룹 ID (표시 중 변경해도 다음 Show부터 적용)
        /// </summary>
        public string AdGroupId
        {
            get => adGroupId;
            set => adGroupId = value;
        }

        /// <summary>
        /// 배치 방식 (표시 중 변경해도 다음 Show부터 적용)
        /// </summary>
        public AITBannerAdPlacement Placement
        {
            get => placement;
            set => placement = value;
        }

        /// <summary>
        /// 배너 표시 중 여부
        /// </summary>
        public bool IsShowing => _instanceId >= 0;

        /// <summary>
        /// 가장 최근 Resized 이벤트에서 역산한 배너의 로컬(RectTransform) 높이.
        /// <see cref="AITBannerAdPlacement.FollowRectTransform"/> 모드에서만 채워집니다
        /// (Top/Bottom 프리셋은 화면 폭 띠라 RectTransform 역산이 의미 없음 — 이때는 0,
        /// 픽셀 높이가 필요하면 <see cref="AITBannerAdEvent.Height"/>를 사용하세요).
        /// Show마다 0으로 리셋되며 다음 Resized 전에는 0입니다. 레이아웃 그룹 하위에서
        /// 자동 높이를 적용하려면 이 값을 LayoutElement.preferredHeight에 설정하세요.
        /// </summary>
        public float RenderedHeightLocal { get; private set; }

        /// <summary>
        /// 렌더된 배너 높이에 맞춰 RectTransform 높이를 자동 조정할지 여부
        /// (FollowRectTransform 모드 전용). 레이아웃 그룹이 높이를 제어하는 호스트에서는
        /// false로 두고 <see cref="RenderedHeightLocal"/>을 LayoutElement.preferredHeight에 직접 설정하세요.
        /// </summary>
        public bool AutoResizeHeight
        {
            get => autoResizeHeight;
            set => autoResizeHeight = value;
        }

        /// <summary>
        /// 배너 표시 (이미 표시 중이면 교체)
        /// </summary>
        public void Show()
        {
            if (string.IsNullOrEmpty(adGroupId))
            {
                Debug.LogWarning("[AITBannerAdView] adGroupId가 비어 있습니다.", this);
                return;
            }

            Hide();

            int mode;
            switch (placement)
            {
                case AITBannerAdPlacement.ScreenTop:
                    mode = AITBannerAdEngine.ModeTop;
                    break;
                case AITBannerAdPlacement.ScreenBottom:
                    mode = AITBannerAdEngine.ModeBottom;
                    break;
                default:
                    mode = AITBannerAdEngine.ModeRect;
                    break;
            }

            _rectTransform = (RectTransform)transform;
            var canvas = GetComponentInParent<Canvas>();
            _rootCanvas = canvas != null ? canvas.rootCanvas : null;
            _lastRect = new Vector4(-1f, -1f, -1f, -1f);
            _lastProjectedHeight = -1f;
            RenderedHeightLocal = 0f; // 새 표시 사이클 — 다음 Resized까지 역산값 없음

            _instanceId = AITBannerAdEngine.Show(adGroupId, mode, theme, tone, variant, HandleAdEvent);

            // attach 전에 초기 위치를 잡는다 (jslib가 attach를 한 틱 미루므로 순서 보장됨)
            if (mode == AITBannerAdEngine.ModeRect)
            {
                PushRect(force: true);
            }
        }

        /// <summary>
        /// 배너 제거
        /// </summary>
        public void Hide()
        {
            if (_instanceId < 0) return;
            AITBannerAdEngine.Hide(_instanceId);
            _instanceId = -1;
        }

        private void OnEnable()
        {
            if (showOnEnable && !string.IsNullOrEmpty(adGroupId))
            {
                Show();
            }
        }

        private void OnDisable()
        {
            Hide();
        }

        private void LateUpdate()
        {
            if (_instanceId < 0 || placement != AITBannerAdPlacement.FollowRectTransform) return;
            PushRect(force: false);
        }

        /// <summary>
        /// RectTransform의 화면 영역을 정규화 rect(0~1, top-left 기준)로 변환해 jslib에 전달
        /// </summary>
        private void PushRect(bool force)
        {
            if (_rectTransform == null) return;

            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            if (screenWidth <= 0f || screenHeight <= 0f) return;

            // ScreenSpaceOverlay는 월드 좌표 = 화면 좌표, 그 외에는 캔버스 카메라로 투영
            Camera cam = null;
            if (_rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                cam = _rootCanvas.worldCamera;
            }

            _rectTransform.GetWorldCorners(_worldCorners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, _worldCorners[0]); // bottom-left
            Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, _worldCorners[2]); // top-right

            // Unity 화면 좌표는 bottom-left 원점 → CSS top-left 원점으로 변환
            float x = Mathf.Min(min.x, max.x) / screenWidth;
            float y = (screenHeight - Mathf.Max(min.y, max.y)) / screenHeight;
            float w = Mathf.Abs(max.x - min.x) / screenWidth;
            float h = Mathf.Abs(max.y - min.y) / screenHeight;

            // 자동 높이 역산용으로 현재 투영 높이를 기록 (epsilon 조기 반환 전에 항상 갱신)
            _lastProjectedHeight = h;

            var rect = new Vector4(x, y, w, h);
            if (!force &&
                Mathf.Abs(rect.x - _lastRect.x) < RectEpsilon &&
                Mathf.Abs(rect.y - _lastRect.y) < RectEpsilon &&
                Mathf.Abs(rect.z - _lastRect.z) < RectEpsilon &&
                Mathf.Abs(rect.w - _lastRect.w) < RectEpsilon)
            {
                return;
            }

            _lastRect = rect;
            AITBannerAdEngine.SetRect(_instanceId, x, y, w, h);
        }

        private void HandleAdEvent(AITBannerAdEvent evt)
        {
            // 사용자 핸들러가 RenderedHeightLocal을 읽을 수 있도록 먼저 역산/적용
            if (evt.Kind == AITBannerAdEventKind.Resized)
            {
                ApplyResized(evt);
            }

            try
            {
                OnAdEvent?.Invoke(evt);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }

            try
            {
                onAdEvent?.Invoke(evt);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }
        }

        /// <summary>
        /// Resized 이벤트의 heightFraction(캔버스 대비 비율)으로 배너 실제 높이를
        /// 로컬(RectTransform) 단위로 역산해 <see cref="RenderedHeightLocal"/>에 저장하고,
        /// <see cref="autoResizeHeight"/>면 RectTransform 높이를 그 값으로 맞춥니다.
        /// </summary>
        private void ApplyResized(AITBannerAdEvent evt)
        {
            // FollowRectTransform 모드에서만 의미 있음 (Top/Bottom은 화면 띠라 높이 추적 불필요)
            if (placement != AITBannerAdPlacement.FollowRectTransform) return;
            if (_rectTransform == null || _instanceId < 0) return;
            if (evt.HeightFraction <= 0f || _lastProjectedHeight <= RectEpsilon) return;

            // 현재 rect 높이 × (배너 화면비율 / 현재 rect 화면비율) = 배너 실제 높이의 로컬 단위.
            // heightFraction·_lastProjectedHeight 모두 화면 높이 대비 비율이라 Screen.height·DPI가 약분됨
            // → 렌더모드(Overlay/Camera)·DPI에 독립.
            float target = _rectTransform.rect.height * (evt.HeightFraction / _lastProjectedHeight);
            if (target <= 0f || float.IsNaN(target) || float.IsInfinity(target)) return;
            RenderedHeightLocal = target;

            if (!autoResizeHeight) return;

            // 세로 stretch 앵커는 높이가 부모에 종속 — 자동 리사이즈가 레이아웃과 충돌하므로 건너뜀
            // (RenderedHeightLocal은 위에서 채웠으니 사용자가 직접 적용 가능)
            Vector2 aMin = _rectTransform.anchorMin;
            Vector2 aMax = _rectTransform.anchorMax;
            if (Mathf.Abs(aMax.y - aMin.y) > RectEpsilon) return;

            if (Mathf.Abs(_rectTransform.rect.height - target) >= 0.5f)
            {
                _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, target);
            }

            // 리사이즈 후 rect의 화면 투영 높이는 heightFraction과 같아짐 → 다음 역산 기준을 즉시 갱신
            // (LateUpdate를 기다리지 않으므로 연속 Resized에도 제곱 오차 없이 수렴)
            _lastProjectedHeight = evt.HeightFraction;
        }
    }
}
