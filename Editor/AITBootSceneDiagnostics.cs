using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 부팅 씬(<c>scenes[0]</c>) 린트 — 텍스처/오디오/폰트 3개 스트리밍 레버 모두 "부팅 씬이 참조하지
    /// 않는 에셋"만 외부화할 수 있다는 공통 전제(AITLargeTextureExternalizer.BuildBootDependencySet)에
    /// 기대므로, 부팅 씬이 비대하면 세 레버가 동시에 조용히 무력화된다. 이 진단은 부팅 씬 재귀
    /// 의존성이 전체 빌드 콘텐츠에서 차지하는 비중을 콘솔에 보고해 그 공백을 메운다.
    ///
    /// 순수 진단(diagnostic-only) — 아무것도 변경하지 않고 경고만 남긴다. 절대 빌드를 실패시키지
    /// 않는다. 훅 방식은 <see cref="AITDataBreakdownReport"/>와 동일(IPostprocessBuildWithReport
    /// 구현만으로 자동 호출).
    /// </summary>
    internal class AITBootSceneDiagnostics : IPostprocessBuildWithReport
    {
        public int callbackOrder => 10000;

        // 부팅 씬 재귀 의존성이 전체 빌드 콘텐츠의 50%를 넘으면, 텍스처/오디오/폰트 스트리밍 3개 레버가
        // 이론상 최선을 다해도 절반 미만의 콘텐츠만 외부화 대상으로 삼을 수 있다는 뜻이다 — 레버 튜닝
        // 이전에 부팅 씬 자체를 줄이는 게 우선순위가 더 높다는 신호로 보기에 defensible한 절반 기준.
        internal const double BootShareWarnThresholdPct = 50.0;

        public void OnPostprocessBuild(BuildReport report)
        {
            try
            {
                if (report == null)
                    return;

                // AITDataBreakdownReport와 동일한 이유로 WebGL 빌드에만 한정한다(UPM 패키지라 파트너의
                // 다른 플랫폼 빌드에도 이 콜백이 호출됨).
                if (report.summary.platform != BuildTarget.WebGL)
                    return;

                string disableEnv = Environment.GetEnvironmentVariable("AIT_BOOT_SCENE_LINT_DISABLE");
                if (string.Equals(disableEnv, "true", StringComparison.OrdinalIgnoreCase))
                {
                    AITLog.Info("[AIT-BootSceneLint] AIT_BOOT_SCENE_LINT_DISABLE=true — 진단을 건너뜁니다.");
                    return;
                }

                Run(report);
            }
            catch (Exception e)
            {
                AITLog.Warning(
                    $"[AIT-BootSceneLint] 진단 실행 중 예외 발생(무시 — 빌드 산출물에는 영향 없음): {e.Message}",
                    sentryCapture: false);
            }
        }

        private static void Run(BuildReport report)
        {
            // scenes[0] — AITLargeTextureExternalizer.BuildBootDependencySet과 동일한 정의(enabled
            // 여부와 무관하게 Build Settings 목록의 첫 씬).
            var scenes = EditorBuildSettings.scenes;
            if (scenes == null || scenes.Length == 0 || string.IsNullOrEmpty(scenes[0].path))
            {
                AITLog.Info("[AIT-BootSceneLint] Build Settings에 씬이 없어 부팅 씬 진단을 건너뜁니다.");
                return;
            }
            string bootScenePath = scenes[0].path;

            HashSet<string> bootDeps;
            try
            {
                bootDeps = new HashSet<string>(
                    AssetDatabase.GetDependencies(bootScenePath, true), StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception e)
            {
                AITLog.Warning(
                    $"[AIT-BootSceneLint] 부팅 씬 의존성 산출 실패(진단 생략): {e.Message}", sentryCapture: false);
                return;
            }

            var byPath = BuildPackedBytesByPath(report, out long grandTotalBytes);
            if (grandTotalBytes <= 0)
            {
                try { grandTotalBytes = (long)report.summary.totalSize; }
                catch { grandTotalBytes = 0; }
            }

            RunBootShareCheck(bootDeps, byPath, grandTotalBytes);
        }

        /// <summary>packedAssets를 소스 경로 → 누적 바이트 맵으로 만든다(여러 출력 파일에 걸쳐 합산).</summary>
        private static Dictionary<string, long> BuildPackedBytesByPath(BuildReport report, out long grandTotal)
        {
            var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            try
            {
                var packed = report.packedAssets;
                if (packed != null)
                {
                    foreach (var pa in packed)
                    {
                        PackedAssetInfo[] contents;
                        try { contents = pa?.contents; }
                        catch { continue; }
                        if (contents == null)
                            continue;

                        foreach (var c in contents)
                        {
                            try
                            {
                                long bytes = (long)c.packedSize;
                                total += bytes;
                                if (!string.IsNullOrEmpty(c.sourceAssetPath))
                                {
                                    map.TryGetValue(c.sourceAssetPath, out long cur);
                                    map[c.sourceAssetPath] = cur + bytes;
                                }
                            }
                            catch { /* 항목 단위 실패는 무시 */ }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                AITLog.Warning($"[AIT-BootSceneLint] packedAssets 조회 실패(무시): {e.Message}", sentryCapture: false);
            }

            grandTotal = total;
            return map;
        }

        // ─────────────────────────── 순수 집계(유닛 테스트 대상) ───────────────────────────

        /// <summary>
        /// 순수 집계 함수 — 부팅 씬 재귀 의존성 경로 집합과 (소스 경로 → 패킹 바이트) 맵만으로 부팅 씬이
        /// 전체 빌드 콘텐츠에서 차지하는 비중을 계산한다. BuildReport 의존이 전혀 없어 유닛 테스트로
        /// 직접 검증 가능하다. grandTotalBytes가 0 이하이거나 bootDependencyPaths가 비어 있어도
        /// 0으로 나누지 않는다.
        /// </summary>
        internal static (long BootBytes, long GrandTotalBytes, double PercentOfTotal, bool ExceedsWarnThreshold) ComputeBootShare(
            IEnumerable<string> bootDependencyPaths,
            IReadOnlyDictionary<string, long> packedBytesByPath,
            long grandTotalBytes,
            double warnThresholdPct)
        {
            long bootBytes = 0;
            if (bootDependencyPaths != null && packedBytesByPath != null)
            {
                foreach (var p in bootDependencyPaths)
                {
                    if (!string.IsNullOrEmpty(p) && packedBytesByPath.TryGetValue(p, out long b))
                        bootBytes += b;
                }
            }

            double pct = grandTotalBytes > 0 ? bootBytes * 100.0 / grandTotalBytes : 0.0;
            bool exceeds = grandTotalBytes > 0 && pct >= warnThresholdPct;

            return (bootBytes, grandTotalBytes, pct, exceeds);
        }

        // ─────────────────────────── 어댑터(BuildReport I/O) ───────────────────────────

        private static void RunBootShareCheck(HashSet<string> bootDeps, Dictionary<string, long> byPath, long grandTotalBytes)
        {
            try
            {
                if (grandTotalBytes <= 0)
                {
                    AITLog.Info("[AIT-BootSceneLint] 전체 빌드 콘텐츠 크기를 확인할 수 없어 부팅 씬 비중 계산을 건너뜁니다.");
                    return;
                }

                var share = ComputeBootShare(bootDeps, byPath, grandTotalBytes, BootShareWarnThresholdPct);

                AITLog.Info(
                    $"[AIT-BootSceneLint] 부팅 씬 재귀 의존성: {MbStr(share.BootBytes)}MB / 전체 {MbStr(share.GrandTotalBytes)}MB " +
                    $"({PctStr(share.PercentOfTotal)}%)");

                if (share.ExceedsWarnThreshold)
                {
                    AITLog.Warning(
                        $"[AIT-BootSceneLint] 경고: 부팅 씬 재귀 의존성이 전체 빌드 콘텐츠의 {PctStr(share.PercentOfTotal)}%" +
                        $"({MbStr(share.BootBytes)}MB/{MbStr(share.GrandTotalBytes)}MB)를 차지합니다(기준값 " +
                        $"{BootShareWarnThresholdPct.ToString("0", CultureInfo.InvariantCulture)}% 이상). 텍스처·오디오·폰트 " +
                        "스트리밍 3개 레버는 모두 '부팅 씬이 참조하지 않는 에셋'만 외부화 대상으로 삼을 수 있어, 부팅 씬 " +
                        "의존성 비중이 크면 세 레버 모두 함께 무력화됩니다. 부팅 씬에서 당장 필요하지 않은 텍스처·오디오·폰트 " +
                        "참조(예: 후속 스테이지 전용 리소스)를 분리하면 세 레버가 함께 활성화됩니다.",
                        sentryCapture: false);
                }
                else
                {
                    AITLog.Info("[AIT-BootSceneLint] 부팅 씬 비중이 기준값 미만입니다 — 건강한 상태입니다.");
                }
            }
            catch (Exception e)
            {
                AITLog.Warning($"[AIT-BootSceneLint] 부팅 씬 비중 계산 실패(무시): {e.Message}", sentryCapture: false);
            }
        }

        private static string MbStr(long bytes) => (bytes / 1048576.0).ToString("0.00", CultureInfo.InvariantCulture);

        private static string PctStr(double pct) => pct.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
