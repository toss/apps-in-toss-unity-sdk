using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// WebGL 빌드 산출물 <c>.data</c> 파일의 구성(타입별 + 개별 자산 TOP-N)을 콘솔 테이블로 남기는
    /// 순수 진단(diagnostic-only) 후처리기.
    ///
    /// CI Heavy 픽스처 기준 TTFF 6.8s 중 ~2.7s가 34.0MB on-wire 다운로드다. 텍스처/오디오/폰트
    /// 최적화 레버가 이미 여러 개 있지만 "지금 .data가 무엇으로 채워져 있는지" 보여주는 수단이 없어
    /// 다음 레버 선택이 근거 없이 이뤄지고 있었다. <see cref="BuildReport.packedAssets"/>가 이미
    /// 그 답(출력 파일별 소스 자산 경로/타입/패킹 크기)을 갖고 있는데 소비되지 않고 있었다.
    ///
    /// IPostprocessBuildWithReport 구현만으로 Unity 빌드 파이프라인이 자동 호출하므로(신규 파일
    /// 추가만으로 훅) 기존 빌드 스크립트를 건드릴 필요가 없다. AITWebGLBuilder가 콘텐츠 최적화
    /// 프로세서를 원복하는 finally 블록보다 이 콜백이 먼저 실행되므로, 실제로 빌드에 패키징된 상태
    /// 그대로를 읽는다. 순수 진단 불변식 — 아무것도 변경하지 않고, 실패해도 경고만 남기고 빌드는
    /// 계속된다(예외를 절대 밖으로 던지지 않음).
    /// </summary>
    internal class AITDataBreakdownReport : IPostprocessBuildWithReport
    {
        // 다른 후처리기와 순서 의존성이 없다 — 충분히 낮은 우선순위(늦게 실행)로 설정해
        // packedAssets가 완전히 채워진 뒤 읽히도록 한다.
        public int callbackOrder => 10000;

        private const int TopAssetCount = 20;

        // ait-stream-* 매니페스트 공통 계약: 모든 엔트리가 "guid" 필드를 갖는다(텍스처/오디오/폰트
        // 세 매니페스트 스키마가 서로 달라도 guid는 공통 — AITLargeTextureExternalizer/
        // AITAudioStreamingProcessor/AITFontExternalizer/AITFontLazyExtensionBuilder의 entries.Add 참조).
        private const string TextureManifestRel = "Assets/StreamingAssets/ait-stream-texture/manifest.json";
        private const string AudioManifestRel = "Assets/StreamingAssets/ait-stream-audio/manifest.json";
        private const string FontManifestRel = "Assets/StreamingAssets/ait-stream-font/manifest.json";

        private static readonly Regex GuidRegex =
            new Regex("\"guid\"\\s*:\\s*\"([0-9a-fA-F]{32})\"", RegexOptions.Compiled);

        public void OnPostprocessBuild(BuildReport report)
        {
            try
            {
                if (report == null)
                    return;

                // 이 SDK는 UPM 패키지로 배포되어 파트너 프로젝트 전체에 임포트된다 — 즉 이 콜백은
                // 파트너가 WebGL이 아닌 다른 플랫폼을 같은 프로젝트에서 빌드할 때도 호출된다. .data
                // 분해는 WebGL 전용 개념이므로 다른 플랫폼 빌드에서는 즉시 종료한다.
                if (report.summary.platform != BuildTarget.WebGL)
                    return;

                string disableEnv = Environment.GetEnvironmentVariable("AIT_DATA_BREAKDOWN_DISABLE");
                if (string.Equals(disableEnv, "true", StringComparison.OrdinalIgnoreCase))
                {
                    AITLog.Info("[AIT-DataBreakdown] AIT_DATA_BREAKDOWN_DISABLE=true — 진단을 건너뜁니다.");
                    return;
                }

                Run(report);
            }
            catch (Exception e)
            {
                // 진단 실패는 절대 빌드를 깨서는 안 된다 — 경고만 남기고 조용히 넘어간다.
                AITLog.Warning(
                    $"[AIT-DataBreakdown] 진단 실행 중 예외 발생(무시 — 빌드 산출물에는 영향 없음): {e.Message}",
                    sentryCapture: false);
            }
        }

        // ─────────────────────────── 순수 집계(유닛 테스트 대상) ───────────────────────────

        /// <summary>집계 결과. ByType/TopAssets 원소는 이름 붙은 튜플이라 별도 타입 선언이 필요 없다.</summary>
        internal readonly struct BreakdownResult
        {
            public readonly long DataTotalBytes;
            public readonly long GrandTotalBytes;
            public readonly List<(string Type, int Count, long Bytes, double PercentOfData)> ByType;
            public readonly List<(string SourcePath, string TypeName, long Bytes, double PercentOfData, bool Externalized, string ExternalizedLever)> TopAssets;

            public BreakdownResult(
                long dataTotalBytes, long grandTotalBytes,
                List<(string, int, long, double)> byType,
                List<(string, string, long, double, bool, string)> topAssets)
            {
                DataTotalBytes = dataTotalBytes;
                GrandTotalBytes = grandTotalBytes;
                ByType = byType;
                TopAssets = topAssets;
            }
        }

        /// <summary>
        /// 순수 집계 함수 — 평탄화된 (소스경로, 타입, 바이트) 목록과 외부화 태그 맵만으로 타입별
        /// 구성과 TOP-N 자산을 계산한다. BuildReport 의존이 전혀 없어 유닛 테스트로 직접 검증
        /// 가능하다. null/빈 입력, topN이 입력보다 큰 경우, total이 0인 경우를 모두 방어한다.
        /// </summary>
        internal static BreakdownResult Aggregate(
            IReadOnlyList<(string SourcePath, string TypeName, long Bytes)> dataEntries,
            long grandTotalBytes,
            int topN,
            IReadOnlyDictionary<string, string> tagMap)
        {
            dataEntries ??= Array.Empty<(string, string, long)>();

            long dataTotal = dataEntries.Sum(e => e.Bytes);

            var byType = dataEntries
                .GroupBy(e => e.TypeName ?? string.Empty)
                .Select(g =>
                {
                    long bytes = g.Sum(e => e.Bytes);
                    return (Type: g.Key, Count: g.Count(), Bytes: bytes, PercentOfData: PercentOf(bytes, dataTotal));
                })
                .OrderByDescending(t => t.Bytes)
                .ToList();

            var topAssets = dataEntries
                .Where(e => !string.IsNullOrEmpty(e.SourcePath))
                .OrderByDescending(e => e.Bytes)
                .Take(Math.Max(0, topN))
                .Select(e =>
                {
                    string lever = null;
                    bool externalized = tagMap != null && tagMap.TryGetValue(e.SourcePath, out lever);
                    return (e.SourcePath, e.TypeName, e.Bytes, PercentOfData: PercentOf(e.Bytes, dataTotal),
                        Externalized: externalized, ExternalizedLever: externalized ? lever : null);
                })
                .ToList();

            return new BreakdownResult(dataTotal, grandTotalBytes, byType, topAssets);
        }

        private static double PercentOf(long part, long total) => total <= 0 ? 0.0 : part * 100.0 / total;

        // ─────────────────────────── 어댑터(BuildReport I/O) ───────────────────────────

        private struct RawPackedEntry
        {
            public string ShortPath;
            public string SourcePath;
            public string TypeName;
            public long Bytes;
        }

        private static void Run(BuildReport report)
        {
            var allRaw = ExtractPackedEntries(report);
            if (allRaw.Count == 0)
            {
                AITLog.Info(
                    "[AIT-DataBreakdown] BuildReport.packedAssets가 비어 있습니다 — 이 Unity 버전/빌드 경로에서 " +
                    "패키징 상세 정보를 제공하지 않을 수 있습니다. 진단을 건너뜁니다(빌드에는 영향 없음).");
                return;
            }

            // WebGL은 Split Application Binary(Android 전용)가 없어 출력 .data가 항상 하나이고, Unity가
            // 직렬화한 파일들(level0 / sharedassets*.assets / resources.assets / globalgamemanagers*)이
            // 그대로 그 하나의 .data 컨테이너로 묶인다. 즉 이 콜백 시점의 packedAssets 전체가 곧 .data의
            // 내용물이므로 별도 필터가 필요 없다.
            //
            // 초기 구현은 shortPath에 ".data"가 포함된 항목만 골랐는데, shortPath는 컨테이너 이름이 아니라
            // 그 안에 묶이는 직렬화 파일 이름이라 WebGL에서 매칭이 항상 0건이 되어 진단이 통째로
            // 건너뛰어졌다(E2E 런 31134175566에서 확인).
            LogPackedContainers(allRaw);

            var dataEntries = allRaw
                .Select(e => (e.SourcePath, e.TypeName, e.Bytes))
                .ToList();

            long grandTotal = allRaw.Sum(e => e.Bytes);

            // 외부화 크로스 레퍼런스(best-effort) — 매니페스트가 없으면(스트리밍 비활성/미실행) 조용히
            // 생략되고 태깅 없는 리포트가 나간다.
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var tagMap = BuildExternalizationTagMap(projectRoot, out var countByLever, out var manifestFound);

            var result = Aggregate(dataEntries, grandTotal, TopAssetCount, tagMap);
            LogConsoleTable(result, countByLever, manifestFound);
        }

        /// <summary>
        /// 패킹된 출력 파일 이름을 한 줄로 남긴다. .data에 무엇이 묶였는지의 근거이자, Unity 버전에 따라
        /// shortPath 명명이 달라졌을 때 집계가 어긋난 원인을 빌드 로그만으로 판별하기 위한 관측 지점이다
        /// (이 진단이 처음 무력화된 원인이 정확히 shortPath 형태에 대한 오해였다).
        /// </summary>
        private static void LogPackedContainers(List<RawPackedEntry> allRaw)
        {
            const int MaxNames = 8;

            var names = allRaw
                .Select(e => e.ShortPath)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (names.Count == 0)
                return;

            string shown = string.Join(", ", names.Take(MaxNames));
            string suffix = names.Count > MaxNames
                ? $" 외 {(names.Count - MaxNames).ToString(CultureInfo.InvariantCulture)}개"
                : string.Empty;
            AITLog.Info(
                $"[AIT-DataBreakdown] 패킹 출력 파일 {names.Count.ToString(CultureInfo.InvariantCulture)}개: {shown}{suffix}");
        }

        /// <summary>
        /// report.packedAssets를 평탄화한다. 배열/필드 접근 어느 하나라도 실패해도 전체 진단이
        /// 죽지 않도록 개별 단위로 방어한다(버전별 가용성 차이에 대한 fail-safe).
        /// </summary>
        private static List<RawPackedEntry> ExtractPackedEntries(BuildReport report)
        {
            var list = new List<RawPackedEntry>();

            PackedAssets[] packed;
            try
            {
                packed = report.packedAssets;
            }
            catch (Exception e)
            {
                AITLog.Warning(
                    $"[AIT-DataBreakdown] packedAssets 접근 실패(이 버전에서 미지원일 수 있음, 무시): {e.Message}",
                    sentryCapture: false);
                return list;
            }

            if (packed == null)
                return list;

            foreach (var pa in packed)
            {
                if (pa == null)
                    continue;

                string shortPath;
                try { shortPath = pa.shortPath ?? string.Empty; }
                catch { shortPath = string.Empty; }

                PackedAssetInfo[] contents;
                try { contents = pa.contents; }
                catch { continue; }
                if (contents == null)
                    continue;

                foreach (var c in contents)
                {
                    try
                    {
                        string typeName = "(알 수 없음)";
                        try { typeName = c.type != null ? c.type.Name : "(알 수 없음)"; }
                        catch { /* 일부 버전/타입에서 조회 실패 가능 — 알 수 없음으로 집계 */ }

                        list.Add(new RawPackedEntry
                        {
                            ShortPath = shortPath,
                            SourcePath = c.sourceAssetPath ?? string.Empty,
                            TypeName = typeName,
                            Bytes = (long)c.packedSize
                        });
                    }
                    catch (Exception e)
                    {
                        AITLog.Warning(
                            $"[AIT-DataBreakdown] packedAssets 항목 파싱 실패(해당 항목만 건너뜀): {e.Message}",
                            sentryCapture: false);
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// 3개 스트리밍 매니페스트(텍스처/오디오/폰트)를 읽어 "이미 .data 밖으로 외부화된" 소스 경로를
        /// 식별한다. guid → AssetDatabase.GUIDToAssetPath 로 원본 경로를 재조회하는 방식이라, 이 콜백
        /// 시점(AITWebGLBuilder의 finally 원복 이전)에는 GUID→경로 매핑이 여전히 원본 자산을 가리킨다.
        /// packedAssets는 이미 "외부화 이후" 상태(스텁/서브셋 크기)를 정확히 반영하므로 바이트 보정은
        /// 필요 없다 — 태그는 이미 외부화된 자산을 재최적화 후보로 착각(오귀속)하지 않도록 붙인다.
        /// </summary>
        private static Dictionary<string, string> BuildExternalizationTagMap(
            string projectRoot,
            out Dictionary<string, int> countByLever,
            out Dictionary<string, bool> manifestFound)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            countByLever = new Dictionary<string, int> { ["texture"] = 0, ["audio"] = 0, ["font"] = 0 };
            manifestFound = new Dictionary<string, bool> { ["texture"] = false, ["audio"] = false, ["font"] = false };

            TagFromManifest(projectRoot, TextureManifestRel, "텍스처 스트리밍", "texture", map, countByLever, manifestFound);
            TagFromManifest(projectRoot, AudioManifestRel, "오디오 스트리밍", "audio", map, countByLever, manifestFound);
            TagFromManifest(projectRoot, FontManifestRel, "폰트 외부화(스트리밍/지연 로드)", "font", map, countByLever, manifestFound);

            return map;
        }

        private static void TagFromManifest(
            string projectRoot, string relManifestPath, string label, string leverKey,
            Dictionary<string, string> map, Dictionary<string, int> countByLever,
            Dictionary<string, bool> manifestFound)
        {
            try
            {
                string full = Path.Combine(projectRoot, relManifestPath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(full))
                    return;

                manifestFound[leverKey] = true;
                string json = File.ReadAllText(full);

                foreach (Match m in GuidRegex.Matches(json))
                {
                    string guid = m.Groups[1].Value;
                    string path;
                    try { path = AssetDatabase.GUIDToAssetPath(guid); }
                    catch { continue; }
                    if (string.IsNullOrEmpty(path))
                        continue;

                    if (!map.ContainsKey(path))
                    {
                        map[path] = label;
                        countByLever[leverKey]++;
                    }
                }
            }
            catch (Exception e)
            {
                AITLog.Warning(
                    $"[AIT-DataBreakdown] 외부화 매니페스트 읽기 실패(무시 — 해당 레버 태깅만 생략, 총계에는 영향 없음): " +
                    $"{relManifestPath} — {e.Message}",
                    sentryCapture: false);
            }
        }

        private static void LogConsoleTable(
            BreakdownResult result,
            Dictionary<string, int> countByLever,
            Dictionary<string, bool> manifestFound)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[AIT-DataBreakdown] ========================================");
            sb.AppendLine("[AIT-DataBreakdown] .data 구성 분해 리포트 (WebGL 빌드 산출물, 순수 진단 — 빌드 결과 변경 없음)");
            sb.AppendLine("[AIT-DataBreakdown] ========================================");
            sb.AppendLine(
                $"[AIT-DataBreakdown] .data 총계: {Mb(result.DataTotalBytes)}MB (packedAssets 기준, 전체 산출물 합계 {Mb(result.GrandTotalBytes)}MB 중 일부)");

            sb.AppendLine("[AIT-DataBreakdown] --- 타입별 구성 ---");
            foreach (var t in result.ByType)
                sb.AppendLine($"[AIT-DataBreakdown]   {t.Type,-16} {Mb(t.Bytes),8}MB ({PctStr(t.PercentOfData),5}%)  {t.Count}개");

            sb.AppendLine($"[AIT-DataBreakdown] --- 개별 자산 TOP {result.TopAssets.Count} ---");
            int rank = 1;
            foreach (var a in result.TopAssets)
            {
                string tag = a.Externalized
                    ? $" [외부화됨: {a.ExternalizedLever} — 표시 크기는 스텁/서브셋 잔존분, 재최적화 후보 아님]"
                    : string.Empty;
                sb.AppendLine($"[AIT-DataBreakdown]   {rank,2}. {a.SourcePath} ({a.TypeName}) {Mb(a.Bytes)}MB ({PctStr(a.PercentOfData)}%){tag}");
                rank++;
            }

            sb.AppendLine("[AIT-DataBreakdown] --- 외부화 크로스 레퍼런스(best-effort) ---");
            sb.AppendLine($"[AIT-DataBreakdown]   텍스처 스트리밍: {(manifestFound["texture"] ? $"발견, {countByLever["texture"]}개 태깅" : "매니페스트 없음(비활성 또는 미실행)")}");
            sb.AppendLine($"[AIT-DataBreakdown]   오디오 스트리밍: {(manifestFound["audio"] ? $"발견, {countByLever["audio"]}개 태깅" : "매니페스트 없음(비활성 또는 미실행)")}");
            sb.AppendLine($"[AIT-DataBreakdown]   폰트 외부화: {(manifestFound["font"] ? $"발견, {countByLever["font"]}개 태깅" : "매니페스트 없음(비활성 또는 미실행)")}");
            sb.Append("[AIT-DataBreakdown] ========================================");

            AITLog.Info(sb.ToString());
        }

        private static string Mb(long bytes) => (bytes / 1048576.0).ToString("0.00", CultureInfo.InvariantCulture);

        private static string PctStr(double pct) => pct.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
