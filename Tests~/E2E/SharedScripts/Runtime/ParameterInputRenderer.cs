using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AppsInToss;

/// <summary>
/// 파라미터 입력 UI 렌더러
/// 타입별 입력 필드를 IMGUI로 렌더링
/// </summary>
public class ParameterInputRenderer
{
    // 파라미터 입력 상태 (fieldPath -> value)
    private readonly Dictionary<string, string> _stringInputs = new Dictionary<string, string>();
    private readonly Dictionary<string, double> _numberInputs = new Dictionary<string, double>();
    private readonly Dictionary<string, bool> _boolInputs = new Dictionary<string, bool>();
    private readonly Dictionary<string, int> _enumSelectedIndices = new Dictionary<string, int>();
    private readonly Dictionary<string, bool> _nestedFoldouts = new Dictionary<string, bool>();
    private readonly Dictionary<string, bool> _enumDropdownOpen = new Dictionary<string, bool>();
    private readonly Dictionary<string, bool> _isPasting = new Dictionary<string, bool>();

    /// <summary>
    /// 모든 입력 상태 초기화
    /// </summary>
    public void ClearAll()
    {
        _stringInputs.Clear();
        _numberInputs.Clear();
        _boolInputs.Clear();
        _enumSelectedIndices.Clear();
        _nestedFoldouts.Clear();
        _enumDropdownOpen.Clear();
        _isPasting.Clear();
    }

    /// <summary>
    /// 파라미터 타입에 따른 기본값 초기화 (재귀)
    /// </summary>
    public void InitializeDefaults(string basePath, Type type)
    {
        if (type == typeof(string))
        {
            _stringInputs[basePath] = "";
        }
        else if (type == typeof(int) || type == typeof(double) || type == typeof(float))
        {
            _numberInputs[basePath] = 0;
        }
        else if (type == typeof(bool))
        {
            _boolInputs[basePath] = false;
        }
        else if (type.IsEnum)
        {
            _enumSelectedIndices[basePath] = 0;
        }
        else if (type.IsClass && type != typeof(string) && !type.IsArray)
        {
            _nestedFoldouts[basePath] = true;

            var fields = APIParameterInspector.GetPublicFields(type);
            foreach (var field in fields)
            {
                if (APIParameterInspector.IsCallbackField(field)) continue;
                string fieldPath = $"{basePath}.{field.Name}";
                InitializeDefaults(fieldPath, field.FieldType);
            }
        }
    }

    /// <summary>
    /// 입력 상태에서 파라미터 객체 조합 (재귀)
    /// </summary>
    public object BuildParameterObject(string basePath, Type type)
    {
        if (type == typeof(string))
        {
            return _stringInputs.TryGetValue(basePath, out var s) ? s : "";
        }
        if (type == typeof(int))
        {
            return (int)(_numberInputs.TryGetValue(basePath, out var n) ? n : 0);
        }
        if (type == typeof(double))
        {
            return _numberInputs.TryGetValue(basePath, out var n) ? n : 0.0;
        }
        if (type == typeof(float))
        {
            return (float)(_numberInputs.TryGetValue(basePath, out var n) ? n : 0.0);
        }
        if (type == typeof(bool))
        {
            return _boolInputs.TryGetValue(basePath, out var b) ? b : false;
        }

        if (type.IsEnum)
        {
            var index = _enumSelectedIndices.TryGetValue(basePath, out var i) ? i : 0;
            return APIParameterInspector.GetEnumValueByIndex(type, index);
        }

        if (type.IsClass && type != typeof(string) && !type.IsArray)
        {
            var obj = Activator.CreateInstance(type);
            var fields = APIParameterInspector.GetPublicFields(type);

            foreach (var field in fields)
            {
                if (APIParameterInspector.IsCallbackField(field)) continue;

                string fieldPath = $"{basePath}.{field.Name}";
                var value = BuildParameterObject(fieldPath, field.FieldType);
                field.SetValue(obj, value);
            }

            return obj;
        }

        return null;
    }

