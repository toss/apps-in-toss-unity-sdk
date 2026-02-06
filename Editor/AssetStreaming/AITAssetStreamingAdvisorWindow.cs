using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor.AssetStreaming
{
    /// <summary>
    /// Asset Streaming Advisor EditorWindow
    /// 에셋 분석 결과를 표시하고 Addressable 변환을 실행하는 UI
    /// </summary>
    public class AITAssetStreamingAdvisorWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private AITAssetAnalysisReport report;
        private HashSet<string> selectedGuids = new HashSet<string>();

        // 필터
        private bool showHighlyRecommended = true;
        private bool showRecommended = true;
        private bool showOptional = false;
        private AssetType? filterAssetType = null;

        // UI 상태
        private bool isAnalyzing = false;

        // 스타일 캐시
        private static GUIStyle headerStyle;
        private static GUIStyle subHeaderStyle;

        public static void ShowWindow()
        {
            var window = GetWindow<AITAssetStreamingAdvisorWindow>("AIT Asset Streaming Advisor");
            window.minSize = new Vector2(600, 500);
            window.Show();
        }

        private void OnEnable()
        {
            RunAnalysis();
        }

        private void OnGUI()
        {
            InitStyles();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            GUILayout.Space(5);
            DrawStatusBar();
            GUILayout.Space(10);
            DrawSummary();
            GUILayout.Space(10);
            DrawFilters();
            GUILayout.Space(10);
            DrawAssetList();
            GUILayout.Space(10);
            DrawActions();
            GUILayout.Space(10);

            EditorGUILayout.EndScrollView();
        }

        private void InitStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
                subHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            }
        }

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal("box");

            // Addressables 설치 여부
            bool installed = report != null && report.addressablesInstalled;
            string installStatus = installed ? "Addressables: 설치됨" : "Addressables: 미설치";
            Color prevColor = GUI.color;
            GUI.color = installed ? Color.green : Color.yellow;
            EditorGUILayout.LabelField(installStatus, GUILayout.Width(180));
            GUI.color = prevColor;

            // 마지막 분석 시간
            if (report != null)
            {
                string timeStr = report.analyzedAt.ToString("HH:mm:ss");
                EditorGUILayout.LabelField($"마지막 분석: {timeStr}", GUILayout.Width(160));
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("분석 새로고침", GUILayout.Width(100)))
            {
                RunAnalysis(forceRefresh: true);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSummary()
        {
            EditorGUILayout.LabelField("요약", headerStyle);
            EditorGUILayout.BeginVertical("box");

            if (report == null || isAnalyzing)
            {
                EditorGUILayout.LabelField("분석 중...");
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField($"분석된 에셋 수: {report.assets.Count}개");
            EditorGUILayout.LabelField($"예상 절감: {AITBuildValidator.FormatFileSize(report.totalEstimatedSavingsBytes)}");

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"  강력 추천: {report.HighlyRecommendedCount}개", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"  추천: {report.RecommendedCount}개", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"  선택적: {report.OptionalCount}개", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawFilters()
        {
            EditorGUILayout.LabelField("필터", subHeaderStyle);
            EditorGUILayout.BeginHorizontal();

            showHighlyRecommended = GUILayout.Toggle(showHighlyRecommended, "강력 추천", "Button");
            showRecommended = GUILayout.Toggle(showRecommended, "추천", "Button");
            showOptional = GUILayout.Toggle(showOptional, "선택적", "Button");

            GUILayout.Space(20);

            // 에셋 타입 필터
            string[] typeNames = { "전체", "Texture", "Audio", "Mesh", "Video", "Font" };
            AssetType?[] typeValues = { null, AssetType.Texture, AssetType.Audio, AssetType.Mesh, AssetType.Video, AssetType.Font };

            int currentIndex = 0;
            for (int i = 0; i < typeValues.Length; i++)
            {
                if (filterAssetType == typeValues[i])
                {
                    currentIndex = i;
                    break;
                }
            }

            int newIndex = EditorGUILayout.Popup(currentIndex, typeNames, GUILayout.Width(100));
            filterAssetType = typeValues[newIndex];

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAssetList()
        {
            EditorGUILayout.LabelField("에셋 목록", subHeaderStyle);

            if (report == null || report.assets.Count == 0)
            {
                EditorGUILayout.HelpBox("분석 결과가 없습니다. 분석을 실행하세요.", MessageType.Info);
                return;
            }

            // 헤더
            EditorGUILayout.BeginHorizontal("box");
            GUILayout.Space(25); // 체크박스 공간
            EditorGUILayout.LabelField("경로", GUILayout.MinWidth(200));
            EditorGUILayout.LabelField("타입", GUILayout.Width(60));
            EditorGUILayout.LabelField("크기", GUILayout.Width(80));
            EditorGUILayout.LabelField("추천", GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            foreach (var asset in report.assets)
            {
                if (!ShouldShowAsset(asset))
                    continue;

                DrawAssetRow(asset);
            }
        }

        private bool ShouldShowAsset(AITAnalyzedAsset asset)
        {
            // 추천 수준 필터
            switch (asset.recommendation)
            {
                case RecommendationLevel.HighlyRecommended:
                    if (!showHighlyRecommended) return false;
                    break;
                case RecommendationLevel.Recommended:
                    if (!showRecommended) return false;
                    break;
                case RecommendationLevel.Optional:
                    if (!showOptional) return false;
                    break;
            }

            // 타입 필터
            if (filterAssetType.HasValue && asset.assetType != filterAssetType.Value)
                return false;

            return true;
        }

        private void DrawAssetRow(AITAnalyzedAsset asset)
        {
            Color prevBg = GUI.backgroundColor;

            // 이미 Addressable인 에셋은 회색 처리
            if (asset.isAlreadyAddressable)
            {
                GUI.backgroundColor = Color.gray;
            }

            EditorGUILayout.BeginHorizontal("box");

            // 체크박스
            bool wasSelected = selectedGuids.Contains(asset.guid);
            bool isSelected = EditorGUILayout.Toggle(wasSelected, GUILayout.Width(20));
            if (isSelected != wasSelected)
            {
                if (isSelected)
                    selectedGuids.Add(asset.guid);
                else
                    selectedGuids.Remove(asset.guid);
            }

            // 경로 (Resources/ 에셋에 경고 아이콘)
            if (asset.isInResources)
            {
                Color prevColor = GUI.contentColor;
                GUI.contentColor = new Color(1f, 0.7f, 0f); // 주황색
                EditorGUILayout.LabelField(new GUIContent(asset.assetPath, "Resources/ 에셋 - 코드 변경 필요"), GUILayout.MinWidth(200));
                GUI.contentColor = prevColor;
            }
            else if (asset.isAlreadyAddressable)
            {
                EditorGUILayout.LabelField(new GUIContent(asset.assetPath, "이미 Addressable로 변환됨"), GUILayout.MinWidth(200));
            }
            else
            {
                EditorGUILayout.LabelField(asset.assetPath, GUILayout.MinWidth(200));
            }

            // 타입
            EditorGUILayout.LabelField(asset.assetType.ToString(), GUILayout.Width(60));

            // 크기
            EditorGUILayout.LabelField(AITBuildValidator.FormatFileSize(asset.estimatedBuildContribution), GUILayout.Width(80));

            // 추천 수준
            string recLabel;
            switch (asset.recommendation)
            {
                case RecommendationLevel.HighlyRecommended:
                    recLabel = "강력 추천";
                    break;
                case RecommendationLevel.Recommended:
                    recLabel = "추천";
                    break;
                default:
                    recLabel = "선택적";
                    break;
            }
            EditorGUILayout.LabelField(recLabel, GUILayout.Width(80));

            EditorGUILayout.EndHorizontal();

            GUI.backgroundColor = prevBg;
        }

        private void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("추천 전체 선택"))
            {
                SelectRecommended();
            }

            if (GUILayout.Button("전체 해제"))
            {
                selectedGuids.Clear();
            }

            GUILayout.FlexibleSpace();

            GUI.enabled = selectedGuids.Count > 0;
            if (GUILayout.Button("변경 적용", GUILayout.Width(120), GUILayout.Height(30)))
            {
                ApplyChanges();
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            // Resources/ 에셋 선택 시 경고
            if (HasResourcesAssetsSelected())
            {
                EditorGUILayout.HelpBox(
                    "Resources/ 에셋이 선택되었습니다.\n변환 시 해당 에셋이 Resources/ 폴더 밖으로 이동됩니다.\nResources.Load() → Addressables.LoadAssetAsync() 코드 변경이 필요합니다.",
                    MessageType.Warning);
            }
        }

        private void SelectRecommended()
        {
            if (report == null) return;

            selectedGuids.Clear();
            foreach (var asset in report.assets)
            {
                if (asset.isAlreadyAddressable)
                    continue;
                if (asset.recommendation == RecommendationLevel.HighlyRecommended ||
                    asset.recommendation == RecommendationLevel.Recommended)
                {
                    selectedGuids.Add(asset.guid);
                }
            }
        }

        private bool HasResourcesAssetsSelected()
        {
            if (report == null) return false;

            foreach (var asset in report.assets)
            {
                if (asset.isInResources && selectedGuids.Contains(asset.guid))
                    return true;
            }
            return false;
        }

        private void ApplyChanges()
        {
#if !AIT_ADDRESSABLES_INSTALLED
            AITPlatformHelper.ShowInfoDialog(
                "Addressables 미설치",
                "Addressables 패키지가 설치되어 있지 않습니다.\n\nPackage Manager에서 'Addressables' 패키지를 설치해 주세요.\n(Window > Package Manager > Unity Registry > Addressables)",
                "확인");
            return;
#else
            // Resources/ 에셋 경고
            if (HasResourcesAssetsSelected())
            {
                int choice = AITPlatformHelper.ShowComplexDialog(
                    "Resources/ 에셋 이동 확인",
                    "선택된 에셋 중 Resources/ 폴더에 있는 에셋이 포함되어 있습니다.\n\n" +
                    "이 에셋들은 Resources/ 폴더 밖으로 자동 이동됩니다.\n" +
                    "기존 Resources.Load() 호출을 Addressables.LoadAssetAsync()로 변경해야 합니다.\n\n" +
                    "계속하시겠습니까?",
                    "계속", "취소", null);

                if (choice != 0) return;
            }

            var guids = new List<string>(selectedGuids);
            AITAddressablesConverter.ConvertAssets(guids);

            // 분석 캐시 무효화 후 재분석
            AITAssetAnalyzer.InvalidateCache();
            RunAnalysis(forceRefresh: true);
            selectedGuids.Clear();

            Debug.Log($"[AIT] {guids.Count}개 에셋이 Addressable로 변환되었습니다.");
#endif
        }

        private void RunAnalysis(bool forceRefresh = false)
        {
            isAnalyzing = true;
            report = AITAssetAnalyzer.Analyze(forceRefresh);
            isAnalyzing = false;
            Repaint();
        }
    }
}
