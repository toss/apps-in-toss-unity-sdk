using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.IO;
using System.Reflection;
using AppsInToss;

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
/// 않는다. 실측으로도 SampleUnityProject-2022.3 의 로컬 Library/PackageCache 에 com.unity.textmeshpro
/// 가 전혀 없음을 확인했다(어떤 샘플 프로젝트 manifest.json 에도 선언돼 있지 않다). 따라서
/// AITFontSubsetProcessor/AITFontExternalizer 와 동일하게 TMP 타입은 전부 리플렉션으로만 접근한다
/// (컴파일 타임 TMPro 참조 없음 — 2022.3/6000.x 모두, TMP 설치 여부와 무관하게 컴파일된다).
/// TMP 가 실제로 설치되지 않은 환경(현재 모든 샘플 프로젝트)에서는 TMP_FontAsset 생성을 건너뛰고
/// 레거시 UnityEngine.UI.Text + 원본 .otf 로 폴백한다(fontSubset 레버는 계속 발화, fontStreaming
/// 레버만 스킵 — GetFontStreamingCandidates 가 t:TMP_FontAsset 만 스캔하기 때문).
/// </summary>
public class DeployProbeBuildRunner
{
    /// <summary>생성 루트(Resources 밖 — 대형 텍스처 외부화가 /Resources/ 를 하드 제외하므로 필수).</summary>
    private const string ProbeRoot = "Assets/DeployProbeGen";

    /// <summary>프로브 씬 경로. E2EBuildRunner 에 env var 로 핸드오프해 index 1 로 추가시킨다.</summary>
    private const string ScenePath = ProbeRoot + "/DeployProbeScene.unity";

    /// <summary>E2EBuildRunner 훅과 공유하는 env var 이름.</summary>
    private const string EnvSceneVar = "AIT_DEPLOY_PROBE_SCENE_PATH";

    [MenuItem("E2E/Build Deploy Probe")]
    public static void BuildDeployProbe()
    {
        Debug.Log("========================================");
        Debug.Log("Deploy Probe Fixture Build");
        Debug.Log("========================================");

        try
        {
            GenerateProbeContent();
        }
        catch (Exception ex)
        {
            // 생성 실패는 빌드 전 단계이므로 CI가 명확히 검출하도록 exit 1 (HeavyBuildRunner 와 동일 규약).
            Debug.LogError("========================================");
            Debug.LogError($"Deploy probe content generation FAILED: {ex}");
            Debug.LogError("========================================");
            EditorApplication.Exit(1);
            return;
        }

        // E2EBuildRunner.BuildWithSDK() 는 매 실행마다 EditorBuildSettings.scenes 를 단일 원소
        // 배열로 덮어써 사전 배선을 전부 지운다(BenchmarkScene 재생성 로직). 프로브 씬을 지우지 않고
        // index 1 로 추가시키려면 env var 훅 외 다른 방법이 없다 — BuildWithSDK 호출 전에 설정한다.
        Environment.SetEnvironmentVariable(EnvSceneVar, ScenePath);

        // 옵트인 레버 명시 활성화. textureStreamJpeg/audioStreamTranscode 는 시각/청취 검증 전까지
        // 기본값이 -1(자동=비활성) 이라, 프로브 빌드에서 발화시키려면 명시적으로 1 을 설정해야 한다.
        // 나머지 레버(fontSubset/fontStreaming/textureStreaming/downscale/recompress/audioStreaming/
        // audioReencode)는 전부 auto-ON(-1) 이라 별도 설정이 필요 없다.
        var config = UnityUtil.GetEditorConf();
        config.textureStreamJpeg = 1;
        config.audioStreamTranscode = 1;
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log("✓ 옵트인 레버 명시 활성화: textureStreamJpeg=1, audioStreamTranscode=1");

        // 검증된 E2E 빌드 파이프라인을 그대로 재사용(씬/SDK 설정/포트 오프셋/산출물 검증/exit code
        // 처리 전부 E2EBuildRunner 소유 — HeavyBuildRunner 와 동일 패턴).
        E2EBuildRunner.BuildWithSDK();
    }

    /// <summary>커맨드라인 진입점(batchmode -executeMethod 용).</summary>
    public static void CommandLineDeployProbeBuild()
    {
        BuildDeployProbe();
    }

    // ─────────────────────────── 콘텐츠 생성 ───────────────────────────

    private static void GenerateProbeContent()
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