    /// <summary>
    /// 타입에 따른 적절한 입력 UI 렌더링 (재귀)
    /// </summary>
    public void DrawParameterField(string fieldPath, Type type, int indentLevel)
    {
        string displayName = GetDisplayName(fieldPath);

        if (type.IsEnum)
        {
            DrawEnumSelector(fieldPath, type, displayName, indentLevel);
            return;
        }

        if (type == typeof(string))
        {
            DrawStringField(fieldPath, displayName, indentLevel);
            return;
        }

        if (type == typeof(int) || type == typeof(double) || type == typeof(float))
        {
            DrawNumberField(fieldPath, displayName, indentLevel);
            return;
        }

        if (type == typeof(bool))
        {
            DrawBoolField(fieldPath, displayName, indentLevel);
            return;
        }

        if (type.IsClass && type != typeof(string) && !type.IsArray)
        {
            DrawNestedObject(fieldPath, type, displayName, indentLevel);
            return;
        }

        // 기타 타입 (폴백)
        GUILayout.BeginHorizontal();
        GUILayout.Space(indentLevel * 20);
        GUILayout.Label($"{displayName}: (지원하지 않는 타입: {type.Name})", InteractiveAPITesterStyles.CallbackLabelStyle);
        GUILayout.EndHorizontal();
    }

    private string GetDisplayName(string fieldPath)
    {
        int lastDot = fieldPath.LastIndexOf('.');
        return lastDot >= 0 ? fieldPath.Substring(lastDot + 1) : fieldPath;
    }

    /// <summary>
    /// Enum 드롭다운 UI
    /// </summary>
    private void DrawEnumSelector(string fieldPath, Type enumType, string displayName, int indentLevel)
    {
        var enumNames = APIParameterInspector.GetEnumNames(enumType);

        if (!_enumSelectedIndices.TryGetValue(fieldPath, out int selectedIndex))
        {
            selectedIndex = 0;
            _enumSelectedIndices[fieldPath] = selectedIndex;
        }

        if (!_enumDropdownOpen.TryGetValue(fieldPath, out bool isOpen))
        {
            isOpen = false;
            _enumDropdownOpen[fieldPath] = isOpen;
        }

        GUILayout.BeginVertical();

        GUILayout.BeginHorizontal();
        GUILayout.Space(indentLevel * 20);
        GUILayout.Label($"{displayName}:", InteractiveAPITesterStyles.FieldLabelStyle, GUILayout.Width(120));

        string buttonLabel = isOpen ? $"▲ {enumNames[selectedIndex]}" : $"▼ {enumNames[selectedIndex]}";
        if (GUILayout.Button(buttonLabel, InteractiveAPITesterStyles.EnumButtonStyle, GUILayout.Height(36), GUILayout.ExpandWidth(true)))
        {
            _enumDropdownOpen[fieldPath] = !isOpen;
        }
        GUILayout.EndHorizontal();

        if (isOpen)
        {
            for (int i = 0; i < enumNames.Length; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(indentLevel * 20 + 120);

                string optionLabel = i == selectedIndex ? $"✓ {enumNames[i]}" : $"   {enumNames[i]}";
                if (GUILayout.Button(optionLabel, InteractiveAPITesterStyles.EnumOptionStyle, GUILayout.Height(32)))
                {
                    _enumSelectedIndices[fieldPath] = i;
                    _enumDropdownOpen[fieldPath] = false;
                }
                GUILayout.EndHorizontal();
            }
        }

        GUILayout.EndVertical();
        GUILayout.Space(4);
    }

