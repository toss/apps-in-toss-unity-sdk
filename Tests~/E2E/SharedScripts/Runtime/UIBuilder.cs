using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// uGUI 요소를 코드로 간결하게 생성하기 위한 정적 헬퍼 클래스.
/// 프리팹 없이 런타임에서 Canvas, Button, InputField 등을 생성합니다.
/// </summary>
public static class UIBuilder
{
    // ─── 테마 상수 ───
    public static class Theme
    {
        // 배경
        public static readonly Color CanvasBg = new Color(0.1f, 0.1f, 0.12f, 0.98f);
        public static readonly Color SectionBg = new Color(0.15f, 0.15f, 0.18f, 0.95f);
        public static readonly Color HeaderBg = new Color(0.18f, 0.22f, 0.28f, 1f);
        public static readonly Color InputBg = new Color(0.2f, 0.2f, 0.22f, 1f);
        public static readonly Color ButtonBg = new Color(0.25f, 0.25f, 0.3f, 1f);
        public static readonly Color ButtonHover = new Color(0.3f, 0.3f, 0.38f, 1f);
        public static readonly Color ButtonPress = new Color(0.18f, 0.18f, 0.22f, 1f);
        public static readonly Color DangerBg = new Color(0.8f, 0.2f, 0.2f, 1f);
        public static readonly Color DangerHover = new Color(0.9f, 0.3f, 0.3f, 1f);
        public static readonly Color DangerPress = new Color(0.6f, 0.1f, 0.1f, 1f);
        public static readonly Color SuccessBg = new Color(0.2f, 0.6f, 0.3f, 1f);
        public static readonly Color FailBg = new Color(0.7f, 0.2f, 0.2f, 1f);
        public static readonly Color AccentBg = new Color(0.3f, 0.5f, 0.8f, 1f);
        public static readonly Color ToggleOnBg = new Color(0.3f, 0.6f, 0.3f, 1f);
        public static readonly Color ToggleOffBg = new Color(0.4f, 0.4f, 0.4f, 1f);
        public static readonly Color APIButtonBg = new Color(0.2f, 0.2f, 0.23f, 1f);
        public static readonly Color ScrollbarBg = new Color(0.15f, 0.15f, 0.15f, 0.5f);
        public static readonly Color ScrollbarHandle = new Color(0.4f, 0.4f, 0.4f, 0.8f);

        // 텍스트
        public static readonly Color TextPrimary = new Color(0.93f, 0.93f, 0.93f);
        public static readonly Color TextSecondary = new Color(0.7f, 0.7f, 0.7f);
        public static readonly Color TextAccent = new Color(0.4f, 0.8f, 1f);
        public static readonly Color TextCallback = new Color(0.6f, 0.6f, 0.6f);
        public static readonly Color TextResultKey = new Color(0.7f, 0.85f, 1f);
        public static readonly Color Placeholder = new Color(0.5f, 0.5f, 0.5f);

        // 폰트 크기
        public const int FontTiny = 11;
        public const int FontSmall = 13;
        public const int FontNormal = 15;
        public const int FontLarge = 17;
        public const int FontHeader = 20;

        // 간격
        public const float SpacingSmall = 4f;
        public const float SpacingNormal = 8f;
        public const float SpacingLarge = 16f;
        public const float Padding = 12f;
        public const float ButtonHeight = 44f;
        public const float InputHeight = 40f;
    }

    // ─── 폰트 캐시 ───
    private static Font _defaultFont;

