using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using AppsInToss;

/// <summary>
/// 대화형 SDK API 테스터 - 사용자가 API를 선택하고 파라미터를 입력하여 실행할 수 있는 UI 제공
/// Unity IMGUI를 통해 API를 테스트하고 결과를 확인할 수 있음
/// 카테고리별 그룹핑과 접기/펼치기 기능 지원
/// </summary>
public class InteractiveAPITester : MonoBehaviour
{
    // UI 상태
    private enum UIState
    {
        APIList,        // API 목록 표시
        ParameterInput, // 파라미터 입력
        Result          // 결과 표시
    }

    // 결과 표시 모드
    private enum ResultDisplayMode
    {
        Structured,     // 구조화 표시
        RawJson         // JSON 표시
    }

    private UIState currentState = UIState.APIList;
    private List<APIMethodInfo> allMethods;
    private Dictionary<string, List<APIMethodInfo>> groupedMethods;
    private Dictionary<string, bool> groupFoldouts = new Dictionary<string, bool>();
    private APIMethodInfo selectedMethod;

    // 검색 관련
    private string searchQuery = "";
    private List<APIMethodInfo> searchResults = new List<APIMethodInfo>();
    private bool isSearchMode = false;
    private string lastResult = "";
    private bool lastResultSuccess = true;
    private object lastResultObject = null;
    private ResultDisplayMode resultDisplayMode = ResultDisplayMode.Structured;
    private Vector2 scrollPosition = Vector2.zero;

    // 터치 스크롤 지원
    private bool isTouchScrolling = false;
    private Vector2 lastTouchPosition;
    private Vector2 scrollVelocity = Vector2.zero;
    private float scrollMomentumDecay = 0.95f;
    private Rect currentScrollViewRect;

    // 파라미터 입력 상태 (fieldPath -> value)
    private Dictionary<string, string> stringInputs = new Dictionary<string, string>();
    private Dictionary<string, double> numberInputs = new Dictionary<string, double>();
    private Dictionary<string, bool> boolInputs = new Dictionary<string, bool>();
    private Dictionary<string, int> enumSelectedIndices = new Dictionary<string, int>();
    private Dictionary<string, bool> nestedFoldouts = new Dictionary<string, bool>();
    private Dictionary<string, bool> enumDropdownOpen = new Dictionary<string, bool>();

    // UI 스타일
    private GUIStyle boxStyle;
    private GUIStyle buttonStyle;
    private GUIStyle apiButtonStyle;
    private GUIStyle groupHeaderStyle;
    private GUIStyle labelStyle;
    private GUIStyle textAreaStyle;
    private GUIStyle textFieldStyle;
    private GUIStyle headerStyle;
    private GUIStyle searchBoxStyle;
    private GUIStyle nestedHeaderStyle;
    private GUIStyle enumButtonStyle;
    private GUIStyle enumOptionStyle;
    private GUIStyle fieldLabelStyle;
    private GUIStyle resultKeyStyle;
    private GUIStyle resultValueStyle;
    private GUIStyle callbackLabelStyle;
    private GUIStyle toggleButtonStyle;
    private bool stylesInitialized = false;

    // 한글 폰트
    private Font koreanFont;

    void Start()
    {
        Debug.Log("[InteractiveAPITester] Loading SDK APIs...");

        // 한글 폰트 로드 (Noto Sans KR - SIL OFL License)
        koreanFont = Resources.Load<Font>("Fonts/NotoSansKR-Regular");
        if (koreanFont != null)
        {
            Debug.Log("[InteractiveAPITester] Korean font (Noto Sans KR) loaded successfully");
        }
        else
        {
            Debug.LogWarning("[InteractiveAPITester] Korean font not found, using default font");
        }

        allMethods = APIParameterInspector.GetAllAPIMethods();
        groupedMethods = APIParameterInspector.GroupByCategory(allMethods);

        // 첫 번째 그룹만 펼치고 나머지는 접기
        bool isFirst = true;
        foreach (var category in groupedMethods.Keys)
        {
            groupFoldouts[category] = isFirst;
            isFirst = false;
        }

        Debug.Log($"[InteractiveAPITester] Found {allMethods.Count} API methods in {groupedMethods.Count} categories");
    }

    void Update()
    {
        HandleTouchScroll();
        ApplyScrollMomentum();
    }

    /// <summary>
    /// 터치 스크롤 처리
    /// </summary>
    private void HandleTouchScroll()
    {
        // 터치 입력 처리
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    // 스크롤 영역 내에서 터치 시작했는지 확인
                    Vector2 touchPos = new Vector2(touch.position.x, Screen.height - touch.position.y);
                    if (currentScrollViewRect.Contains(touchPos))
                    {
                        isTouchScrolling = true;
                        lastTouchPosition = touch.position;
                        scrollVelocity = Vector2.zero;
                    }
                    break;

                case TouchPhase.Moved:
                    if (isTouchScrolling)
                    {
                        Vector2 delta = touch.position - lastTouchPosition;
                        // 위로 스와이프하면 (delta.y > 0) 컨텐츠가 위로 올라감 (scrollPosition.y 증가)
                        // 아래로 스와이프하면 (delta.y < 0) 컨텐츠가 아래로 내려감 (scrollPosition.y 감소)
                        scrollPosition.y += delta.y;
                        scrollPosition.x += delta.x;

                        // 스크롤 범위 제한
                        scrollPosition.y = Mathf.Max(0, scrollPosition.y);
                        scrollPosition.x = Mathf.Max(0, scrollPosition.x);

                        // 속도 계산 (관성용)
                        scrollVelocity = new Vector2(delta.x, delta.y) / Time.deltaTime * 0.1f;
                        lastTouchPosition = touch.position;
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    isTouchScrolling = false;
                    break;
            }
        }
        // 마우스 드래그 지원 (WebGL 데스크톱 테스트용)
        else if (Input.GetMouseButton(0))
        {
            Vector2 mousePos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);

