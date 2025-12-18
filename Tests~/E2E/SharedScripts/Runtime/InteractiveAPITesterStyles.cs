using UnityEngine;

/// <summary>
/// InteractiveAPITester용 UI 스타일 및 유틸리티 클래스
/// GUIStyle 초기화와 공통 GUI 헬퍼 메서드를 제공
/// </summary>
public static class InteractiveAPITesterStyles
{
    // GUIStyle 필드 (18개)
    public static GUIStyle BoxStyle;
    public static GUIStyle ButtonStyle;
    public static GUIStyle ApiButtonStyle;
    public static GUIStyle GroupHeaderStyle;
    public static GUIStyle LabelStyle;
    public static GUIStyle TextAreaStyle;
    public static GUIStyle TextFieldStyle;
    public static GUIStyle HeaderStyle;
    public static GUIStyle SearchBoxStyle;
    public static GUIStyle NestedHeaderStyle;
    public static GUIStyle EnumButtonStyle;
    public static GUIStyle EnumOptionStyle;
    public static GUIStyle FieldLabelStyle;
    public static GUIStyle ResultKeyStyle;
    public static GUIStyle ResultValueStyle;
    public static GUIStyle CallbackLabelStyle;
    public static GUIStyle ToggleButtonStyle;
    public static GUIStyle DangerButtonStyle;

    // 상태 필드
    private static bool _initialized = false;
    private static Font _koreanFont;

    /// <summary>
    /// 스타일이 초기화되었는지 여부
    /// </summary>
    public static bool IsInitialized => _initialized;