    private static Font DefaultFont
    {
        get
        {
            if (_defaultFont == null)
            {
                // 한국어 지원 폰트 우선 로드 (WebGL에서 LegacyRuntime은 한국어 미지원)
                _defaultFont = Resources.Load<Font>("Fonts/NotoSansKR-Regular");
                if (_defaultFont == null)
                    _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return _defaultFont;
        }
    }

    // ─── Canvas ───

    /// <summary>
    /// ScreenSpaceOverlay Canvas를 생성합니다.
    /// CanvasScaler(ScaleWithScreenSize)와 GraphicRaycaster를 포함합니다.
    /// </summary>
    public static Canvas CreateCanvas(string name = "UICanvas", int sortingOrder = 100)
    {
        var go = new GameObject(name);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(390, 844);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();

        // EventSystem이 없으면 생성 (uGUI 인터랙션에 필수)
        if (EventSystem.current == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();
        }

        return canvas;
    }

    // ─── Safe Area ───

    /// <summary>
    /// Screen.safeArea를 반영하는 패널을 생성합니다.
    /// </summary>
    public static RectTransform CreateSafeAreaPanel(Transform parent)
    {
        var go = new GameObject("SafeAreaPanel");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);

        // 전체 영역으로 초기화
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Safe area 밖 콘텐츠 클리핑 (스크롤 시 콘텐츠가 safe area 밖으로 보이는 현상 방지)
        go.AddComponent<UnityEngine.UI.RectMask2D>();

        // SafeAreaUpdater를 통해 매 프레임 업데이트
        go.AddComponent<SafeAreaUpdater>();

        return rt;
    }

    // ─── Panel ───

    public static RectTransform CreatePanel(Transform parent, Color? color = null)
    {
        var go = new GameObject("Panel");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = color ?? Theme.SectionBg;

        return rt;
    }

    // ─── VerticalLayout / HorizontalLayout ───

    public static RectTransform CreateVerticalLayout(Transform parent, float spacing = -1, RectOffset padding = null)
    {
        var go = new GameObject("VerticalLayout");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);

        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = spacing >= 0 ? spacing : Theme.SpacingNormal;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        if (padding != null) vlg.padding = padding;

