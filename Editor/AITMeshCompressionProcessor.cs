// -----------------------------------------------------------------------
// <copyright file="AITMeshCompressionProcessor.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Mesh 압축 (build-time vertex quantization)
// </copyright>
// -----------------------------------------------------------------------
//
// 빌드 직전, 대상 Mesh(모델 임포트 자산 및 직렬화 Mesh .asset)의 압축 설정을 일시적으로
//   ModelImporterMeshCompression.Medium 으로 올려 정점 데이터(position/normal/uv/tangent)를
//   양자화한 뒤, .data 에 더 작은 메시가 구워지도록 한다. 빌드 종료(성공/실패 무관) 후
//   원본 설정/바이트로 원상 복원한다.
//
// 두 경로:
//   (a) 모델 임포트 자산(.fbx/.obj 등 AssetImporter 가 ModelImporter 인 것): importer.meshCompression
//       이 Off 인 것만 Medium 으로 올리고 SaveAndReimport. 이미 Low/Medium/High 로 명시 설정된 자산은
//       사용자 의도를 존중해 건드리지 않는다.
//   (b) 직렬화 Mesh .asset(예: AssetDatabase.CreateAsset(mesh, path) 로 만들어진 순수 Mesh 에셋):
//       임포터가 없으므로 UnityEditor.MeshUtility.SetMeshCompression(mesh, ...) 로 Mesh 객체에
//       직접 압축 플래그를 기록한다(2021.3 부터 존재하는 구 API). 이 값은 다음 직렬화(SaveAssets)
//       시점에 실제로 정점 데이터를 양자화한다.
//
// 대상 탐지: 빌드 씬 의존성(AssetDatabase.GetDependencies, 재귀) + Resources/ 하위(문자열 런타임
//   로드로 항상 강제 포함되는 경로) 의 Mesh 보유 자산만, Assets/ 아래만(기존 처리기 컨벤션 유지),
//   개당 원본 파일 크기가 <see cref="MinSourceBytes"/> 이상인 것만 대상으로 한다. 렌더러에 연결되지
//   않은 채 Resources/ 로만 강제 포함되는 메시(예: 절차 생성 배경 데이터)도 이 경로로 잡힌다.
//
// 비파괴: (a) 는 원본 .meta 를 <path>.meta.aitmeshcompbak 로, (b) 는 에셋 본체 파일을 통째로
//   <path>.aitmeshcompbak 로 백업(정점 압축 플래그가 에셋 자체에 직렬화되므로 .meta 만으로는 복원
//   불가), 빌드 후 원본을 그대로 복원한다. 빌드가 비정상 종료되어 복원이 누락돼도, 다음 에디터
//   로드 시 안전망(SafetyNetRestore)이 잔존 백업을 자동 복원한다.
//
// 통합: AITWebGLBuilder 가 BuildPipeline.BuildPlayer 직전에 ApplyForBuild, try/finally 의 finally
//   에서 RestoreForBuild 를 호출한다(다른 콘텐츠 최적화 처리기와 동일 패턴).
//
// ⚠ 손실: 정점 데이터(position/normal/uv/tangent)를 고정 소수점으로 양자화하는 lossy 변경이다.
//   저폴리·소형 메시는 대개 육안 차이가 없지만, 대형 지형/정밀 지오메트리는 아티팩트가 보일 수
//   있어 켠 뒤 시각 검증이 필요하다 — 신규 손실 레버 opt-in 컨벤션(auto=OFF)을 따른다.
//
// ⚠ 비용: reimport(모델) / 재직렬화(Mesh .asset) 는 대상 수·정점 수에 비례해 무겁다. apply +
//   복원으로 2회 발생하므로 명시적 opt-in 으로 기본 비활성이다.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 빌드 단계 Mesh 압축(정점 데이터 양자화) 처리기.
    /// <see cref="AITEditorScriptObject.meshCompression"/> 설정에 따라 동작한다.
    /// 런타임 컴포넌트는 없다(빌드 산출물만 작아질 뿐, 런타임 동작 동일).
    /// </summary>
    [InitializeOnLoad]
    public static class AITMeshCompressionProcessor
    {
        /// <summary>원본 .meta(모델) / 에셋 본체(직렬화 Mesh)를 보관하는 백업 접미사.</summary>
        private const string BackupSuffix = ".aitmeshcompbak";

        /// <summary>apply 가 진행 중임을 표시하는 마커(Unity 가 무시하는 '.' 접두 숨김 파일).</summary>
        private const string MarkerRelative = "Assets/.ait-meshcomp-active";

        /// <summary>개당 원본 파일 크기 임계값(바이트). 미만은 제외(작은 소품 메시 보호). 기본 256KB.</summary>
        private const long MinSourceBytes = 256 * 1024L;

        /// <summary>양쪽 경로 공통 목표 압축 레벨.</summary>
        private const ModelImporterMeshCompression TargetCompression = ModelImporterMeshCompression.Medium;

        /// <summary>한 번의 압축 적용 결과 핸들. finally 에서 정확한 복원에 사용.</summary>
        public sealed class MeshCompressionHandle
        {
            /// <summary>이번 빌드에서 압축이 실제로 수행되었는지.</summary>
            public bool Active;

            /// <summary>처리된 모델 임포트 자산 개수.</summary>
            public int ModelCount;

            /// <summary>처리된 직렬화 Mesh .asset 개수.</summary>
            public int AssetCount;

            /// <summary>처리 대상의 원본 파일 크기 합계(바이트).</summary>
            public long OriginalBytes;
        }

        static AITMeshCompressionProcessor()
        {
            EditorApplication.delayCall += SafetyNetRestore;
        }

        /// <summary>
        /// 빌드 직전 호출: 설정이 켜져 있으면 대상 메시를 Medium 압축으로 reimport/재직렬화한다.
        /// </summary>
        /// <param name="config">프로젝트 에디터 설정. null 이거나 기능이 꺼져 있으면 no-op.</param>
        /// <returns>복원에 사용할 핸들(항상 non-null).</returns>
        public static MeshCompressionHandle ApplyForBuild(AITEditorScriptObject config)
        {
            var handle = new MeshCompressionHandle();

            bool enabled = EffectiveEnabled(config);
            if (config == null || !enabled)
            {
                return handle;
            }

            try
            {
                var candidates = DetectInScopePaths();
                if (candidates.Count == 0)
                {
                    return handle;
                }

                CreateMarker();

                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                int modelCount = 0;
                int assetCount = 0;
                long totalBytes = 0;

                foreach (var path in candidates)
                {
                    if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/"))
                    {
                        continue; // Assets/ 아래만(기존 처리기 컨벤션 유지).
                    }

                    try
                    {
                        string full = Path.Combine(projectRoot, path);
                        if (!File.Exists(full))
                        {
                            continue;
                        }

                        long size = new FileInfo(full).Length;
                        if (size < MinSourceBytes)
                        {
                            continue; // 임계값 미만 → 소형 소품 보호.
                        }

                        var modelImporter = AssetImporter.GetAtPath(path) as ModelImporter;
                        if (modelImporter != null)
                        {
                            // (a) 모델 임포트 자산: Off 인 것만 Medium 으로 상향(이미 설정된 값은 사용자 의도 존중).
                            if (modelImporter.meshCompression != ModelImporterMeshCompression.Off)
                            {
                                continue;
                            }

                            if (!BackupAssetMeta(path, projectRoot))
                            {
                                continue;
                            }

                            modelImporter.meshCompression = TargetCompression;
                            modelImporter.SaveAndReimport();
                            modelCount++;
                            totalBytes += size;
                        }
                        else
                        {
                            // (b) 직렬화 Mesh .asset: 임포터가 없으므로 MeshUtility 로 정점 데이터 직접 양자화.
                            var mesh = AssetDatabase.LoadMainAssetAtPath(path) as Mesh;
                            if (mesh == null)
                            {
                                continue; // Mesh 를 담고 있지 않은 다른 타입의 씬 의존성 → 대상 아님.
                            }

                            if (!BackupAssetFile(path, projectRoot))
                            {
                                continue;
                            }

                            MeshUtility.SetMeshCompression(mesh, TargetCompression);
                            EditorUtility.SetDirty(mesh);
                            assetCount++;
                            totalBytes += size;
                        }
                    }
                    catch (Exception e)
                    {
                        // fail-open: 개별 자산 실패는 그 자산만 스킵, 전체 빌드는 계속 진행.
                        AITLog.Warning($"[AIT-MeshCompression] 에셋 처리 실패({path}), 건너뜀: {e.Message}", sentryCapture: false);
                    }
                }

                if (assetCount > 0)
                {
                    AssetDatabase.SaveAssets(); // 직렬화 Mesh 변경분을 디스크에 반영.
                }

                AssetDatabase.Refresh();

                handle.Active = (modelCount + assetCount) > 0;
                handle.ModelCount = modelCount;
                handle.AssetCount = assetCount;
                handle.OriginalBytes = totalBytes;

                if (!handle.Active)
                {
                    // 대상 0건 → 마커 제거(복원할 것 없음).
                    RemoveMarker();
                }

                double mb = totalBytes / (1024.0 * 1024.0);
                Debug.Log($"[AIT-MeshCompression] ✓ 메시 {modelCount + assetCount}개 압축(모델 {modelCount}, 에셋 {assetCount}), " +
                    $"원본 합계 {mb:F1}MB{(config.meshCompression < 0 ? " (자동)" : "")}.");
                return handle;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AIT-MeshCompression] 적용 예외 → 복원 후 건너뜀: {e}");
                RestoreAllBackups();
                RemoveMarker();
                AssetDatabase.Refresh();
                return new MeshCompressionHandle();
            }
        }

        /// <summary>빌드 종료 후(성공/실패 무관) 호출: 원본 압축 설정/바이트로 복원한다.</summary>
        public static void RestoreForBuild(MeshCompressionHandle handle)
        {
            if (handle == null || !handle.Active)
            {
                return;
            }

            try
            {
                int restored = RestoreAllBackups();
                RemoveMarker();
                AssetDatabase.Refresh();
                Debug.Log($"[AIT-MeshCompression] 복원 완료: {restored}개 에셋 원본 압축 설정 원상.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AIT-MeshCompression] 복원 예외: {e}");
            }
        }

        /// <summary>에디터 로드 시 안전망. 마커가 잔존하면(=이전 빌드가 복원 전에 종료) 백업을 자동 복원.</summary>
        private static void SafetyNetRestore()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string markerFull = Path.Combine(projectRoot, MarkerRelative);
                if (!File.Exists(markerFull))
                {
                    return; // 공통 경로: 잔존물 없음(빠른 반환)
                }

                int restored = RestoreAllBackups();
                RemoveMarker();
                if (restored > 0)
                {
                    AssetDatabase.Refresh();
                    Debug.LogWarning($"[AIT-MeshCompression] 안전망: 이전 빌드 잔존 백업 {restored}개를 복원했습니다.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-MeshCompression] 안전망 복원 중 예외(무시): {e}");
            }
        }

        // ─────────────────────────── 대상 탐지 ───────────────────────────

        /// <summary>
        /// 빌드에 포함될 가능성이 있는 Mesh 보유 자산 경로를 보수적으로 수집한다.
        /// 후보 출처:
        ///   - EditorBuildSettings 활성 씬들의 AssetDatabase.GetDependencies(재귀)
        ///   - Resources/ 하위(런타임 문자열 로드/강제 포함 경로) 의 t:Mesh 자산
        /// 크기·타입 필터는 호출자(ApplyForBuild)가 순회하며 적용한다. 탐지 실패/예외 시 빈 집합을
        /// 반환한다(no-op + 경고 로그).
        /// </summary>
        private static HashSet<string> DetectInScopePaths()
        {
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                // 1) 활성 씬 + 의존성(재귀) — 씬이 참조하는 모든 에셋 경로.
                var scenePaths = new List<string>();
                foreach (var scene in EditorBuildSettings.scenes)
                {
                    if (scene != null && scene.enabled && !string.IsNullOrEmpty(scene.path))
                    {
                        scenePaths.Add(scene.path);
                    }
                }

                if (scenePaths.Count > 0)
                {
                    foreach (var dep in AssetDatabase.GetDependencies(scenePaths.ToArray(), true))
                    {
                        if (!string.IsNullOrEmpty(dep) && dep.StartsWith("Assets/"))
                        {
                            candidates.Add(dep);
                        }
                    }
                }

                // 2) Resources/ 하위 Mesh 보유 자산(모델 임포트 자산 포함) — 런타임 문자열 로드로
                //    씬 의존성 그래프에 잡히지 않아도 항상 강제 포함되는 경로.
                foreach (var guid in AssetDatabase.FindAssets("t:Mesh"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/")
                        && path.Replace('\\', '/').Contains("/Resources/"))
                    {
                        candidates.Add(path);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-MeshCompression] 대상 탐지 예외(빈 목록으로 계속): {e.Message}");
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return candidates;
        }

        // ─────────────────────────── 백업/복원 ───────────────────────────

        /// <summary>에셋의 .meta 파일을 <path>.meta.aitmeshcompbak 로 백업(모델 임포트 자산용).</summary>
        private static bool BackupAssetMeta(string assetPath, string projectRoot)
        {
            try
            {
                string metaFull = Path.Combine(projectRoot, assetPath + ".meta");
                if (!File.Exists(metaFull))
                {
                    return false;
                }

                string bak = metaFull + BackupSuffix;
                if (!File.Exists(bak))
                {
                    File.Copy(metaFull, bak, true);
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-MeshCompression] .meta 백업 실패({assetPath}): {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 에셋 본체 파일을 <path>.aitmeshcompbak 로 백업(직렬화 Mesh .asset 용). 압축 플래그가
        /// 에셋 자체에 직렬화되므로(모델과 달리 .meta 가 아님) 본체 파일을 통째로 백업해야 복원 가능.
        /// </summary>
        private static bool BackupAssetFile(string assetPath, string projectRoot)
        {
            try
            {
                string full = Path.Combine(projectRoot, assetPath);
                if (!File.Exists(full))
                {
                    return false;
                }

                string bak = full + BackupSuffix;
                if (!File.Exists(bak))
                {
                    File.Copy(full, bak, true);
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-MeshCompression] 에셋 백업 실패({assetPath}): {e.Message}");
                return false;
            }
        }

        /// <summary>Assets 트리의 모든 *.aitmeshcompbak 를 원본으로 되돌리고 백업을 삭제한다. 복원 개수 반환.</summary>
        private static int RestoreAllBackups()
        {
            int restored = 0;
            string assetsPath = Application.dataPath;
            string projectRoot = Directory.GetParent(assetsPath).FullName;
            string[] backups;
            try
            {
                backups = Directory.GetFiles(assetsPath, "*" + BackupSuffix, SearchOption.AllDirectories);
            }
            catch
            {
                return 0;
            }

            foreach (var bak in backups)
            {
                // bak = "<original>.aitmeshcompbak" → original 은 .meta(모델) 또는 Mesh .asset 본체.
                string original = bak.Substring(0, bak.Length - BackupSuffix.Length);
                try
                {
                    File.Copy(bak, original, true);
                    File.Delete(bak);

                    // reimport 대상 에셋 경로 산출: .meta 면 본체 경로로 환원, 아니면 그 파일 자체.
                    string assetFull = original.EndsWith(".meta")
                        ? original.Substring(0, original.Length - ".meta".Length)
                        : original;
                    string rel = AbsoluteToProjectRelative(assetFull, projectRoot);
                    if (!string.IsNullOrEmpty(rel))
                    {
                        AssetDatabase.ImportAsset(rel, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    }

                    restored++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AIT-MeshCompression] 백업 복원 실패({bak}): {e.Message}");
                }
            }

            return restored;
        }

        private static void CreateMarker()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                File.WriteAllText(Path.Combine(projectRoot, MarkerRelative), "active");
            }
            catch
            {
                // 마커 생성 실패는 치명적이지 않음(안전망 가속용일 뿐).
            }
        }

        private static void RemoveMarker()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string m = Path.Combine(projectRoot, MarkerRelative);
                if (File.Exists(m))
                {
                    File.Delete(m);
                }
            }
            catch
            {
                // 무시
            }
        }

        private static string AbsoluteToProjectRelative(string absolute, string projectRoot)
        {
            string norm = absolute.Replace('\\', '/');
            string root = projectRoot.Replace('\\', '/').TrimEnd('/') + "/";
            return norm.StartsWith(root) ? norm.Substring(root.Length) : null;
        }

        // ─────────────────────────── 순수 내부 헬퍼 (테스트용, 에셋 DB 비의존) ───────────────────────────

        /// <summary>
        /// Mesh 압축 실효 활성 여부를 반환한다(tri-state 해석).
        /// null → false, meshCompression >= 0 → ==1, &lt;0 → GetDefaultMeshCompression()(=신규 손실
        /// 레버 opt-in 컨벤션에 따라 항상 false — audioStreamTranscode/textureStreamJpeg 와 동일 posture).
        /// </summary>
        internal static bool EffectiveEnabled(AITEditorScriptObject config)
        {
            if (config == null) return false;
            return config.meshCompression >= 0
                ? config.meshCompression == 1
                : AITDefaultSettings.GetDefaultMeshCompression();
        }
    }
}
