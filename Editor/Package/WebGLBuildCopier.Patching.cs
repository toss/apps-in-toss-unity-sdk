using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AppsInToss.Editor.Package
{
    internal static partial class WebGLBuildCopier
    {
        /// <summary>
        /// Decompression Fallback(.unityweb) 빌드인지 판별한다. 빌드 마커 값 외에 실제 파일 확장자도 본다
        /// (마커가 없는 수동 빌드 대응). .unityweb 은 JS 디컴프레서가 감지 마커로 쓰는 포맷이라
        /// 재압축·패치·data 재포장을 하면 안 된다(AITBrotliCompressor 의 가드와 같은 이유).
        /// </summary>
        internal static bool IsUnitywebBuild(bool decompressionFallback, params string[] buildFileNames)
        {
            if (decompressionFallback) return true;
            if (buildFileNames == null) return false;
            foreach (string name in buildFileNames)
            {
                if (!string.IsNullOrEmpty(name) && name.EndsWith(".unityweb", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 빌드 후 패처(framework → loader)를 실행하고, 패처가 rename 한 파일명으로 호출부의 이름 변수를 갱신한다.
        ///
        /// 호출 위치 계약(CopyWebGLToPublic): Unity 산출물이 buildSrc 에 있고 필수 파일 검증을 통과한 직후,
        /// brotli 재압축·BuildDataCacheName 크기 스냅숏·page cache/warm manifest 산출·index.html 치환보다 앞.
        /// 패처가 파일명을 바꾸므로(.aitpN, AITPatchedFileNaming) 이 이름을 읽는 모든 후속 단계가 새 이름을 써야 한다.
        ///
        /// 패치 실패는 빌드를 막지 않는다(fail-open): 패처가 던진 예외는 경고로 남기고 그 패처만 건너뛴다.
        /// </summary>
        /// <param name="unitywebBuild">true 면 패치를 전부 건너뛴다(<see cref="IsUnitywebBuild"/>).</param>
        /// <returns>패치를 적용한 파일 수 합계.</returns>
        internal static int ApplyBuildPatches(
            AITEditorScriptObject config,
            string buildSrc,
            bool unitywebBuild,
            ref string loaderFile,
            ref string dataFile,
            ref string frameworkFile,
            ref string wasmFile,
            ref string symbolsFile)
        {
            if (unitywebBuild)
            {
                Debug.Log("[AIT] Decompression Fallback(.unityweb) 빌드 — framework/loader 패치를 건너뜁니다.");
                return 0;
            }

            var renames = new Dictionary<string, string>(StringComparer.Ordinal);
            int patched = 0;

            try
            {
                patched += AITFrameworkPatcher.Apply(buildSrc, config, renames);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AIT] framework 패치 실패 — 패치 없이 계속합니다: {ex.GetType().Name}: {ex.Message}");
            }

            try
            {
                patched += AITLoaderPatcher.Apply(buildSrc, config, renames);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AIT] loader 패치 실패 — 패치 없이 계속합니다: {ex.GetType().Name}: {ex.Message}");
            }

            if (renames.Count > 0)
            {
                loaderFile = ResolveRenamed(loaderFile, renames);
                dataFile = ResolveRenamed(dataFile, renames);
                frameworkFile = ResolveRenamed(frameworkFile, renames);
                wasmFile = ResolveRenamed(wasmFile, renames);
                symbolsFile = ResolveRenamed(symbolsFile, renames);
                Debug.Log($"[AIT] 빌드 후 패치 {patched}개 적용, 파일명 {renames.Count}개 변경 (.aitp{AITPatchedFileNaming.PatchSetVersion})");
            }

            return patched;
        }

        /// <summary>rename 맵을 따라가 최종 이름을 돌려준다(패처가 연쇄로 rename 한 경우 포함, 순환은 8단계에서 끊는다).</summary>
        internal static string ResolveRenamed(string fileName, IDictionary<string, string> renames)
        {
            if (string.IsNullOrEmpty(fileName) || renames == null) return fileName;

            string current = fileName;
            for (int i = 0; i < 8; i++)
            {
                if (!renames.TryGetValue(current, out string next) || string.IsNullOrEmpty(next) || next == current)
                {
                    break;
                }
                current = next;
            }
            return current;
        }

        /// <summary>
        /// exactDataBody 가 켜져 있을 때만 .data 의 압축 해제 크기를 잰다(AITDataRawSize).
        /// 비활성이거나 .unityweb 이거나 측정이 실패하면 -1(런타임 훅은 종전 동작).
        /// </summary>
        internal static long MeasureDataRawSizeIfEnabled(AITEditorScriptObject config, string buildSrc, string dataFile, bool unitywebBuild)
        {
            if (unitywebBuild || string.IsNullOrEmpty(dataFile) || !AITPerfFlags.EffectiveExactDataBody(config))
            {
                return -1;
            }

            try
            {
                return AITDataRawSize.Measure(Path.Combine(buildSrc, dataFile));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AIT] data 원본 크기 측정 실패 — exactDataBody 가 적용되지 않습니다: {ex.GetType().Name}: {ex.Message}");
                return -1;
            }
        }
    }
}
