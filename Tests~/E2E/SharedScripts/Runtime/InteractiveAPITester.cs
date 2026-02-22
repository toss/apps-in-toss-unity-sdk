using System;
using System.Collections.Generic;
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

    // Safe Area (AIT API)
#if AIT_SDK_1_7_1_OR_LATER
    private SafeAreaInsets cachedSafeAreaInsets = null;
    private bool safeAreaLoaded = false;
#endif

    // 분리된 컴포넌트 참조
    private OOMTester _oomTester;
    private Component _sentryTester;
    private System.Reflection.MethodInfo _sentryDrawUI;
    private IAPv2Tester _iapTester;
    private AdV2Tester _adV2Tester; // current LoadFullScreenAd/ShowFullScreenAd API
    private ContactsViralTester _contactsViralTester; // ContactsViral API 테스터
    private VisibilityBGMTester _visibilityBGMTester; // Visibility Helper BGM 테스터
    private TouchScrollHandler _scrollHandler;
    private ParameterInputRenderer _paramRenderer;

    // 한글 폰트
    private Font koreanFont;

    async void Start()
    {
        Debug.Log("[InteractiveAPITester] Loading SDK APIs...");

        // 분리된 컴포넌트 초기화
        _oomTester = GetComponent<OOMTester>();
        if (_oomTester == null)
        {
            _oomTester = gameObject.AddComponent<OOMTester>();
        }
        // SentryTester는 별도 어셈블리(AppsInTossTestScripts.Sentry)에 있으므로 리플렉션으로 로드
        var sentryTesterType = Type.GetType("SentryTester, AppsInTossTestScripts.Sentry");
        if (sentryTesterType != null)
        {
            _sentryTester = GetComponent(sentryTesterType) ?? gameObject.AddComponent(sentryTesterType);
            _sentryDrawUI = sentryTesterType.GetMethod("DrawUI");
        }
        _iapTester = GetComponent<IAPv2Tester>();
        if (_iapTester == null)
        {
            _iapTester = gameObject.AddComponent<IAPv2Tester>();
        }
        _adV2Tester = GetComponent<AdV2Tester>();
        if (_adV2Tester == null)
        {
            _adV2Tester = gameObject.AddComponent<AdV2Tester>();
        }
        _contactsViralTester = GetComponent<ContactsViralTester>();
        if (_contactsViralTester == null)
        {
            _contactsViralTester = gameObject.AddComponent<ContactsViralTester>();
        }
        _visibilityBGMTester = GetComponent<VisibilityBGMTester>();
        if (_visibilityBGMTester == null)
        {
            _visibilityBGMTester = gameObject.AddComponent<VisibilityBGMTester>();
        }
        _scrollHandler = new TouchScrollHandler();
        _paramRenderer = new ParameterInputRenderer();

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
#if AIT_SDK_1_7_1_OR_LATER
        await LoadSafeAreaInsets();
#endif
    }

