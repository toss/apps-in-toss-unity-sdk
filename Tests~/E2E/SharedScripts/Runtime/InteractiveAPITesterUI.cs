using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using AppsInToss;

/// <summary>
/// InteractiveAPITester의 uGUI UI를 구축하고 뷰 전환/상태를 관리하는 클래스.
/// Canvas, 3개 뷰(APIList, ParameterInput, Result)를 코드로 생성합니다.
/// </summary>
public class InteractiveAPITesterUI
{
    // ─── 뷰 상태 ───
    public enum ViewState { APIList, ParameterInput, Result }

    // ─── 루트 오브젝트 ───
    private Canvas _canvas;
    private RectTransform _safeArea;

    // ─── 3개 뷰 ───
    private GameObject _apiListView;
    private GameObject _paramInputView;
    private GameObject _resultView;

    // ─── API List 뷰 ───
    private InputField _searchInput;
    private RectTransform _categoryContent;
    private ScrollRect _apiListScrollRect;
    private Text _searchCountText;

    // 카테고리 접기/펼치기
    private Dictionary<string, bool> _groupFoldouts = new Dictionary<string, bool>();
    private Dictionary<string, GameObject> _categoryMethodContainers = new Dictionary<string, GameObject>();
    private Dictionary<string, Text> _categoryHeaderTexts = new Dictionary<string, Text>();

    // 검색
    private string _searchQuery = "";
    private List<APIMethodInfo> _allMethods;
    private Dictionary<string, List<APIMethodInfo>> _groupedMethods;
    private GameObject _searchResultsContainer;
    private GameObject _normalListContainer;

    // ─── Parameter Input 뷰 ───
    private Text _paramHeaderText;
    private Text _paramCategoryText;
    private RectTransform _paramContent;
    private ScrollRect _paramScrollRect;
    private Text _noParamText;

    // 파라미터 상태
    private Dictionary<string, string> _stringInputs = new Dictionary<string, string>();
    private Dictionary<string, double> _numberInputs = new Dictionary<string, double>();
    private Dictionary<string, bool> _boolInputs = new Dictionary<string, bool>();
    private Dictionary<string, int> _enumSelectedIndices = new Dictionary<string, int>();
    private Dictionary<string, bool> _nestedFoldouts = new Dictionary<string, bool>();
    private Dictionary<string, bool> _isPasting = new Dictionary<string, bool>();
    private Dictionary<string, InputField> _inputFieldRefs = new Dictionary<string, InputField>();

    // ─── Result 뷰 ───
    private Text _resultHeaderText;
    private Text _statusBadge;
    private Image _statusBadgeBg;
    private RectTransform _resultContent;
    private ScrollRect _resultScrollRect;
    private Button _structuredBtn;
    private Button _jsonBtn;
    private Image _structuredBtnImg;
    private Image _jsonBtnImg;
    private GameObject _displayModeRow;

    private enum ResultDisplayMode { Structured, RawJson }
    private ResultDisplayMode _resultDisplayMode = ResultDisplayMode.Structured;
    private object _lastResultObject;
    private string _lastResultText = "";
    private bool _lastResultSuccess = true;

    // ─── 서브 테스터 컨테이너 ───
    private RectTransform _subTesterContainer;

    // ─── 콜백 ───
    public Action<APIMethodInfo> OnAPISelected;
    public Action OnExecuteRequested;
    public Action OnBackToList;
    public Action OnRetry;

    // ─── DPI debug info ───
    private Text _dpiDebugText;

    // ─── 구축 ───

    public void Build(List<APIMethodInfo> allMethods, Dictionary<string, List<APIMethodInfo>> groupedMethods)
    {
        _allMethods = allMethods;
        _groupedMethods = groupedMethods;

        // Canvas
        _canvas = UIBuilder.CreateCanvas("InteractiveAPITesterCanvas", 100);
        _safeArea = UIBuilder.CreateSafeAreaPanel(_canvas.transform);

        // 배경
        var bgImg = _safeArea.gameObject.AddComponent<Image>();
        bgImg.color = UIBuilder.Theme.CanvasBg;

        // 3개 뷰 생성
        BuildAPIListView();
        BuildParameterInputView();
        BuildResultView();

        // 초기 상태
        ShowView(ViewState.APIList);
    }

    /// <summary>
    /// 서브 테스터 섹션을 추가할 수 있는 부모 Transform을 반환합니다.
    /// </summary>
    public RectTransform GetSubTesterContainer()
    {
        return _subTesterContainer;
    }

    /// <summary>
    /// DPI 디버그 정보 텍스트를 업데이트합니다.
    /// </summary>
    public void UpdateDpiDebug()
    {
        if (_dpiDebugText != null)
        {
            _dpiDebugText.text = $"Screen: {Screen.width}x{Screen.height} | dpi:{Screen.dpi:F0}";
        }
    }

    // ─── API List 뷰 구축 ───

