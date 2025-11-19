using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AppsInToss
{
    /// <summary>
    /// 빌드 히스토리 관리 클래스
    /// </summary>
    public class AITBuildHistory
    {
        private const string HistoryFilePath = "Library/AITBuildHistory.json";
        private const int MaxHistoryCount = 50; // 최대 50개 히스토리 저장

        /// <summary>
        /// 빌드 히스토리 추가
        /// </summary>
        public static void AddHistory(BuildHistoryEntry entry)
        {
            var history = LoadHistory();
            history.Insert(0, entry); // 최신 항목을 앞에 추가

            // 최대 개수 제한
            if (history.Count > MaxHistoryCount)
            {
                history = history.Take(MaxHistoryCount).ToList();
            }

            SaveHistory(history);
        }

        /// <summary>
        /// 빌드 히스토리 로드
        /// </summary>
        public static List<BuildHistoryEntry> LoadHistory()
        {
            try
            {
                if (File.Exists(HistoryFilePath))
                {
                    string json = File.ReadAllText(HistoryFilePath);
                    var wrapper = JsonUtility.FromJson<BuildHistoryWrapper>(json);
                    return wrapper?.entries ?? new List<BuildHistoryEntry>();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] 빌드 히스토리 로드 실패: {e.Message}");
            }

            return new List<BuildHistoryEntry>();
        }

        /// <summary>
        /// 빌드 히스토리 저장
        /// </summary>
        private static void SaveHistory(List<BuildHistoryEntry> history)
        {
            try
            {
                var wrapper = new BuildHistoryWrapper { entries = history };
                string json = JsonUtility.ToJson(wrapper, true);
                File.WriteAllText(HistoryFilePath, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] 빌드 히스토리 저장 실패: {e.Message}");
            }
        }

        /// <summary>
        /// 빌드 히스토리 삭제
        /// </summary>
        public static void ClearHistory()
        {
            try
            {
                if (File.Exists(HistoryFilePath))
                {
                    File.Delete(HistoryFilePath);
                    Debug.Log("[AIT] 빌드 히스토리가 삭제되었습니다.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] 빌드 히스토리 삭제 실패: {e.Message}");
            }
        }

        /// <summary>
        /// 빌드 통계 계산
        /// </summary>
        public static BuildStatistics GetStatistics()
        {
            var history = LoadHistory();
            if (history.Count == 0)
            {
                return new BuildStatistics();
            }

            var stats = new BuildStatistics
            {
                totalBuilds = history.Count,
                successfulBuilds = history.Count(h => h.success),
                failedBuilds = history.Count(h => !h.success),
                averageBuildTime = history.Where(h => h.success).Average(h => h.buildTimeSeconds),
                lastBuildDate = history.First().timestamp
            };

            return stats;
        }
    }

    /// <summary>
    /// 빌드 히스토리 항목
    /// </summary>
    [Serializable]
    public class BuildHistoryEntry
    {
        public string timestamp;
        public string buildType; // "WebGL", "Package", "Full"
        public bool success;
        public float buildTimeSeconds;
        public string unityVersion;
        public string appVersion;
        public long buildSizeBytes;
        public string errorMessage;

        public BuildHistoryEntry()
        {
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            unityVersion = Application.unityVersion;
        }
    }

    /// <summary>
    /// 빌드 히스토리 래퍼 (JsonUtility용)
    /// </summary>
    [Serializable]
    public class BuildHistoryWrapper
    {
        public List<BuildHistoryEntry> entries = new List<BuildHistoryEntry>();
    }

    /// <summary>
    /// 빌드 통계
    /// </summary>
    [Serializable]
    public class BuildStatistics
    {
        public int totalBuilds;
        public int successfulBuilds;
        public int failedBuilds;
        public double averageBuildTime;
        public string lastBuildDate;

        public float SuccessRate => totalBuilds > 0 ? (float)successfulBuilds / totalBuilds * 100f : 0f;
    }
}
