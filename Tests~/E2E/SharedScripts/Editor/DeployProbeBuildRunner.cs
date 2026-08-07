using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
#if UNITY_6000_0_OR_NEWER
using UnityEditor.Build;
#endif
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AppsInToss;
using AppsInToss.Editor;

/// <summary>
/// 배포 프로브(deploy-probe) 픽스처 빌드 진입점.
///
/// 배경: 기존 deploy-probe(.ait 를 실서버에 업로드해 수락 검증)는 빈 SampleUnityProject 를 그대로
/// 빌드해 perf 최적화 레버(텍스처/폰트/오디오 스트리밍 등)가 하나도 발화하지 않는다. Heavy 프로젝트도
/// 재사용 불가하다 — HeavyGen 콘텐츠는 <c>Assets/Resources/HeavyGen/</c> 밑이라
/// <c>AITLargeTextureExternalizer</c>가 <c>/Resources/</c> 경로를 하드 제외하고, raw .otf 사본만으론
/// TMP_FontAsset 이 없어 fontStreaming 도 걸리지 않는다.
///
/// 이 러너는 빌드 시점에 <c>Assets/DeployProbeGen/</c>(Resources 밖)에 결정론적 합성 콘텐츠를 생성해
/// 텍스처/폰트/오디오 관련 레버가 실제 후보를 갖도록 만든 뒤, 검증된
/// <see cref="E2EBuildRunner.BuildWithSDK"/> 파이프라인을 그대로 호출한다 — <see cref="HeavyBuildRunner"/>
/// 와 동일한 철학(별도 픽스처 생성 + 기존 빌드 파이프라인 재사용).
///
/// 결정론: LCG 시드 기반 생성만 사용한다(System.Random/DateTime 금지) — HeavyBuildRunner 와 동일 패턴.
/// 각 Generate* 메서드는 "파일 기록 → ForceSynchronousImport → 임포터 설정 → SaveAndReimport" 를
/// 자기 완결적으로 수행하며, HeavyBuildRunner 와 동일한 이유로 StartAssetEditing/StopAssetEditing
/// 배칭을 사용하지 않는다(배칭 중에는 AssetImporter.GetAtPath() 가 아직 임포트 전이라 null 을
/// 반환해 NRE 가 난다).
///
/// TMP(TextMeshPro) 의존성: SharedScripts 의 Runtime/Editor asmdef 는 Unity.TextMeshPro 를 참조하지
/// 않는다. 따라서 AITFontSubsetProcessor/AITFontExternalizer 와 동일하게 TMP 타입은 전부 리플렉션으로만
/// 접근한다(컴파일 타임 TMPro 참조 없음 — 2022.3/6000.x 모두, TMP 설치 여부와 무관하게 컴파일된다).
/// 샘플 프로젝트별 TMP 실측(Packages/manifest.json): 2021.3/2022.3 는 "com.unity.textmeshpro": "3.0.6"
/// 을 명시 추가했고(TMP 가용), 6000.x 는 "com.unity.ugui": "2.0.0" 만 선언한다(ugui 2.0 이 TMP 를
/// 내장 제공하는 것으로 추정 — 별도 com.unity.textmeshpro 항목 없이도 TMPro.* 타입이 해석될 수 있다).
/// 즉 "TMP 미설치" 는 현재 샘플 매트릭스의 상시 상태가 아니라, 타입 해석/Essential Resources
/// 임포트/셰이더 가용성 중 하나가 실패하는 예외적 경우를 위한 안전망이다 — 그 경우
/// TMP_FontAsset 생성을 건너뛰고 레거시 UnityEngine.UI.Text + 원본 .otf 로 폴백한다(fontSubset
/// 레버는 아래에서 명시 선택하는 fontSubsetLanguages="ja" 덕분에 계속 발화, fontStreaming 레버만
/// 스킵 — GetFontStreamingCandidates 가 t:TMP_FontAsset 만 스캔하기 때문). 같은 이유로
/// fontSubsetLazyLanguages=1 도 lazy 확장 경로(AITFontLazyExtensionBuilder.TryCreateDynamicTmpFontAsset)가
/// TMP 부재로 실패해 안전 불변식에 따라 fallback-to-boot 하는 경로를 커버하게 된다 — TMP 가 실제로
/// 가용한 환경에서는 lazy 확장 성공 경로(서브셋 TTF → Dynamic TMP_FontAsset → AssetBundle)를 커버한다.
/// </summary>
[InitializeOnLoad]
public class DeployProbeBuildRunner
{
    /// <summary>생성 루트(Resources 밖 — 대형 텍스처 외부화가 /Resources/ 를 하드 제외하므로 필수).</summary>
    private const string ProbeRoot = "Assets/DeployProbeGen";

    /// <summary>프로브 씬 경로. E2EBuildRunner 에 env var 로 핸드오프해 index 1 로 추가시킨다.</summary>
    private const string ScenePath = ProbeRoot + "/DeployProbeScene.unity";

    /// <summary>E2EBuildRunner 훅과 공유하는 env var 이름.</summary>
    private const string EnvSceneVar = "AIT_DEPLOY_PROBE_SCENE_PATH";

    /// <summary>
    /// WebGL 스크립팅 디파인 게이트. 이 빌드에서만 추가되어 DeployProbeLazyTextSpawner(Runtime)의
    /// 부트스트랩이 컴파일/발화하게 만든다 — 표준 E2E 빌드에는 이 디파인이 없어 해당 파일 전체가
    /// #if 밖(빈 컴파일 유닛)이라 영향이 없다.
    /// </summary>
    private const string DeployProbeDefine = "AIT_E2E_DEPLOY_PROBE";

    /// <summary>
    /// AITFontLazyExtensionBuilder.BuildLazyExtensionForTag 의 B2(커버리지 0 폴백) 경고 조각.
    /// 정확한 문자열은 Editor/AITFontLazyExtensionBuilder.cs 의 HasAnyCoverage 호출부에서 확인함 —
    /// 그 파일의 로그 문구가 바뀌면 이 상수도 함께 갱신해야 한다.
    /// </summary>
    private const string ThCoverageFallbackLogFragment =
        "'th' 소스 폰트가 해당 문자체계를 포함하지 않아 lazy 확장을 건너뜁니다.";

    /// <summary>
    /// AITFontLazyExtensionBuilder.ApplyLazyExtensions 의 S3(TMP Settings 리소스 부재) 폴백 경고 조각.
    /// TMP 미설치 환경(HasTmpSettingsResource()==false)에서 lazy 확장 전체를 포기할 때 남는다.
    /// </summary>
    private const string TmpAbsentFallbackLogFragment =
        "TMP Settings 리소스를 찾을 수 없어 lazy 확장을 건너뜁니다";

