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

    private UIState currentState = UIState.APIList;
    private List<APIMethodInfo> allMethods;
    private Dictionary<string, List<APIMethodInfo>> groupedMethods;
    private Dictionary<string, bool> groupFoldouts = new Dictionary<string, bool>();
    private APIMethodInfo selectedMethod;

    // 검색 관련
    private string searchQuery = "";
    private List<APIMethodInfo> searchResults = new List<APIMethodInfo>();
    private bool isSearchMode = false;
    private Dictionary<string, string> parameterInputs = new Dictionary<string, string>();
    private string lastResult = "";
    private bool lastResultSuccess = true;
    private Vector2 scrollPosition = Vector2.zero;

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
    private bool stylesInitialized = false;

    // 한글 폰트
    private Font koreanFont;

    void Start()
    {
        Debug.Log("[InteractiveAPITester] Loading SDK APIs...");

        // 한글 폰트 로드 (Pretendard - SIL OFL License)
        koreanFont = Resources.Load<Font>("Fonts/Pretendard-Regular");
        if (koreanFont != null)
        {
            Debug.Log("[InteractiveAPITester] Korean font (Pretendard) loaded successfully");
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

        stylesInitialized = true;
    }

    private void DrawAPIList()
    {
        // 검색창 (상단 고정)
        DrawSearchBox();

        // 스크롤뷰 - 세로 스크롤만 활성화, 가로 스크롤 비활성화
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true, GUILayout.ExpandHeight(true));

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

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(Screen.height - 280));

            foreach (var param in selectedMethod.Parameters)
            {
                GUILayout.BeginVertical(boxStyle);

                string typeLabel = param.IsSimpleType ? param.Type.Name : $"{param.Type.Name} (JSON)";
                GUILayout.Label($"{param.Name} ({typeLabel})", labelStyle);

                if (!parameterInputs.ContainsKey(param.Name))
                {
                    parameterInputs[param.Name] = GetDefaultInputForParameter(param);
                }

                // 입력 필드 높이 조정
                int lines = param.IsSimpleType ? 1 : 5;
                parameterInputs[param.Name] = GUILayout.TextArea(
                    parameterInputs[param.Name],
                    textAreaStyle,
                    GUILayout.Height(lines * 20)
                );

                GUILayout.EndVertical();
                GUILayout.Space(5);
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
        GUILayout.Label("Response:", labelStyle);

        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(Screen.height - 280));
        GUILayout.TextArea(lastResult, textAreaStyle, GUILayout.ExpandHeight(true));
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

    private void SelectAPI(APIMethodInfo method)
    {
        selectedMethod = method;
        parameterInputs.Clear();
        currentState = UIState.ParameterInput;
        scrollPosition = Vector2.zero;
        Debug.Log($"[InteractiveAPITester] Selected API: {method.Name} ({method.Category})");
    }

    private void BackToList()
    {
        currentState = UIState.APIList;
        selectedMethod = null;
        parameterInputs.Clear();
        scrollPosition = Vector2.zero;
    }

    private string GetDefaultInputForParameter(APIParameterInfo param)
    {
        if (param.IsSimpleType)
        {
            if (param.Type == typeof(string))
                return "";
            if (param.Type == typeof(bool))
                return "false";
            return "0";
        }
        else
        {
            // 복잡한 객체는 빈 JSON 템플릿 제공
            try
            {
                object defaultObj = Activator.CreateInstance(param.Type);
                return JsonUtility.ToJson(defaultObj, true);
            }
            catch
            {
                return "{}";
            }
        }
    }

    private async void ExecuteAPI()
    {
        Debug.Log($"[InteractiveAPITester] Executing API: {selectedMethod.Name}");

        try
        {
            // 파라미터 파싱
            object[] parameters = new object[selectedMethod.Parameters.Count];
            for (int i = 0; i < selectedMethod.Parameters.Count; i++)
            {
                var param = selectedMethod.Parameters[i];
                string input = parameterInputs[param.Name];
                parameters[i] = APIParameterInspector.ParseParameterFromJson(input, param.Type);
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
