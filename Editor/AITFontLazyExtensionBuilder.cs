// -----------------------------------------------------------------------
// <copyright file="AITFontLazyExtensionBuilder.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Font subset lazy language extension (build-time)
// </copyright>
// -----------------------------------------------------------------------
//
// fontSubsetLanguages 로 선택한 동적 텍스트 언어(닉네임/채팅 등, AITFontSubsetLanguages 참조) 중
// LazyEligible(ko/la 를 제외한 전부) 태그를, 부트 폰트 union 대신 "lazy 확장"으로 분리한다.
// 한글·영어(기본 라틴)는 한국 서비스 특성상 부트 폰트에 항상 포함되므로(AlwaysIncluded, 이미 보장됨)
// 대상이 아니다.
//
// 동작(fontSubsetLazyLanguages == 1 명시 활성 전용 — audioStreamTranscode 와 동일 posture):
//   a) 원본 폰트 바이트에서 태그별 서브셋(부트 서브셋으로 소스가 치환되기 전에 실행 — 반드시
//      AITFontSubsetProcessor.ApplyForBuild 의 boot subset 루프보다 앞에서 호출된다).
//   b) 기존 subset-font-runner.mjs(npm, harfbuzz) 를 재사용해 태그 범위의 확장 TTF 생성.
//   c) TMP_FontAsset 을 리플렉션으로 생성(atlasPopulationMode=Dynamic), AssetDatabase.CreateAsset.
//   d) AITFontExternalizer.BuildFontBundle 을 재사용해 WebGL AssetBundle 로 빌드 + brotli 인코딩(가능 시),
//      StreamingAssets ait-stream-font/ 에 lazy-<tag>-<hash>.bundle[.br] 로 배치.
//   e) manifest.json 에 lazyTag/lazyRanges 필드를 포함한 엔트리를 read-merge-write 로 기록(기존
//      fontStreaming(AITFontExternalizer) entry 를 덮어쓰지 않음 — 아래 "호출 순서" 참조).
//   f) 임시 에셋(Assets/AppsInToss/AITFontLazyTmp/)은 태그별 처리 직후 정리.
//
// ── 안전 불변식(최우선) ──
//   위 a~e 의 어느 단계든 실패하면 그 태그는 lazySet 에서 제외되고 boot union 으로 복귀한다
//   (fallback-to-boot). 즉 lazy 가 실패한 언어는 1단계(순수 boot subset)처럼 부트 폰트에 포함되어,
//   어떤 실패 조합에서도 tofu 리스크가 1단계 대비 증가하지 않는다. 실패는 태그당 1줄 경고로 로깅한다.
//   이 폴백은 실제 union 계산(ApplyLazyExtensions 반환값)에 반영되는 구조다 — 로그만 남기고 범위가
//   빠지는 구조가 아니다.
//
// ── AITWebGLBuilder 호출 순서(중요 — 매니페스트 read-merge-write 의 근거) ──
//   BuildWebGL 은 fontSubset(AITFontSubsetProcessor.ApplyForBuild, 이 클래스가 그 안에서 훅됨)을
//   fontStreaming(AITFontExternalizer.ExternalizeForBuild) "보다 먼저" 호출한다(AITWebGLBuilder.cs
//   BuildWebGL 참조). 따라서 실제로는 이 클래스가 manifest.json 을 먼저 쓰고 AITFontExternalizer 가
//   나중에 읽어 병합한다. 다만 순서가 바뀌어도 안전하도록 이 클래스도 자신의 쓰기 시점에 기존
//   매니페스트를 읽어 병합한다(대칭적 read-merge-write) — 어느 한쪽이 상대 엔트리를 덮어쓰지 않는다.
//
// ── 정리(빌드 후) ──
//   AITFontExternalizer.ExternalizeForBuild 가 실제로 폰트를 외부화한 빌드(handle.Active)에서는
//   그 RestoreForBuild 가 StreamingAssets 디렉토리 전체를 정리하며 lazy 아티팩트도 함께 사라진다
//   (BuildPlayer 이후 시점이라 안전). fontStreaming 이 아무 것도 외부화하지 않은(흔한) 빌드에서는
//   AITFontSubsetProcessor.RestoreForBuild 가 handle.LazyActive 를 보고 CleanupAfterBuild 를 호출한다.
//   반대로 BuildPlayer "이전"(apply 단계) 에 fontStreaming 이 대상 0건/예외로 스스로 정리할 때는
//   무조건 삭제 대신 HasLazyArtifacts 로 lazy 아티팩트 보존 여부를 확인한다(AITFontExternalizer 참조) —
//   그렇지 않으면 BuildPlayer 가 실행되기도 전에 lazy 산출물이 사라져 빌드 출력에 반영되지 못한다.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    /// <summary>
    /// fontSubsetLanguages 선택 언어 중 LazyEligible 태그를 빌드 시 lazy 확장(서브셋 TTF → Dynamic
    /// TMP_FontAsset → AssetBundle)으로 분리하는 빌드타임 빌더. <see cref="AITFontSubsetProcessor"/>가
    /// boot subset 루프 이전에 호출하고, <see cref="AITFontExternalizer"/>와 StreamingAssets
    /// ait-stream-font/manifest.json 을 공유한다(계약: lazyTag/lazyRanges 필드).
    /// </summary>
    [InitializeOnLoad]
    internal static class AITFontLazyExtensionBuilder
    {
        /// <summary>
        /// fontStreaming(AITFontExternalizer)과 공유하는 StreamingAssets 경로. AITFontExternalizer 의
        /// private const StreamRootAssets 와 반드시 같은 값이어야 한다(직접 참조 불가 — 문자열 상수로 동기화).
        /// </summary>
        private const string StreamRootAssets = "Assets/StreamingAssets/ait-stream-font";

        /// <summary>lazy 번들 빌드용 임시 디렉토리. fontStreaming 의 Library/ait-fontbundle 과 분리해
        /// 정리 순서 충돌을 피한다.</summary>
        private const string BundleTempDir = "Library/ait-fontbundle-lazy";

        /// <summary>확장 TTF/TMP_FontAsset 을 담는 임시 임포트 폴더(번들 빌드 직후 정리).</summary>
        private const string TempFolder = "Assets/AppsInToss/AITFontLazyTmp";

        /// <summary>lazy 처리 진행 중 마커(Unity 가 무시하는 '.' 접두 숨김 파일). 안전망 대상.</summary>
        private const string MarkerRelative = "Assets/.ait-fontsubset-lazy-active";

        /// <summary>매니페스트 엔트리(JsonUtility 직렬화). fontStreaming 의 기존 엔트리와 필드 호환
        /// (guid/bundle/encoding/fonts) + lazy 전용 필드(lazyTag/lazyRanges) 추가.</summary>
        [Serializable]
        internal struct ManifestEntryDto
        {
            public string guid;
            public string bundle;
            public string encoding;
            public string[] fonts;

            /// <summary>비어 있으면(또는 필드 부재) 기존 eager(fontStreaming) 엔트리.</summary>
            public string lazyTag;

            /// <summary>lazyTag 의 전체 유니코드 범위(AITFontSubsetLanguages.Table 값 그대로).</summary>
            public string lazyRanges;
        }

        [Serializable]
        internal struct ManifestDto
        {
            public int maxConcurrent;
            public ManifestEntryDto[] entries;
        }

        static AITFontLazyExtensionBuilder()
        {
            EditorApplication.delayCall += SafetyNetRestore;
        }

        // ─────────────────────────── 진입점 ───────────────────────────

        /// <summary>
        /// fontSubsetLazyLanguages tri-state 를 해석해 lazy 확장이 이번 빌드에서 활성인지 반환한다(N11:
        /// AITFontSubsetProcessor 가 EnsureTool 조기 호출 여부를 이 값만으로 판단할 수 있도록 노출).
        /// -1(자동)→GetDefaultFontSubsetLazyLanguages()(=항상 false), 0=비활성, 1=명시 활성.
        /// audioStreamTranscode 와 동일 posture — 값 1일 때만 true.
        /// </summary>
        internal static bool IsLazyEnabled(AITEditorScriptObject config)
        {
            if (config == null)
            {
                return false;
            }

            return config.fontSubsetLazyLanguages >= 0
                ? config.fontSubsetLazyLanguages == 1
                : AITDefaultSettings.GetDefaultFontSubsetLazyLanguages();
        }

        /// <summary>
        /// fontSubsetLazyLanguages tri-state 에 따라 lazy 확장을 시도하고, boot union 계산에 쓸 언어
        /// CSV(=lazy 로 성공적으로 분리되지 않은 나머지, 즉 bootTags)를 반환한다. 이 함수는 어떤 경우에도
        /// 예외를 던지지 않는다(호출부의 boot subset 흐름을 절대 막지 않기 위함 — 안전 불변식).
        /// </summary>
        /// <param name="config">에디터 설정. null 이면 원본 언어 CSV 그대로 반환(no-op).</param>
        /// <param name="targets">boot subset 대상 폰트 경로들(원본 바이트 상태 — 아직 치환 전). 첫 번째
        /// (주 폰트) 만 lazy 확장 소스로 사용한다.</param>
        /// <param name="node">EnsureTool 로 해석된 node 실행 경로.</param>
        /// <param name="runner">subset-font-runner.mjs 경로.</param>
        /// <param name="anyLazyArtifactsPersisted">true 면 StreamingAssets 에 lazy 아티팩트가 남았음
        /// (RestoreForBuild 가 CleanupAfterBuild 를 호출해야 함을 뜻함).</param>
        /// <returns>boot union 계산에 사용할 언어 태그 CSV(bootTags, Table 순서로 결정적 직렬화).</returns>
        internal static string ApplyLazyExtensions(
            AITEditorScriptObject config, string[] targets, string node, string runner,
            out bool anyLazyArtifactsPersisted)
        {
            anyLazyArtifactsPersisted = false;
            string originalLanguagesCsv = config != null ? (config.fontSubsetLanguages ?? string.Empty) : string.Empty;

            // outer catch 에서도 잔존 아티팩트 정리(S4)에 써야 하므로 try 진입 전에 선언(try 로컬 변수는
            // catch 에서 보이지 않음).
            string streamRootFull = null;

            try
            {
                if (config == null)
                {
                    return originalLanguagesCsv;
                }

                if (!IsLazyEnabled(config))
                {
                    return originalLanguagesCsv;
                }

                if (!HasRequiredRuntimeModules(out string missingModuleReason))
                {
                    Debug.LogWarning($"[AIT-FontSubset-Lazy] {missingModuleReason} — lazy 확장을 건너뜁니다(선택 언어는 부트 union 유지).");
                    return originalLanguagesCsv;
                }

                // S3: TMP Settings 리소스 자체가 없으면(TMP 패키지는 있어도 'TMP Settings' 에셋 미생성)
                // 런타임 ResolveTmpFallback 이 결국 fallback 주입에 실패한다 — 빌드 시점에 미리 걸러
                // lazy 를 통째로 포기한다(부트 union 은 항상 안전한 폴백).
                if (!HasTmpSettingsResource())
                {
                    Debug.LogWarning("[AIT-FontSubset-Lazy] TMP Settings 리소스를 찾을 수 없어 lazy 확장을 건너뜁니다(선택 언어는 부트 union 유지).");
                    return originalLanguagesCsv;
                }

                SplitLazyAndBootTags(originalLanguagesCsv, out List<string> lazyTags, out List<string> bootTags);
                if (lazyTags.Count == 0)
                {
                    return originalLanguagesCsv;
                }

                if (targets == null || targets.Length == 0)
                {
                    Debug.LogWarning("[AIT-FontSubset-Lazy] subset 대상 폰트가 없어 lazy 확장을 건너뜁니다(선택 언어는 부트 union 유지).");
                    return originalLanguagesCsv;
                }

                // B1: 다중 target 폰트는 target 별 확장을 아직 만들지 않으므로(후속 과제), lazy 를 전부
                // 포기하고 전 언어를 boot union 으로 폴백한다 — targets[0] 하나에만 확장을 만들면 다른
                // 폰트를 쓰는 텍스트의 해당 언어가 부트/lazy 어느 쪽에도 없게 된다(HashSet 열거 비결정성도 동반).
                if (targets.Length > 1)
                {
                    Debug.LogWarning("[AIT-FontSubset-Lazy] 대상 폰트가 여러 개라 lazy 확장을 건너뜁니다 — 전 언어를 부트 폰트에 포함");
                    return originalLanguagesCsv;
                }

                string primarySource = targets[0].Trim().Replace('\\', '/');
                Debug.Log($"[AIT-FontSubset-Lazy] 주 폰트로 사용: {primarySource} (lazy 시도 언어: {string.Join(",", lazyTags)})");

                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                streamRootFull = Path.Combine(projectRoot, StreamRootAssets);
                string bundleTempFull = Path.Combine(projectRoot, BundleTempDir);

                CreateMarker();
                var newEntries = new List<ManifestEntryDto>();
                try
                {
                    bool brotliAvailable = AITBrotliCompressor.TryResolveNode(out _);
                    Directory.CreateDirectory(streamRootFull);
                    Directory.CreateDirectory(bundleTempFull);
                    EnsureTempFolder();

                    foreach (var tag in lazyTags)
                    {
                        ManifestEntryDto? entry = null;
                        try
                        {
                            entry = BuildLazyExtensionForTag(
                                tag, primarySource, node, runner, streamRootFull, bundleTempFull,
                                brotliAvailable, projectRoot);
                        }
                        catch (Exception tagEx)
                        {
                            Debug.LogWarning($"[AIT-FontSubset-Lazy]   '{tag}' 예외 → 부트 union 으로 복귀: {tagEx.Message}");
                        }

                        if (entry.HasValue)
                        {
                            newEntries.Add(entry.Value);
                        }
                        else
                        {
                            // 안전 불변식: 실패한 태그는 boot union 으로 복귀.
                            bootTags.Add(tag);
                        }
                    }
                }
                finally
                {
                    CleanupTempFolder();
                }

                if (newEntries.Count > 0)
                {
                    int fallbackConcurrent = config.fontStreamingMaxConcurrent > 0 ? config.fontStreamingMaxConcurrent : 2;
                    WriteMergedManifest(streamRootFull, fallbackConcurrent, newEntries);
                    anyLazyArtifactsPersisted = true;
                    var appliedTags = new List<string>();
                    foreach (var e in newEntries)
                    {
                        appliedTags.Add(e.lazyTag);
                    }

                    Debug.Log($"[AIT-FontSubset-Lazy] ✓ {newEntries.Count}개 언어 lazy 확장 완료: {string.Join(",", appliedTags)}");
                }

                if (!anyLazyArtifactsPersisted)
                {
                    // 아무 것도 안 남았으면(전부 실패) 마커도 즉시 제거 — CleanupAfterBuild 가 나중에
                    // 호출되지 않으므로(handle.LazyActive=false) 여기서 제거하지 않으면 마커가 영구 잔존한다.
                    RemoveMarker();
                }

                return JoinInTableOrder(bootTags);
            }
            catch (Exception e)
            {
                // 예상 밖 실패 → 전체 폴백: 이번에 시도된 태그 상태를 신뢰할 수 없으므로 선택 언어 전부를
                // 부트 union 으로 되돌린다(과잉 보존이라 tofu 리스크 증가는 없음 — 안전 우선).
                Debug.LogWarning($"[AIT-FontSubset-Lazy] 예외 → 전체 폴백(선택 언어 전부 부트 union 유지): {e.Message}");

                // S4: 일부 태그가 이미 성공해 StreamingAssets 에 lazy-*.bundle 아티팩트를 남긴 상태에서
                // 이후 단계(매니페스트 쓰기 등)가 실패했을 수 있다 — 참조 없는 잔존 번들이 배포본에 실리지
                // 않도록 정리한다. CleanupAfterBuild 는 실패 시 예외를 삼키고 마커를 남겨두므로(RemoveMarker
                // 호출 전에 끊김) SafetyNetRestore 가 다음 에디터 로드 시 마저 정리한다.
                if (HasLazyArtifacts(streamRootFull))
                {
                    CleanupAfterBuild();
                }
                else
                {
                    RemoveMarker();
                }

                return originalLanguagesCsv;
            }
        }

        /// <summary>
        /// 런타임(AITStreamingFont)이 lazy 폰트를 읽는 데 필요한 두 모듈(unitywebrequest, assetbundle)이
        /// 프로젝트에 모두 등록돼 있는지 확인한다(R2). 에디터 AppDomain 은 프로젝트에서 제거된 모듈도
        /// 타입 자체는 로드해 두는 경우가 있어 Type.GetType 기반 확인은 실효가 의심된다 — 대신
        /// PackageManager.PackageInfo.GetAllRegisteredPackages() 로 실제 등록 여부(빌드에 반영되는 상태)를
        /// 직접 조회한다. 런타임(AIT_HAS_UNITYWEBREQUEST/AIT_HAS_ASSETBUNDLE 매크로 부재 시 즉시 종료)은
        /// 두 모듈을 모두 요구하므로, 여기서도 하나라도 없으면(또는 확인 자체가 실패하면) lazy 전체를
        /// 포기해야 한다 — 그렇지 않으면 빌드만 lazy 아티팩트를 만들고 런타임이 못 읽어 영구 tofu 가 된다.
        /// </summary>
        private static bool HasRequiredRuntimeModules(out string reason)
        {
            const string UnityWebRequestModule = "com.unity.modules.unitywebrequest";
            const string AssetBundleModule = "com.unity.modules.assetbundle";

            try
            {
                var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
                if (packages == null || packages.Length == 0)
                {
                    // 레지스트리가 아직 채워지지 않은 타이밍 — "두 모듈 모두 비활성화" 와는 다른 상황이므로
                    // 오진단을 피해 확인 실패로 구분한다(폴백 방향은 동일하게 안전).
                    reason = "패키지 레지스트리가 비어 있어 모듈 등록을 확인할 수 없음";
                    return false;
                }

                bool hasUnityWebRequest = false;
                bool hasAssetBundle = false;
                foreach (var pkg in packages)
                {
                    if (pkg.name == UnityWebRequestModule)
                    {
                        hasUnityWebRequest = true;
                    }
                    else if (pkg.name == AssetBundleModule)
                    {
                        hasAssetBundle = true;
                    }
                }

                if (!hasUnityWebRequest && !hasAssetBundle)
                {
                    reason = $"{UnityWebRequestModule}, {AssetBundleModule} 모듈이 모두 비활성화됨";
                    return false;
                }

                if (!hasUnityWebRequest)
                {
                    reason = $"{UnityWebRequestModule} 모듈이 비활성화됨";
                    return false;
                }

                if (!hasAssetBundle)
                {
                    reason = $"{AssetBundleModule} 모듈이 비활성화됨";
                    return false;
                }

                reason = null;
                return true;
            }
            catch (Exception e)
            {
                reason = $"모듈 등록 확인 실패({e.Message})";
                return false;
            }
        }

        /// <summary>
        /// TMP_Settings 타입 존재 + 'TMP Settings' 리소스 에셋(Resources.Load) 존재를 함께 확인한다(S3).
        /// TMP 패키지는 설치돼 있어도 'TMP Settings' 리소스 에셋이 생성되지 않은 프로젝트가 있을 수 있는데,
        /// 그런 경우 런타임 ResolveTmpFallback 이 결국 실패하므로 빌드 시점에 미리 걸러낸다.
        /// </summary>
        private static bool HasTmpSettingsResource()
        {
            try
            {
                Type settingsType = Type.GetType("TMPro.TMP_Settings, Unity.TextMeshPro")
                    ?? FindTypeAcrossAssemblies("TMPro.TMP_Settings");
                if (settingsType == null)
                {
                    return false; // TMP 미설치.
                }

                var asset = UnityEngine.Resources.Load("TMP Settings", settingsType);
                return asset != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// R5: Type.GetType(어셈블리 한정 문자열)이 null 을 반환하면(어셈블리명 차이로 흔함) 런타임의
        /// FindType 관용구(AIT.StreamingFont.cs)와 동일하게 로드된 전 어셈블리를 스캔해 흡수한다.
        /// </summary>
        private static Type FindTypeAcrossAssemblies(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType(fullName);
                    if (t != null)
                    {
                        return t;
                    }
                }
                catch
                {
                    // 일부 어셈블리는 GetType 에서 예외 — 무시하고 계속.
                }
            }

            return null;
        }

        /// <summary>
        /// 태그 1건의 lazy 확장을 전부 시도한다(a~e). 어느 단계든 실패하면 null(호출부가 boot 로 폴백).
        /// </summary>
        private static ManifestEntryDto? BuildLazyExtensionForTag(
            string tag, string primarySourceAssetPath, string node, string runner,
            string streamRootFull, string bundleTempFull, bool brotliAvailable, string projectRoot)
        {
            if (!AITFontSubsetLanguages.TryFindEntry(tag, out var langEntry) || string.IsNullOrEmpty(langEntry.Ranges))
            {
                Debug.LogWarning($"[AIT-FontSubset-Lazy]   '{tag}' 언어 테이블에 범위 없음(skip).");
                return null;
            }

            string srcFull = Path.Combine(projectRoot, primarySourceAssetPath);
            if (!File.Exists(srcFull))
            {
                Debug.LogWarning($"[AIT-FontSubset-Lazy]   '{tag}' 소스 폰트 없음: {primarySourceAssetPath}");
                return null;
            }

            // B2: 서브셋 실행 전에 소스 폰트가 이 태그의 문자체계를 실제로 담고 있는지 커버리지를
            // 샘플 검사한다. harfbuzz 서브셋은 소스에 없는 문자체계를 요청해도 '유효하지만 빈' 폰트를
            // 반환해 성공 처리될 수 있어, 검사 없이는 해당 태그가 boot 에서도 lazy 에서도 빠져 영구
            // tofu 가 된다.
            if (!HasAnyCoverage(srcFull, langEntry.Ranges))
            {
                Debug.LogWarning($"[AIT-FontSubset-Lazy]   '{tag}' 소스 폰트가 해당 문자체계를 포함하지 않아 lazy 확장을 건너뜁니다.");
                return null;
            }

            string safeTag = SanitizeTag(tag);
            string ttfAssetPath = $"{TempFolder}/lazy_{safeTag}.ttf";
            string ttfFull = Path.Combine(projectRoot, ttfAssetPath);
            string tmpAssetPath = null;

            try
            {
                // a-b) 원본 바이트에서 태그 범위만 확장 서브셋(기존 subset-font-runner.mjs 재사용).
                if (!RunSubsetRunner(node, runner, srcFull, ttfFull, langEntry.Ranges, out string subsetErr))
                {
                    Debug.LogWarning($"[AIT-FontSubset-Lazy]   '{tag}' 서브셋 실패: {subsetErr}");
                    return null;
                }

                if (!File.Exists(ttfFull) || new FileInfo(ttfFull).Length <= 0)
                {
                    Debug.LogWarning($"[AIT-FontSubset-Lazy]   '{tag}' 서브셋 산출물 비정상.");
                    return null;
                }

                byte[] ttfBytes = File.ReadAllBytes(ttfFull);

                // 방어선(S1 러너 재시도가 1차 방어인 belt-and-suspenders): 러너가 ok:true 를 반환했어도
                // 산출물에 외곽선 테이블이 없으면(harfbuzz wasm 대규모 서브셋 드롭 등) 여기서 걸러낸다.
                if (!AITSfntLite.HasOutlineTable(ttfBytes))
                {
                    Debug.LogWarning($"[AIT-FontSubset-Lazy]   '{tag}' 서브셋 산출물에 외곽선 테이블이 없어 lazy 확장을 건너뜁니다.");
                    return null;
                }

                string hash = ComputeShortHash(ttfBytes);

                AssetDatabase.ImportAsset(ttfAssetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

                // c) TMP_FontAsset 생성(리플렉션) + Dynamic atlas.
                if (!TryCreateDynamicTmpFontAsset(ttfAssetPath, safeTag, out tmpAssetPath, out string fontAssetName, out string tmpErr))
                {
                    Debug.LogWarning($"[AIT-FontSubset-Lazy]   '{tag}' TMP_FontAsset 생성 실패: {tmpErr}");
                    return null;
                }

                // d) 번들 빌드(AITFontExternalizer.BuildFontBundle 재사용) + brotli.
                string bundleFileName = $"lazy-{tag}-{hash}.bundle";
                if (!AITFontExternalizer.BuildFontBundle(tmpAssetPath, bundleFileName, bundleTempFull, streamRootFull, brotliAvailable))
                {
                    Debug.LogWarning($"[AIT-FontSubset-Lazy]   '{tag}' 번들 빌드 실패.");
                    return null;
                }

                string finalFileName = bundleFileName;
                bool isBr = false;
                if (brotliAvailable)
                {
                    string abs = Path.Combine(streamRootFull, bundleFileName);
                    var results = AITBrotliCompressor.Compress(new List<string> { abs });
                    if (results.TryGetValue(abs, out var r) && r.Ok
                        && AITBrotliCompressor.ShouldKeep(r.raw, r.br, AITBrotliCompressor.DefaultMinGainPercent))
                    {
                        SafeDeleteFile(abs);
                        finalFileName = bundleFileName + ".br";
                        isBr = true;
                    }
                    else
                    {
                        SafeDeleteFile(abs + ".br");
                    }
                }

                Debug.Log($"[AIT-FontSubset-Lazy]   '{tag}' lazy 확장 완료 → {finalFileName}");

                return new ManifestEntryDto
                {
                    guid = "lazy-" + tag,
                    bundle = finalFileName,
                    encoding = isBr ? "br" : string.Empty,
                    fonts = new[] { fontAssetName },
                    lazyTag = tag,
                    lazyRanges = langEntry.Ranges,
                };
            }
            finally
            {
                // f) 임시 에셋 정리(번들 빌드 직후).
                if (AssetDatabase.LoadMainAssetAtPath(ttfAssetPath) != null)
                {
                    AssetDatabase.DeleteAsset(ttfAssetPath);
                }
                else
                {
                    SafeDeleteFile(ttfFull);
                }

                if (!string.IsNullOrEmpty(tmpAssetPath) && AssetDatabase.LoadMainAssetAtPath(tmpAssetPath) != null)
                {
                    AssetDatabase.DeleteAsset(tmpAssetPath);
                }
            }
        }

        // ─────────────────────────── 글리프 커버리지 검사(B2) ───────────────────────────

        /// <summary>
        /// 소스 폰트 파일(targetFullPath, 절대 경로)이 ranges 의 대표 코드포인트를 하나라도 가지고
        /// 있는지 검사한다. 하나라도 있으면 진행(서브셋은 소스가 가진 글리프만 보존하므로 1단계
        /// subset 과 동등한 커버리지가 보장된다).
        ///
        /// 과거에는 UnityEngine.Font.HasCharacter 로 샘플 검사했으나, 에디터의 Font.HasCharacter 는
        /// OS 폰트 폴백을 포함해 판정하므로 소스 폰트에 실제로 없는 문자체계(예: NotoSansKR 소스에
        /// 없는 th)에도 true 를 반환하는 거짓 양성이 있었다(빈 lazy 번들이 만들어지는 원인). 지금은
        /// AITSfntLite 로 폰트 파일의 cmap 테이블을 직접 판독해 신뢰 가능한 커버리지를 판정한다.
        /// 파일 읽기 실패·cmap 파싱 실패는 전부 false(안전 방향 fallback-to-boot).
        /// </summary>
        private static bool HasAnyCoverage(string targetFullPath, string ranges)
        {
            try
            {
                if (string.IsNullOrEmpty(targetFullPath) || !File.Exists(targetFullPath))
                {
                    return false;
                }

                byte[] bytes = File.ReadAllBytes(targetFullPath);
                var samples = SampleCoverageCodepoints(ranges);
                return AITSfntLite.CmapCoversAny(bytes, samples);
            }
            catch
            {
                return false;
            }
        }

        // ─────────────────────────── TMP_FontAsset 생성(리플렉션) ───────────────────────────

        /// <summary>
        /// 서브셋된 소스 Font 로부터 Dynamic TMP_FontAsset 을 생성해 임시 폴더에 저장한다. TMP 컴파일
        /// 의존 없이 전부 리플렉션(AITFontExternalizer/AITFontSubsetProcessor 와 동일 관용구)으로 접근한다.
        /// </summary>
        private static bool TryCreateDynamicTmpFontAsset(
            string ttfAssetPath, string safeTag, out string tmpAssetPath, out string fontAssetName, out string error)
        {
            tmpAssetPath = null;
            fontAssetName = null;
            error = null;
            try
            {
                Type fontAssetType = Type.GetType("TMPro.TMP_FontAsset, Unity.TextMeshPro");
                if (fontAssetType == null)
                {
                    error = "TMP(Unity.TextMeshPro) 미설치";
                    return false;
                }

                var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(ttfAssetPath);
                if (sourceFont == null)
                {
                    error = $"소스 Font 로드 실패: {ttfAssetPath}";
                    return false;
                }

                // 버전 간 가장 안정적인 단일 오버로드만 사용(다중 파라미터 오버로드는 TMP 버전별
                // 시그니처가 달라 리플렉션 안정성이 낮다 — DeployProbeBuildRunner 와 동일 판단).
                var createMethod = fontAssetType.GetMethod(
                    "CreateFontAsset",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(Font) },
                    null);
                if (createMethod == null)
                {
                    error = "TMP_FontAsset.CreateFontAsset(Font) 오버로드를 찾지 못함";
                    return false;
                }

                object fontAssetObj = createMethod.Invoke(null, new object[] { sourceFont });
                var mainAsset = fontAssetObj as UnityEngine.Object;
                if (mainAsset == null)
                {
                    error = "TMP_FontAsset 생성 실패(null 반환)";
                    return false;
                }

                // atlasPopulationMode = Dynamic(계약 명시 사항 — 런타임 즉석 래스터화).
                var atlasModeProp = fontAssetType.GetProperty("atlasPopulationMode");
                if (atlasModeProp != null && atlasModeProp.CanWrite)
                {
                    object dynamicValue = Enum.Parse(atlasModeProp.PropertyType, "Dynamic");
                    atlasModeProp.SetValue(fontAssetObj, dynamicValue);
                }

                string assetPath = $"{TempFolder}/lazy_{safeTag} SDF.asset";
                AssetDatabase.CreateAsset(mainAsset, assetPath);

                TryAddTmpSubAsset(fontAssetType, fontAssetObj, mainAsset, "material");
                TryAddTmpAtlasTextures(fontAssetType, fontAssetObj, mainAsset);

                EditorUtility.SetDirty(mainAsset);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

                tmpAssetPath = assetPath;
                fontAssetName = mainAsset.name;
                return true;
            }
            catch (Exception e)
            {
                // TargetInvocationException 은 메시지가 무의미하므로 inner 를 끝까지 벗겨 실제 원인을 남긴다
                // (DeployProbeBuildRunner 와 동일 관용구).
                Exception root = e;
                while (root is TargetInvocationException tie && tie.InnerException != null)
                {
                    root = tie.InnerException;
                }

                error = $"{root.GetType().Name}: {root.Message}";
                return false;
            }
        }

        private static void TryAddTmpSubAsset(Type fontAssetType, object fontAssetObj, UnityEngine.Object mainAsset, string propertyName)
        {
            try
            {
                var prop = fontAssetType.GetProperty(propertyName);
                var sub = prop?.GetValue(fontAssetObj) as UnityEngine.Object;
                if (sub != null && AssetDatabase.GetAssetPath(sub) != AssetDatabase.GetAssetPath(mainAsset))
                {
                    AssetDatabase.AddObjectToAsset(sub, mainAsset);
                }
            }
            catch
            {
                // 무시 — 서브에셋 동봉 실패는 치명적이지 않음(TMP 버전별 API 차이 방어).
            }
        }

        private static void TryAddTmpAtlasTextures(Type fontAssetType, object fontAssetObj, UnityEngine.Object mainAsset)
        {
            try
            {
                var prop = fontAssetType.GetProperty("atlasTextures");
                if (prop?.GetValue(fontAssetObj) is System.Collections.IEnumerable list)
                {
                    foreach (var item in list)
                    {
                        if (item is UnityEngine.Object tex && tex != null)
                        {
                            AssetDatabase.AddObjectToAsset(tex, mainAsset);
                        }
                    }
                }
            }
            catch
            {
                // 무시
            }
        }

        // ─────────────────────────── subset-font-runner.mjs 실행(FontSubset 관용구) ───────────────────────────

        private static bool RunSubsetRunner(string node, string runner, string inAbs, string outAbs, string ranges, out string err)
        {
            err = null;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = node,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(runner),
                };
                psi.ArgumentList.Add(runner);
                psi.ArgumentList.Add(inAbs);
                psi.ArgumentList.Add(outAbs);
                psi.ArgumentList.Add(ranges);

                string nodeDir = Path.GetDirectoryName(node);
                if (!string.IsNullOrEmpty(nodeDir))
                {
                    string sep = AITPlatformHelper.IsWindows ? ";" : ":";
                    string existing = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                    psi.EnvironmentVariables["PATH"] = nodeDir + sep + existing;
                }

                using (var p = new Process { StartInfo = psi })
                {
                    p.Start();
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(120000))
                    {
                        try { p.Kill(); } catch { /* 무시 */ }
                        err = "러너 타임아웃";
                        return false;
                    }

                    if (p.ExitCode != 0)
                    {
                        err = string.IsNullOrEmpty(stderr) ? stdout : stderr;
                        if (string.IsNullOrEmpty(err))
                        {
                            err = "러너 비정상 종료";
                        }

                        return false;
                    }

                    return stdout != null && stdout.Contains("\"ok\":true");
                }
            }
            catch (Exception e)
            {
                err = e.Message;
                return false;
            }
        }

        // ─────────────────────────── 순수 로직(단위 테스트 대상) ───────────────────────────

        /// <summary>
        /// langEntry.Ranges("U+XXXX-YYYY,U+ZZZZ" 콤마 구분) 문자열에서 대표 코드포인트를 샘플링한다(B2).
        /// 범위 토큰마다 시작 코드포인트 + (폭이 1보다 크면) 중간 코드포인트를 뽑아 최대 maxSamples 개까지
        /// 모은다. Font.HasCharacter 가 char(UTF-16 코드유닛) 인자만 받으므로 BMP 밖(U+FFFF 초과) 코드
        /// 포인트는 샘플에서 제외한다. 형식이 어긋난 토큰은 조용히 건너뛴다. 부수 효과 없음 — 테스트 대상.
        /// </summary>
        internal static List<int> SampleCoverageCodepoints(string ranges, int maxSamples = 20)
        {
            var result = new List<int>();
            if (string.IsNullOrEmpty(ranges))
            {
                return result;
            }

            foreach (var rawToken in ranges.Split(','))
            {
                if (result.Count >= maxSamples)
                {
                    break;
                }

                string token = rawToken.Trim();
                if (token.Length < 3 || !token.StartsWith("U+", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // 형식 불일치 — 무시.
                }

                string body = token.Substring(2);
                int dashIdx = body.IndexOf('-');
                string startHex = dashIdx >= 0 ? body.Substring(0, dashIdx) : body;
                string endHex = dashIdx >= 0 ? body.Substring(dashIdx + 1) : body;

                if (!int.TryParse(startHex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out int start)
                    || !int.TryParse(endHex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out int end)
                    || start > end)
                {
                    continue; // 파싱 실패/역전 구간 — 이 토큰만 무시.
                }

                AddSampleIfEligible(result, start, maxSamples);
                if (end != start)
                {
                    int mid = start + (end - start) / 2;
                    AddSampleIfEligible(result, mid, maxSamples);
                }
            }

            return result;
        }

        private static void AddSampleIfEligible(List<int> result, int codepoint, int maxSamples)
        {
            if (result.Count >= maxSamples || codepoint > 0xFFFF || result.Contains(codepoint))
            {
                return; // BMP 밖은 char 로 표현 불가 → 제외. 중복도 스킵.
            }

            result.Add(codepoint);
        }

        /// <summary>
        /// 선택 언어 CSV 를 LazyEligible 여부로 lazySet/bootTags 로 분할한다. 태그 중복은 첫 등장만
        /// 반영(순서 보존은 하지 않음 — 최종 직렬화는 JoinInTableOrder 가 담당). 미지 태그는 안전하게
        /// bootTags 로 분류한다(기존 BuildRanges 도 미지 태그를 무시하므로 boot 에 있어도 무해).
        /// 부수 효과 없음 → 단위 테스트 대상.
        /// </summary>
        internal static void SplitLazyAndBootTags(string selectedLanguagesCsv, out List<string> lazyTags, out List<string> bootTags)
        {
            lazyTags = new List<string>();
            bootTags = new List<string>();
            if (string.IsNullOrEmpty(selectedLanguagesCsv))
            {
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var raw in selectedLanguagesCsv.Split(','))
            {
                string tag = raw.Trim();
                if (tag.Length == 0 || !seen.Add(tag))
                {
                    continue;
                }

                if (AITFontSubsetLanguages.TryFindEntry(tag, out var entry) && entry.LazyEligible)
                {
                    lazyTags.Add(tag);
                }
                else
                {
                    bootTags.Add(tag);
                }
            }
        }

        /// <summary>태그 목록을 AITFontSubsetLanguages.Table 순서로 결정적 직렬화(CSV). 부수 효과 없음.</summary>
        internal static string JoinInTableOrder(IEnumerable<string> tags)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (tags != null)
            {
                foreach (var t in tags)
                {
                    set.Add(t);
                }
            }

            var ordered = new List<string>();
            foreach (var entry in AITFontSubsetLanguages.Table)
            {
                if (set.Contains(entry.Tag))
                {
                    ordered.Add(entry.Tag);
                }
            }

            return string.Join(",", ordered);
        }

        /// <summary>매니페스트 JSON 문자열을 파싱한다(파일 IO 없음 — 순수 함수, 단위 테스트 대상).
        /// 파싱 실패/빈 입력은 entries=빈 배열로 안전 반환.</summary>
        internal static ManifestDto ParseManifestJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new ManifestDto { entries = Array.Empty<ManifestEntryDto>() };
            }

            try
            {
                var dto = JsonUtility.FromJson<ManifestDto>(json);
                if (dto.entries == null)
                {
                    dto.entries = Array.Empty<ManifestEntryDto>();
                }

                return dto;
            }
            catch
            {
                return new ManifestDto { entries = Array.Empty<ManifestEntryDto>() };
            }
        }

        /// <summary>엔트리 1건을 매니페스트 JSON 조각으로 직렬화한다(파일 IO 없음 — 순수 함수).
        /// encoding/lazyTag/lazyRanges 는 비어 있으면 생략(기존 eager 엔트리와 형식 동일 유지).</summary>
        internal static string BuildEntryJson(ManifestEntryDto e)
        {
            var sb = new StringBuilder();
            sb.Append("{\"guid\":").Append(JsonStr(e.guid ?? string.Empty))
              .Append(",\"bundle\":").Append(JsonStr(e.bundle ?? string.Empty));
            if (!string.IsNullOrEmpty(e.encoding))
            {
                sb.Append(",\"encoding\":").Append(JsonStr(e.encoding));
            }

            sb.Append(",\"fonts\":[");
            if (e.fonts != null)
            {
                for (int i = 0; i < e.fonts.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append(JsonStr(e.fonts[i]));
                }
            }

            sb.Append(']');
            if (!string.IsNullOrEmpty(e.lazyTag))
            {
                sb.Append(",\"lazyTag\":").Append(JsonStr(e.lazyTag));
                sb.Append(",\"lazyRanges\":").Append(JsonStr(e.lazyRanges ?? string.Empty));
            }

            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// 기존 엔트리 배열과 이번에 새로 만든 lazy 엔트리를 병합한다(파일 IO 없음 — 순수 함수).
        /// 규칙: eager 엔트리(lazyTag 비어있음)는 항상 보존. 기존 lazy 엔트리는 무조건 버리고
        /// newLazyEntries 로만 대체한다(N10) — ApplyLazyExtensions 는 매 호출마다 이번 빌드가 시도하는
        /// lazyTags 전부를 처리해 성공(newLazyEntries)/실패(boot 폴백) 중 하나로 귀결시키므로, "이번에
        /// 다시 만들지 못한" 기존 lazy 엔트리는 태그가 더 이상 선택되지 않았거나 이번 빌드에서 실패해
        /// boot 로 폴백된 것 — 어느 쪽이든 stale 이라 보존할 이유가 없다(그대로 두면 참조 없는 잔존
        /// 번들·구 태그 엔트리가 매니페스트에 영구 축적된다).
        /// </summary>
        internal static List<ManifestEntryDto> MergeLazyEntries(ManifestEntryDto[] existingEntries, List<ManifestEntryDto> newLazyEntries)
        {
            var merged = new List<ManifestEntryDto>();
            if (existingEntries != null)
            {
                foreach (var e in existingEntries)
                {
                    if (string.IsNullOrEmpty(e.lazyTag))
                    {
                        merged.Add(e); // eager 엔트리는 항상 보존.
                    }
                }
            }

            if (newLazyEntries != null)
            {
                merged.AddRange(newLazyEntries);
            }

            return merged;
        }

        private static string JsonStr(string s)
            => "\"" + (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        // ─────────────────────────── 매니페스트 IO ───────────────────────────

        /// <summary>StreamingAssets 에 lazy-*.bundle[.br] 아티팩트가 존재하는지(파일 존재 검사만 — 가벼움).
        /// fontStreaming(AITFontExternalizer)의 apply-phase 정리 로직이 BuildPlayer 전에 이 디렉토리를
        /// 무조건 삭제하지 않도록 가드하는 데 쓰인다.</summary>
        internal static bool HasLazyArtifacts(string streamRootFull)
        {
            try
            {
                if (string.IsNullOrEmpty(streamRootFull) || !Directory.Exists(streamRootFull))
                {
                    return false;
                }

                return Directory.GetFiles(streamRootFull, "lazy-*.bundle*").Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>manifest.json 을 읽어 파싱한다(없거나 실패 시 entries=빈 배열).</summary>
        internal static ManifestDto ReadManifest(string manifestPath)
        {
            try
            {
                if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
                {
                    return new ManifestDto { entries = Array.Empty<ManifestEntryDto>() };
                }

                return ParseManifestJson(File.ReadAllText(manifestPath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-FontSubset-Lazy] 매니페스트 읽기 실패({manifestPath}): {e.Message}");
                return new ManifestDto { entries = Array.Empty<ManifestEntryDto>() };
            }
        }

        /// <summary>
        /// 기존 manifest.json 에서 lazy 엔트리(lazyTag 비어있지 않음)만 골라 원본과 동일한 JSON 조각으로
        /// 재직렬화한다. AITFontExternalizer 가 자신의 eager 엔트리 쓰기 직전에 호출해 read-merge-write 를
        /// 완성한다(계약: 서로의 엔트리를 덮어쓰지 않음).
        /// </summary>
        internal static List<string> ReadExistingLazyEntryJson(string manifestPath)
        {
            var manifest = ReadManifest(manifestPath);
            var result = new List<string>();
            if (manifest.entries != null)
            {
                foreach (var e in manifest.entries)
                {
                    if (!string.IsNullOrEmpty(e.lazyTag))
                    {
                        result.Add(BuildEntryJson(e));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 롤백/안전망 경로(AITFontExternalizer.PruneStreamRootForLazyOnly) 전용: manifest 를 lazy 엔트리만
        /// 남기고 다시 쓴다. 그 경로는 eager 번들 파일을 삭제하므로, 삭제된 번들을 가리키는 stale eager
        /// 엔트리가 남으면 런타임이 태그별 다운로드 실패 경고를 찍고 이후 빌드의 read-merge-write 로
        /// 이월될 수 있다. 실패는 삼킨다(정리 부속 작업이 롤백 본류를 막으면 안 됨) — 멱등.
        /// </summary>
        internal static void PruneManifestToLazyOnly(string manifestPath)
        {
            try
            {
                if (!File.Exists(manifestPath))
                {
                    return;
                }

                var manifest = ReadManifest(manifestPath);
                var lazyJsons = new List<string>();
                bool hadEager = false;
                if (manifest.entries != null)
                {
                    foreach (var e in manifest.entries)
                    {
                        if (!string.IsNullOrEmpty(e.lazyTag))
                        {
                            lazyJsons.Add(BuildEntryJson(e));
                        }
                        else
                        {
                            hadEager = true;
                        }
                    }
                }

                if (!hadEager)
                {
                    return; // 이미 lazy 전용 — 다시 쓸 필요 없음.
                }

                var sb = new StringBuilder();
                sb.Append("{\"maxConcurrent\":").Append(manifest.maxConcurrent > 0 ? manifest.maxConcurrent : 2)
                  .Append(",\"entries\":[").Append(string.Join(",", lazyJsons)).Append("]}");
                File.WriteAllText(manifestPath, sb.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-FontSubset-Lazy] manifest lazy 전용 정리 실패(무시): {e.Message}");
            }
        }

        /// <summary>read-merge-write: 기존 매니페스트를 읽어 eager/다른 태그의 lazy 엔트리를 보존한 채
        /// 이번 lazy 엔트리를 병합해 다시 쓴다.</summary>
        private static void WriteMergedManifest(string streamRootFull, int fallbackMaxConcurrent, List<ManifestEntryDto> newLazyEntries)
        {
            string manifestPath = Path.Combine(streamRootFull, "manifest.json");
            var existing = ReadManifest(manifestPath);
            var merged = MergeLazyEntries(existing.entries, newLazyEntries);
            int maxConcurrent = existing.maxConcurrent > 0 ? existing.maxConcurrent : fallbackMaxConcurrent;

            var entryJsons = new List<string>();
            foreach (var e in merged)
            {
                entryJsons.Add(BuildEntryJson(e));
            }

            var sb = new StringBuilder();
            sb.Append("{\"maxConcurrent\":").Append(maxConcurrent)
              .Append(",\"entries\":[").Append(string.Join(",", entryJsons)).Append("]}");

            Directory.CreateDirectory(streamRootFull);
            File.WriteAllText(manifestPath, sb.ToString());
            AssetDatabase.Refresh();
        }

        // ─────────────────────────── 정리/안전망 ───────────────────────────

        /// <summary>
        /// 빌드 후(AITFontSubsetProcessor.RestoreForBuild, handle.LazyActive 일 때) 또는 안전망에서 호출:
        /// StreamingAssets ait-stream-font 디렉토리·lazy 번들 임시 디렉토리·임시 임포트 폴더·마커를 정리한다.
        /// BuildPlayer 이후 시점에서만 호출되어야 안전하다(그 전에 부르면 빌드 산출물에 lazy 번들이
        /// 반영되지 못한다 — AITFontExternalizer 쪽 HasLazyArtifacts 가드 참조).
        /// </summary>
        internal static void CleanupAfterBuild()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string streamRootFull = Path.Combine(projectRoot, StreamRootAssets);
                if (Directory.Exists(streamRootFull))
                {
                    Directory.Delete(streamRootFull, true);
                }

                string meta = streamRootFull + ".meta";
                if (File.Exists(meta))
                {
                    File.Delete(meta);
                }

                string bundleTempFull = Path.Combine(projectRoot, BundleTempDir);
                if (Directory.Exists(bundleTempFull))
                {
                    Directory.Delete(bundleTempFull, true);
                }

                CleanupTempFolder();
                RemoveMarker();
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-FontSubset-Lazy] 빌드 후 정리 예외(무시): {e.Message}");
            }
        }

        /// <summary>에디터 로드 시 안전망. 마커가 잔존하면(=이전 빌드가 lazy 처리 도중 종료) 잔존물을 정리한다.</summary>
        private static void SafetyNetRestore()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                if (!File.Exists(Path.Combine(projectRoot, MarkerRelative)))
                {
                    return; // 공통 경로: 잔존물 없음(빠른 반환)
                }

                Debug.LogWarning("[AIT-FontSubset-Lazy] 안전망: 이전 빌드가 lazy 확장 진행 중 종료된 잔존물을 정리합니다.");
                CleanupAfterBuild();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-FontSubset-Lazy] 안전망 복원 중 예외(무시): {e}");
            }
        }

        /// <summary>R7: 부모 폴더(Assets/AppsInToss) 생성 추적/정리 분기는 실제 빌드에서 절대 실행되지
        /// 않는 dead code 였다(config 에셋이 Assets/AppsInToss 하위에 위치해 폴더가 항상 선재) — 제거하고
        /// AITFontLazyTmp 자신의 생성/정리만 유지한다. Assets/AppsInToss 가 없는 극단적 상황을 대비해
        /// 생성 자체는 방어적으로 남긴다(추적/역정리는 하지 않음).</summary>
        private static void EnsureTempFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/AppsInToss"))
            {
                AssetDatabase.CreateFolder("Assets", "AppsInToss");
            }

            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder("Assets/AppsInToss", "AITFontLazyTmp");
            }
        }

        private static void CleanupTempFolder()
        {
            try
            {
                if (AssetDatabase.IsValidFolder(TempFolder))
                {
                    AssetDatabase.DeleteAsset(TempFolder);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-FontSubset-Lazy] 임시 폴더 정리 예외(무시): {e.Message}");
            }
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

        private static string SanitizeTag(string tag)
            => (tag ?? string.Empty).Replace('/', '_').Replace('\\', '_');

        private static string ComputeShortHash(byte[] data)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(data);
                var sb = new StringBuilder(8);
                for (int i = 0; i < 4 && i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }

                return sb.ToString();
            }
        }

        private static void SafeDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // 무시
            }
        }
    }
}