    private void BuildAPIListView()
    {
        _apiListView = new GameObject("APIListView");
        var rt = _apiListView.AddComponent<RectTransform>();
        rt.SetParent(_safeArea, false);
        UIBuilder.SetStretch(rt);

        // 세로 레이아웃: [DPI debug] [검색바] [스크롤 영역]
        var vlg = _apiListView.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 0;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.padding = new RectOffset(0, 0, 4, 0);

        // DPI debug
        _dpiDebugText = UIBuilder.CreateText(_apiListView.transform, "",
            UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextSecondary, TextAnchor.MiddleCenter);
        UIBuilder.SetLayout(_dpiDebugText.gameObject, minHeight: 20, preferredHeight: 20, flexibleHeight: 0);

        // 검색바 행
        BuildSearchBar(_apiListView.transform);

        // 스크롤 영역
        var scrollGo = new GameObject("APIListScroll");
        var scrollRt = scrollGo.AddComponent<RectTransform>();
        scrollRt.SetParent(_apiListView.transform, false);
        UIBuilder.SetLayout(scrollGo, flexibleHeight: 1, minHeight: 100);

        _categoryContent = UIBuilder.CreateScrollView(scrollRt, out _apiListScrollRect);

        // 일반 리스트 컨테이너
        _normalListContainer = new GameObject("NormalList");
        var nlRt = _normalListContainer.AddComponent<RectTransform>();
        nlRt.SetParent(_categoryContent, false);
        var nlVlg = _normalListContainer.AddComponent<VerticalLayoutGroup>();
        nlVlg.spacing = UIBuilder.Theme.SpacingNormal;
        nlVlg.childForceExpandWidth = true;
        nlVlg.childForceExpandHeight = false;
        nlVlg.childControlWidth = true;
        nlVlg.childControlHeight = true;
        var nlCsf = _normalListContainer.AddComponent<ContentSizeFitter>();
        nlCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 카테고리 그룹 생성
        bool isFirst = true;
        foreach (var group in _groupedMethods)
        {
            string category = group.Key;
            var methods = group.Value;

            _groupFoldouts[category] = isFirst;
            isFirst = false;

            BuildCategoryGroup(_normalListContainer.transform, category, methods);
        }

        // 서브 테스터 컨테이너 (카테고리 하단에 배치)
        UIBuilder.CreateSpacer(_normalListContainer.transform, UIBuilder.Theme.SpacingLarge);
        _subTesterContainer = UIBuilder.CreateVerticalLayout(_normalListContainer.transform, UIBuilder.Theme.SpacingLarge);
        // ContentSizeFitter 제거 (부모가 관리)
        var stCsf = _subTesterContainer.GetComponent<ContentSizeFitter>();
        if (stCsf != null) UnityEngine.Object.Destroy(stCsf);

        // 검색 결과 컨테이너 (일반 리스트와 토글)
        _searchResultsContainer = new GameObject("SearchResults");
        var srRt = _searchResultsContainer.AddComponent<RectTransform>();
        srRt.SetParent(_categoryContent, false);
        var srVlg = _searchResultsContainer.AddComponent<VerticalLayoutGroup>();
        srVlg.spacing = UIBuilder.Theme.SpacingSmall;
        srVlg.childForceExpandWidth = true;
        srVlg.childForceExpandHeight = false;
        srVlg.childControlWidth = true;
        srVlg.childControlHeight = true;
        var srCsf = _searchResultsContainer.AddComponent<ContentSizeFitter>();
        srCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _searchResultsContainer.SetActive(false);

        // 하단 여백
        UIBuilder.CreateSpacer(_normalListContainer.transform, 80);
    }

    private void BuildSearchBar(Transform parent)
    {
        // 검색바 패널
        var panel = UIBuilder.CreatePanel(parent, UIBuilder.Theme.HeaderBg);
        UIBuilder.SetLayout(panel.gameObject, minHeight: 56, preferredHeight: 56, flexibleHeight: 0);

        var hlg = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.padding = new RectOffset(12, 12, 8, 8);

        // 검색 아이콘
        var icon = UIBuilder.CreateText(panel, "\ud83d\udd0d",
            UIBuilder.Theme.FontNormal, UIBuilder.Theme.TextSecondary);
        UIBuilder.SetLayout(icon.gameObject, minWidth: 28, preferredWidth: 28);

        // 검색 입력 필드
        _searchInput = UIBuilder.CreateInputField(panel, "Search...", onValueChanged: OnSearchChanged);
        UIBuilder.SetLayout(_searchInput.gameObject, flexibleWidth: 1, flexibleHeight: 0);

        // 검색어 지우기 버튼
        var clearBtn = UIBuilder.CreateButton(panel, "\u2715", onClick: () =>
        {
            _searchInput.text = ""; // onValueChanged가 OnSearchChanged("")를 트리거
        });
        UIBuilder.SetLayout(clearBtn.gameObject, minWidth: 40, preferredWidth: 40);

        // 검색 결과 수 텍스트 (검색바 아래)
        _searchCountText = UIBuilder.CreateText(parent, "",
            UIBuilder.Theme.FontTiny, UIBuilder.Theme.TextSecondary, TextAnchor.MiddleLeft);
        UIBuilder.SetLayout(_searchCountText.gameObject, minHeight: 0, preferredHeight: 0);
        _searchCountText.gameObject.SetActive(false);
    }

