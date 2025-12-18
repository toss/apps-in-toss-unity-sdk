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
    private bool isDragging = false;  // 실제 드래그 중인지 (임계값 초과)
    private Vector2 touchStartPosition;  // 터치 시작 위치
    private Vector2 lastTouchPosition;
    private Vector2 scrollVelocity = Vector2.zero;
    private float scrollMomentumDecay = 0.95f;
    private float dragThreshold = 10f;  // 드래그 인식 임계값 (픽셀)
    private Rect currentScrollViewRect;

    // Safe Area (AIT API)
    private SafeAreaInsetsGetResult cachedSafeAreaInsets = null;
    private bool safeAreaLoaded = false;

    // 파라미터 입력 상태 (fieldPath -> value)
    private Dictionary<string, string> stringInputs = new Dictionary<string, string>();
    private Dictionary<string, double> numberInputs = new Dictionary<string, double>();
    private Dictionary<string, bool> boolInputs = new Dictionary<string, bool>();
    private Dictionary<string, int> enumSelectedIndices = new Dictionary<string, int>();
    private Dictionary<string, bool> nestedFoldouts = new Dictionary<string, bool>();
    private Dictionary<string, bool> enumDropdownOpen = new Dictionary<string, bool>();

    // OOM Tester 컴포넌트 참조
    private OOMTester _oomTester;

    // 한글 폰트
    private Font koreanFont;

    async void Start()
    {
        Debug.Log("[InteractiveAPITester] Loading SDK APIs...");

        // OOM Tester 컴포넌트 추가
        _oomTester = GetComponent<OOMTester>();
        if (_oomTester == null)
        {
            _oomTester = gameObject.AddComponent<OOMTester>();
        }

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

        // Safe Area Insets 로드 (Apps in Toss 플랫폼)
        await LoadSafeAreaInsets();
    }

    /// <summary>
    /// Apps in Toss 플랫폼에서 Safe Area Insets를 로드합니다.
    /// 플랫폼 미지원 시 Unity Screen.safeArea를 폴백으로 사용합니다.
    /// </summary>
    private async Task LoadSafeAreaInsets()
    {
        // Unity Screen.safeArea 값 먼저 로깅 (비교용)
        Rect unitySafeArea = Screen.safeArea;
        Debug.Log($"[InteractiveAPITester] Unity Screen.safeArea: x={unitySafeArea.x}, y={unitySafeArea.y}, width={unitySafeArea.width}, height={unitySafeArea.height}");
        Debug.Log($"[InteractiveAPITester] Screen size: width={Screen.width}, height={Screen.height}");

        try
        {
            cachedSafeAreaInsets = await AIT.SafeAreaInsetsGet();
            safeAreaLoaded = true;
            Debug.Log($"[InteractiveAPITester] AIT SafeAreaInsetsGet: top={cachedSafeAreaInsets.Top}, bottom={cachedSafeAreaInsets.Bottom}, left={cachedSafeAreaInsets.Left}, right={cachedSafeAreaInsets.Right}");

            // 최종 적용될 safeRect 계산 및 로깅
            float top = (float)cachedSafeAreaInsets.Top;
            float bottom = (float)cachedSafeAreaInsets.Bottom;
            float left = (float)cachedSafeAreaInsets.Left;
            float right = (float)cachedSafeAreaInsets.Right;
            Rect finalRect = new Rect(left, top, Screen.width - left - right, Screen.height - top - bottom);
            Debug.Log($"[InteractiveAPITester] Final SafeArea Rect (using AIT): x={finalRect.x}, y={finalRect.y}, width={finalRect.width}, height={finalRect.height}");
        }
        catch (AITException ex)
        {
            // 플랫폼 미지원 시 Unity 기본값 사용
            Debug.LogWarning($"[InteractiveAPITester] SafeAreaInsetsGet failed: {ex.Message}, using Unity Screen.safeArea as fallback");
            safeAreaLoaded = false;
        }
        catch (Exception ex)
        {
            // 기타 예외
            Debug.LogWarning($"[InteractiveAPITester] SafeAreaInsetsGet error: {ex.Message}, using Unity Screen.safeArea as fallback");
            safeAreaLoaded = false;
        }
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
                        isDragging = false;  // 아직 드래그 시작 안함
                        touchStartPosition = touch.position;
                        lastTouchPosition = touch.position;
                        scrollVelocity = Vector2.zero;
                    }
                    break;

                case TouchPhase.Moved:
                    if (isTouchScrolling)
                    {
                        // 드래그 임계값 확인
                        float totalDragDistance = Vector2.Distance(touch.position, touchStartPosition);
                        if (!isDragging && totalDragDistance > dragThreshold)
                        {
                            isDragging = true;
                        }

                        Vector2 delta = touch.position - lastTouchPosition;
                        // 위로 스와이프하면 (delta.y > 0) 컨텐츠가 위로 올라감 (scrollPosition.y 증가)
                        // 아래로 스와이프하면 (delta.y < 0) 컨텐츠가 아래로 내려감 (scrollPosition.y 감소)
                        scrollPosition.y += delta.y;

                        // 스크롤 범위 제한 (세로만)
                        scrollPosition.y = Mathf.Max(0, scrollPosition.y);

                        // 속도 계산 (관성용, 세로만)
                        scrollVelocity = new Vector2(0, delta.y) / Time.deltaTime * 0.1f;
                        lastTouchPosition = touch.position;
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    isTouchScrolling = false;
                    isDragging = false;
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
                    isDragging = false;
                    touchStartPosition = Input.mousePosition;
                    lastTouchPosition = Input.mousePosition;
                    scrollVelocity = Vector2.zero;
                }
            }
            else if (isTouchScrolling)
            {
                // 드래그 임계값 확인
                float totalDragDistance = Vector2.Distance(Input.mousePosition, touchStartPosition);
                if (!isDragging && totalDragDistance > dragThreshold)
                {
                    isDragging = true;
                }

                Vector2 delta = (Vector2)Input.mousePosition - lastTouchPosition;
                // 위로 드래그하면 (delta.y > 0) 컨텐츠가 위로 올라감 (scrollPosition.y 증가)
                scrollPosition.y += delta.y;

                // 스크롤 범위 제한 (세로만)
                scrollPosition.y = Mathf.Max(0, scrollPosition.y);

                // 속도 계산 (관성용, 세로만)
                scrollVelocity = new Vector2(0, delta.y) / Time.deltaTime * 0.1f;
                lastTouchPosition = Input.mousePosition;
            }
        }
        else
        {
            if (isTouchScrolling)
            {
                isTouchScrolling = false;
                isDragging = false;
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
            // 세로 스크롤 관성만 적용
            scrollPosition.y += scrollVelocity.y * Time.deltaTime;
            scrollPosition.y = Mathf.Max(0, scrollPosition.y);
            scrollVelocity *= scrollMomentumDecay;

            if (scrollVelocity.sqrMagnitude < 0.01f)
            {
                scrollVelocity = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// Safe Area를 IMGUI 좌표계로 변환하여 반환
    /// AIT API 값이 있으면 우선 사용, 없으면 Unity Screen.safeArea 폴백
    /// </summary>
    private Rect GetSafeAreaRect()
    {
        // AIT API에서 로드된 값이 있으면 사용
        if (safeAreaLoaded && cachedSafeAreaInsets != null)
        {
            float top = (float)cachedSafeAreaInsets.Top;
            float bottom = (float)cachedSafeAreaInsets.Bottom;
            float left = (float)cachedSafeAreaInsets.Left;
            float right = (float)cachedSafeAreaInsets.Right;

            // IMGUI 좌표계: 좌상단 원점
            // AIT API는 insets (여백)을 반환하므로 직접 사용
            return new Rect(
                left,
                top,
                Screen.width - left - right,
                Screen.height - top - bottom
            );
        }

        // 폴백: Unity Screen.safeArea 사용
        Rect safeArea = Screen.safeArea;

        // Screen.safeArea: 좌하단 원점, (x, y)는 safe area의 좌하단 코너
        // IMGUI: 좌상단 원점
        // 변환: IMGUI의 y = Screen.height - (safeArea.y + safeArea.height)
        float x = safeArea.x;
        float y = Screen.height - (safeArea.y + safeArea.height);
        float width = safeArea.width;
        float height = safeArea.height;

        return new Rect(x, y, width, height);
    }

    /// <summary>
    /// 스크롤 중 버튼 클릭 방지 여부
    /// </summary>
    private bool ShouldBlockInput()
    {
        return isDragging || scrollVelocity.sqrMagnitude > 1f;
    }

    /// <summary>
    /// 스크롤 영역 내 버튼 - 드래그 중에는 클릭 무시
    /// </summary>
    private bool ScrollAreaButton(string text, GUIStyle style, params GUILayoutOption[] options)
    {
        bool clicked = GUILayout.Button(text, style, options);
        return clicked && !ShouldBlockInput();
    }

    void OnGUI()
    {
        InteractiveAPITesterStyles.Initialize(koreanFont);

        // 메인 컨테이너 - Safe Area 내에서만 UI 표시 (iOS 노치/상단바 회피)
        Rect safeRect = GetSafeAreaRect();
        GUILayout.BeginArea(safeRect);
        GUILayout.BeginVertical(InteractiveAPITesterStyles.BoxStyle);

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

    private void DrawAPIList()
    {
        // 검색창 (상단 고정)
        DrawSearchBox();

        // 스크롤뷰 - 세로 스크롤만 활성화, 가로 스크롤 비활성화
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true, GUILayout.ExpandHeight(true));
        // 터치 스크롤을 위한 영역 저장 (전체 화면 기준 좌표, safe area 오프셋 포함)
        Rect safeArea = GetSafeAreaRect();
        currentScrollViewRect = new Rect(safeArea.x, safeArea.y + 100, safeArea.width, safeArea.height - 100);

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

            // OOM Tester 섹션 (API 목록 하단에 추가)
            GUILayout.Space(20);
            _oomTester?.DrawUI(
                InteractiveAPITesterStyles.BoxStyle,
                InteractiveAPITesterStyles.GroupHeaderStyle,
                InteractiveAPITesterStyles.LabelStyle,
                InteractiveAPITesterStyles.DangerButtonStyle,
                InteractiveAPITesterStyles.ButtonStyle
            );
        }

        GUILayout.EndScrollView();
    }

    private void DrawSearchBox()
    {
        GUILayout.BeginVertical(InteractiveAPITesterStyles.SearchBoxStyle);

        GUILayout.BeginHorizontal();

        // 검색 아이콘/레이블
        GUILayout.Label("🔍", InteractiveAPITesterStyles.LabelStyle, GUILayout.Width(24));

        // 검색 입력 필드
        string newQuery = GUILayout.TextField(searchQuery, InteractiveAPITesterStyles.TextFieldStyle, GUILayout.Height(36), GUILayout.ExpandWidth(true));

        // 검색어가 변경되면 검색 수행
        if (newQuery != searchQuery)
        {
            searchQuery = newQuery;
            UpdateSearchResults();
        }

        // 검색어 지우기 버튼
        if (!string.IsNullOrEmpty(searchQuery))
        {
            if (GUILayout.Button("✕", InteractiveAPITesterStyles.ButtonStyle, GUILayout.Width(40), GUILayout.Height(36)))
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
            GUILayout.Label($"검색 결과: {searchResults.Count}개", InteractiveAPITesterStyles.LabelStyle);
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
            GUILayout.Label("검색 결과가 없습니다.", InteractiveAPITesterStyles.LabelStyle);
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
        GUILayout.Label($"[{method.Category}]", InteractiveAPITesterStyles.LabelStyle, GUILayout.Width(100));

        // API 버튼
        if (ScrollAreaButton(method.Name, InteractiveAPITesterStyles.ApiButtonStyle, GUILayout.Height(44), GUILayout.ExpandWidth(true)))
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

        if (ScrollAreaButton(label, InteractiveAPITesterStyles.GroupHeaderStyle, GUILayout.Height(44)))
        {
            groupFoldouts[categoryName] = !isExpanded;
        }
    }

    private void DrawAPIButton(APIMethodInfo method)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(20); // 들여쓰기

        // API 버튼 - 반응형으로 남은 공간 채우기
        if (ScrollAreaButton(method.Name, InteractiveAPITesterStyles.ApiButtonStyle, GUILayout.Height(44), GUILayout.ExpandWidth(true)))
        {
            SelectAPI(method);
        }

        GUILayout.EndHorizontal();
    }

    private void DrawParameterInput()
    {
        GUILayout.Label($"API: {selectedMethod.Name}", InteractiveAPITesterStyles.HeaderStyle);
        GUILayout.Label($"Category: {selectedMethod.Category}", InteractiveAPITesterStyles.LabelStyle);
        GUILayout.Space(10);

        if (selectedMethod.HasParameters)
        {
            GUILayout.Label("Parameters:", InteractiveAPITesterStyles.LabelStyle);
            GUILayout.Space(5);

            // 터치 스크롤을 위한 영역 저장 (전체 화면 기준 좌표, safe area 오프셋 포함)
            Rect safeArea = GetSafeAreaRect();
            float scrollHeight = safeArea.height - 280;
            currentScrollViewRect = new Rect(safeArea.x, safeArea.y + 150, safeArea.width, scrollHeight);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(scrollHeight));

            foreach (var param in selectedMethod.Parameters)
            {
                DrawParameterField(param.Name, param.Type, 0);
            }

            GUILayout.EndScrollView();
        }
        else
        {
            GUILayout.Label("No parameters required", InteractiveAPITesterStyles.LabelStyle);
        }

        GUILayout.Space(10);
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("← Back", InteractiveAPITesterStyles.ButtonStyle, GUILayout.Height(48), GUILayout.Width(120)))
        {
            BackToList();
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Execute →", InteractiveAPITesterStyles.ButtonStyle, GUILayout.Height(48), GUILayout.Width(140)))
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
        GUILayout.Label($"{displayName}:", InteractiveAPITesterStyles.FieldLabelStyle, GUILayout.Width(120));

        string buttonLabel = isOpen ? $"▲ {enumNames[selectedIndex]}" : $"▼ {enumNames[selectedIndex]}";
        if (GUILayout.Button(buttonLabel, InteractiveAPITesterStyles.EnumButtonStyle, GUILayout.Height(36), GUILayout.ExpandWidth(true)))
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
                if (GUILayout.Button(optionLabel, InteractiveAPITesterStyles.EnumOptionStyle, GUILayout.Height(32)))
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
        GUILayout.Label($"{displayName}:", InteractiveAPITesterStyles.FieldLabelStyle, GUILayout.Width(120));
        stringInputs[fieldPath] = GUILayout.TextField(value, InteractiveAPITesterStyles.TextFieldStyle, GUILayout.Height(36), GUILayout.ExpandWidth(true));
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
        GUILayout.Label($"{displayName}:", InteractiveAPITesterStyles.FieldLabelStyle, GUILayout.Width(120));

        string strValue = value.ToString();
        string newStrValue = GUILayout.TextField(strValue, InteractiveAPITesterStyles.TextFieldStyle, GUILayout.Height(36), GUILayout.ExpandWidth(true));

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
        GUILayout.Label($"{displayName}:", InteractiveAPITesterStyles.FieldLabelStyle, GUILayout.Width(120));

        // 토글 버튼
        string btnLabel = value ? "✓ true" : "✗ false";
        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = value ? new Color(0.4f, 0.7f, 0.4f) : new Color(0.5f, 0.5f, 0.5f);

        if (GUILayout.Button(btnLabel, InteractiveAPITesterStyles.ToggleButtonStyle, GUILayout.Height(36), GUILayout.Width(100)))
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
            GUILayout.Label($"{displayName}: (콜백 전용 - 편집 불가)", InteractiveAPITesterStyles.CallbackLabelStyle);
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
        if (GUILayout.Button($"{icon} {displayName} ({type.Name})", InteractiveAPITesterStyles.NestedHeaderStyle, GUILayout.Height(36), GUILayout.ExpandWidth(true)))
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

    private void DrawResult()
    {
        GUILayout.Label($"Result: {selectedMethod.Name}", InteractiveAPITesterStyles.HeaderStyle);
        GUILayout.Space(10);

        // 성공/실패 상태 표시
        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = lastResultSuccess ? Color.green : Color.red;
        GUILayout.Label(lastResultSuccess ? "✓ Success" : "✗ Failed", InteractiveAPITesterStyles.HeaderStyle);
        GUI.backgroundColor = originalColor;

        GUILayout.Space(10);

        // 표시 모드 토글 (성공 시에만)
        if (lastResultSuccess && lastResultObject != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("표시 모드:", InteractiveAPITesterStyles.LabelStyle, GUILayout.Width(80));

            Color origBg = GUI.backgroundColor;

            GUI.backgroundColor = resultDisplayMode == ResultDisplayMode.Structured
                ? new Color(0.3f, 0.6f, 0.3f)
                : new Color(0.3f, 0.3f, 0.3f);
            if (GUILayout.Button("구조화", InteractiveAPITesterStyles.ToggleButtonStyle, GUILayout.Height(32), GUILayout.Width(80)))
            {
                resultDisplayMode = ResultDisplayMode.Structured;
            }

            GUI.backgroundColor = resultDisplayMode == ResultDisplayMode.RawJson
                ? new Color(0.3f, 0.6f, 0.3f)
                : new Color(0.3f, 0.3f, 0.3f);
            if (GUILayout.Button("JSON", InteractiveAPITesterStyles.ToggleButtonStyle, GUILayout.Height(32), GUILayout.Width(80)))
            {
                resultDisplayMode = ResultDisplayMode.RawJson;
            }

            GUI.backgroundColor = origBg;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
        }

        GUILayout.Label("Response:", InteractiveAPITesterStyles.LabelStyle);

        // 터치 스크롤을 위한 영역 저장 (전체 화면 기준 좌표, safe area 오프셋 포함)
        Rect safeArea = GetSafeAreaRect();
        float scrollHeight = safeArea.height - 320;
        currentScrollViewRect = new Rect(safeArea.x, safeArea.y + 200, safeArea.width, scrollHeight);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(scrollHeight));

        if (lastResultSuccess && lastResultObject != null && resultDisplayMode == ResultDisplayMode.Structured)
        {
            // 구조화 표시
            DrawStructuredResult(lastResultObject, 0);
        }
        else
        {
            // JSON 표시
            GUILayout.TextArea(lastResult, InteractiveAPITesterStyles.TextAreaStyle, GUILayout.ExpandHeight(true));
        }

        GUILayout.EndScrollView();

        GUILayout.Space(10);
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("← Back to List", InteractiveAPITesterStyles.ButtonStyle, GUILayout.Height(48), GUILayout.Width(160)))
        {
            BackToList();
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Retry", InteractiveAPITesterStyles.ButtonStyle, GUILayout.Height(48), GUILayout.Width(120)))
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
                GUILayout.Label($"[{i}]:", InteractiveAPITesterStyles.ResultKeyStyle, GUILayout.Width(60));
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
                GUILayout.Label($"{field.Name}:", InteractiveAPITesterStyles.ResultKeyStyle, GUILayout.Width(150));
                string displayValue = value == null ? "null" :
                    (fieldType == typeof(string) ? $"\"{value}\"" : value.ToString());
                GUILayout.Label(displayValue, InteractiveAPITesterStyles.ResultValueStyle, GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();
                GUILayout.Space(2);
            }
            else
            {
                // 중첩 객체
                GUILayout.BeginHorizontal();
                GUILayout.Space(indentLevel * 20);
                GUILayout.Label($"{field.Name}:", InteractiveAPITesterStyles.ResultKeyStyle);
                GUILayout.EndHorizontal();
                DrawStructuredResult(value, indentLevel + 1);
            }
        }
    }

    private void DrawResultValue(string value, int indentLevel)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(indentLevel * 20);
        GUILayout.Label(value, InteractiveAPITesterStyles.ResultValueStyle);
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
}