        var csf = go.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return rt;
    }

    public static RectTransform CreateHorizontalLayout(Transform parent, float spacing = -1)
    {
        var go = new GameObject("HorizontalLayout");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = spacing >= 0 ? spacing : Theme.SpacingNormal;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        return rt;
    }

    // ─── ScrollView ───

    /// <summary>
    /// ScrollRect (세로 스크롤 전용)을 생성합니다.
    /// 반환값은 Content RectTransform이며, 여기에 자식을 추가합니다.
    /// scrollRect out 파라미터로 ScrollRect 자체에 접근 가능합니다.
    /// </summary>
    public static RectTransform CreateScrollView(Transform parent, out ScrollRect scrollRect)
    {
        // ScrollView 루트
        var scrollGo = new GameObject("ScrollView");
        var scrollRt = scrollGo.AddComponent<RectTransform>();
        scrollRt.SetParent(parent, false);
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = Vector2.zero;
        scrollRt.offsetMax = Vector2.zero;

        scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.elasticity = 0.1f;
        scrollRect.decelerationRate = 0.135f;
        scrollRect.scrollSensitivity = 30f;

        // Viewport
        var viewportGo = new GameObject("Viewport");
        var viewportRt = viewportGo.AddComponent<RectTransform>();
        viewportRt.SetParent(scrollRt, false);
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;

        var mask = viewportGo.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        var viewportImg = viewportGo.AddComponent<Image>();
        viewportImg.color = Color.white; // Mask requires Image but we hide it

        // Content
        var contentGo = new GameObject("Content");
        var contentRt = contentGo.AddComponent<RectTransform>();
        contentRt.SetParent(viewportRt, false);
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.offsetMin = new Vector2(0, 0);
        contentRt.offsetMax = new Vector2(0, 0);

        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = Theme.SpacingNormal;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.padding = new RectOffset(
            (int)Theme.Padding, (int)Theme.Padding,
            (int)Theme.Padding, (int)Theme.Padding);

        var csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ScrollRect 연결
        scrollRect.viewport = viewportRt;
        scrollRect.content = contentRt;

        // Scrollbar (vertical)
        var scrollbar = CreateVerticalScrollbar(scrollRt);
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalScrollbarSpacing = -3;

        return contentRt;
    }

    private static Scrollbar CreateVerticalScrollbar(Transform parent)
    {
        var go = new GameObject("Scrollbar");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(1, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.sizeDelta = new Vector2(8, 0);
        rt.anchoredPosition = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.color = Theme.ScrollbarBg;

        var scrollbar = go.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        // Handle
        var slideGo = new GameObject("SlidingArea");
        var slideRt = slideGo.AddComponent<RectTransform>();
        slideRt.SetParent(rt, false);
        slideRt.anchorMin = Vector2.zero;
        slideRt.anchorMax = Vector2.one;
        slideRt.offsetMin = Vector2.zero;
        slideRt.offsetMax = Vector2.zero;

        var handleGo = new GameObject("Handle");
        var handleRt = handleGo.AddComponent<RectTransform>();
        handleRt.SetParent(slideRt, false);
        handleRt.anchorMin = Vector2.zero;
        handleRt.anchorMax = Vector2.one;
        handleRt.offsetMin = Vector2.zero;
        handleRt.offsetMax = Vector2.zero;

        var handleImg = handleGo.AddComponent<Image>();
        handleImg.color = Theme.ScrollbarHandle;

        scrollbar.handleRect = handleRt;
        scrollbar.targetGraphic = handleImg;

        return scrollbar;
    }

    // ─── Text ───

    public static Text CreateText(Transform parent, string text,
        int fontSize = -1, Color? color = null, TextAnchor alignment = TextAnchor.MiddleLeft,
        FontStyle fontStyle = FontStyle.Normal)
    {
        var go = new GameObject("Text");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);

        var t = go.AddComponent<Text>();
        t.text = text;
        t.font = DefaultFont;
        t.fontSize = fontSize > 0 ? fontSize : Theme.FontNormal;
        t.color = color ?? Theme.TextPrimary;
        t.alignment = alignment;
        t.fontStyle = fontStyle;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;

        // LayoutElement 추가 (높이를 텍스트에 맞춤)
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = t.fontSize + 8;

        return t;
    }

    // ─── Button ───

    public static Button CreateButton(Transform parent, string label, Action onClick = null, ButtonStyle style = ButtonStyle.Normal)
    {
        var go = new GameObject("Button");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        var btn = go.AddComponent<Button>();

        // 스타일 적용
        Color normalColor, hoverColor, pressColor, textColor;
        switch (style)
        {
            case ButtonStyle.Danger:
                normalColor = Theme.DangerBg;
                hoverColor = Theme.DangerHover;
                pressColor = Theme.DangerPress;
                textColor = Color.white;
                break;
            case ButtonStyle.Accent:
                normalColor = Theme.AccentBg;
                hoverColor = new Color(0.35f, 0.55f, 0.85f, 1f);
                pressColor = new Color(0.2f, 0.4f, 0.7f, 1f);
                textColor = Color.white;
                break;
            default: // Normal
                normalColor = Theme.ButtonBg;
                hoverColor = Theme.ButtonHover;
                pressColor = Theme.ButtonPress;
                textColor = Theme.TextPrimary;
                break;
        }

        img.color = normalColor;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(
            hoverColor.r / Mathf.Max(normalColor.r, 0.01f),
            hoverColor.g / Mathf.Max(normalColor.g, 0.01f),
            hoverColor.b / Mathf.Max(normalColor.b, 0.01f), 1f);
        colors.pressedColor = new Color(
            pressColor.r / Mathf.Max(normalColor.r, 0.01f),
            pressColor.g / Mathf.Max(normalColor.g, 0.01f),
            pressColor.b / Mathf.Max(normalColor.b, 0.01f), 1f);
        btn.colors = colors;

        // 텍스트
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(10, 4);
        textRt.offsetMax = new Vector2(-10, -4);

        var text = textGo.AddComponent<Text>();
        text.text = label;
        text.font = DefaultFont;
        text.fontSize = Theme.FontNormal;
        text.color = textColor;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;

        // LayoutElement
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = Theme.ButtonHeight;
        le.preferredHeight = Theme.ButtonHeight;

        if (onClick != null) btn.onClick.AddListener(() => onClick());

        return btn;
    }

    /// <summary>
    /// 텍스트 왼쪽 정렬 API 버튼을 생성합니다.
    /// </summary>
    public static Button CreateAPIButton(Transform parent, string label, Action onClick = null)
    {
        var btn = CreateButton(parent, label, onClick);
        // 텍스트를 왼쪽 정렬로 변경
        var text = btn.GetComponentInChildren<Text>();
        if (text != null) text.alignment = TextAnchor.MiddleLeft;
        return btn;
    }

    // ─── InputField ───

    public static InputField CreateInputField(Transform parent, string placeholder = "",
        Action<string> onValueChanged = null, Action<string> onEndEdit = null)
    {
        var go = new GameObject("InputField");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = Theme.InputBg;

        var inputField = go.AddComponent<InputField>();

        // Text
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(10, 4);
        textRt.offsetMax = new Vector2(-10, -4);

        var text = textGo.AddComponent<Text>();
        text.font = DefaultFont;
        text.fontSize = Theme.FontNormal;
        text.color = Theme.TextPrimary;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.supportRichText = false;

        // Placeholder
        var phGo = new GameObject("Placeholder");
        phGo.transform.SetParent(go.transform, false);
        var phRt = phGo.AddComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero;
        phRt.anchorMax = Vector2.one;
        phRt.offsetMin = new Vector2(10, 4);
        phRt.offsetMax = new Vector2(-10, -4);

        var phText = phGo.AddComponent<Text>();
        phText.text = placeholder;
        phText.font = DefaultFont;
        phText.fontSize = Theme.FontNormal;
        phText.fontStyle = FontStyle.Italic;
        phText.color = Theme.Placeholder;
        phText.alignment = TextAnchor.MiddleLeft;
        phText.horizontalOverflow = HorizontalWrapMode.Wrap;

        inputField.textComponent = text;
        inputField.placeholder = phText;

        // LayoutElement
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = Theme.InputHeight;
        le.preferredHeight = Theme.InputHeight;
        le.flexibleWidth = 1;

        if (onValueChanged != null) inputField.onValueChanged.AddListener((v) => onValueChanged(v));
        if (onEndEdit != null) inputField.onEndEdit.AddListener((v) => onEndEdit(v));

        return inputField;
    }

    // ─── Toggle (bool용) ───

    public static Toggle CreateToggle(Transform parent, string label, bool isOn = false, Action<bool> onToggle = null)
    {
        var go = new GameObject("Toggle");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.padding = new RectOffset(4, 4, 4, 4);

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 36;
        le.preferredHeight = 36;

        var toggle = go.AddComponent<Toggle>();

        // Background (체크박스 외곽)
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(go.transform, false);
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = isOn ? Theme.ToggleOnBg : Theme.ToggleOffBg;
        var bgLe = bgGo.AddComponent<LayoutElement>();
        bgLe.minWidth = 60;
        bgLe.preferredWidth = 60;

        // Checkmark text
        var checkGo = new GameObject("Checkmark");
        checkGo.transform.SetParent(bgGo.transform, false);
        var checkRt = checkGo.AddComponent<RectTransform>();
        checkRt.anchorMin = Vector2.zero;
        checkRt.anchorMax = Vector2.one;
        checkRt.offsetMin = Vector2.zero;
        checkRt.offsetMax = Vector2.zero;
        var checkText = checkGo.AddComponent<Text>();
        checkText.text = isOn ? "true" : "false";
        checkText.font = DefaultFont;
        checkText.fontSize = Theme.FontSmall;
        checkText.color = Color.white;
        checkText.alignment = TextAnchor.MiddleCenter;

        toggle.graphic = bgImg;
        toggle.targetGraphic = bgImg;
        toggle.isOn = isOn;

        // Label
        var labelText = CreateText(go.transform, label, Theme.FontSmall, Theme.TextSecondary);
        var labelLe = labelText.GetComponent<LayoutElement>();
        if (labelLe != null) labelLe.flexibleWidth = 1;

        toggle.onValueChanged.AddListener((val) =>
        {
            bgImg.color = val ? Theme.ToggleOnBg : Theme.ToggleOffBg;
            checkText.text = val ? "true" : "false";
            onToggle?.Invoke(val);
        });

        return toggle;
    }

    // ─── Dropdown (enum용) ───

    public static Dropdown CreateDropdown(Transform parent, string[] options, int selectedIndex = 0,
        Action<int> onValueChanged = null)
    {
        var go = new GameObject("Dropdown");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = Theme.InputBg;

        var dropdown = go.AddComponent<Dropdown>();

        // Label (선택된 값 표시)
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        var labelRt = labelGo.AddComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(10, 2);
        labelRt.offsetMax = new Vector2(-30, -2);
        var label = labelGo.AddComponent<Text>();
        label.font = DefaultFont;
        label.fontSize = Theme.FontNormal;
        label.color = Theme.TextPrimary;
        label.alignment = TextAnchor.MiddleLeft;

        // Arrow
        var arrowGo = new GameObject("Arrow");
        arrowGo.transform.SetParent(go.transform, false);
        var arrowRt = arrowGo.AddComponent<RectTransform>();
        arrowRt.anchorMin = new Vector2(1, 0);
        arrowRt.anchorMax = new Vector2(1, 1);
        arrowRt.pivot = new Vector2(1, 0.5f);
        arrowRt.sizeDelta = new Vector2(24, 0);
        arrowRt.anchoredPosition = new Vector2(-6, 0);
        var arrowText = arrowGo.AddComponent<Text>();
        arrowText.text = "\u25BC";
        arrowText.font = DefaultFont;
        arrowText.fontSize = Theme.FontSmall;
        arrowText.color = Theme.TextSecondary;
        arrowText.alignment = TextAnchor.MiddleCenter;

        // Template
        var templateGo = new GameObject("Template");
        templateGo.transform.SetParent(go.transform, false);
        var templateRt = templateGo.AddComponent<RectTransform>();
        templateRt.anchorMin = new Vector2(0, 0);
        templateRt.anchorMax = new Vector2(1, 0);
        templateRt.pivot = new Vector2(0.5f, 1);
        templateRt.anchoredPosition = Vector2.zero;

        var templateImg = templateGo.AddComponent<Image>();
        templateImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);

        var templateSR = templateGo.AddComponent<ScrollRect>();
        templateSR.horizontal = false;
        templateSR.movementType = ScrollRect.MovementType.Clamped;

        // Viewport
        var tViewportGo = new GameObject("Viewport");
        tViewportGo.transform.SetParent(templateGo.transform, false);
        var tViewportRt = tViewportGo.AddComponent<RectTransform>();
        tViewportRt.anchorMin = Vector2.zero;
        tViewportRt.anchorMax = Vector2.one;
        tViewportRt.offsetMin = Vector2.zero;
        tViewportRt.offsetMax = Vector2.zero;
        tViewportGo.AddComponent<Image>().color = Color.white;
        tViewportGo.AddComponent<Mask>().showMaskGraphic = false;
        templateSR.viewport = tViewportRt;

        // Content
        var tContentGo = new GameObject("Content");
        tContentGo.transform.SetParent(tViewportRt, false);
        var tContentRt = tContentGo.AddComponent<RectTransform>();
        tContentRt.anchorMin = new Vector2(0, 1);
        tContentRt.anchorMax = new Vector2(1, 1);
        tContentRt.pivot = new Vector2(0.5f, 1);
        tContentRt.sizeDelta = new Vector2(0, 0);
        templateSR.content = tContentRt;

        // Item
        var itemGo = new GameObject("Item");
        itemGo.transform.SetParent(tContentGo.transform, false);
        var itemRt = itemGo.AddComponent<RectTransform>();
        itemRt.anchorMin = new Vector2(0, 0.5f);
        itemRt.anchorMax = new Vector2(1, 0.5f);
        itemRt.sizeDelta = new Vector2(0, 36);

        var itemToggle = itemGo.AddComponent<Toggle>();

        var itemBgGo = new GameObject("Item Background");
        itemBgGo.transform.SetParent(itemGo.transform, false);
        var itemBgRt = itemBgGo.AddComponent<RectTransform>();
        itemBgRt.anchorMin = Vector2.zero;
        itemBgRt.anchorMax = Vector2.one;
        itemBgRt.offsetMin = Vector2.zero;
        itemBgRt.offsetMax = Vector2.zero;
        var itemBgImg = itemBgGo.AddComponent<Image>();
        itemBgImg.color = new Color(0.3f, 0.3f, 0.35f, 1f);

        var itemCheckGo = new GameObject("Item Checkmark");
        itemCheckGo.transform.SetParent(itemGo.transform, false);
        var itemCheckRt = itemCheckGo.AddComponent<RectTransform>();
        itemCheckRt.anchorMin = new Vector2(0, 0);
        itemCheckRt.anchorMax = new Vector2(0, 1);
        itemCheckRt.pivot = new Vector2(0, 0.5f);
        itemCheckRt.sizeDelta = new Vector2(24, 0);
        itemCheckRt.anchoredPosition = new Vector2(6, 0);
        var itemCheck = itemCheckGo.AddComponent<Text>();
        itemCheck.text = "\u2713";
        itemCheck.font = DefaultFont;
        itemCheck.fontSize = Theme.FontSmall;
        itemCheck.color = Theme.TextAccent;
        itemCheck.alignment = TextAnchor.MiddleCenter;

        var itemLabelGo = new GameObject("Item Label");
        itemLabelGo.transform.SetParent(itemGo.transform, false);
        var itemLabelRt = itemLabelGo.AddComponent<RectTransform>();
        itemLabelRt.anchorMin = Vector2.zero;
        itemLabelRt.anchorMax = Vector2.one;
        itemLabelRt.offsetMin = new Vector2(30, 2);
        itemLabelRt.offsetMax = new Vector2(-10, -2);
        var itemLabel = itemLabelGo.AddComponent<Text>();
        itemLabel.font = DefaultFont;
        itemLabel.fontSize = Theme.FontNormal;
        itemLabel.color = Theme.TextPrimary;
        itemLabel.alignment = TextAnchor.MiddleLeft;

        itemToggle.targetGraphic = itemBgImg;
        itemToggle.graphic = itemCheck;
        itemToggle.isOn = false;

        dropdown.captionText = label;
        dropdown.itemText = itemLabel;
        dropdown.template = templateRt;

        // 템플릿 비표시
        templateGo.SetActive(false);

        // 템플릿 높이 설정
        templateRt.sizeDelta = new Vector2(0, 180);

        // 옵션 설정
        dropdown.ClearOptions();
        var optionList = new System.Collections.Generic.List<Dropdown.OptionData>();
        foreach (var opt in options)
        {
            optionList.Add(new Dropdown.OptionData(opt));
        }
        dropdown.AddOptions(optionList);
        dropdown.value = selectedIndex;
        dropdown.RefreshShownValue();

        // LayoutElement
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = Theme.InputHeight;
        le.preferredHeight = Theme.InputHeight;
        le.flexibleWidth = 1;

        if (onValueChanged != null) dropdown.onValueChanged.AddListener((i) => onValueChanged(i));

        return dropdown;
    }

    // ─── Spacer ───

    public static void CreateSpacer(Transform parent, float height)
    {
        var go = new GameObject("Spacer");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;
    }

    // ─── LayoutElement ───

    /// <summary>
    /// 기존 GameObject에 LayoutElement를 설정합니다.
    /// </summary>
    public static LayoutElement SetLayout(GameObject go, float minWidth = -1, float preferredWidth = -1,
        float flexibleWidth = -1, float minHeight = -1, float preferredHeight = -1, float flexibleHeight = -1)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        if (minWidth >= 0) le.minWidth = minWidth;
        if (preferredWidth >= 0) le.preferredWidth = preferredWidth;
        if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
        if (minHeight >= 0) le.minHeight = minHeight;
        if (preferredHeight >= 0) le.preferredHeight = preferredHeight;
        if (flexibleHeight >= 0) le.flexibleHeight = flexibleHeight;
        return le;
    }

    /// <summary>
    /// RectTransform의 앵커를 전체 화면(stretch)으로 설정합니다.
    /// </summary>
    public static void SetStretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ─── ButtonStyle enum ───

    public enum ButtonStyle
    {
        Normal,
        Danger,
        Accent
    }
}