    /// <summary>
    /// F1: AITFontLazyExtensionBuilder.ApplyLazyExtensions(Editor/AITFontLazyExtensionBuilder.cs
    /// 164-201행 부근)가 lazy 확장을 통째로 포기할 때 남기는 스킵 사유 로그 조각 목록. tmpSettingsAvailable
    /// (SDK 게이트) 이 true 인데 manifest 에 ja lazy 엔트리가 없는 불일치가 나면, 이 조각들로
    /// capturedLogs 를 스캔해 실제 스킵 사유를 failureReason 에 덧붙인다 — CI 로그만으로 원인이
    /// 확정되도록 한다. 원본 로그 문구가 바뀌면 이 목록도 함께 갱신해야 한다.
    ///   - 모듈 게이트(HasRequiredRuntimeModules, 169-172행): 사유 문자열이 동적이라(missingModuleReason)
    ///     고정 접두/접미 중 module-gate 메시지에만 나타나는 em-dash 접미부로 식별한다.
    ///   - TMP Settings 리소스 부재(178-181행): TmpAbsentFallbackLogFragment 재사용.
    ///   - subset 대상 폰트 없음(190-193행): "subset 대상 폰트가 없어" 접두부로 식별한다.
    ///   - 다중 target 폰트(199-202행): "대상 폰트가 여러 개라" 접두부로 식별한다.
    /// </summary>
    private static readonly (string Label, string Fragment)[] LazySkipReasonLogFragments =
    {
        ("모듈 게이트(HasRequiredRuntimeModules)", " — lazy 확장을 건너뜁니다(선택 언어는 부트 union 유지)."),
        ("TMP Settings 리소스 부재", TmpAbsentFallbackLogFragment),
        ("subset 대상 폰트 없음", "subset 대상 폰트가 없어"),
        ("다중 target 폰트", "대상 폰트가 여러 개라"),
    };

    /// <summary>
    /// 설정 원복 안전망(sentinel) 파일 경로(프로젝트 루트 기준, Library/ 하위 — .gitignore 로
    /// 이미 무시 대상: "Tests~/E2E/SampleUnityProject*/Library/"). BuildDeployProbe() 가 옵트인
    /// 레버(config 4필드) + WebGL 스크립팅 디파인을 변경하기 직전에 원본 값을 이 파일에 기록하고,
    /// 정상 종료(finally) 시 삭제한다. E2EBuildRunner.BuildWithSDK() 가 실패 경로에서
    /// EditorApplication.Exit(1|2) 로 프로세스를 즉시 종료하면 finally 가 돌지 않아 이 파일이
    /// 잔존하는데, 그 경우 다음 에디터 로드 시 <see cref="SafetyNetRestore"/>(AITFontSubsetProcessor
    /// 의 SafetyNetRestore 와 동일 패턴)가 자동 복원한다.
    /// </summary>
    private const string SentinelRelativePath = "Library/AITDeployProbeSentinel.json";

    /// <summary>sentinel 파일에 기록되는 원본 설정 스냅샷.</summary>
    [Serializable]
    private class ProbeSettingsSentinel
    {
        public int textureStreamJpeg;
        public int audioStreamTranscode;
        public string fontSubsetLanguages;
        public int fontSubsetLazyLanguages;
        public string webglDefines;
    }

    static DeployProbeBuildRunner()
    {
        EditorApplication.delayCall += SafetyNetRestore;
    }

    /// <summary>
    /// 에디터 로드 시 안전망. sentinel 이 잔존하면(=이전 프로브 빌드가 원복 전 비정상 종료) 원본
    /// config 값 + WebGL 스크립팅 디파인을 복원한다. sentinel 이 없으면(공통 경로) no-op.
    /// </summary>
    private static void SafetyNetRestore()
    {
        try
        {
            string path = GetSentinelFullPath();
            if (!File.Exists(path))
            {
                return; // 공통 경로: 잔존물 없음(빠른 반환).
            }

            var data = JsonUtility.FromJson<ProbeSettingsSentinel>(File.ReadAllText(path));
            if (data != null)
            {
                var config = UnityUtil.GetEditorConf();
                if (config != null)
                {
                    config.textureStreamJpeg = data.textureStreamJpeg;
                    config.audioStreamTranscode = data.audioStreamTranscode;
                    config.fontSubsetLanguages = data.fontSubsetLanguages;
                    config.fontSubsetLazyLanguages = data.fontSubsetLazyLanguages;
                    EditorUtility.SetDirty(config);
                }

                SetWebGLDefines(data.webglDefines ?? string.Empty);

                // F0: config + WebGL 디파인 원복이 모두 반영된 뒤 한 번에 영속화 — 센티널을 지우기
                // 전에 디스크 flush 가 끝나야 한다(아래 finally 원복 경로와 동일한 순서 보장).
                AssetDatabase.SaveAssets();
            }

            DeleteSentinel();
            Debug.LogWarning("[deploy-probe] 안전망: 이전 빌드가 원복 전 비정상 종료되어 잔존한 프로브 설정을 복원했습니다.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[deploy-probe] 안전망 복원 중 예외(무시): {e}");
        }
    }

    private static string GetSentinelFullPath()
    {
        return Path.Combine(UnityUtil.GetProjectPath(), SentinelRelativePath);
    }

    /// <summary>설정 변경 직전에 원본 값을 sentinel 파일에 기록한다.</summary>
    private static void WriteSentinel(ProbeSettingsSentinel data)
    {
        try
        {
            string path = GetSentinelFullPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(data));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[deploy-probe] 설정 원복 sentinel 기록 실패(비정상 종료 시 자동 복원 안전망이 비활성화됨): {e.Message}");
        }
    }

    /// <summary>정상 원복이 끝난 뒤 sentinel 파일을 삭제한다.</summary>
    private static void DeleteSentinel()
    {
        try
        {
            string path = GetSentinelFullPath();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[deploy-probe] sentinel 삭제 실패(무시): {e.Message}");
        }
    }

    /// <summary>WebGL 스크립팅 디파인 조회(Unity 6000 이상은 obsolete 된 *ForGroup 대신
    /// NamedBuildTarget 오버로드 사용 — Editor/AITBuildInitializer.cs 의 기존 컨벤션과 동일).</summary>
    private static string GetWebGLDefines()
    {
#if UNITY_6000_0_OR_NEWER
        return PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL);
#else
        return PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.WebGL);
#endif
    }

    /// <summary>WebGL 스크립팅 디파인 설정(위 GetWebGLDefines() 와 동일 사유).</summary>
    private static void SetWebGLDefines(string defines)
    {
#if UNITY_6000_0_OR_NEWER
        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.WebGL, defines);