    private void BuildCategoryGroup(Transform parent, string category, List<APIMethodInfo> methods)
    {
        var groupGo = new GameObject($"Category_{category}");
        var groupRt = groupGo.AddComponent<RectTransform>();
        groupRt.SetParent(parent, false);
        var groupVlg = groupGo.AddComponent<VerticalLayoutGroup>();
        groupVlg.spacing = 3;
        groupVlg.childForceExpandWidth = true;
        groupVlg.childForceExpandHeight = false;
        groupVlg.childControlWidth = true;
        groupVlg.childControlHeight = true;
        var groupCsf = groupGo.AddComponent<ContentSizeFitter>();
        groupCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 헤더 버튼
        bool isExpanded = _groupFoldouts.ContainsKey(category) && _groupFoldouts[category];
        string icon = isExpanded ? "\u25bc" : "\u25b6";
        var headerBtn = UIBuilder.CreateButton(groupGo.transform,
            $"{icon}  {category} ({methods.Count})",
            onClick: () => ToggleCategory(category));

        // 헤더 배경색 강화
        headerBtn.GetComponent<Image>().color = UIBuilder.Theme.HeaderBg;

        // 헤더 텍스트 스타일
        var headerText = headerBtn.GetComponentInChildren<Text>();
        headerText.alignment = TextAnchor.MiddleLeft;
        headerText.color = UIBuilder.Theme.TextAccent;
        headerText.fontStyle = FontStyle.Bold;
        headerText.fontSize = UIBuilder.Theme.FontLarge;
        _categoryHeaderTexts[category] = headerText;

        // 메서드 리스트 컨테이너
        var methodsGo = new GameObject("Methods");
        var methodsRt = methodsGo.AddComponent<RectTransform>();
        methodsRt.SetParent(groupGo.transform, false);
        var methodsVlg = methodsGo.AddComponent<VerticalLayoutGroup>();
        methodsVlg.spacing = 3;
        methodsVlg.childForceExpandWidth = true;
        methodsVlg.childForceExpandHeight = false;
        methodsVlg.childControlWidth = true;
        methodsVlg.childControlHeight = true;
        methodsVlg.padding = new RectOffset(20, 0, 0, 0);
        var methodsCsf = methodsGo.AddComponent<ContentSizeFitter>();
        methodsCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _categoryMethodContainers[category] = methodsGo;

        foreach (var method in methods)
        {
            var apiMethod = method; // closure capture
            var apiBtn = UIBuilder.CreateAPIButton(methodsGo.transform, method.Name,
                onClick: () => OnAPISelected?.Invoke(apiMethod));
            // API 버튼 배경색 차별화
            apiBtn.GetComponent<Image>().color = UIBuilder.Theme.APIButtonBg;
        }

        methodsGo.SetActive(isExpanded);
    }

    private void ToggleCategory(string category)
    {
        bool isExpanded = _groupFoldouts.ContainsKey(category) && _groupFoldouts[category];
        _groupFoldouts[category] = !isExpanded;

        if (_categoryMethodContainers.TryGetValue(category, out var container))
        {
            container.SetActive(!isExpanded);
        }

        if (_categoryHeaderTexts.TryGetValue(category, out var headerText))
        {
            string icon = !isExpanded ? "\u25bc" : "\u25b6";
            int count = _groupedMethods.ContainsKey(category) ? _groupedMethods[category].Count : 0;
            headerText.text = $"{icon}  {category} ({count})";
        }
    }

    // ─── 검색 ───

    private void OnSearchChanged(string query)
    {
        _searchQuery = query;

        if (string.IsNullOrEmpty(query))
        {
            // 일반 모드로 복원
            _normalListContainer.SetActive(true);
            _searchResultsContainer.SetActive(false);
            _searchCountText.gameObject.SetActive(false);
            UIBuilder.SetLayout(_searchCountText.gameObject, minHeight: 0, preferredHeight: 0);
            return;
        }

        // 검색 모드
        _normalListContainer.SetActive(false);
        _searchResultsContainer.SetActive(true);

        // 기존 검색 결과 제거
        // 즉시 제거 (같은 프레임에 재생성하므로 Destroy 대신 DestroyImmediate 사용)
        for (int i = _searchResultsContainer.transform.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.DestroyImmediate(_searchResultsContainer.transform.GetChild(i).gameObject);
        }

        // Fuzzy search
        string queryLower = query.ToLower();
        var scoredResults = new List<(APIMethodInfo method, int score)>();

        foreach (var method in _allMethods)
        {
            int score = CalculateFuzzyScore(method.Name.ToLower(), queryLower);
            if (method.Category != null)
            {
                int categoryScore = CalculateFuzzyScore(method.Category.ToLower(), queryLower);
                score = Math.Max(score, categoryScore / 2);
            }
            if (score > 0) scoredResults.Add((method, score));
        }

        scoredResults.Sort((a, b) => b.score.CompareTo(a.score));

        // 결과 표시
        foreach (var (method, score) in scoredResults)
        {
            var apiMethod = method;
            var row = UIBuilder.CreateHorizontalLayout(_searchResultsContainer.transform, 8);
            row.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var catLabel = UIBuilder.CreateText(row, $"[{method.Category}]",
                UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
            UIBuilder.SetLayout(catLabel.gameObject, minWidth: 100, preferredWidth: 100);

            var btn = UIBuilder.CreateAPIButton(row, method.Name,
                onClick: () => OnAPISelected?.Invoke(apiMethod));
            UIBuilder.SetLayout(btn.gameObject, flexibleWidth: 1);
        }

        _searchCountText.text = $"Results: {scoredResults.Count}";
        _searchCountText.gameObject.SetActive(true);
        UIBuilder.SetLayout(_searchCountText.gameObject, minHeight: 20, preferredHeight: 20);
    }

    private int CalculateFuzzyScore(string text, string query)
    {
        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(text)) return 0;
        if (text == query) return 1000;

        if (text.Contains(query))
        {
            if (text.StartsWith(query)) return 800 + query.Length * 10;
            return 500 + query.Length * 5;
        }

        int queryIndex = 0;
        int consecutiveBonus = 0;
        int lastMatchIndex = -1;
        int score = 0;

        for (int i = 0; i < text.Length && queryIndex < query.Length; i++)
        {
            if (text[i] == query[queryIndex])
            {
                score += 10;
                if (lastMatchIndex == i - 1)
                {
                    consecutiveBonus += 5;
                    score += consecutiveBonus;
                }
                else
                {
                    consecutiveBonus = 0;
                }
                if (i == 0 || !char.IsLetterOrDigit(text[i - 1])) score += 20;
                lastMatchIndex = i;
                queryIndex++;
            }
        }

        return queryIndex < query.Length ? 0 : score;
    }