    /// <summary>
    /// 스타일을 초기화합니다. OnGUI 내에서 호출해야 합니다.
    /// </summary>
    /// <param name="koreanFont">한글 폰트 (선택)</param>
    public static void Initialize(Font koreanFont = null)
    {
        if (_initialized) return;

        _koreanFont = koreanFont;

        BoxStyle = new GUIStyle(GUI.skin.box);
        BoxStyle.padding = new RectOffset(10, 10, 10, 10);
        BoxStyle.normal.background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.1f, 0.95f));

        // 기본 버튼 스타일
        ButtonStyle = new GUIStyle(GUI.skin.button);
        ButtonStyle.fontSize = 14;
        ButtonStyle.padding = new RectOffset(10, 10, 8, 8);
        ButtonStyle.margin = new RectOffset(4, 4, 4, 4);
        if (_koreanFont != null) ButtonStyle.font = _koreanFont;

        // API 버튼 스타일 (너비 제한, 높이 증가)
        ApiButtonStyle = new GUIStyle(GUI.skin.button);
        ApiButtonStyle.fontSize = 15;
        ApiButtonStyle.fontStyle = FontStyle.Normal;
        ApiButtonStyle.padding = new RectOffset(15, 15, 12, 12);
        ApiButtonStyle.margin = new RectOffset(4, 4, 3, 3);
        ApiButtonStyle.alignment = TextAnchor.MiddleLeft;
        if (_koreanFont != null) ApiButtonStyle.font = _koreanFont;

        // 그룹 헤더 스타일
        GroupHeaderStyle = new GUIStyle(GUI.skin.button);
        GroupHeaderStyle.fontSize = 16;
        GroupHeaderStyle.fontStyle = FontStyle.Bold;
        GroupHeaderStyle.padding = new RectOffset(12, 12, 10, 10);
        GroupHeaderStyle.margin = new RectOffset(0, 0, 8, 4);
        GroupHeaderStyle.alignment = TextAnchor.MiddleLeft;
        GroupHeaderStyle.normal.textColor = new Color(0.4f, 0.8f, 1f);
        if (_koreanFont != null) GroupHeaderStyle.font = _koreanFont;

        LabelStyle = new GUIStyle(GUI.skin.label);
        LabelStyle.fontSize = 12;
        LabelStyle.wordWrap = true;
        if (_koreanFont != null) LabelStyle.font = _koreanFont;

        TextAreaStyle = new GUIStyle(GUI.skin.textArea);
        TextAreaStyle.fontSize = 12;
        TextAreaStyle.padding = new RectOffset(5, 5, 5, 5);
        TextAreaStyle.wordWrap = true;
        if (_koreanFont != null) TextAreaStyle.font = _koreanFont;

        HeaderStyle = new GUIStyle(GUI.skin.label);
        HeaderStyle.fontSize = 20;
        HeaderStyle.fontStyle = FontStyle.Bold;
        HeaderStyle.alignment = TextAnchor.MiddleCenter;
        HeaderStyle.margin = new RectOffset(0, 0, 10, 5);
        if (_koreanFont != null) HeaderStyle.font = _koreanFont;

        // 검색 입력 필드 스타일
        TextFieldStyle = new GUIStyle(GUI.skin.textField);
        TextFieldStyle.fontSize = 16;
        TextFieldStyle.padding = new RectOffset(12, 12, 10, 10);
        TextFieldStyle.margin = new RectOffset(0, 0, 5, 10);
        if (_koreanFont != null) TextFieldStyle.font = _koreanFont;

        // 검색 박스 배경 스타일
        SearchBoxStyle = new GUIStyle(GUI.skin.box);
        SearchBoxStyle.padding = new RectOffset(10, 10, 8, 8);
        SearchBoxStyle.margin = new RectOffset(0, 0, 0, 5);
        SearchBoxStyle.normal.background = MakeTex(2, 2, new Color(0.15f, 0.15f, 0.2f, 0.95f));

        // 중첩 객체 헤더 스타일
        NestedHeaderStyle = new GUIStyle(GUI.skin.button);
        NestedHeaderStyle.fontSize = 14;
        NestedHeaderStyle.fontStyle = FontStyle.Bold;
        NestedHeaderStyle.padding = new RectOffset(10, 10, 8, 8);
        NestedHeaderStyle.margin = new RectOffset(0, 0, 4, 4);
        NestedHeaderStyle.alignment = TextAnchor.MiddleLeft;
        NestedHeaderStyle.normal.textColor = new Color(0.6f, 0.9f, 0.6f);
        if (_koreanFont != null) NestedHeaderStyle.font = _koreanFont;

        // Enum 버튼 스타일 (현재 선택값 표시)
        EnumButtonStyle = new GUIStyle(GUI.skin.button);
        EnumButtonStyle.fontSize = 14;
        EnumButtonStyle.padding = new RectOffset(12, 12, 8, 8);
        EnumButtonStyle.alignment = TextAnchor.MiddleLeft;
        EnumButtonStyle.normal.textColor = new Color(0.9f, 0.9f, 0.5f);
        if (_koreanFont != null) EnumButtonStyle.font = _koreanFont;

        // Enum 옵션 스타일
        EnumOptionStyle = new GUIStyle(GUI.skin.button);
        EnumOptionStyle.fontSize = 13;
        EnumOptionStyle.padding = new RectOffset(20, 10, 6, 6);
        EnumOptionStyle.margin = new RectOffset(0, 0, 1, 1);
        EnumOptionStyle.alignment = TextAnchor.MiddleLeft;
        if (_koreanFont != null) EnumOptionStyle.font = _koreanFont;

        // 필드 라벨 스타일
        FieldLabelStyle = new GUIStyle(GUI.skin.label);
        FieldLabelStyle.fontSize = 13;
        FieldLabelStyle.fontStyle = FontStyle.Normal;
        FieldLabelStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
        if (_koreanFont != null) FieldLabelStyle.font = _koreanFont;

        // 결과 키 스타일
        ResultKeyStyle = new GUIStyle(GUI.skin.label);
        ResultKeyStyle.fontSize = 13;
        ResultKeyStyle.fontStyle = FontStyle.Bold;
        ResultKeyStyle.normal.textColor = new Color(0.7f, 0.85f, 1f);
        if (_koreanFont != null) ResultKeyStyle.font = _koreanFont;

        // 결과 값 스타일
        ResultValueStyle = new GUIStyle(GUI.skin.label);
        ResultValueStyle.fontSize = 13;
        ResultValueStyle.wordWrap = true;
        ResultValueStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        if (_koreanFont != null) ResultValueStyle.font = _koreanFont;

        // 콜백 필드 라벨 스타일
        CallbackLabelStyle = new GUIStyle(GUI.skin.label);
        CallbackLabelStyle.fontSize = 12;
        CallbackLabelStyle.fontStyle = FontStyle.Italic;
        CallbackLabelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
        if (_koreanFont != null) CallbackLabelStyle.font = _koreanFont;

        // 토글 버튼 스타일
        ToggleButtonStyle = new GUIStyle(GUI.skin.button);
        ToggleButtonStyle.fontSize = 13;
        ToggleButtonStyle.padding = new RectOffset(10, 10, 6, 6);
        if (_koreanFont != null) ToggleButtonStyle.font = _koreanFont;

        // 위험 버튼 스타일 (OOM 테스트용)
        DangerButtonStyle = new GUIStyle(GUI.skin.button);
        DangerButtonStyle.fontSize = 14;
        DangerButtonStyle.fontStyle = FontStyle.Bold;
        DangerButtonStyle.padding = new RectOffset(15, 15, 12, 12);
        DangerButtonStyle.margin = new RectOffset(4, 4, 8, 8);
        DangerButtonStyle.normal.textColor = Color.white;
        DangerButtonStyle.normal.background = MakeTex(2, 2, new Color(0.8f, 0.2f, 0.2f, 1f));
        DangerButtonStyle.hover.background = MakeTex(2, 2, new Color(0.9f, 0.3f, 0.3f, 1f));
        DangerButtonStyle.active.background = MakeTex(2, 2, new Color(0.6f, 0.1f, 0.1f, 1f));
        if (_koreanFont != null) DangerButtonStyle.font = _koreanFont;

        _initialized = true;
    }

    /// <summary>
    /// 단색 텍스처를 생성합니다.
    /// </summary>
    public static Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}