#if AIT_SDK_1_7_1_OR_LATER
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
            Debug.Log($"[InteractiveAPITester] AIT SafeAreaInsetsGet (CSS px): top={cachedSafeAreaInsets.Top}, bottom={cachedSafeAreaInsets.Bottom}, left={cachedSafeAreaInsets.Left}, right={cachedSafeAreaInsets.Right}");

            // devicePixelRatio 로깅
            double dpr = AIT.GetDevicePixelRatio();
            Debug.Log($"[InteractiveAPITester] DevicePixelRatio: {dpr}");

            // 최종 적용될 safeRect 계산 및 로깅 (DPR 적용)
            float top = (float)(cachedSafeAreaInsets.Top * dpr);
            float bottom = (float)(cachedSafeAreaInsets.Bottom * dpr);
            float left = (float)(cachedSafeAreaInsets.Left * dpr);
            float right = (float)(cachedSafeAreaInsets.Right * dpr);
            Rect finalRect = new Rect(left, top, Screen.width - left - right, Screen.height - top - bottom);
            Debug.Log($"[InteractiveAPITester] Final SafeArea Rect (device px): top={top}, bottom={bottom}, left={left}, right={right}");
            Debug.Log($"[InteractiveAPITester] Final SafeArea Rect: x={finalRect.x}, y={finalRect.y}, width={finalRect.width}, height={finalRect.height}");
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
#endif

    void Update()
    {
        _scrollHandler.HandleInput();
    }

    /// <summary>
    /// Safe Area를 IMGUI 좌표계로 변환하여 반환
    /// AIT API 값이 있으면 우선 사용, 없으면 Unity Screen.safeArea 폴백
    /// </summary>
    private Rect GetSafeAreaRect()
    {
#if AIT_SDK_1_7_1_OR_LATER
        // AIT API에서 로드된 값이 있으면 사용
        if (safeAreaLoaded && cachedSafeAreaInsets != null)
        {
            // devicePixelRatio 적용 (CSS px → device px)
            // SafeAreaInsets API는 CSS 픽셀 단위로 반환
            // Unity Screen.width/height는 device 픽셀 단위이므로 스케일링 필요
            float dpr = (float)AIT.GetDevicePixelRatio();

            float top = (float)cachedSafeAreaInsets.Top * dpr;
            float bottom = (float)cachedSafeAreaInsets.Bottom * dpr;
            float left = (float)cachedSafeAreaInsets.Left * dpr;
            float right = (float)cachedSafeAreaInsets.Right * dpr;

            // IMGUI 좌표계: 좌상단 원점
            // AIT API는 insets (여백)을 반환하므로 직접 사용
            return new Rect(
                left,
                top,
                Screen.width - left - right,
                Screen.height - top - bottom
            );
        }
#endif

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
        return _scrollHandler.ShouldBlockInput();
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

        // DPI 디버그 정보
        GUILayout.Label($"Screen: {Screen.width}x{Screen.height} | dpi:{Screen.dpi} | dpiScale:{InteractiveAPITesterStyles.DpiScale:F2}", InteractiveAPITesterStyles.LabelStyle);

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
        _scrollHandler.ScrollPosition = GUILayout.BeginScrollView(_scrollHandler.ScrollPosition, false, true, GUILayout.ExpandHeight(true));
        // 터치 스크롤을 위한 영역 저장 (전체 화면 기준 좌표, safe area 오프셋 포함)
        Rect safeArea = GetSafeAreaRect();
        _scrollHandler.SetScrollViewRect(new Rect(safeArea.x, safeArea.y + 100, safeArea.width, safeArea.height - 100));

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

            // Visibility BGM Tester 섹션
            GUILayout.Space(20);
            _visibilityBGMTester?.DrawUI(
                InteractiveAPITesterStyles.BoxStyle,
                InteractiveAPITesterStyles.GroupHeaderStyle,
                InteractiveAPITesterStyles.LabelStyle,
                InteractiveAPITesterStyles.ButtonStyle
            );

            // OOM Tester 섹션 (API 목록 하단에 추가)
            GUILayout.Space(20);
            _oomTester?.DrawUI(
                InteractiveAPITesterStyles.BoxStyle,
                InteractiveAPITesterStyles.GroupHeaderStyle,
                InteractiveAPITesterStyles.LabelStyle,
                InteractiveAPITesterStyles.DangerButtonStyle,
                InteractiveAPITesterStyles.ButtonStyle
            );

            // Sentry Tester 섹션 (별도 어셈블리 — 리플렉션으로 호출)
            if (_sentryTester != null && _sentryDrawUI != null)
            {
                GUILayout.Space(20);
                _sentryDrawUI.Invoke(_sentryTester, new object[] {
                    InteractiveAPITesterStyles.BoxStyle,
                    InteractiveAPITesterStyles.GroupHeaderStyle,
                    InteractiveAPITesterStyles.LabelStyle,
                    InteractiveAPITesterStyles.ButtonStyle,
                    InteractiveAPITesterStyles.DangerButtonStyle
                });
            }

            // IAP 테스터 섹션
            GUILayout.Space(20);
            _iapTester?.DrawUI(
                InteractiveAPITesterStyles.BoxStyle,
                InteractiveAPITesterStyles.GroupHeaderStyle,
                InteractiveAPITesterStyles.LabelStyle,
                InteractiveAPITesterStyles.ButtonStyle,
                InteractiveAPITesterStyles.TextFieldStyle,
                InteractiveAPITesterStyles.FieldLabelStyle,
                InteractiveAPITesterStyles.CallbackLabelStyle
            );

            // AdV2 테스터 섹션 (인앱광고v2 - 현재 API)
            GUILayout.Space(20);
            _adV2Tester?.DrawUI(
                InteractiveAPITesterStyles.BoxStyle,
                InteractiveAPITesterStyles.GroupHeaderStyle,
                InteractiveAPITesterStyles.LabelStyle,
                InteractiveAPITesterStyles.ButtonStyle,
                InteractiveAPITesterStyles.TextFieldStyle,
                InteractiveAPITesterStyles.FieldLabelStyle,
                InteractiveAPITesterStyles.CallbackLabelStyle
            );

            // ContactsViral 테스터 섹션
            GUILayout.Space(20);
            _contactsViralTester?.DrawUI(
                InteractiveAPITesterStyles.BoxStyle,
                InteractiveAPITesterStyles.GroupHeaderStyle,
                InteractiveAPITesterStyles.LabelStyle,
                InteractiveAPITesterStyles.ButtonStyle,
                InteractiveAPITesterStyles.TextFieldStyle,
                InteractiveAPITesterStyles.FieldLabelStyle,
                InteractiveAPITesterStyles.CallbackLabelStyle
            );
        }

        GUILayout.EndScrollView();
    }

    private void DrawSearchBox()
    {
        GUILayout.BeginVertical(InteractiveAPITesterStyles.SearchBoxStyle);

        GUILayout.BeginHorizontal();

        // 검색 아이콘/레이블
        GUILayout.Label("🔍", InteractiveAPITesterStyles.LabelStyle, GUILayout.Width(InteractiveAPITesterStyles.ScaledInt(24)));

        // 검색 입력 필드
        string newQuery = GUILayout.TextField(searchQuery, InteractiveAPITesterStyles.TextFieldStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(36)), GUILayout.ExpandWidth(true));

        // 검색어가 변경되면 검색 수행
        if (newQuery != searchQuery)
        {
            searchQuery = newQuery;
            UpdateSearchResults();
        }

        // 검색어 지우기 버튼
        if (!string.IsNullOrEmpty(searchQuery))
        {
            if (GUILayout.Button("✕", InteractiveAPITesterStyles.ButtonStyle, GUILayout.Width(InteractiveAPITesterStyles.ScaledInt(40)), GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(36))))
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
        GUILayout.Label($"[{method.Category}]", InteractiveAPITesterStyles.LabelStyle, GUILayout.Width(InteractiveAPITesterStyles.ScaledInt(100)));

        // API 버튼
        if (ScrollAreaButton(method.Name, InteractiveAPITesterStyles.ApiButtonStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(44)), GUILayout.ExpandWidth(true)))
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

        if (ScrollAreaButton(label, InteractiveAPITesterStyles.GroupHeaderStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(44))))
        {
            groupFoldouts[categoryName] = !isExpanded;
        }
    }

    private void DrawAPIButton(APIMethodInfo method)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(20); // 들여쓰기

        // API 버튼 - 반응형으로 남은 공간 채우기
        if (ScrollAreaButton(method.Name, InteractiveAPITesterStyles.ApiButtonStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(44)), GUILayout.ExpandWidth(true)))
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
            _scrollHandler.SetScrollViewRect(new Rect(safeArea.x, safeArea.y + 150, safeArea.width, scrollHeight));
            _scrollHandler.ScrollPosition = GUILayout.BeginScrollView(_scrollHandler.ScrollPosition, GUILayout.Height(scrollHeight));

            foreach (var param in selectedMethod.Parameters)
            {
                _paramRenderer.DrawParameterField(param.Name, param.Type, 0);
            }

            GUILayout.EndScrollView();
        }
        else
        {
            GUILayout.Label("No parameters required", InteractiveAPITesterStyles.LabelStyle);
        }

        GUILayout.Space(10);
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("← Back", InteractiveAPITesterStyles.ButtonStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(48)), GUILayout.Width(InteractiveAPITesterStyles.ScaledInt(120))))
        {
            BackToList();
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Execute →", InteractiveAPITesterStyles.ButtonStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(48)), GUILayout.Width(InteractiveAPITesterStyles.ScaledInt(140))))
        {
            ExecuteAPI();
        }

        GUILayout.EndHorizontal();
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
            GUILayout.Label("표시 모드:", InteractiveAPITesterStyles.LabelStyle, GUILayout.Width(InteractiveAPITesterStyles.ScaledInt(80)));

            Color origBg = GUI.backgroundColor;

            GUI.backgroundColor = resultDisplayMode == ResultDisplayMode.Structured
                ? new Color(0.3f, 0.6f, 0.3f)
                : new Color(0.3f, 0.3f, 0.3f);
            if (GUILayout.Button("구조화", InteractiveAPITesterStyles.ToggleButtonStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(32)), GUILayout.Width(InteractiveAPITesterStyles.ScaledInt(80))))
            {
                resultDisplayMode = ResultDisplayMode.Structured;
            }

            GUI.backgroundColor = resultDisplayMode == ResultDisplayMode.RawJson
                ? new Color(0.3f, 0.6f, 0.3f)
                : new Color(0.3f, 0.3f, 0.3f);
            if (GUILayout.Button("JSON", InteractiveAPITesterStyles.ToggleButtonStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(32)), GUILayout.Width(InteractiveAPITesterStyles.ScaledInt(80))))
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
        _scrollHandler.SetScrollViewRect(new Rect(safeArea.x, safeArea.y + 200, safeArea.width, scrollHeight));
        _scrollHandler.ScrollPosition = GUILayout.BeginScrollView(_scrollHandler.ScrollPosition, GUILayout.Height(scrollHeight));

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

        if (GUILayout.Button("← Back to List", InteractiveAPITesterStyles.ButtonStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(48)), GUILayout.Width(InteractiveAPITesterStyles.ScaledInt(160))))
        {
            BackToList();
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Retry", InteractiveAPITesterStyles.ButtonStyle, GUILayout.Height(InteractiveAPITesterStyles.ScaledInt(48)), GUILayout.Width(InteractiveAPITesterStyles.ScaledInt(120))))
        {
            currentState = UIState.ParameterInput;
            _scrollHandler.ResetScroll();
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
                GUILayout.Space(indentLevel * InteractiveAPITesterStyles.ScaledInt(20));
                GUILayout.Label($"[{i}]:", InteractiveAPITesterStyles.ResultKeyStyle, GUILayout.Width(InteractiveAPITesterStyles.ScaledInt(60)));
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
                GUILayout.Space(indentLevel * InteractiveAPITesterStyles.ScaledInt(20));
                GUILayout.Label($"{field.Name}:", InteractiveAPITesterStyles.ResultKeyStyle, GUILayout.Width(InteractiveAPITesterStyles.ScaledInt(150)));
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
                GUILayout.Space(indentLevel * InteractiveAPITesterStyles.ScaledInt(20));
                GUILayout.Label($"{field.Name}:", InteractiveAPITesterStyles.ResultKeyStyle);
                GUILayout.EndHorizontal();
                DrawStructuredResult(value, indentLevel + 1);
            }
        }
    }

    private void DrawResultValue(string value, int indentLevel)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(indentLevel * InteractiveAPITesterStyles.ScaledInt(20));
        GUILayout.Label(value, InteractiveAPITesterStyles.ResultValueStyle);
        GUILayout.EndHorizontal();
    }

    private void SelectAPI(APIMethodInfo method)
    {
        selectedMethod = method;

        // 모든 입력 상태 초기화
        _paramRenderer.ClearAll();

        // 파라미터 기본값 초기화
        foreach (var param in method.Parameters)
        {
            _paramRenderer.InitializeDefaults(param.Name, param.Type);
        }

        currentState = UIState.ParameterInput;
        _scrollHandler.ResetScroll();
        Debug.Log($"[InteractiveAPITester] Selected API: {method.Name} ({method.Category})");
    }

    private void BackToList()
    {
        currentState = UIState.APIList;
        selectedMethod = null;
        _paramRenderer.ClearAll();
        _scrollHandler.ResetScroll();
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
                parameters[i] = _paramRenderer.BuildParameterObject(param.Name, param.Type);
                Debug.Log($"[InteractiveAPITester] Parameter {param.Name}: {parameters[i]}");
            }

            // API 호출
            object result = selectedMethod.Method.Invoke(null, parameters);

            // Task 또는 Awaitable 대기
            var resultType = result.GetType();
            var resultTypeName = resultType.Name;
            Debug.Log($"[InteractiveAPITester] Return type: {resultTypeName}");

            if (result is Task task)
            {
                // Task 또는 Task<T> 처리
                await task;

                // Task<T>인 경우 결과 추출
                Debug.Log($"[InteractiveAPITester] Task type: {resultType.FullName}");

                if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    // Task<T>에서 Result 가져오기
                    var resultProperty = resultType.GetProperty("Result");
                    if (resultProperty != null)
                    {
                        object taskResult = resultProperty.GetValue(task);
                        Debug.Log($"[InteractiveAPITester] Task result: {taskResult}");
                        ShowResult(taskResult, true);
                    }
                    else
                    {
                        // GetAwaiter().GetResult() 사용
                        var awaiter = resultType.GetMethod("GetAwaiter").Invoke(task, null);
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
#if UNITY_6000_0_OR_NEWER
            else if (resultTypeName.StartsWith("Awaitable"))
            {
                // Unity 6 Awaitable 또는 Awaitable<T> 처리
                Debug.Log($"[InteractiveAPITester] Awaitable type: {resultType.FullName}");

                // 리플렉션으로 Awaitable await 처리 (dynamic은 IL2CPP에서 미지원)
                object awaitableResult = await AwaitAndGetResult(result, resultType);

                // Awaitable<T>인 경우 결과 표시, Awaitable (void)인 경우 성공 메시지
                if (resultType.IsGenericType)
                {
                    Debug.Log($"[InteractiveAPITester] Awaitable result: {awaitableResult}");
                    ShowResult(awaitableResult, true);
                }
                else
                {
                    ShowResult("Success (void)", true);
                }
            }
#endif
            else
            {
                ShowResult($"Unexpected return type: {resultTypeName}", false);
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

#if UNITY_6000_0_OR_NEWER
    /// <summary>
    /// Awaitable 또는 Awaitable<T>를 await하고 결과를 반환 (IL2CPP 호환)
    /// </summary>
    private async Awaitable<object> AwaitAndGetResult(object awaitable, Type awaitableType)
    {
        // Awaitable (non-generic, void 반환)인 경우
        if (!awaitableType.IsGenericType)
        {
            await (Awaitable)awaitable;
            return null;
        }

        // Awaitable<T>인 경우 - GetAwaiter().GetResult() 패턴 사용
        var getAwaiterMethod = awaitableType.GetMethod("GetAwaiter");
        var awaiter = getAwaiterMethod.Invoke(awaitable, null);
        var awaiterType = awaiter.GetType();

        // IsCompleted 확인하며 대기
        var isCompletedProperty = awaiterType.GetProperty("IsCompleted");
        while (!(bool)isCompletedProperty.GetValue(awaiter))
        {
            await Awaitable.NextFrameAsync();
        }

        // GetResult() 호출하여 결과 반환
        var getResultMethod = awaiterType.GetMethod("GetResult");
        return getResultMethod.Invoke(awaiter, null);
    }
#endif

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
        _scrollHandler.ResetScroll();
        resultDisplayMode = ResultDisplayMode.Structured; // 기본은 구조화 표시
        Debug.Log($"[InteractiveAPITester] Result: {lastResult}");
    }

}