    // ─── Parameter Input 뷰 구축 ───

    private void BuildParameterInputView()
    {
        _paramInputView = new GameObject("ParameterInputView");
        var rt = _paramInputView.AddComponent<RectTransform>();
        rt.SetParent(_safeArea, false);
        UIBuilder.SetStretch(rt);

        var vlg = _paramInputView.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = UIBuilder.Theme.SpacingNormal;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.padding = new RectOffset(
            (int)UIBuilder.Theme.Padding, (int)UIBuilder.Theme.Padding,
            (int)UIBuilder.Theme.Padding, (int)UIBuilder.Theme.Padding);

        // 헤더
        _paramHeaderText = UIBuilder.CreateText(_paramInputView.transform, "API: ",
            UIBuilder.Theme.FontHeader, UIBuilder.Theme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIBuilder.SetLayout(_paramHeaderText.gameObject, minHeight: 32, flexibleHeight: 0);

        _paramCategoryText = UIBuilder.CreateText(_paramInputView.transform, "Category: ",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        UIBuilder.SetLayout(_paramCategoryText.gameObject, minHeight: 20, flexibleHeight: 0);

        _noParamText = UIBuilder.CreateText(_paramInputView.transform, "No parameters required",
            UIBuilder.Theme.FontNormal, UIBuilder.Theme.TextSecondary);
        UIBuilder.SetLayout(_noParamText.gameObject, minHeight: 24, flexibleHeight: 0);

        // 스크롤 영역
        var scrollGo = new GameObject("ParamScroll");
        var scrollRt = scrollGo.AddComponent<RectTransform>();
        scrollRt.SetParent(_paramInputView.transform, false);
        UIBuilder.SetLayout(scrollGo, flexibleHeight: 1, minHeight: 100);

        _paramContent = UIBuilder.CreateScrollView(scrollRt, out _paramScrollRect);

        // 버튼 바
        var btnBar = UIBuilder.CreateHorizontalLayout(_paramInputView.transform, UIBuilder.Theme.SpacingNormal);
        UIBuilder.SetLayout(btnBar.gameObject, minHeight: 52, preferredHeight: 52, flexibleHeight: 0);

        var backBtn = UIBuilder.CreateButton(btnBar, "\u2190 Back", onClick: () => OnBackToList?.Invoke());
        UIBuilder.SetLayout(backBtn.gameObject, minWidth: 120, preferredWidth: 120);

        // 스페이서
        var spacer = new GameObject("Spacer");
        spacer.AddComponent<RectTransform>().SetParent(btnBar, false);
        UIBuilder.SetLayout(spacer, flexibleWidth: 1);

        var execBtn = UIBuilder.CreateButton(btnBar, "Execute \u2192", onClick: () => OnExecuteRequested?.Invoke(),
            style: UIBuilder.ButtonStyle.Accent);
        UIBuilder.SetLayout(execBtn.gameObject, minWidth: 140, preferredWidth: 140);

        _paramInputView.SetActive(false);
    }

    // ─── Result 뷰 구축 ───

    private void BuildResultView()
    {
        _resultView = new GameObject("ResultView");
        var rt = _resultView.AddComponent<RectTransform>();
        rt.SetParent(_safeArea, false);
        UIBuilder.SetStretch(rt);

        var vlg = _resultView.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = UIBuilder.Theme.SpacingNormal;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.padding = new RectOffset(
            (int)UIBuilder.Theme.Padding, (int)UIBuilder.Theme.Padding,
            (int)UIBuilder.Theme.Padding, (int)UIBuilder.Theme.Padding);

        // 헤더
        _resultHeaderText = UIBuilder.CreateText(_resultView.transform, "Result: ",
            UIBuilder.Theme.FontHeader, UIBuilder.Theme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIBuilder.SetLayout(_resultHeaderText.gameObject, minHeight: 32, flexibleHeight: 0);

        // 상태 배지
        var badgePanel = UIBuilder.CreatePanel(_resultView.transform, UIBuilder.Theme.SuccessBg);
        UIBuilder.SetLayout(badgePanel.gameObject, minHeight: 36, preferredHeight: 36, flexibleHeight: 0);
        _statusBadgeBg = badgePanel.GetComponent<Image>();
        _statusBadge = UIBuilder.CreateText(badgePanel, "",
            UIBuilder.Theme.FontLarge, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        var badgeTextRt = _statusBadge.GetComponent<RectTransform>();
        UIBuilder.SetStretch(badgeTextRt);

        // 표시 모드 토글 행
        _displayModeRow = new GameObject("DisplayModeRow");
        var dmRt = _displayModeRow.AddComponent<RectTransform>();
        dmRt.SetParent(_resultView.transform, false);
        var dmHlg = _displayModeRow.AddComponent<HorizontalLayoutGroup>();
        dmHlg.spacing = 8;
        dmHlg.childForceExpandWidth = false;
        dmHlg.childForceExpandHeight = false;
        dmHlg.childControlWidth = true;
        dmHlg.childControlHeight = true;
        dmHlg.childAlignment = TextAnchor.MiddleLeft;
        dmHlg.padding = new RectOffset(0, 0, 0, 0);
        UIBuilder.SetLayout(_displayModeRow, minHeight: 36, preferredHeight: 36, flexibleHeight: 0);

        UIBuilder.CreateText(_displayModeRow.transform, "Display:",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);

        _structuredBtn = UIBuilder.CreateButton(_displayModeRow.transform, "Structured",
            onClick: () => SetDisplayMode(ResultDisplayMode.Structured));
        UIBuilder.SetLayout(_structuredBtn.gameObject, minWidth: 80, preferredWidth: 80);
        _structuredBtnImg = _structuredBtn.GetComponent<Image>();

        _jsonBtn = UIBuilder.CreateButton(_displayModeRow.transform, "JSON",
            onClick: () => SetDisplayMode(ResultDisplayMode.RawJson));
        UIBuilder.SetLayout(_jsonBtn.gameObject, minWidth: 80, preferredWidth: 80);
        _jsonBtnImg = _jsonBtn.GetComponent<Image>();

        // Response label
        UIBuilder.CreateText(_resultView.transform, "Response:",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);

        // 스크롤 영역
        var scrollGo = new GameObject("ResultScroll");
        var scrollRt = scrollGo.AddComponent<RectTransform>();
        scrollRt.SetParent(_resultView.transform, false);
        UIBuilder.SetLayout(scrollGo, flexibleHeight: 1, minHeight: 100);

        _resultContent = UIBuilder.CreateScrollView(scrollRt, out _resultScrollRect);

        // 버튼 바
        var btnBar = UIBuilder.CreateHorizontalLayout(_resultView.transform, UIBuilder.Theme.SpacingNormal);
        UIBuilder.SetLayout(btnBar.gameObject, minHeight: 52, preferredHeight: 52, flexibleHeight: 0);

        var backBtn = UIBuilder.CreateButton(btnBar, "\u2190 Back to List",
            onClick: () => OnBackToList?.Invoke());
        UIBuilder.SetLayout(backBtn.gameObject, minWidth: 160, preferredWidth: 160);

        var spacer = new GameObject("Spacer");
        spacer.AddComponent<RectTransform>().SetParent(btnBar, false);
        UIBuilder.SetLayout(spacer, flexibleWidth: 1);

        var retryBtn = UIBuilder.CreateButton(btnBar, "Retry",
            onClick: () => OnRetry?.Invoke());
        UIBuilder.SetLayout(retryBtn.gameObject, minWidth: 120, preferredWidth: 120);

        _resultView.SetActive(false);
    }

    // ─── 뷰 전환 ───

    public void ShowView(ViewState view)
    {
        _apiListView.SetActive(view == ViewState.APIList);
        _paramInputView.SetActive(view == ViewState.ParameterInput);
        _resultView.SetActive(view == ViewState.Result);
    }

    // ─── Parameter Input 표시 ───

    /// <summary>
    /// 선택된 API의 파라미터 입력 UI를 생성하고 ParameterInput 뷰로 전환합니다.
    /// </summary>
    public void ShowParameterInput(APIMethodInfo method)
    {
        _paramHeaderText.text = $"API: {method.Name}";
        _paramCategoryText.text = $"Category: {method.Category}";

        // 기존 파라미터 필드 제거 (즉시 제거 후 새 필드 생성)
        for (int i = _paramContent.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.DestroyImmediate(_paramContent.GetChild(i).gameObject);
        }

        // 입력 상태 초기화
        ClearParameterState();

        if (!method.HasParameters)
        {
            _noParamText.gameObject.SetActive(true);
        }
        else
        {
            _noParamText.gameObject.SetActive(false);

            foreach (var param in method.Parameters)
            {
                InitializeDefaults(param.Name, param.Type);
                BuildParameterField(_paramContent, param.Name, param.Type, 0);
            }
        }

        // 스크롤 위치 리셋
        if (_paramScrollRect != null) _paramScrollRect.normalizedPosition = new Vector2(0, 1);

        ShowView(ViewState.ParameterInput);
    }

    // ─── 파라미터 UI 생성 (재귀) ───

    private void BuildParameterField(Transform parent, string fieldPath, Type type, int indentLevel)
    {
        string displayName = GetDisplayName(fieldPath);

        if (type.IsEnum)
        {
            BuildEnumField(parent, fieldPath, type, displayName, indentLevel);
        }
        else if (type == typeof(string))
        {
            BuildStringField(parent, fieldPath, displayName, indentLevel);
        }
        else if (type == typeof(int) || type == typeof(double) || type == typeof(float))
        {
            BuildNumberField(parent, fieldPath, displayName, indentLevel);
        }
        else if (type == typeof(bool))
        {
            BuildBoolField(parent, fieldPath, displayName, indentLevel);
        }
        else if (IsComplexObjectType(type))
        {
            BuildNestedObjectField(parent, fieldPath, type, displayName, indentLevel);
        }
        else
        {
            // 지원하지 않는 타입
            var label = UIBuilder.CreateText(parent, $"{displayName}: (Unsupported type: {type.Name})",
                UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextCallback);
        }
    }

    private void BuildStringField(Transform parent, string fieldPath, string displayName, int indentLevel)
    {
        var row = UIBuilder.CreateHorizontalLayout(parent, 8);
        row.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        if (indentLevel > 0)
        {
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(indentLevel * 20, 0, 0, 0);
        }

        var label = UIBuilder.CreateText(row, $"{displayName}:",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        UIBuilder.SetLayout(label.gameObject, minWidth: 100, preferredWidth: 100);

        var path = fieldPath;
        var input = UIBuilder.CreateInputField(row, "",
            onValueChanged: (v) => _stringInputs[path] = v);
        UIBuilder.SetLayout(input.gameObject, flexibleWidth: 1);
        input.text = _stringInputs.TryGetValue(fieldPath, out var s) ? s : "";
        _inputFieldRefs[fieldPath] = input;

        // PASTE 버튼
        var pasteBtn = UIBuilder.CreateButton(row, "PASTE", onClick: () => PasteFromClipboard(path));
        UIBuilder.SetLayout(pasteBtn.gameObject, minWidth: 70, preferredWidth: 70);
    }

    private void BuildNumberField(Transform parent, string fieldPath, string displayName, int indentLevel)
    {
        var row = UIBuilder.CreateHorizontalLayout(parent, 8);
        row.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        if (indentLevel > 0)
        {
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(indentLevel * 20, 0, 0, 0);
        }

        var label = UIBuilder.CreateText(row, $"{displayName}:",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        UIBuilder.SetLayout(label.gameObject, minWidth: 100, preferredWidth: 100);

        var path = fieldPath;
        var input = UIBuilder.CreateInputField(row, "0",
            onValueChanged: (v) =>
            {
                if (double.TryParse(v, out double n)) _numberInputs[path] = n;
                else if (string.IsNullOrEmpty(v)) _numberInputs[path] = 0;
            });
        UIBuilder.SetLayout(input.gameObject, flexibleWidth: 1);
        input.contentType = InputField.ContentType.DecimalNumber;
        double numVal = _numberInputs.TryGetValue(fieldPath, out var d) ? d : 0;
        input.text = numVal.ToString();
    }

    private void BuildBoolField(Transform parent, string fieldPath, string displayName, int indentLevel)
    {
        var container = new GameObject("BoolField");
        container.AddComponent<RectTransform>().SetParent(parent, false);
        var hlg = container.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        if (indentLevel > 0) hlg.padding = new RectOffset(indentLevel * 20, 0, 0, 0);
        container.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        bool isOn = _boolInputs.TryGetValue(fieldPath, out var b) ? b : false;
        var path = fieldPath;
        var toggle = UIBuilder.CreateToggle(container.transform, displayName, isOn,
            onToggle: (val) => _boolInputs[path] = val);
        UIBuilder.SetLayout(toggle.gameObject, flexibleWidth: 1);
    }

    private void BuildEnumField(Transform parent, string fieldPath, Type enumType, string displayName, int indentLevel)
    {
        var row = UIBuilder.CreateHorizontalLayout(parent, 8);
        row.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        if (indentLevel > 0)
        {
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(indentLevel * 20, 0, 0, 0);
        }

        var label = UIBuilder.CreateText(row, $"{displayName}:",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextSecondary);
        UIBuilder.SetLayout(label.gameObject, minWidth: 100, preferredWidth: 100);

        var enumNames = APIParameterInspector.GetEnumNames(enumType);
        int selectedIndex = _enumSelectedIndices.TryGetValue(fieldPath, out var idx) ? idx : 0;
        var path = fieldPath;
        var dropdown = UIBuilder.CreateDropdown(row, enumNames, selectedIndex,
            onValueChanged: (i) => _enumSelectedIndices[path] = i);
        UIBuilder.SetLayout(dropdown.gameObject, flexibleWidth: 1);
    }

    private void BuildNestedObjectField(Transform parent, string fieldPath, Type type, string displayName, int indentLevel)
    {
        var fields = APIParameterInspector.GetPublicFields(type);
        bool hasEditableFields = fields.Any(f => !APIParameterInspector.IsCallbackField(f));

        if (!hasEditableFields)
        {
            var label = UIBuilder.CreateText(parent, $"{displayName}: (Callback only - not editable)",
                UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextCallback);
            return;
        }

        bool isExpanded = _nestedFoldouts.TryGetValue(fieldPath, out var exp) ? exp : true;
        var path = fieldPath;

        // 헤더 버튼
        var headerRow = new GameObject("NestedHeader");
        headerRow.AddComponent<RectTransform>().SetParent(parent, false);
        var headerHlg = headerRow.AddComponent<HorizontalLayoutGroup>();
        headerHlg.spacing = 0;
        headerHlg.childForceExpandWidth = true;
        headerHlg.childForceExpandHeight = false;
        headerHlg.childControlWidth = true;
        headerHlg.childControlHeight = true;
        headerHlg.childAlignment = TextAnchor.MiddleLeft;
        if (indentLevel > 0) headerHlg.padding = new RectOffset(indentLevel * 20, 0, 0, 0);
        headerRow.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        string icon = isExpanded ? "\u25bc" : "\u25b6";

        // Content 컨테이너 (먼저 생성)
        var contentGo = new GameObject("NestedContent");
        contentGo.AddComponent<RectTransform>().SetParent(parent, false);
        var contentVlg = contentGo.AddComponent<VerticalLayoutGroup>();
        contentVlg.spacing = UIBuilder.Theme.SpacingSmall;
        contentVlg.childForceExpandWidth = true;
        contentVlg.childForceExpandHeight = false;
        contentVlg.childControlWidth = true;
        contentVlg.childControlHeight = true;
        contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Button headerBtn = null;
        headerBtn = UIBuilder.CreateButton(headerRow.transform,
            $"{icon} {displayName} ({type.Name})",
            onClick: () =>
            {
                bool nowExpanded = _nestedFoldouts.ContainsKey(path) && _nestedFoldouts[path];
                _nestedFoldouts[path] = !nowExpanded;
                contentGo.SetActive(!nowExpanded);

                var txt = headerBtn.GetComponentInChildren<Text>();
                if (txt != null)
                {
                    string newIcon = !nowExpanded ? "\u25bc" : "\u25b6";
                    txt.text = $"{newIcon} {displayName} ({type.Name})";
                }
            });
        var headerBtnText = headerBtn.GetComponentInChildren<Text>();
        if (headerBtnText != null)
        {
            headerBtnText.alignment = TextAnchor.MiddleLeft;
            headerBtnText.color = new Color(0.6f, 0.9f, 0.6f);
            headerBtnText.fontStyle = FontStyle.Bold;
        }

        // Content 내부에 필드 생성
        foreach (var field in fields)
        {
            if (APIParameterInspector.IsCallbackField(field))
            {
                UIBuilder.CreateText(contentGo.transform, $"{field.Name}: (Callback - not editable)",
                    UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextCallback);
                continue;
            }

            string nestedPath = $"{fieldPath}.{field.Name}";
            BuildParameterField(contentGo.transform, nestedPath, field.FieldType, indentLevel + 1);
        }

        contentGo.SetActive(isExpanded);
    }

    // ─── 파라미터 상태 관리 ───

    private void ClearParameterState()
    {
        _stringInputs.Clear();
        _numberInputs.Clear();
        _boolInputs.Clear();
        _enumSelectedIndices.Clear();
        _nestedFoldouts.Clear();
        _isPasting.Clear();
        _inputFieldRefs.Clear();
    }

    /// <summary>
    /// 재귀 탐색이 필요한 복합 타입(class 또는 struct)인지 판별합니다.
    /// string, 배열, primitive, enum은 제외합니다.
    /// </summary>
    private static bool IsComplexObjectType(Type type)
    {
        return type != typeof(string) && !type.IsArray && !type.IsPrimitive && !type.IsEnum
            && (type.IsClass || (type.IsValueType && !type.IsPrimitive));
    }

    private void InitializeDefaults(string basePath, Type type)
    {
        if (type == typeof(string))
            _stringInputs[basePath] = "";
        else if (type == typeof(int) || type == typeof(double) || type == typeof(float))
            _numberInputs[basePath] = 0;
        else if (type == typeof(bool))
            _boolInputs[basePath] = false;
        else if (type.IsEnum)
            _enumSelectedIndices[basePath] = 0;
        else if (IsComplexObjectType(type))
        {
            _nestedFoldouts[basePath] = true;
            var fields = APIParameterInspector.GetPublicFields(type);
            foreach (var field in fields)
            {
                if (APIParameterInspector.IsCallbackField(field)) continue;
                InitializeDefaults($"{basePath}.{field.Name}", field.FieldType);
            }
        }
    }

    /// <summary>
    /// 입력 상태에서 파라미터 객체를 조합합니다 (재귀).
    /// </summary>
    public object BuildParameterObject(string basePath, Type type)
    {
        if (type == typeof(string))
            return _stringInputs.TryGetValue(basePath, out var s) ? s : "";
        if (type == typeof(int))
            return (int)(_numberInputs.TryGetValue(basePath, out var n) ? n : 0);
        if (type == typeof(double))
            return _numberInputs.TryGetValue(basePath, out var d) ? d : 0.0;
        if (type == typeof(float))
            return (float)(_numberInputs.TryGetValue(basePath, out var f) ? f : 0.0);
        if (type == typeof(bool))
            return _boolInputs.TryGetValue(basePath, out var b) ? b : false;

        if (type.IsEnum)
        {
            var index = _enumSelectedIndices.TryGetValue(basePath, out var i) ? i : 0;
            return APIParameterInspector.GetEnumValueByIndex(type, index);
        }

        if (IsComplexObjectType(type))
        {
            var obj = Activator.CreateInstance(type);
            var fields = APIParameterInspector.GetPublicFields(type);
            foreach (var field in fields)
            {
                if (APIParameterInspector.IsCallbackField(field)) continue;
                var value = BuildParameterObject($"{basePath}.{field.Name}", field.FieldType);
                field.SetValue(obj, value);
            }
            return obj;
        }

        return null;
    }

    private string GetDisplayName(string fieldPath)
    {
        int lastDot = fieldPath.LastIndexOf('.');
        return lastDot >= 0 ? fieldPath.Substring(lastDot + 1) : fieldPath;
    }

    private async void PasteFromClipboard(string fieldPath)
    {
        if (_isPasting.TryGetValue(fieldPath, out var p) && p) return;
        _isPasting[fieldPath] = true;
        try
        {
            string text = await AIT.GetClipboardText();
            if (!string.IsNullOrEmpty(text))
            {
                _stringInputs[fieldPath] = text.Trim();
                if (_inputFieldRefs.TryGetValue(fieldPath, out var input))
                    input.text = text.Trim();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[InteractiveAPITesterUI] Clipboard read failed: {ex.Message}");
        }
        finally
        {
            _isPasting[fieldPath] = false;
        }
    }

    // ─── Result 표시 ───

    /// <summary>
    /// API 실행 결과를 표시합니다.
    /// </summary>
    public void ShowResult(string methodName, object result, bool success)
    {
        _lastResultSuccess = success;
        _lastResultObject = (success && result != null && !(result is string)) ? result : null;
        _lastResultText = result == null ? "null"
            : (result is string s) ? s
            : APIParameterInspector.SerializeToJson(result);

        _resultHeaderText.text = $"Result: {methodName}";

        // 상태 배지
        _statusBadge.text = success ? "\u2713 Success" : "\u2717 Failed";
        _statusBadgeBg.color = success ? UIBuilder.Theme.SuccessBg : UIBuilder.Theme.FailBg;

        // 표시 모드 토글 (성공 + 비문자열 결과인 경우에만)
        _displayModeRow.SetActive(success && _lastResultObject != null);

        _resultDisplayMode = ResultDisplayMode.Structured;
        UpdateDisplayModeButtons();
        RenderResult();

        // 스크롤 위치 리셋
        if (_resultScrollRect != null) _resultScrollRect.normalizedPosition = new Vector2(0, 1);

        ShowView(ViewState.Result);
    }

    private void SetDisplayMode(ResultDisplayMode mode)
    {
        _resultDisplayMode = mode;
        UpdateDisplayModeButtons();
        RenderResult();
    }

    private void UpdateDisplayModeButtons()
    {
        if (_structuredBtnImg != null)
            _structuredBtnImg.color = _resultDisplayMode == ResultDisplayMode.Structured
                ? new Color(0.3f, 0.6f, 0.3f) : UIBuilder.Theme.ButtonBg;
        if (_jsonBtnImg != null)
            _jsonBtnImg.color = _resultDisplayMode == ResultDisplayMode.RawJson
                ? new Color(0.3f, 0.6f, 0.3f) : UIBuilder.Theme.ButtonBg;
    }

    private void RenderResult()
    {
        // 기존 결과 제거 (즉시 제거 후 새 콘텐츠 생성)
        for (int i = _resultContent.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.DestroyImmediate(_resultContent.GetChild(i).gameObject);
        }

        if (_lastResultSuccess && _lastResultObject != null && _resultDisplayMode == ResultDisplayMode.Structured)
        {
            RenderStructuredResult(_resultContent, _lastResultObject, 0);
        }
        else
        {
            var text = UIBuilder.CreateText(_resultContent, _lastResultText,
                UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextPrimary);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var le = text.GetComponent<LayoutElement>();
            if (le != null) le.minHeight = 100;
        }
    }

    private const int MaxRenderDepth = 5;

    private void RenderStructuredResult(Transform parent, object obj, int indentLevel)
    {
        if (indentLevel > MaxRenderDepth)
        {
            RenderResultValue(parent, "(max depth)", indentLevel);
            return;
        }

        if (obj == null)
        {
            RenderResultValue(parent, "null", indentLevel);
            return;
        }

        var type = obj.GetType();

        if (APIParameterInspector.IsSimpleType(type))
        {
            string value = type == typeof(string) ? $"\"{obj}\"" : obj.ToString();
            RenderResultValue(parent, value, indentLevel);
            return;
        }

        if (type.IsEnum)
        {
            RenderResultValue(parent, obj.ToString(), indentLevel);
            return;
        }

        if (type.IsArray)
        {
            var array = (Array)obj;
            if (array.Length == 0)
            {
                RenderResultValue(parent, "[]", indentLevel);
                return;
            }
            for (int i = 0; i < array.Length; i++)
            {
                RenderResultKeyValue(parent, $"[{i}]", null, indentLevel);
                RenderStructuredResult(parent, array.GetValue(i), indentLevel + 1);
            }
            return;
        }

        var fields = APIParameterInspector.GetPublicFields(type);
        if (fields.Length == 0)
        {
            RenderResultValue(parent, obj.ToString(), indentLevel);
            return;
        }

        foreach (var field in fields)
        {
            if (APIParameterInspector.IsCallbackField(field)) continue;

            var value = field.GetValue(obj);
            var fieldType = field.FieldType;

            if (APIParameterInspector.IsSimpleType(fieldType) || fieldType.IsEnum)
            {
                string displayValue = value == null ? "null"
                    : (fieldType == typeof(string) ? $"\"{value}\"" : value.ToString());
                RenderResultKeyValue(parent, field.Name, displayValue, indentLevel);
            }
            else
            {
                RenderResultKeyValue(parent, field.Name, null, indentLevel);
                RenderStructuredResult(parent, value, indentLevel + 1);
            }
        }
    }

    private void RenderResultKeyValue(Transform parent, string key, string value, int indentLevel)
    {
        var row = UIBuilder.CreateHorizontalLayout(parent, 8);
        row.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        if (indentLevel > 0)
        {
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(indentLevel * 20, 0, 0, 0);
        }

        var keyText = UIBuilder.CreateText(row, $"{key}:",
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextResultKey, fontStyle: FontStyle.Bold);

        if (value != null)
        {
            UIBuilder.SetLayout(keyText.gameObject, minWidth: 140, preferredWidth: 140);
            var valText = UIBuilder.CreateText(row, value,
                UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextPrimary);
            valText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIBuilder.SetLayout(valText.gameObject, flexibleWidth: 1);
        }
    }

    private void RenderResultValue(Transform parent, string value, int indentLevel)
    {
        var container = new GameObject("ResultValue");
        container.AddComponent<RectTransform>().SetParent(parent, false);
        var hlg = container.AddComponent<HorizontalLayoutGroup>();
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        if (indentLevel > 0) hlg.padding = new RectOffset(indentLevel * 20, 0, 0, 0);
        container.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        UIBuilder.CreateText(container.transform, value,
            UIBuilder.Theme.FontSmall, UIBuilder.Theme.TextPrimary);
    }

    // ─── 정리 ───

    public void Destroy()
    {
        if (_canvas != null) UnityEngine.Object.Destroy(_canvas.gameObject);
    }
}