    /// <summary>
    /// String 입력 UI
    /// </summary>
    private void DrawStringField(string fieldPath, string displayName, int indentLevel)
    {
        if (!_stringInputs.TryGetValue(fieldPath, out string value))
        {
            value = "";
            _stringInputs[fieldPath] = value;
        }

        if (!_isPasting.TryGetValue(fieldPath, out bool isPasting))
        {
            isPasting = false;
            _isPasting[fieldPath] = isPasting;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Space(indentLevel * 20);
        GUILayout.Label($"{displayName}:", InteractiveAPITesterStyles.FieldLabelStyle, GUILayout.Width(120));
        _stringInputs[fieldPath] = GUILayout.TextField(value, InteractiveAPITesterStyles.TextFieldStyle, GUILayout.Height(36), GUILayout.ExpandWidth(true));

        // PASTE 버튼
        GUI.enabled = !isPasting;
        if (GUILayout.Button(isPasting ? "..." : "PASTE", InteractiveAPITesterStyles.ButtonStyle, GUILayout.Width(70), GUILayout.Height(36)))
        {
            PasteFromClipboard(fieldPath);
        }
        GUI.enabled = true;

        GUILayout.EndHorizontal();
        GUILayout.Space(4);
    }

    /// <summary>
    /// 클립보드에서 문자열 붙여넣기
    /// </summary>
    private async void PasteFromClipboard(string fieldPath)
    {
        _isPasting[fieldPath] = true;
        try
        {
            string text = await AIT.GetClipboardText();
            if (!string.IsNullOrEmpty(text))
            {
                _stringInputs[fieldPath] = text.Trim();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ParameterInputRenderer] Clipboard read failed: {ex.Message}");
        }
        finally
        {
            _isPasting[fieldPath] = false;
        }
    }

    /// <summary>
    /// Number 입력 UI
    /// </summary>
    private void DrawNumberField(string fieldPath, string displayName, int indentLevel)
    {
        if (!_numberInputs.TryGetValue(fieldPath, out double value))
        {
            value = 0;
            _numberInputs[fieldPath] = value;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Space(indentLevel * 20);
        GUILayout.Label($"{displayName}:", InteractiveAPITesterStyles.FieldLabelStyle, GUILayout.Width(120));

        string strValue = value.ToString();
        string newStrValue = GUILayout.TextField(strValue, InteractiveAPITesterStyles.TextFieldStyle, GUILayout.Height(36), GUILayout.ExpandWidth(true));

        if (newStrValue != strValue)
        {
            if (double.TryParse(newStrValue, out double newValue))
            {
                _numberInputs[fieldPath] = newValue;
            }
            else if (string.IsNullOrEmpty(newStrValue))
            {
                _numberInputs[fieldPath] = 0;
            }
        }

        GUILayout.EndHorizontal();
        GUILayout.Space(4);
    }

    /// <summary>
    /// Bool 토글 UI
    /// </summary>
    private void DrawBoolField(string fieldPath, string displayName, int indentLevel)
    {
        if (!_boolInputs.TryGetValue(fieldPath, out bool value))
        {
            value = false;
            _boolInputs[fieldPath] = value;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Space(indentLevel * 20);
        GUILayout.Label($"{displayName}:", InteractiveAPITesterStyles.FieldLabelStyle, GUILayout.Width(120));

        string btnLabel = value ? "✓ true" : "✗ false";
        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = value ? new Color(0.4f, 0.7f, 0.4f) : new Color(0.5f, 0.5f, 0.5f);

        if (GUILayout.Button(btnLabel, InteractiveAPITesterStyles.ToggleButtonStyle, GUILayout.Height(36), GUILayout.Width(100)))
        {
            _boolInputs[fieldPath] = !value;
        }

        GUI.backgroundColor = originalColor;
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
    }

    /// <summary>
    /// 중첩 객체 UI (접기/펼치기 지원)
    /// </summary>
    private void DrawNestedObject(string fieldPath, Type type, string displayName, int indentLevel)
    {
        var fields = APIParameterInspector.GetPublicFields(type);

        bool hasEditableFields = fields.Any(f => !APIParameterInspector.IsCallbackField(f));

        if (!hasEditableFields)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(indentLevel * 20);
            GUILayout.Label($"{displayName}: (콜백 전용 - 편집 불가)", InteractiveAPITesterStyles.CallbackLabelStyle);
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
            return;
        }

        if (!_nestedFoldouts.TryGetValue(fieldPath, out bool isExpanded))
        {
            isExpanded = true;
            _nestedFoldouts[fieldPath] = isExpanded;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Space(indentLevel * 20);

        string icon = isExpanded ? "▼" : "▶";
        if (GUILayout.Button($"{icon} {displayName} ({type.Name})", InteractiveAPITesterStyles.NestedHeaderStyle, GUILayout.Height(36), GUILayout.ExpandWidth(true)))
        {
            _nestedFoldouts[fieldPath] = !isExpanded;
        }

        GUILayout.EndHorizontal();

        if (isExpanded)
        {
            foreach (var field in fields)
            {
                if (APIParameterInspector.IsCallbackField(field))
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space((indentLevel + 1) * 20);
                    GUILayout.Label($"{field.Name}: (콜백 - 편집 불가)", InteractiveAPITesterStyles.CallbackLabelStyle);
                    GUILayout.EndHorizontal();
                    GUILayout.Space(2);
                    continue;
                }

                string nestedPath = $"{fieldPath}.{field.Name}";
                DrawParameterField(nestedPath, field.FieldType, indentLevel + 1);
            }
        }

        GUILayout.Space(4);
    }
}