/// <summary>
/// AIT SafeAreaInsetsGet에서 받은 CSS px 단위의 insets와 devicePixelRatio를 담는 구조체.
/// </summary>
public struct AITSafeAreaInsets
{
    public double TopCss;
    public double BottomCss;
    public double LeftCss;
    public double RightCss;
    public double Dpr;

    public AITSafeAreaInsets(double topCss, double bottomCss, double leftCss, double rightCss, double dpr)
    {
        TopCss = topCss;
        BottomCss = bottomCss;
        LeftCss = leftCss;
        RightCss = rightCss;
        Dpr = dpr;
    }
}

/// <summary>
/// Screen.safeArea 변경을 감시하고 RectTransform 앵커를 업데이트합니다.
/// </summary>
public class SafeAreaUpdater : MonoBehaviour
{
    private RectTransform _rt;
    private Rect _lastSafeArea;
    private bool _useAITInsets;
    private AITSafeAreaInsets _aitInsets;
    private int _lastScreenWidth;
    private int _lastScreenHeight;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    void Update()
    {
        if (_useAITInsets)
        {
            if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
            {
                ApplyAITInsets();
            }
        }
        else if (_lastSafeArea != Screen.safeArea)
        {
            ApplySafeArea();
        }
    }

    /// <summary>
    /// AIT SafeAreaInsetsGet 결과를 적용합니다.
    /// CSS px 단위의 insets를 dpr로 변환하여 Unity 앵커에 반영합니다.
    /// 호출 후 Screen.safeArea 폴링을 중지합니다.
    /// </summary>
    public void SetAITInsets(AITSafeAreaInsets insets)
    {
        _aitInsets = insets;
        _useAITInsets = true;
        ApplyAITInsets(log: true);
    }

