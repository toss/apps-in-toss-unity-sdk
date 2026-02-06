#if AIT_ADDRESSABLES_INSTALLED
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace AppsInToss.Editor.AssetStreaming
{
    /// <summary>
    /// Addressables 변환 엔진
    /// 에셋을 AIT-Streaming-Assets 그룹으로 마킹하고 빌드
    /// </summary>
    public static class AITAddressablesConverter
    {
        /// <summary>
        /// Resources/ 에서 이동된 에셋의 원래 경로 기록 (Revert용)
        /// Key: GUID, Value: 원래 Resources/ 경로
        /// </summary>
        private static readonly Dictionary<string, string> movedFromResources = new Dictionary<string, string>();

        /// <summary>
        /// 에셋을 Addressable로 변환
        /// </summary>
        /// <param name="assetGuids">변환할 에셋 GUID 목록</param>
        public static void ConvertAssets(List<string> assetGuids)
        {
            if (assetGuids == null || assetGuids.Count == 0)
            {
                Debug.LogWarning("[AIT] 변환할 에셋이 없습니다.");
                return;
            }

            // Settings 가져오기 또는 생성
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                Debug.LogError("[AIT] Addressable Asset Settings를 생성할 수 없습니다.");
                return;
            }

            // AIT-Streaming-Assets 그룹 찾기 또는 생성
            var group = FindOrCreateGroup(settings);
            if (group == null)
            {
                Debug.LogError("[AIT] Addressable 그룹을 생성할 수 없습니다.");
                return;
            }

            int convertedCount = 0;
            foreach (string guid in assetGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    Debug.LogWarning($"[AIT] GUID를 찾을 수 없습니다: {guid}");
                    continue;
                }

                // Resources/ 에셋은 폴더 밖으로 이동
                if (AITAssetAnalyzer.IsInResourcesFolder(path))
                {
                    string newPath = MoveOutOfResources(path);
                    if (newPath != null)
                    {
                        movedFromResources[guid] = path;
                        path = newPath;
                    }
                    else
                    {
                        Debug.LogWarning($"[AIT] Resources/ 에셋 이동 실패: {path}");
                        continue;
                    }
                }

                // Addressable 엔트리 생성 또는 이동
                var entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
                if (entry != null)
                {
                    // 에셋 주소를 원래 경로로 설정 (기존 참조 유지)
                    entry.address = path;
                    convertedCount++;
                }
            }

            // Build/Load Path를 Local (StreamingAssets)로 설정
            ConfigureGroupPaths(group);

            AssetDatabase.SaveAssets();
            Debug.Log($"[AIT] {convertedCount}개 에셋이 '{AITAssetStreamingConfig.AddressableGroupName}' 그룹에 추가되었습니다.");
        }

        /// <summary>
        /// Addressable 변환 되돌리기
        /// </summary>
        /// <param name="assetGuids">되돌릴 에셋 GUID 목록</param>
        public static void RevertAssets(List<string> assetGuids)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning("[AIT] Addressable Asset Settings가 없습니다.");
                return;
            }

            int revertedCount = 0;
            foreach (string guid in assetGuids)
            {
                // Addressable 엔트리 제거
                if (settings.RemoveAssetEntry(guid))
                {
                    revertedCount++;

                    // Resources/ 에서 이동된 에셋은 원래 위치로 복원
                    if (movedFromResources.TryGetValue(guid, out string originalPath))
                    {
                        string currentPath = AssetDatabase.GUIDToAssetPath(guid);
                        if (!string.IsNullOrEmpty(currentPath))
                        {
                            string directory = Path.GetDirectoryName(originalPath);
                            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                            {
                                Directory.CreateDirectory(directory);
                            }

                            string result = AssetDatabase.MoveAsset(currentPath, originalPath);
                            if (string.IsNullOrEmpty(result))
                            {
                                Debug.Log($"[AIT] 에셋 복원: {currentPath} → {originalPath}");
                            }
                        }

                        movedFromResources.Remove(guid);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[AIT] {revertedCount}개 에셋의 Addressable 설정이 제거되었습니다.");
        }

        /// <summary>
        /// Addressable 에셋 빌드
        /// </summary>
        public static void BuildAddressableContent()
        {
            Debug.Log("[AIT] Addressable 에셋 빌드 시작...");

            AddressableAssetSettings.BuildPlayerContent(out var result);

            if (!string.IsNullOrEmpty(result.Error))
            {
                Debug.LogError($"[AIT] Addressable 빌드 실패: {result.Error}");
            }
            else
            {
                Debug.Log($"[AIT] Addressable 빌드 완료 (Duration: {result.Duration:F1}s)");
            }
        }

        /// <summary>
        /// AIT-Streaming-Assets 그룹 찾기 또는 생성
        /// </summary>
        private static AddressableAssetGroup FindOrCreateGroup(AddressableAssetSettings settings)
        {
            string groupName = AITAssetStreamingConfig.AddressableGroupName;

            // 기존 그룹 검색
            var group = settings.FindGroup(groupName);
            if (group != null)
                return group;

            // 새 그룹 생성
            group = settings.CreateGroup(groupName, false, false, true,
                new List<AddressableAssetGroupSchema>());

            // 스키마 추가
            group.AddSchema<BundledAssetGroupSchema>();
            group.AddSchema<ContentUpdateGroupSchema>();

            ConfigureGroupPaths(group);

            Debug.Log($"[AIT] Addressable 그룹 생성: {groupName}");
            return group;
        }

        /// <summary>
        /// 그룹의 Build/Load Path를 Local (StreamingAssets)로 설정
        /// </summary>
        private static void ConfigureGroupPaths(AddressableAssetGroup group)
        {
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null) return;

            // Local Build Path와 Local Load Path 사용 (StreamingAssets 기반)
            schema.BuildPath.SetVariableByName(group.Settings, AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(group.Settings, AddressableAssetSettings.kLocalLoadPath);
        }

        /// <summary>
        /// Resources/ 폴더에서 에셋을 밖으로 이동
        /// </summary>
        /// <param name="originalPath">원래 경로 (Resources/ 포함)</param>
        /// <returns>이동 후 새 경로 (실패 시 null)</returns>
        private static string MoveOutOfResources(string originalPath)
        {
            // Resources/ 를 StreamableAssets/로 대체
            // 예: Assets/Resources/Textures/Large.png → Assets/StreamableAssets/Textures/Large.png
            string newPath = originalPath.Replace("/Resources/", "/StreamableAssets/");
            if (newPath == originalPath)
            {
                // Assets/Resources/ 루트에 있는 경우
                newPath = originalPath.Replace("Assets/Resources/", "Assets/StreamableAssets/");
            }

            // 대상 디렉토리 생성
            string directory = Path.GetDirectoryName(newPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string result = AssetDatabase.MoveAsset(originalPath, newPath);
            if (string.IsNullOrEmpty(result))
            {
                Debug.Log($"[AIT] 에셋 이동: {originalPath} → {newPath}");
                return newPath;
            }

            Debug.LogWarning($"[AIT] 에셋 이동 실패: {result}");
            return null;
        }
    }
}
#endif