            if (Input.GetMouseButtonDown(0))
            {
                if (currentScrollViewRect.Contains(mousePos))
                {
                    isTouchScrolling = true;
                    lastTouchPosition = Input.mousePosition;
                    scrollVelocity = Vector2.zero;
                }
            }
            else if (isTouchScrolling)
            {
                Vector2 delta = (Vector2)Input.mousePosition - lastTouchPosition;
                // 위로 드래그하면 (delta.y > 0) 컨텐츠가 위로 올라감 (scrollPosition.y 증가)
                scrollPosition.y += delta.y;
                scrollPosition.x += delta.x;

                scrollPosition.y = Mathf.Max(0, scrollPosition.y);
                scrollPosition.x = Mathf.Max(0, scrollPosition.x);

                scrollVelocity = new Vector2(delta.x, delta.y) / Time.deltaTime * 0.1f;
                lastTouchPosition = Input.mousePosition;
            }
        }
        else
        {
            if (isTouchScrolling)
            {
                isTouchScrolling = false;
            }
        }
    }

    /// <summary>
    /// 스크롤 관성 적용
    /// </summary>
    private void ApplyScrollMomentum()
    {
        if (!isTouchScrolling && scrollVelocity.sqrMagnitude > 0.01f)
        {
            scrollPosition += scrollVelocity * Time.deltaTime;
            scrollPosition.y = Mathf.Max(0, scrollPosition.y);
            scrollPosition.x = Mathf.Max(0, scrollPosition.x);
            scrollVelocity *= scrollMomentumDecay;

            if (scrollVelocity.sqrMagnitude < 0.01f)
            {
                scrollVelocity = Vector2.zero;
            }
        }
    }

    void OnGUI()
    {
        InitializeStyles();

        // 메인 컨테이너 - 전체 화면 사용
        GUILayout.BeginArea(new Rect(0, 0, Screen.width, Screen.height));
        GUILayout.BeginVertical(boxStyle);

        switch (currentState)
        {
            case UIState.APIList:
                DrawAPIList();
                break;
            case UIState.ParameterInput:
                DrawParameterInput();
                break;
            case UIState.Result:
                DrawResult();
                break;
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void InitializeStyles()
    {
        if (stylesInitialized) return;

        boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.padding = new RectOffset(10, 10, 10, 10);
        boxStyle.normal.background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.1f, 0.95f));

        // 기본 버튼 스타일
        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 14;
        buttonStyle.padding = new RectOffset(10, 10, 8, 8);
        buttonStyle.margin = new RectOffset(4, 4, 4, 4);
        if (koreanFont != null) buttonStyle.font = koreanFont;

        // API 버튼 스타일 (너비 제한, 높이 증가)
        apiButtonStyle = new GUIStyle(GUI.skin.button);
        apiButtonStyle.fontSize = 15;
        apiButtonStyle.fontStyle = FontStyle.Normal;
        apiButtonStyle.padding = new RectOffset(15, 15, 12, 12);
        apiButtonStyle.margin = new RectOffset(4, 4, 3, 3);
        apiButtonStyle.alignment = TextAnchor.MiddleLeft;
        if (koreanFont != null) apiButtonStyle.font = koreanFont;

        // 그룹 헤더 스타일
        groupHeaderStyle = new GUIStyle(GUI.skin.button);
        groupHeaderStyle.fontSize = 16;
        groupHeaderStyle.fontStyle = FontStyle.Bold;
        groupHeaderStyle.padding = new RectOffset(12, 12, 10, 10);
        groupHeaderStyle.margin = new RectOffset(0, 0, 8, 4);
        groupHeaderStyle.alignment = TextAnchor.MiddleLeft;
        groupHeaderStyle.normal.textColor = new Color(0.4f, 0.8f, 1f);
        if (koreanFont != null) groupHeaderStyle.font = koreanFont;

        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 12;
        labelStyle.wordWrap = true;
        if (koreanFont != null) labelStyle.font = koreanFont;

        textAreaStyle = new GUIStyle(GUI.skin.textArea);
        textAreaStyle.fontSize = 12;
        textAreaStyle.padding = new RectOffset(5, 5, 5, 5);
        textAreaStyle.wordWrap = true;
        if (koreanFont != null) textAreaStyle.font = koreanFont;

        headerStyle = new GUIStyle(GUI.skin.label);
        headerStyle.fontSize = 20;
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.alignment = TextAnchor.MiddleCenter;
        headerStyle.margin = new RectOffset(0, 0, 10, 5);
        if (koreanFont != null) headerStyle.font = koreanFont;

        // 검색 입력 필드 스타일
        textFieldStyle = new GUIStyle(GUI.skin.textField);
        textFieldStyle.fontSize = 16;
        textFieldStyle.padding = new RectOffset(12, 12, 10, 10);
        textFieldStyle.margin = new RectOffset(0, 0, 5, 10);
        if (koreanFont != null) textFieldStyle.font = koreanFont;

        // 검색 박스 배경 스타일
        searchBoxStyle = new GUIStyle(GUI.skin.box);
        searchBoxStyle.padding = new RectOffset(10, 10, 8, 8);
        searchBoxStyle.margin = new RectOffset(0, 0, 0, 5);
        searchBoxStyle.normal.background = MakeTex(2, 2, new Color(0.15f, 0.15f, 0.2f, 0.95f));

        // 중첩 객체 헤더 스타일
        nestedHeaderStyle = new GUIStyle(GUI.skin.button);
        nestedHeaderStyle.fontSize = 14;
        nestedHeaderStyle.fontStyle = FontStyle.Bold;
        nestedHeaderStyle.padding = new RectOffset(10, 10, 8, 8);
        nestedHeaderStyle.margin = new RectOffset(0, 0, 4, 4);
        nestedHeaderStyle.alignment = TextAnchor.MiddleLeft;
        nestedHeaderStyle.normal.textColor = new Color(0.6f, 0.9f, 0.6f);
        if (koreanFont != null) nestedHeaderStyle.font = koreanFont;

        // Enum 버튼 스타일 (현재 선택값 표시)
        enumButtonStyle = new GUIStyle(GUI.skin.button);
        enumButtonStyle.fontSize = 14;
        enumButtonStyle.padding = new RectOffset(12, 12, 8, 8);
        enumButtonStyle.alignment = TextAnchor.MiddleLeft;
        enumButtonStyle.normal.textColor = new Color(0.9f, 0.9f, 0.5f);
        if (koreanFont != null) enumButtonStyle.font = koreanFont;

        // Enum 옵션 스타일
        enumOptionStyle = new GUIStyle(GUI.skin.button);
        enumOptionStyle.fontSize = 13;
        enumOptionStyle.padding = new RectOffset(20, 10, 6, 6);
        enumOptionStyle.margin = new RectOffset(0, 0, 1, 1);
        enumOptionStyle.alignment = TextAnchor.MiddleLeft;
        if (koreanFont != null) enumOptionStyle.font = koreanFont;

        // 필드 라벨 스타일
        fieldLabelStyle = new GUIStyle(GUI.skin.label);
        fieldLabelStyle.fontSize = 13;
        fieldLabelStyle.fontStyle = FontStyle.Normal;
        fieldLabelStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
        if (koreanFont != null) fieldLabelStyle.font = koreanFont;

        // 결과 키 스타일
        resultKeyStyle = new GUIStyle(GUI.skin.label);
        resultKeyStyle.fontSize = 13;
        resultKeyStyle.fontStyle = FontStyle.Bold;
        resultKeyStyle.normal.textColor = new Color(0.7f, 0.85f, 1f);
        if (koreanFont != null) resultKeyStyle.font = koreanFont;

        // 결과 값 스타일
        resultValueStyle = new GUIStyle(GUI.skin.label);
        resultValueStyle.fontSize = 13;
        resultValueStyle.wordWrap = true;
        resultValueStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        if (koreanFont != null) resultValueStyle.font = koreanFont;

        // 콜백 필드 라벨 스타일
        callbackLabelStyle = new GUIStyle(GUI.skin.label);
        callbackLabelStyle.fontSize = 12;
        callbackLabelStyle.fontStyle = FontStyle.Italic;
        callbackLabelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
        if (koreanFont != null) callbackLabelStyle.font = koreanFont;

        // 토글 버튼 스타일
        toggleButtonStyle = new GUIStyle(GUI.skin.button);
        toggleButtonStyle.fontSize = 13;
        toggleButtonStyle.padding = new RectOffset(10, 10, 6, 6);
        if (koreanFont != null) toggleButtonStyle.font = koreanFont;

        stylesInitialized = true;
    }

    private void DrawAPIList()
    {
        // 검색창 (상단 고정)
        DrawSearchBox();

        // 스크롤뷰 - 세로 스크롤만 활성화, 가로 스크롤 비활성화
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true, GUILayout.ExpandHeight(true));
        // 터치 스크롤을 위한 영역 저장 (대략적인 스크롤 영역)
        currentScrollViewRect = new Rect(0, 100, Screen.width, Screen.height - 100);

        if (isSearchMode && !string.IsNullOrEmpty(searchQuery))
        {
            // 검색 모드: 검색 결과만 표시
            DrawSearchResults();
        }
        else
        {
            // 일반 모드: 카테고리별 그룹 표시
            foreach (var group in groupedMethods)
            {
                string category = group.Key;
                var methods = group.Value;

                // 그룹 헤더 (접기/펼치기 가능)
                DrawGroupHeader(category, methods.Count);

                // 그룹이 펼쳐져 있으면 API 버튼들 표시
                if (groupFoldouts.ContainsKey(category) && groupFoldouts[category])
                {
                    foreach (var method in methods)
                    {
                        DrawAPIButton(method);
                    }
                    GUILayout.Space(5);
                }
            }
        }

        GUILayout.EndScrollView();
    }

    private void DrawSearchBox()
    {
        GUILayout.BeginVertical(searchBoxStyle);

        GUILayout.BeginHorizontal();

        // 검색 아이콘/레이블
        GUILayout.Label("🔍", labelStyle, GUILayout.Width(24));

        // 검색 입력 필드
        string newQuery = GUILayout.TextField(searchQuery, textFieldStyle, GUILayout.Height(36), GUILayout.ExpandWidth(true));

        // 검색어가 변경되면 검색 수행
        if (newQuery != searchQuery)
        {
            searchQuery = newQuery;
            UpdateSearchResults();
        }

        // 검색어 지우기 버튼
        if (!string.IsNullOrEmpty(searchQuery))
        {
            if (GUILayout.Button("✕", buttonStyle, GUILayout.Width(40), GUILayout.Height(36)))
            {
                searchQuery = "";
                searchResults.Clear();
                isSearchMode = false;
            }
        }

        GUILayout.EndHorizontal();

        // 검색 결과 개수 표시
        if (isSearchMode && !string.IsNullOrEmpty(searchQuery))
        {
            GUILayout.Label($"검색 결과: {searchResults.Count}개", labelStyle);
        }

        GUILayout.EndVertical();
    }

    private void UpdateSearchResults()
    {
        if (string.IsNullOrEmpty(searchQuery))
        {
            searchResults.Clear();
            isSearchMode = false;
            return;
        }

        isSearchMode = true;
        searchResults.Clear();

        string queryLower = searchQuery.ToLower();

        // 모든 API에 대해 fuzzy matching 수행
        var scoredResults = new List<(APIMethodInfo method, int score)>();

        foreach (var method in allMethods)
        {
            int score = CalculateFuzzyScore(method.Name.ToLower(), queryLower);

            // 카테고리에서도 검색
            if (method.Category != null)
            {
                int categoryScore = CalculateFuzzyScore(method.Category.ToLower(), queryLower);
                score = Math.Max(score, categoryScore / 2); // 카테고리 매치는 절반 점수
            }

            if (score > 0)
            {
                scoredResults.Add((method, score));
            }
        }

        // 점수 높은 순으로 정렬
        scoredResults.Sort((a, b) => b.score.CompareTo(a.score));

        // 상위 결과만 사용
        foreach (var (method, score) in scoredResults)
        {
            searchResults.Add(method);
        }
    }

    /// <summary>
    /// Fuzzy matching 점수 계산
    /// - 정확히 일치: 가장 높은 점수
    /// - 접두사 일치: 높은 점수
    /// - 연속 문자 일치: 중간 점수
    /// - 개별 문자 순서대로 일치: 낮은 점수
    /// </summary>
    private int CalculateFuzzyScore(string text, string query)
    {
        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(text))
            return 0;

        // 정확히 일치
        if (text == query)
            return 1000;

        // 포함 (contains)
        if (text.Contains(query))
        {
            // 접두사 일치는 더 높은 점수
            if (text.StartsWith(query))
                return 800 + query.Length * 10;
            return 500 + query.Length * 5;
        }

        // Fuzzy matching: 쿼리의 각 문자가 순서대로 나타나는지 확인
        int queryIndex = 0;
        int consecutiveBonus = 0;
        int lastMatchIndex = -1;
        int score = 0;

        for (int i = 0; i < text.Length && queryIndex < query.Length; i++)
        {
            if (text[i] == query[queryIndex])
            {
                score += 10;

                // 연속 매치 보너스
                if (lastMatchIndex == i - 1)
                {
                    consecutiveBonus += 5;
                    score += consecutiveBonus;
                }
                else
                {
                    consecutiveBonus = 0;
                }

                // 단어 시작 부분 매치 보너스
                if (i == 0 || !char.IsLetterOrDigit(text[i - 1]))
                {
                    score += 20;
                }

                lastMatchIndex = i;
                queryIndex++;
            }
        }

        // 모든 쿼리 문자가 매치되었는지 확인
        if (queryIndex < query.Length)
            return 0;

        return score;
    }

    private void DrawSearchResults()
    {
        if (searchResults.Count == 0)
        {
            GUILayout.Label("검색 결과가 없습니다.", labelStyle);
            return;
        }

        foreach (var method in searchResults)
        {
            DrawSearchResultButton(method);
        }
    }

    private void DrawSearchResultButton(APIMethodInfo method)
    {
        GUILayout.BeginHorizontal();

        // 카테고리 라벨
        GUILayout.Label($"[{method.Category}]", labelStyle, GUILayout.Width(100));

        // API 버튼
        if (GUILayout.Button(method.Name, apiButtonStyle, GUILayout.Height(44), GUILayout.ExpandWidth(true)))
        {
            SelectAPI(method);
        }

        GUILayout.EndHorizontal();
    }

    private void DrawGroupHeader(string categoryName, int apiCount)
    {
        bool isExpanded = groupFoldouts.ContainsKey(categoryName) && groupFoldouts[categoryName];
        string icon = isExpanded ? "▼" : "▶";
        string label = $"{icon}  {categoryName} ({apiCount})";

        if (GUILayout.Button(label, groupHeaderStyle, GUILayout.Height(44)))
        {
            groupFoldouts[categoryName] = !isExpanded;
        }
    }

    private void DrawAPIButton(APIMethodInfo method)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(20); // 들여쓰기

        // API 버튼 - 반응형으로 남은 공간 채우기
        if (GUILayout.Button(method.Name, apiButtonStyle, GUILayout.Height(44), GUILayout.ExpandWidth(true)))
        {
            SelectAPI(method);
        }

        GUILayout.EndHorizontal();
    }

    private void DrawParameterInput()
    {
        GUILayout.Label($"API: {selectedMethod.Name}", headerStyle);
        GUILayout.Label($"Category: {selectedMethod.Category}", labelStyle);
        GUILayout.Space(10);

        if (selectedMethod.HasParameters)
        {
            GUILayout.Label("Parameters:", labelStyle);
            GUILayout.Space(5);

            // 터치 스크롤을 위한 영역 저장 (대략적인 스크롤 영역)
            float scrollHeight = Screen.height - 280;
            currentScrollViewRect = new Rect(0, 150, Screen.width, scrollHeight);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(scrollHeight));

            foreach (var param in selectedMethod.Parameters)
            {
                DrawParameterField(param.Name, param.Type, 0);
            }

            GUILayout.EndScrollView();
        }
        else
        {
            GUILayout.Label("No parameters required", labelStyle);
        }

        GUILayout.Space(10);
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("← Back", buttonStyle, GUILayout.Height(48), GUILayout.Width(120)))
        {
            BackToList();
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Execute →", buttonStyle, GUILayout.Height(48), GUILayout.Width(140)))
        {
            ExecuteAPI();
        }

        GUILayout.EndHorizontal();
    }

    /// <summary>
    /// 타입에 따른 적절한 입력 UI 렌더링 (재귀)
    /// </summary>
    private void DrawParameterField(string fieldPath, Type type, int indentLevel)
    {
        string displayName = GetDisplayName(fieldPath);

        // Enum 타입
        if (type.IsEnum)
        {
            DrawEnumSelector(fieldPath, type, displayName, indentLevel);
            return;
        }

        // String 타입
        if (type == typeof(string))
        {
            DrawStringField(fieldPath, displayName, indentLevel);
            return;
        }

        // Number 타입
        if (type == typeof(int) || type == typeof(double) || type == typeof(float))
        {
            DrawNumberField(fieldPath, displayName, indentLevel);
            return;
        }

        // Bool 타입
        if (type == typeof(bool))
        {
            DrawBoolField(fieldPath, displayName, indentLevel);
            return;
        }

        // 복합 객체 타입
        if (type.IsClass && type != typeof(string) && !type.IsArray)
        {
            DrawNestedObject(fieldPath, type, displayName, indentLevel);
            return;
        }

        // 기타 타입 (폴백)
        GUILayout.BeginHorizontal();
        GUILayout.Space(indentLevel * 20);
        GUILayout.Label($"{displayName}: (지원하지 않는 타입: {type.Name})", callbackLabelStyle);
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

        if (!enumSelectedIndices.TryGetValue(fieldPath, out int selectedIndex))
        {
            selectedIndex = 0;
            enumSelectedIndices[fieldPath] = selectedIndex;
        }

        if (!enumDropdownOpen.TryGetValue(fieldPath, out bool isOpen))
        {
            isOpen = false;
            enumDropdownOpen[fieldPath] = isOpen;
        }

        GUILayout.BeginVertical();

        // 현재 선택값 버튼
        GUILayout.BeginHorizontal();
        GUILayout.Space(indentLevel * 20);
        GUILayout.Label($"{displayName}:", fieldLabelStyle, GUILayout.Width(120));

        string buttonLabel = isOpen ? $"▲ {enumNames[selectedIndex]}" : $"▼ {enumNames[selectedIndex]}";
        if (GUILayout.Button(buttonLabel, enumButtonStyle, GUILayout.Height(36), GUILayout.ExpandWidth(true)))
        {
            enumDropdownOpen[fieldPath] = !isOpen;
        }
        GUILayout.EndHorizontal();

        // 드롭다운 옵션 목록
        if (isOpen)
        {
            for (int i = 0; i < enumNames.Length; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(indentLevel * 20 + 120);

                string optionLabel = i == selectedIndex ? $"✓ {enumNames[i]}" : $"   {enumNames[i]}";
                if (GUILayout.Button(optionLabel, enumOptionStyle, GUILayout.Height(32)))
                {
                    enumSelectedIndices[fieldPath] = i;
                    enumDropdownOpen[fieldPath] = false;
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
        if (!stringInputs.TryGetValue(fieldPath, out string value))
        {
            value = "";
            stringInputs[fieldPath] = value;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Space(indentLevel * 20);
        GUILayout.Label($"{displayName}:", fieldLabelStyle, GUILayout.Width(120));
        stringInputs[fieldPath] = GUILayout.TextField(value, textFieldStyle, GUILayout.Height(36), GUILayout.ExpandWidth(true));
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
    }

    /// <summary>
    /// Number 입력 UI
    /// </summary>
    private void DrawNumberField(string fieldPath, string displayName, int indentLevel)
    {
        if (!numberInputs.TryGetValue(fieldPath, out double value))
        {
            value = 0;
            numberInputs[fieldPath] = value;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Space(indentLevel * 20);
        GUILayout.Label($"{displayName}:", fieldLabelStyle, GUILayout.Width(120));

        string strValue = value.ToString();
        string newStrValue = GUILayout.TextField(strValue, textFieldStyle, GUILayout.Height(36), GUILayout.ExpandWidth(true));

        if (newStrValue != strValue)
        {
            if (double.TryParse(newStrValue, out double newValue))
            {
                numberInputs[fieldPath] = newValue;
            }
            else if (string.IsNullOrEmpty(newStrValue))
            {
                numberInputs[fieldPath] = 0;
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
        if (!boolInputs.TryGetValue(fieldPath, out bool value))
        {
            value = false;
            boolInputs[fieldPath] = value;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Space(indentLevel * 20);
        GUILayout.Label($"{displayName}:", fieldLabelStyle, GUILayout.Width(120));

        // 토글 버튼
        string btnLabel = value ? "✓ true" : "✗ false";
        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = value ? new Color(0.4f, 0.7f, 0.4f) : new Color(0.5f, 0.5f, 0.5f);

        if (GUILayout.Button(btnLabel, toggleButtonStyle, GUILayout.Height(36), GUILayout.Width(100)))
        {
            boolInputs[fieldPath] = !value;
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

        // 모든 필드가 콜백인지 확인
        bool hasEditableFields = fields.Any(f => !APIParameterInspector.IsCallbackField(f));

        if (!hasEditableFields)
        {
            // 편집 가능한 필드가 없으면 라벨만 표시
            GUILayout.BeginHorizontal();
            GUILayout.Space(indentLevel * 20);
            GUILayout.Label($"{displayName}: (콜백 전용 - 편집 불가)", callbackLabelStyle);
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
            return;
        }

        if (!nestedFoldouts.TryGetValue(fieldPath, out bool isExpanded))
        {
            isExpanded = true;
            nestedFoldouts[fieldPath] = isExpanded;
        }

        // 접기/펼치기 헤더
        GUILayout.BeginHorizontal();
        GUILayout.Space(indentLevel * 20);

        string icon = isExpanded ? "▼" : "▶";
        if (GUILayout.Button($"{icon} {displayName} ({type.Name})", nestedHeaderStyle, GUILayout.Height(36), GUILayout.ExpandWidth(true)))
        {
            nestedFoldouts[fieldPath] = !isExpanded;
        }

        GUILayout.EndHorizontal();

        // 펼쳐져 있으면 필드들 렌더링
        if (isExpanded)
        {
            foreach (var field in fields)
            {
                if (APIParameterInspector.IsCallbackField(field))
                {
                    // 콜백 필드는 라벨로 표시
                    GUILayout.BeginHorizontal();
                    GUILayout.Space((indentLevel + 1) * 20);
                    GUILayout.Label($"{field.Name}: (콜백 - 편집 불가)", callbackLabelStyle);
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

    private void DrawResult()
    {
        GUILayout.Label($"Result: {selectedMethod.Name}", headerStyle);
        GUILayout.Space(10);

        // 성공/실패 상태 표시
        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = lastResultSuccess ? Color.green : Color.red;
        GUILayout.Label(lastResultSuccess ? "✓ Success" : "✗ Failed", headerStyle);
        GUI.backgroundColor = originalColor;

        GUILayout.Space(10);

        // 표시 모드 토글 (성공 시에만)
        if (lastResultSuccess && lastResultObject != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("표시 모드:", labelStyle, GUILayout.Width(80));

            Color origBg = GUI.backgroundColor;

            GUI.backgroundColor = resultDisplayMode == ResultDisplayMode.Structured
                ? new Color(0.3f, 0.6f, 0.3f)
                : new Color(0.3f, 0.3f, 0.3f);
            if (GUILayout.Button("구조화", toggleButtonStyle, GUILayout.Height(32), GUILayout.Width(80)))
            {
                resultDisplayMode = ResultDisplayMode.Structured;
            }

            GUI.backgroundColor = resultDisplayMode == ResultDisplayMode.RawJson
                ? new Color(0.3f, 0.6f, 0.3f)
                : new Color(0.3f, 0.3f, 0.3f);
            if (GUILayout.Button("JSON", toggleButtonStyle, GUILayout.Height(32), GUILayout.Width(80)))
            {
                resultDisplayMode = ResultDisplayMode.RawJson;
            }

            GUI.backgroundColor = origBg;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
        }

        GUILayout.Label("Response:", labelStyle);

        // 터치 스크롤을 위한 영역 저장 (대략적인 스크롤 영역)
        float scrollHeight = Screen.height - 320;
        currentScrollViewRect = new Rect(0, 200, Screen.width, scrollHeight);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(scrollHeight));

        if (lastResultSuccess && lastResultObject != null && resultDisplayMode == ResultDisplayMode.Structured)
        {
            // 구조화 표시
            DrawStructuredResult(lastResultObject, 0);
        }
        else
        {
            // JSON 표시
            GUILayout.TextArea(lastResult, textAreaStyle, GUILayout.ExpandHeight(true));
        }

        GUILayout.EndScrollView();

        GUILayout.Space(10);
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("← Back to List", buttonStyle, GUILayout.Height(48), GUILayout.Width(160)))
        {
            BackToList();
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Retry", buttonStyle, GUILayout.Height(48), GUILayout.Width(120)))
        {
            currentState = UIState.ParameterInput;
            scrollPosition = Vector2.zero;
        }

        GUILayout.EndHorizontal();
    }

    /// <summary>
    /// 결과 객체를 구조화하여 표시 (재귀)
    /// </summary>
    private void DrawStructuredResult(object obj, int indentLevel)
    {
        if (obj == null)
        {
            DrawResultValue("null", indentLevel);
            return;
        }

        var type = obj.GetType();

        // 단순 타입
        if (APIParameterInspector.IsSimpleType(type))
        {
            string value = type == typeof(string) ? $"\"{obj}\"" : obj.ToString();
            DrawResultValue(value, indentLevel);
            return;
        }

        // Enum
        if (type.IsEnum)
        {
            DrawResultValue(obj.ToString(), indentLevel);
            return;
        }

        // 배열
        if (type.IsArray)
        {
            var array = (Array)obj;
            if (array.Length == 0)
            {
                DrawResultValue("[]", indentLevel);
                return;
            }

            for (int i = 0; i < array.Length; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(indentLevel * 20);
                GUILayout.Label($"[{i}]:", resultKeyStyle, GUILayout.Width(60));
                GUILayout.EndHorizontal();
                DrawStructuredResult(array.GetValue(i), indentLevel + 1);
            }
            return;
        }

        // 복합 객체
        var fields = APIParameterInspector.GetPublicFields(type);
        if (fields.Length == 0)
        {
            DrawResultValue(obj.ToString(), indentLevel);
            return;
        }

        foreach (var field in fields)
        {
            var value = field.GetValue(obj);
            var fieldType = field.FieldType;

            // 콜백 필드 건너뛰기
            if (APIParameterInspector.IsCallbackField(field))
            {
                continue;
            }

            // 단순 타입은 한 줄에 표시
            if (APIParameterInspector.IsSimpleType(fieldType) || fieldType.IsEnum)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(indentLevel * 20);
                GUILayout.Label($"{field.Name}:", resultKeyStyle, GUILayout.Width(150));
                string displayValue = value == null ? "null" :
                    (fieldType == typeof(string) ? $"\"{value}\"" : value.ToString());
                GUILayout.Label(displayValue, resultValueStyle, GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();
                GUILayout.Space(2);
            }
            else
            {
                // 중첩 객체
                GUILayout.BeginHorizontal();
                GUILayout.Space(indentLevel * 20);
                GUILayout.Label($"{field.Name}:", resultKeyStyle);
                GUILayout.EndHorizontal();
                DrawStructuredResult(value, indentLevel + 1);
            }
        }
    }

    private void DrawResultValue(string value, int indentLevel)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(indentLevel * 20);
        GUILayout.Label(value, resultValueStyle);
        GUILayout.EndHorizontal();
    }

    private void SelectAPI(APIMethodInfo method)
    {
        selectedMethod = method;

        // 모든 입력 상태 초기화
        stringInputs.Clear();
        numberInputs.Clear();
        boolInputs.Clear();
        enumSelectedIndices.Clear();
        nestedFoldouts.Clear();
        enumDropdownOpen.Clear();

        // 파라미터 기본값 초기화
        foreach (var param in method.Parameters)
        {
            InitializeParameterDefaults(param.Name, param.Type);
        }

        currentState = UIState.ParameterInput;
        scrollPosition = Vector2.zero;
        Debug.Log($"[InteractiveAPITester] Selected API: {method.Name} ({method.Category})");
    }

    /// <summary>
    /// 파라미터 타입에 따른 기본값 초기화 (재귀)
    /// </summary>
    private void InitializeParameterDefaults(string basePath, Type type)
    {
        if (type == typeof(string))
        {
            stringInputs[basePath] = "";
        }
        else if (type == typeof(int) || type == typeof(double) || type == typeof(float))
        {
            numberInputs[basePath] = 0;
        }
        else if (type == typeof(bool))
        {
            boolInputs[basePath] = false;
        }
        else if (type.IsEnum)
        {
            enumSelectedIndices[basePath] = 0;
        }
        else if (type.IsClass && type != typeof(string) && !type.IsArray)
        {
            // 중첩 객체: 기본적으로 펼침
            nestedFoldouts[basePath] = true;

            // 중첩 필드들도 초기화
            var fields = APIParameterInspector.GetPublicFields(type);
            foreach (var field in fields)
            {
                if (APIParameterInspector.IsCallbackField(field)) continue;
                string fieldPath = $"{basePath}.{field.Name}";
                InitializeParameterDefaults(fieldPath, field.FieldType);
            }
        }
    }

    private void BackToList()
    {
        currentState = UIState.APIList;
        selectedMethod = null;
        stringInputs.Clear();
        numberInputs.Clear();
        boolInputs.Clear();
        enumSelectedIndices.Clear();
        nestedFoldouts.Clear();
        enumDropdownOpen.Clear();
        scrollPosition = Vector2.zero;
    }

    /// <summary>
    /// 입력 상태에서 파라미터 객체 조합 (재귀)
    /// </summary>
    private object BuildParameterObject(string basePath, Type type)
    {
        // 단순 타입
        if (type == typeof(string))
        {
            return stringInputs.TryGetValue(basePath, out var s) ? s : "";
        }
        if (type == typeof(int))
        {
            return (int)(numberInputs.TryGetValue(basePath, out var n) ? n : 0);
        }
        if (type == typeof(double))
        {
            return numberInputs.TryGetValue(basePath, out var n) ? n : 0.0;
        }
        if (type == typeof(float))
        {
            return (float)(numberInputs.TryGetValue(basePath, out var n) ? n : 0.0);
        }
        if (type == typeof(bool))
        {
            return boolInputs.TryGetValue(basePath, out var b) ? b : false;
        }

        // Enum 타입
        if (type.IsEnum)
        {
            var index = enumSelectedIndices.TryGetValue(basePath, out var i) ? i : 0;
            return APIParameterInspector.GetEnumValueByIndex(type, index);
        }

        // 복합 객체
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

        // 지원하지 않는 타입
        return null;
    }

    private async void ExecuteAPI()
    {
        Debug.Log($"[InteractiveAPITester] Executing API: {selectedMethod.Name}");

        try
        {
            // 파라미터 조합
            object[] parameters = new object[selectedMethod.Parameters.Count];
            for (int i = 0; i < selectedMethod.Parameters.Count; i++)
            {
                var param = selectedMethod.Parameters[i];
                parameters[i] = BuildParameterObject(param.Name, param.Type);
                Debug.Log($"[InteractiveAPITester] Parameter {param.Name}: {parameters[i]}");
            }

            // API 호출
            object result = selectedMethod.Method.Invoke(null, parameters);

            // Task 대기 - await시 AITException이 발생하면 catch에서 처리됨
            if (result is Task task)
            {
                await task;

                // Task<T>인 경우 결과 추출
                var taskType = task.GetType();
                Debug.Log($"[InteractiveAPITester] Task type: {taskType.FullName}");

                // Task<T>인지 확인
                if (taskType.IsGenericType && taskType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    // Task<T>에서 Result 가져오기
                    var resultProperty = taskType.GetProperty("Result");
                    if (resultProperty != null)
                    {
                        object taskResult = resultProperty.GetValue(task);
                        Debug.Log($"[InteractiveAPITester] Task result: {taskResult}");
                        ShowResult(taskResult, true);
                    }
                    else
                    {
                        // GetAwaiter().GetResult() 사용
                        var awaiter = taskType.GetMethod("GetAwaiter").Invoke(task, null);
                        var getResultMethod = awaiter.GetType().GetMethod("GetResult");
                        var taskResult = getResultMethod.Invoke(awaiter, null);
                        Debug.Log($"[InteractiveAPITester] Task result via GetAwaiter: {taskResult}");
                        ShowResult(taskResult, true);
                    }
                }
                else
                {
                    // 일반 Task (void 반환)
                    ShowResult("Success (void)", true);
                }
            }
            else
            {
                ShowResult("Unexpected return type", false);
            }
        }
        catch (TargetInvocationException tie) when (tie.InnerException is AITException aitEx)
        {
            // MethodInfo.Invoke에서 발생한 AITException 처리
            Debug.LogError($"[InteractiveAPITester] AITException: {aitEx.Message}");
            ShowAITException(aitEx);
        }
        catch (AITException aitEx)
        {
            // 직접 발생한 AITException 처리
            Debug.LogError($"[InteractiveAPITester] AITException: {aitEx.Message}");
            ShowAITException(aitEx);
        }
        catch (AggregateException ae)
        {
            // Task에서 발생한 예외 처리 (await 시 AggregateException으로 wrap됨)
            var innerEx = ae.Flatten().InnerException;
            Debug.LogError($"[InteractiveAPITester] AggregateException: {innerEx}");
            if (innerEx is AITException aitEx)
            {
                ShowAITException(aitEx);
            }
            else
            {
                ShowResult($"Error: {innerEx?.Message ?? ae.Message}\n\nStack Trace:\n{innerEx?.StackTrace ?? ae.StackTrace}", false);
            }
        }
        catch (Exception ex)
        {
            // 기타 예외 처리
            var innerEx = ex.InnerException ?? ex;
            Debug.LogError($"[InteractiveAPITester] API execution failed: {innerEx}");
            if (innerEx is AITException aitEx)
            {
                ShowAITException(aitEx);
            }
            else
            {
                ShowResult($"Error: {innerEx.Message}\n\nStack Trace:\n{innerEx.StackTrace}", false);
            }
        }
    }

    /// <summary>
    /// AITException의 구조화된 에러 정보를 표시
    /// </summary>
    private void ShowAITException(AITException ex)
    {
        string errorInfo = $"API Error: {ex.APIName}\n\n";
        errorInfo += $"Message: {ex.Message}\n";

        if (!string.IsNullOrEmpty(ex.ErrorCode))
        {
            errorInfo += $"Error Code: {ex.ErrorCode}\n";
        }

        if (ex.IsPlatformUnavailable)
        {
            errorInfo += "\n⚠️ Platform Unavailable\n";
            errorInfo += "This API requires the Apps in Toss platform environment.\n";
            errorInfo += "It will not work in browser or Unity Editor.";
        }

        ShowResult(errorInfo, false);
    }

    private void ShowResult(object result, bool success)
    {
        lastResultSuccess = success;

        // 결과 객체 저장 (구조화 표시용)
        if (success && result != null && !(result is string))
        {
            lastResultObject = result;
        }
        else
        {
            lastResultObject = null;
        }

        if (result == null)
        {
            lastResult = "null";
        }
        else if (result is string strResult)
        {
            lastResult = strResult;
        }
        else
        {
            lastResult = APIParameterInspector.SerializeToJson(result);
        }

        currentState = UIState.Result;
        scrollPosition = Vector2.zero;
        resultDisplayMode = ResultDisplayMode.Structured; // 기본은 구조화 표시
        Debug.Log($"[InteractiveAPITester] Result: {lastResult}");
    }

    private Texture2D MakeTex(int width, int height, Color col)
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