    private void ApplyAITInsets(bool log = false)
    {
        if (_rt == null) _rt = GetComponent<RectTransform>();

        float top = (float)(_aitInsets.TopCss * _aitInsets.Dpr);
        float bottom = (float)(_aitInsets.BottomCss * _aitInsets.Dpr);
        float left = (float)(_aitInsets.LeftCss * _aitInsets.Dpr);
        float right = (float)(_aitInsets.RightCss * _aitInsets.Dpr);

        float screenW = Screen.width;
        float screenH = Screen.height;
        if (screenW <= 0 || screenH <= 0) return;

        _rt.anchorMin = new Vector2(left / screenW, bottom / screenH);
        _rt.anchorMax = new Vector2(1f - right / screenW, 1f - top / screenH);

        _lastScreenWidth = (int)screenW;
        _lastScreenHeight = (int)screenH;

        if (log)
        {
            Debug.Log($"[SafeAreaUpdater] Applied AIT insets: top={top}px, bottom={bottom}px, left={left}px, right={right}px (screen={screenW}x{screenH})");
        }
    }

    private void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;
        _lastSafeArea = safeArea;

        if (Screen.width <= 0 || Screen.height <= 0) return;

        Vector2 anchorMin = new Vector2(
            safeArea.x / Screen.width,
            safeArea.y / Screen.height);
        Vector2 anchorMax = new Vector2(
            safeArea.xMax / Screen.width,
            safeArea.yMax / Screen.height);

        _rt.anchorMin = anchorMin;
        _rt.anchorMax = anchorMax;
    }
}