#else
        PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.WebGL, defines);
#endif
    }

    [MenuItem("E2E/Build Deploy Probe")]
    public static void BuildDeployProbe()
    {
        Debug.Log("========================================");
        Debug.Log("Deploy Probe Fixture Build");
        Debug.Log("========================================");

        bool fontAssetGenerated;
        try
        {
            fontAssetGenerated = GenerateProbeContent();
        }
        catch (Exception ex)
        {
            // 생성 실패는 빌드 전 단계이므로 CI가 명확히 검출하도록 exit 1 (HeavyBuildRunner 와 동일 규약).
            // 예외 메시지/스택트레이스에 "ait-build" 등 AIT 키워드가 섞여 들어가면 SDK 에러 트래커가
            // 캡처(IsAitRelated 통과)하므로 sentryCapture:false 로 차단한다 — 이 실패는 CI exit code 1로
            // 검출되며 Sentry 대상이 아니다(E2EBuildRunner.cs 의 동일 컨벤션 참조).
            Debug.LogError("========================================");
            AITLog.Error($"Deploy probe content generation FAILED: {ex}", sentryCapture: false);
            Debug.LogError("========================================");
            EditorApplication.Exit(1);
            return;
        }

        // E2EBuildRunner.BuildWithSDK() 는 매 실행마다 EditorBuildSettings.scenes 를 단일 원소
        // 배열로 덮어써 사전 배선을 전부 지운다(BenchmarkScene 재생성 로직). 프로브 씬을 지우지 않고
        // index 1 로 추가시키려면 env var 훅 외 다른 방법이 없다 — BuildWithSDK 호출 전에 설정한다.
        Environment.SetEnvironmentVariable(EnvSceneVar, ScenePath);

        var config = UnityUtil.GetEditorConf();

        // 원본 값 캡처(로컬 반복 실행 시 설정 오염 방지 — CI는 매번 clean checkout 이라 무관하지만
        // 로컬에서 이 메뉴를 반복 실행해도 프로젝트 설정이 프로브 값으로 고착되지 않도록 한다).
        int originalTextureStreamJpeg = config.textureStreamJpeg;
        int originalAudioStreamTranscode = config.audioStreamTranscode;
        string originalFontSubsetLanguages = config.fontSubsetLanguages;
        int originalFontSubsetLazyLanguages = config.fontSubsetLazyLanguages;
        string originalWebGLDefines = GetWebGLDefines();

        bool assertionsPassed = true;
        string failureReason = null;
        var capturedLogs = new List<string>();

        try
        {
            // F0: 옵트인 레버를 건드리기 직전에 원본 값을 sentinel 파일(Library/ 하위, untracked)에
            // 기록한다 — 아래에서 호출하는 E2EBuildRunner.BuildWithSDK() 가 실패 경로에서
            // EditorApplication.Exit(1|2) 로 프로세스를 즉시 종료하면 이 메서드의 finally 가 돌지 않아
            // 원복이 누락되는데, 그 경우 다음 에디터 로드 시 SafetyNetRestore() 가 이 sentinel 로 복원한다.
            WriteSentinel(new ProbeSettingsSentinel
            {
                textureStreamJpeg = originalTextureStreamJpeg,
                audioStreamTranscode = originalAudioStreamTranscode,
                fontSubsetLanguages = originalFontSubsetLanguages,
                fontSubsetLazyLanguages = originalFontSubsetLazyLanguages,
                webglDefines = originalWebGLDefines,
            });

            // 옵트인 레버 명시 활성화. textureStreamJpeg/audioStreamTranscode 는 시각/청취 검증 전까지
            // 기본값이 -1(자동=비활성) 이라, 프로브 빌드에서 발화시키려면 명시적으로 1 을 설정해야 한다.
            // fontSubset 는 자동(-1) 모드에서 동적 텍스트 언어가 하나도 선택되지 않으면 서브셋 자체를
            // 건너뛰므로(선택 = 인지된 활성화, AITFontSubsetProcessor.ShouldSkipAutoWithoutSelection)
            // fontSubsetLanguages 를 명시 선택해 러너 실행·복원·리포트와 언어 union 경로를 계속 커버한다.
            // fontSubsetLazyLanguages=1(명시 활성)을 fontSubsetLanguages="ja,th"(둘 다 LazyEligible)와
            // 결합해 AITFontLazyExtensionBuilder 의 lazy 확장 파이프라인(서브셋 TTF → Dynamic
            // TMP_FontAsset → AssetBundle → manifest.json lazyTag/lazyRanges 기록)이 실빌드에서 실제로
            // 발화하게 한다(TMP 미설치 환경에서는 TryCreateDynamicTmpFontAsset 이 실패해
            // fallback-to-boot 경로가 대신 발화한다 — 두 경로 모두 이 프로브로 커버됨). th 는
            // NotoSansKR 이 커버리지 0 인 언어라 B2(커버리지 폴백) 부정 경로를 함께 검증한다.
            // 나머지 레버(fontStreaming/textureStreaming/downscale/recompress/audioStreaming/
            // audioReencode)는 전부 auto-ON(-1) 이라 별도 설정이 필요 없다.
            config.textureStreamJpeg = 1;
            config.audioStreamTranscode = 1;
            config.fontSubsetLanguages = "ja,th";
            config.fontSubsetLazyLanguages = 1;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log("✓ 옵트인 레버 명시 활성화: textureStreamJpeg=1, audioStreamTranscode=1, " +
                "fontSubsetLanguages=ja,th, fontSubsetLazyLanguages=1");

            // DeployProbeLazyTextSpawner(Runtime, #if AIT_E2E_DEPLOY_PROBE 게이트)가 이 빌드에서만
            // 컴파일/부팅되도록 WebGL 스크립팅 디파인에 추가한다.
            string newDefines = AddDefine(originalWebGLDefines, DeployProbeDefine);
            SetWebGLDefines(newDefines);
            Debug.Log($"✓ WebGL 스크립팅 디파인에 {DeployProbeDefine} 추가");

            // 빌드 중 로그를 전부 캡처해 lazy 확장의 성공/폴백 경고 문자열을 사후 검증한다.
            Application.LogCallback logHandler = (condition, stackTrace, type) => capturedLogs.Add(condition);
            Application.logMessageReceived += logHandler;
            try
            {
                // 검증된 E2E 빌드 파이프라인을 그대로 재사용(씬/SDK 설정/포트 오프셋/산출물 검증/exit
                // code 처리 전부 E2EBuildRunner 소유 — HeavyBuildRunner 와 동일 패턴). 실패 시
                // E2EBuildRunner 가 EditorApplication.Exit(1|2) 을 직접 호출해 프로세스가 종료되므로,
                // 아래로 정상 반환됐다는 것 자체가 "빌드 성공"을 의미한다.
                E2EBuildRunner.BuildWithSDK();
            }
            finally
            {
                Application.logMessageReceived -= logHandler;
            }

            // F1: SDK 의 실제 lazy 진입 게이트(HasTmpSettingsResource)와 동일 기준으로 판정한다.
            // fontAssetGenerated(GenerateProbeContent()의 TMP_FontAsset 생성 성공 여부)와는 별개다 —
            // essentials 임포트 직후 Shader.Find 지연으로 폰트 에셋 생성만 실패해도 TMP Settings
            // 리소스 자체는 존재해 SDK lazy 게이트는 정상 발화할 수 있다(그 반대 불일치도 가능).
            bool tmpSettingsAvailable = HasTmpSettingsResource();
            if (tmpSettingsAvailable != fontAssetGenerated)
            {
                Debug.Log("[deploy-probe] TMP 판정 불일치 감지(정상 범위): " +
                    $"tmpSettingsAvailable={tmpSettingsAvailable}, fontAssetGenerated={fontAssetGenerated} " +
                    "— 어서션은 tmpSettingsAvailable(SDK 게이트)을 기준으로 진행합니다.");
            }

            assertionsPassed = ValidateLazyArtifacts(tmpSettingsAvailable, capturedLogs, out failureReason);
        }
        finally
        {
            // S5: 캐시된 config 참조 대신 원복 시점에 config 에셋을 신선하게 재로드한다. E2EBuildRunner.
            // BuildWithSDK() 가 전체 빌드 + AssetDatabase 리프레시를 거치는 동안 위에서 캐시해 둔
            // config(네이티브 ScriptableObject 래퍼)가 파괴된 fake-null 상태가 될 수 있고, 그 상태로
            // EditorUtility.SetDirty 를 호출하면 ArgumentNullException 이 난다 — 이 finally 를
            // 통째로 삼켜 아래 assertionsPassed 실패 처리(exit code 3)까지 함께 유실시킨 원인이었다.
            // 그래서 재로드부터 sentinel 삭제까지 전체를 try/catch 로 감싼다: 원복 중 예외가 나도
            // 여기서 흡수해 아래의 exit code 분기가 반드시 실행되게 한다.
            try
            {
                var freshConfig = UnityUtil.GetEditorConf();
                if (freshConfig != null)
                {
                    freshConfig.textureStreamJpeg = originalTextureStreamJpeg;
                    freshConfig.audioStreamTranscode = originalAudioStreamTranscode;
                    freshConfig.fontSubsetLanguages = originalFontSubsetLanguages;
                    freshConfig.fontSubsetLazyLanguages = originalFontSubsetLazyLanguages;
                    EditorUtility.SetDirty(freshConfig);
                }
                else
                {
                    Debug.LogWarning("[deploy-probe] 원복 시점에 config 에셋 재로드 실패 — config 필드 원복을 건너뜁니다" +
                        "(sentinel 유지, 다음 에디터 로드 시 SafetyNetRestore 가 복원).");
                }

                // WebGL 디파인 원복은 config 와 무관하므로 재로드 성공 여부와 상관없이 항상 시도.
                SetWebGLDefines(originalWebGLDefines);

                // F0: config + WebGL 디파인 원복이 모두 반영된 뒤 한 번에 영속화 — 센티널을 지우기
                // 전에 디스크 flush 가 끝나야 한다(위 SafetyNetRestore 경로와 동일한 순서 보장).
                AssetDatabase.SaveAssets();

                if (freshConfig != null)
                {
                    // 정상 원복(config 포함)이 여기까지 도달했다는 것 자체가 sentinel 이 더 이상
                    // 필요 없다는 뜻 — 삭제. config 재로드가 실패한 경우는 원복이 불완전하므로
                    // sentinel 을 남겨 다음 에디터 로드 시 안전망이 마저 처리하게 한다.
                    DeleteSentinel();
                }
            }
            catch (Exception restoreEx)
            {
                // 원복 예외가 위 assertionsPassed 실패 처리(exit code 3)를 삼키지 않도록 흡수한다.
                // sentinel 은 지우지 않는다 — 다음 에디터 로드 시 SafetyNetRestore 가 원복을 이어받는다.
                Debug.LogWarning($"[deploy-probe] finally 원복 중 예외(sentinel 유지, 다음 에디터 로드 시 안전망이 복원): {restoreEx}");
            }
        }

        if (!assertionsPassed)
        {
            Debug.LogError("========================================");
            AITLog.Error($"Deploy probe lazy 산출물 검증 FAILED: {failureReason}", sentryCapture: false);
            Debug.LogError("========================================");
            EditorApplication.Exit(3);
        }
    }

    /// <summary>커맨드라인 진입점(batchmode -executeMethod 용).</summary>
    public static void CommandLineDeployProbeBuild()
    {
        BuildDeployProbe();
    }

    // ─────────────────────────── lazy 산출물 검증(W1) ───────────────────────────

    /// <summary>
    /// SDK 의 실제 lazy 진입 게이트를 미러링한 판정(F1). 출처:
    /// Editor/AITFontLazyExtensionBuilder.cs 의 private HasTmpSettingsResource() — 같은 어셈블리가
    /// 아니라 직접 참조할 수 없어 로직을 복제한다(원본이 바뀌면 이 메서드도 함께 갱신 필요).
    /// TMP_Settings 타입 해석(어셈블리 한정 시도 → 실패 시 AppDomain 전 어셈블리 스캔 폴백) +
    /// Resources.Load("TMP Settings", type) != null 로 판정한다. GenerateProbeContent() 의
    /// TMP_FontAsset 생성 성공 여부(fontAssetGenerated)와는 독립적인 신호다 — essentials 임포트
    /// 직후 Shader.Find 지연 등으로 폰트 에셋 생성만 실패해도 TMP Settings 리소스 자체는 존재해 이
    /// 게이트는 true 를 반환할 수 있다.
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
    /// Type.GetType(어셈블리 한정 문자열)이 null 을 반환하면(어셈블리명 차이로 흔함) 로드된 전
    /// 어셈블리를 스캔해 흡수한다(AITFontLazyExtensionBuilder.FindTypeAcrossAssemblies 와 동일).
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
    /// 빌드 성공 후 lazy 확장 산출물을 검증한다. tmpSettingsAvailable(SDK 의 실제 lazy 진입 게이트 —
    /// <see cref="HasTmpSettingsResource"/>) 여부에 따라 기대 결과가 갈린다:
    ///   - TMP 가용: ja 가 lazy 로 성공 분리(manifest 엔트리 + 번들 파일 실존) + th 는 커버리지 0 이라
    ///     boot 로 폴백(manifest 에 엔트리 없음 + 커버리지 폴백 경고 로그 존재).
    ///   - TMP 부재: lazy 산출물이 전혀 없어야 하고(manifest 부재 또는 lazy 엔트리 0건), lazy 포기 경고
    ///     로그가 있어야 한다. 이 경로에서도 빌드 자체는 성공해야 한다(안전 불변식).
    /// </summary>
    private static bool ValidateLazyArtifacts(bool tmpSettingsAvailable, List<string> capturedLogs, out string failureReason)
    {
        failureReason = null;

        string projectPath = UnityUtil.GetProjectPath();
        string streamFontDir = Path.Combine(
            projectPath, "ait-build", "dist", "web", "StreamingAssets", "ait-stream-font");
        string manifestPath = Path.Combine(streamFontDir, "manifest.json");

        var manifest = AITFontLazyExtensionBuilder.ReadManifest(manifestPath);
        var entries = manifest.entries ?? Array.Empty<AITFontLazyExtensionBuilder.ManifestEntryDto>();

        AITFontLazyExtensionBuilder.ManifestEntryDto? jaEntry = null;
        int lazyEntryCount = 0;
        foreach (var e in entries)
        {
            if (string.IsNullOrEmpty(e.lazyTag))
            {
                continue;
            }

            lazyEntryCount++;
            if (e.lazyTag == "ja")
            {
                jaEntry = e;
            }
        }

        if (tmpSettingsAvailable)
        {
            if (jaEntry == null)
            {
                failureReason = $"TMP 가용 환경인데 manifest 에 ja lazy 엔트리가 없음: {manifestPath}";
                // F1: capturedLogs 에서 SDK 의 lazy 스킵 사유 로그 조각을 찾아 덧붙인다 — CI 로그만으로
                // 원인이 확정되도록(모듈 게이트/TMP Settings 부재/subset 대상 없음/다중 target 여부).
                string skipReasonDetail = DescribeLazySkipReasons(capturedLogs);
                if (!string.IsNullOrEmpty(skipReasonDetail))
                {
                    failureReason += $" (capturedLogs 에서 감지된 SDK lazy 스킵 사유: {skipReasonDetail})";
                }
                return false;
            }

            if (string.IsNullOrEmpty(jaEntry.Value.lazyRanges))
            {
                failureReason = "ja lazy 엔트리의 lazyRanges 가 비어있음";
                return false;
            }

            string bundlePath = Path.Combine(streamFontDir, jaEntry.Value.bundle ?? string.Empty);
            if (string.IsNullOrEmpty(jaEntry.Value.bundle) || !File.Exists(bundlePath) || new FileInfo(bundlePath).Length <= 0)
            {
                failureReason = $"ja lazy 번들 파일이 없거나 크기 0: {bundlePath}";
                return false;
            }

            if (lazyEntryCount != 1)
            {
                // th 는 커버리지 0 이라 lazy 엔트리가 없어야 한다 — ja 외 다른 lazy 태그가 있으면 이상.
                failureReason = $"기대한 lazy 엔트리 수(1: ja)와 다름(실제 {lazyEntryCount}건) — th 가 부당하게 lazy 로 분리됐을 수 있음";
                return false;
            }

            if (!ContainsLog(capturedLogs, ThCoverageFallbackLogFragment))
            {
                failureReason = $"캡처 로그에 th 커버리지 폴백 경고가 없음(기대 문자열 조각: \"{ThCoverageFallbackLogFragment}\")";
                return false;
            }

            Debug.Log($"[deploy-probe] lazy 검증 통과(TMP 가용 경로): ja bundle={jaEntry.Value.bundle}, th 는 boot 폴백 확인");
        }
        else
        {
            if (lazyEntryCount > 0)
            {
                failureReason = $"TMP 부재 환경인데 manifest 에 lazy 엔트리가 {lazyEntryCount}건 존재함: {manifestPath}";
                return false;
            }

            if (!ContainsLog(capturedLogs, TmpAbsentFallbackLogFragment))
            {
                failureReason = $"캡처 로그에 lazy 포기 경고가 없음(기대 문자열 조각: \"{TmpAbsentFallbackLogFragment}\")";
                return false;
            }

            Debug.Log("[deploy-probe] lazy 검증 통과(TMP 부재 경로): lazy 산출물 없음 확인 + 포기 경고 로그 확인");
        }

        return true;
    }

    private static bool ContainsLog(List<string> logs, string fragment)
    {
        if (logs == null || string.IsNullOrEmpty(fragment))
        {
            return false;
        }

        foreach (var l in logs)
        {
            if (!string.IsNullOrEmpty(l) && l.Contains(fragment))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// F1: capturedLogs 를 <see cref="LazySkipReasonLogFragments"/> 목록으로 스캔해, 매칭된 스킵 사유
    /// 라벨을 쉼표로 이어붙여 반환한다(매칭 없으면 null). ValidateLazyArtifacts 가 tmpSettingsAvailable
    /// =true 인데 ja lazy 엔트리가 없는 불일치를 만났을 때 failureReason 을 정밀화하는 데 쓰인다.
    /// </summary>
    private static string DescribeLazySkipReasons(List<string> capturedLogs)
    {
        var hits = new List<string>();
        foreach (var (label, fragment) in LazySkipReasonLogFragments)
        {
            if (ContainsLog(capturedLogs, fragment))
            {
                hits.Add(label);
            }
        }

        return hits.Count > 0 ? string.Join(", ", hits) : null;
    }

    /// <summary>세미콜론 구분 스크립팅 디파인 문자열에 define 을 멱등 추가한다(이미 있으면 그대로 반환).</summary>
    private static string AddDefine(string existingDefines, string define)
    {
        if (string.IsNullOrEmpty(existingDefines))
        {
            return define;
        }

        foreach (var p in existingDefines.Split(';'))
        {
            if (p == define)
            {
                return existingDefines;
            }
        }

        return existingDefines + ";" + define;
    }

    // ─────────────────────────── 콘텐츠 생성 ───────────────────────────

    /// <summary>프로브 콘텐츠를 생성한다. 반환값은 TMP_FontAsset 생성 성공 여부(fontAssetGenerated) —
    /// 진단 로그(BuildDeployProbe)에서 SDK 의 실제 lazy 게이트(HasTmpSettingsResource)와 교차
    /// 비교하는 데 쓰인다(F1 — 어서션 자체는 더 이상 이 값에 의존하지 않는다).</summary>
    private static bool GenerateProbeContent()
    {
        // 결정론 보장: 매 빌드 전 생성 루트를 비우고 새로 만든다(HeavyBuildRunner 와 동일 규약).
        if (AssetDatabase.IsValidFolder(ProbeRoot))
        {
            AssetDatabase.DeleteAsset(ProbeRoot);
        }
        EnsureFolder(ProbeRoot);
        EnsureFolder(ProbeRoot + "/Textures");
        EnsureFolder(ProbeRoot + "/Fonts");
        EnsureFolder(ProbeRoot + "/Audio");

        string texPath = GenerateProbeTexture();
        string fontRawPath = GenerateProbeFontRaw();
        string fontAssetPath = TryGenerateProbeFontAsset(fontRawPath);
        string audioPath = GenerateProbeAudio();

        BuildProbeScene(texPath, fontRawPath, fontAssetPath, audioPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        LogProbeFootprint();

        return !string.IsNullOrEmpty(fontAssetPath);
    }

    // ---- 텍스처: 3072² 완전 불투명 LCG 노이즈 (textureStreaming/downscale/recompress/JPEG 전환) ----
    private static string GenerateProbeTexture()
    {
        int size = GetEnvInt("AIT_DEPLOY_PROBE_TEXTURE_SIZE", 3072);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];

        // 비압축성(노이즈) 픽셀 → PNG 수 MB(512KB 플로어 통과) + downscale cap(2048) 초과로 다운스케일도 발화.
        uint state = 0x9E3779B9u ^ 0xD1B54A35u; // 프로브 전용 고정 시드(결정론).
        for (int p = 0; p < pixels.Length; p++)
        {
            state = NextLcg(state);
            byte r = (byte)(state >> 24);
            byte g = (byte)(state >> 16);
            byte b = (byte)(state >> 8);
            // 알파 항상 255 — JPEG 전환 조건(완전 불투명)을 만족시켜야 한다.
            pixels[p] = new Color32(r, g, b, 255);
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        byte[] png = tex.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(tex);

        string assetPath = $"{ProbeRoot}/Textures/probe_tex.png";
        File.WriteAllBytes(assetPath, png);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

        var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        if (importer == null)
            throw new Exception(
                $"[deploy-probe] TextureImporter 가 null: {assetPath} (ForceSynchronousImport 후에도 임포트 안 됨 — " +
                "StartAssetEditing 배치로 감싸면 임포트가 지연되어 이 NRE 가 난다)");

        // 임포터 기본값 유지: Default 타입 + sRGB true + SpriteAtlas/NormalMap 미지정.
        // (textureStreaming 자동 탐지가 /Resources/, Splash, SpriteAtlas, NormalMap, non-sRGB(linear)
        //  를 하드 제외하므로 전부 피해야 한다.)
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.isReadable = false;
        importer.SaveAndReimport();

        return assetPath;
    }

    // ---- 폰트(원본 .otf): SharedScripts 패키지 동봉 NotoSansKR 사본 (fontSubset 대상) ----
    private static string GenerateProbeFontRaw()
    {
        const string knownSrc = "Packages/im.toss.sdk-test-scripts/Runtime/Resources/Fonts/NotoSansKR-Regular.otf";
        string dst = $"{ProbeRoot}/Fonts/probe_font.otf";

        if (!AssetDatabase.CopyAsset(knownSrc, dst))
        {
            // 폴백: AssetDatabase 검색으로 재해석(패키지 물리 경로가 버전/설치 방식에 따라 달라질 대비 —
            // HeavyBuildRunner.FindNotoSansKrPath 와 동일 사유).
            string resolved = FindNotoSansKrPath();
            if (string.IsNullOrEmpty(resolved) || !AssetDatabase.CopyAsset(resolved, dst))
            {
                throw new Exception($"[deploy-probe] NotoSansKR 폰트 복사 실패: {knownSrc}");
            }
        }

        AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
        return dst;
    }

    private static string FindNotoSansKrPath()
    {
        foreach (string guid in AssetDatabase.FindAssets("NotoSansKR t:Font"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.EndsWith(".otf") || p.EndsWith(".ttf")) return p;
        }
        return null;
    }

    // ---- TMP_FontAsset(리플렉션): 설치돼 있을 때만 생성(fontStreaming 대상). 미설치 시 null 반환 ----
    private static string TryGenerateProbeFontAsset(string rawFontPath)
    {
        try
        {
            Type fontAssetType = Type.GetType("TMPro.TMP_FontAsset, Unity.TextMeshPro");
            if (fontAssetType == null)
            {
                Debug.LogWarning("[deploy-probe] TMP(Unity.TextMeshPro) 미설치 감지 — TMP_FontAsset 생성 및 " +
                    "fontStreaming 레버 검증을 건너뜁니다. 씬은 레거시 UI Text + 원본 .otf 로 폴백합니다.");
                return null;
            }

            EnsureTmpEssentialResources(fontAssetType);

            // CreateFontAsset(Font) 은 내부에서 TMP SDF 셰이더로 머티리얼을 만든다 — Essential Resources
            // 임포트가 아직 반영되지 않아 셰이더가 없으면 new Material(null) 로 즉사하므로 선제 가드.
            if (Shader.Find("TextMeshPro/Distance Field") == null &&
                Shader.Find("TextMeshPro/Mobile/Distance Field") == null)
            {
                Debug.LogWarning("[deploy-probe] TMP SDF 셰이더 미가용(Essential Resources 임포트 미반영?) — " +
                    "TMP_FontAsset 생성을 건너뜁니다. 씬은 레거시 UI Text 폴백.");
                return null;
            }

            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(rawFontPath);
            if (sourceFont == null)
            {
                Debug.LogWarning($"[deploy-probe] 소스 Font 로드 실패: {rawFontPath} — TMP_FontAsset 생성 스킵.");
                return null;
            }

            // 버전 간 가장 안정적인 단일 오버로드만 사용한다: CreateFontAsset(Font). 다중 파라미터
            // 오버로드(atlas 크기/렌더모드 등)는 TMP 버전별 시그니처가 달라 리플렉션 안정성이 낮다.
            var createMethod = fontAssetType.GetMethod(
                "CreateFontAsset",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new Type[] { typeof(Font) },
                null);
            if (createMethod == null)
            {
                Debug.LogWarning("[deploy-probe] TMP_FontAsset.CreateFontAsset(Font) 오버로드를 찾지 못함 — 스킵.");
                return null;
            }

            object fontAssetObj = createMethod.Invoke(null, new object[] { sourceFont });
            var mainAsset = fontAssetObj as UnityEngine.Object;
            if (mainAsset == null)
            {
                Debug.LogWarning("[deploy-probe] TMP_FontAsset 생성 실패(null 반환) — 스킵.");
                return null;
            }

            // atlasPopulationMode = Dynamic(계획서 명시 사항 — 런타임 즉석 래스터화, static 베이킹 비용 회피).
            var atlasModeProp = fontAssetType.GetProperty("atlasPopulationMode");
            if (atlasModeProp != null && atlasModeProp.CanWrite)
            {
                object dynamicValue = Enum.Parse(atlasModeProp.PropertyType, "Dynamic");
                atlasModeProp.SetValue(fontAssetObj, dynamicValue);
            }

            string assetPath = $"{ProbeRoot}/Fonts/ProbeFontAsset.asset";
            AssetDatabase.CreateAsset(mainAsset, assetPath);

            // material/atlas 텍스처가 있으면 서브에셋으로 동봉(없거나 버전별 API 차이가 있어도 치명적이지 않음).
            TryAddSubAsset(fontAssetType, fontAssetObj, mainAsset, "material");
            TryAddAtlasTextures(fontAssetType, fontAssetObj, mainAsset);

            EditorUtility.SetDirty(mainAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            Debug.Log($"[deploy-probe] TMP_FontAsset 생성 완료: {assetPath}");
            return assetPath;
        }
        catch (Exception e)
        {
            // TMP 버전별 API 차이로 인한 실패는 fontStreaming 레버만 스킵하고 나머지는 계속 진행한다
            // (AITFontExternalizer/AITFontSubsetProcessor 와 동일한 관용적 실패 처리 철학).
            // TargetInvocationException 은 메시지가 무의미하므로 inner 를 끝까지 벗겨 실제 원인을 남긴다.
            Exception root = e;
            while (root is TargetInvocationException tie && tie.InnerException != null)
            {
                root = tie.InnerException;
            }
            Debug.LogWarning("[deploy-probe] TMP_FontAsset 생성 예외(fontStreaming 레버 스킵, 나머지 레버는 계속): " +
                $"{root.GetType().Name}: {root.Message}\n{root.StackTrace}");
            return null;
        }
    }

    /// <summary>TMP Essential Resources 를 1회 임포트(headless 안전, CI 결정성). 이미 임포트됐으면 멱등 skip.</summary>
    private static void EnsureTmpEssentialResources(Type fontAssetType)
    {
        try
        {
            const string marker = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(marker) != null)
            {
                return; // 이미 임포트됨.
            }

            var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(fontAssetType.Assembly);
            if (pkg == null || string.IsNullOrEmpty(pkg.resolvedPath))
            {
                Debug.LogWarning("[deploy-probe] TMP 패키지 경로 해석 실패 — Essential Resources 임포트를 건너뜁니다.");
                return;
            }

            string unityPackagePath = Path.Combine(pkg.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
            if (!File.Exists(unityPackagePath))
            {
                Debug.LogWarning($"[deploy-probe] TMP Essential Resources.unitypackage 없음: {unityPackagePath} — 건너뜁니다.");
                return;
            }

            // ImportPackage(비대화식)는 배치 모드에서도 비동기로 남아 후속 Shader.Find 가 임포트 완료 전에
            // null 을 본다(Refresh 로도 unitypackage 임포트는 플러시되지 않음 — 2022.3 실측). Unity 내부의
            // 동기 API ImportPackageImmediately 를 리플렉션으로 우선 시도하고, 없으면 비동기+Refresh 폴백.
            var importImmediately = typeof(AssetDatabase).GetMethod(
                "ImportPackageImmediately",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new Type[] { typeof(string) },
                null);
            if (importImmediately != null)
            {
                importImmediately.Invoke(null, new object[] { unityPackagePath });
            }
            else
            {
                AssetDatabase.ImportPackage(unityPackagePath, false); // false = 다이얼로그 없이(headless 안전).
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[deploy-probe] TMP Essential Resources 임포트 완료 (동기 API: {importImmediately != null}).");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[deploy-probe] TMP Essential Resources 임포트 예외(무시, ProbeFontAsset 은 계속 사용 가능): {e.Message}");
        }
    }

    private static void TryAddSubAsset(Type fontAssetType, object fontAssetObj, UnityEngine.Object mainAsset, string propertyName)
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

    private static void TryAddAtlasTextures(Type fontAssetType, object fontAssetObj, UnityEngine.Object mainAsset)
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

    // ---- 오디오: ~6초 스테레오 44.1kHz PCM16 WAV (audioStreaming/audioReencode/audioStreamTranscode) ----
    private static string GenerateProbeAudio()
    {
        const int sampleRate = 44100;
        const int channels = 2;
        int seconds = GetEnvInt("AIT_DEPLOY_PROBE_AUDIO_SECONDS", 6);
        int frames = sampleRate * seconds;
        byte[] wav = BuildWav(frames, channels, sampleRate);

        string assetPath = $"{ProbeRoot}/Audio/probe_audio.wav";
        File.WriteAllBytes(assetPath, wav);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

        var importer = (AudioImporter)AssetImporter.GetAtPath(assetPath);
        if (importer == null)
            throw new Exception(
                $"[deploy-probe] AudioImporter 가 null: {assetPath} (ForceSynchronousImport 후에도 임포트 안 됨 — " +
                "StartAssetEditing 배치로 감싸면 임포트가 지연되어 이 NRE 가 난다)");

        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad; // 오디오 스트리밍 외부화 대상.
        settings.compressionFormat = AudioCompressionFormat.PCM; // PCM(raw) ≈1411kbps → audioStreamTranscode 의
                                                                  // 최소 유효 비트레이트(256kbps) 조건을 여유 있게 충족.
        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();

        return assetPath;
    }

    // ---- WAV(RIFF/PCM 16-bit) 바이트 생성 (HeavyBuildRunner.BuildWav 와 동일 패턴) ----
    private static byte[] BuildWav(int frames, int channels, int sampleRate)
    {
        int bitsPerSample = 16;
        int blockAlign = channels * bitsPerSample / 8;
        int dataBytes = frames * blockAlign;
        int byteRate = sampleRate * blockAlign;

        using (var ms = new MemoryStream(44 + dataBytes))
        using (var w = new BinaryWriter(ms))
        {
            w.Write(new char[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + dataBytes);
            w.Write(new char[] { 'W', 'A', 'V', 'E' });
            w.Write(new char[] { 'f', 'm', 't', ' ' });
            w.Write(16);                 // PCM fmt chunk size
            w.Write((short)1);           // PCM
            w.Write((short)channels);
            w.Write(sampleRate);
            w.Write(byteRate);
            w.Write((short)blockAlign);
            w.Write((short)bitsPerSample);
            w.Write(new char[] { 'd', 'a', 't', 'a' });
            w.Write(dataBytes);

            uint state = 0xC2B2AE35u ^ 0x27D4EB2Fu; // 프로브 전용 고정 시드(결정론).
            double phase = 0.0;
            const double freq = 330.0;
            double phaseInc = 2.0 * Math.PI * freq / sampleRate;
            for (int f = 0; f < frames; f++)
            {
                state = NextLcg(state);
                double noise = (((state >> 12) & 0xFFFF) / 65535.0 - 0.5) * 0.2;
                double s = Math.Sin(phase) * 0.6 + noise;
                phase += phaseInc;
                short sample = (short)Mathf.Clamp((float)(s * short.MaxValue), short.MinValue, short.MaxValue);
                for (int c = 0; c < channels; c++) w.Write(sample);
            }
            w.Flush();
            return ms.ToArray();
        }
    }

    // ---- 씬 배선 ----
    private static void BuildProbeScene(string texPath, string fontRawPath, string fontAssetPath, string audioPath)
    {
        // 결정론 보장: 매 빌드 전 기존 프로브 씬을 지우고 새로 만든다.
        if (File.Exists(ScenePath))
        {
            File.Delete(ScenePath);
            string metaPath = ScenePath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
            AssetDatabase.Refresh();
        }

        // 비부트 씬(index 1) — Camera/Light 불필요, 런타임 로드 목적이 아니라 .ait 수락 검증용
        // (EmptyScene 은 E2EBuildRunner 가 Unity 6 DefaultGameObjects 직렬화 문제 회피에도 쓰는
        //  안전한 선택 — 씬 데이터 손상 리스크가 없다).
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Canvas ──
        var canvasGo = new GameObject("ProbeCanvas", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        // ── RawImage(텍스처 스트리밍/다운스케일/재압축/JPEG 전환 레버 대상) ──
        var rawImageGo = new GameObject("ProbeRawImage", typeof(RectTransform));
        rawImageGo.transform.SetParent(canvasGo.transform, false);
        var rawImage = rawImageGo.AddComponent<RawImage>();
        rawImage.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        var rawImageRt = rawImageGo.GetComponent<RectTransform>();
        rawImageRt.sizeDelta = new Vector2(512, 512);
        rawImageRt.anchoredPosition = Vector2.zero;

        // ── 텍스트(fontSubset 은 항상 대상, TMP 설치 시 fontStreaming 도 대상) ──
        var textGo = new GameObject("ProbeText", typeof(RectTransform));
        textGo.transform.SetParent(canvasGo.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.sizeDelta = new Vector2(600, 200);
        textRt.anchoredPosition = new Vector2(0, -300);

        Component textComponent = string.IsNullOrEmpty(fontAssetPath) ? null : TryAddTmpText(textGo, fontAssetPath);
        if (textComponent == null)
        {
            // TMP 미설치(또는 TMP_FontAsset 생성 실패) — 레거시 UI Text + 원본 .otf 로 폴백.
            // 원본 .otf 는 이 비부트 씬(index 1)의 의존성이 되어 fontSubset 자동 탐지 대상에는
            // 여전히 포함된다(fontStreaming 만 TMP_FontAsset 부재로 스킵됨).
            var legacyText = textGo.AddComponent<Text>();
            legacyText.font = AssetDatabase.LoadAssetAtPath<Font>(fontRawPath);
            legacyText.fontSize = 32;
            legacyText.color = Color.white;
            legacyText.alignment = TextAnchor.MiddleCenter;
            textComponent = legacyText;
        }

        // ── 런타임 한글 텍스트 주입(Runtime MonoBehaviour — TMP/레거시 어느 쪽이든 "text" 프로퍼티로 동작) ──
        var setter = textGo.AddComponent<DeployProbeTextSetter>();
        setter.textComponent = textComponent;

        // ── AudioSource(오디오 스트리밍/재인코딩/트랜스코드 레버 대상) ──
        var audioGo = new GameObject("ProbeAudioSource");
        var audioSource = audioGo.AddComponent<AudioSource>();
        audioSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);
        audioSource.playOnAwake = false;
        audioSource.loop = false;

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"✓ Deploy probe scene saved: {ScenePath}");
    }

    /// <summary>
    /// TMP(TextMeshProUGUI)를 리플렉션으로 부착하고 ProbeFontAsset 을 지정한다.
    /// TMP 미설치/타입 미발견/예외 시 null 반환(상위에서 레거시 Text 로 폴백).
    /// </summary>
    private static Component TryAddTmpText(GameObject go, string fontAssetPath)
    {
        try
        {
            Type tmpUguiType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            Type fontAssetType = Type.GetType("TMPro.TMP_FontAsset, Unity.TextMeshPro");
            if (tmpUguiType == null || fontAssetType == null)
            {
                return null;
            }

            var fontAsset = AssetDatabase.LoadAssetAtPath(fontAssetPath, fontAssetType);
            if (fontAsset == null)
            {
                Debug.LogWarning($"[deploy-probe] ProbeFontAsset 로드 실패: {fontAssetPath} — 레거시 Text 로 폴백.");
                return null;
            }

            var component = go.AddComponent(tmpUguiType) as Component;
            if (component == null)
            {
                return null;
            }

            var fontProp = tmpUguiType.GetProperty("font");
            fontProp?.SetValue(component, fontAsset);

            // 정렬 설정은 TMP 버전별 열거형 타입/이름이 달라(TextAlignmentOptions 등) 실패해도 무해하므로
            // 실패를 흡수하고 진행한다.
            try
            {
                var alignProp = tmpUguiType.GetProperty("alignment");
                if (alignProp != null)
                {
                    object centerValue = Enum.Parse(alignProp.PropertyType, "Center");
                    alignProp.SetValue(component, centerValue);
                }
            }
            catch
            {
                // 무시
            }

            return component;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[deploy-probe] TextMeshProUGUI 부착 예외(레거시 Text 로 폴백): {e.Message}");
            return null;
        }
    }

    // ---- 유틸 ----
    private static uint NextLcg(uint state)
    {
        // numerical recipes LCG — 결정론적, 시드 의존(HeavyBuildRunner 와 동일).
        return state * 1664525u + 1013904223u;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    private static void LogProbeFootprint()
    {
        long total = 0;
        string abs = Path.GetFullPath(ProbeRoot);
        if (Directory.Exists(abs))
        {
            foreach (string file in Directory.GetFiles(abs, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta")) continue;
                total += new FileInfo(file).Length;
            }
        }
        Debug.Log($"[deploy-probe] generated source footprint: {total / (1024.0 * 1024.0):F1} MB on disk " +
                  "(gitignore 대상)");
    }

    private static int GetEnvInt(string name, int defaultValue)
    {
        string value = Environment.GetEnvironmentVariable(name);
        return (!string.IsNullOrEmpty(value) && int.TryParse(value, out int r)) ? r : defaultValue;
    }
}
