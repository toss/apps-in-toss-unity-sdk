using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using AppsInToss;

/// <summary>
/// 로딩 성능 실측(perf) 하네스용 빌드 진입점.
///
/// 일반 E2E 픽스처(<see cref="E2EBuildRunner"/>)는 의도적으로 가벼워(유의미 에셋 = 16MB 폰트 1종)
/// WebGL 로드타임 최적화 레버들의 효과(Δ)가 측정 노이즈에 묻힌다. 이 러너는 빌드 시점에
/// 무거운 콘텐츠(고해상 텍스처·대용량 오디오·다수 메시·폰트 사본)를 결정론적으로 생성해
/// <c>Assets/Resources/HeavyGen/</c>(gitignore 대상)에 넣은 뒤, 검증된 <see cref="E2EBuildRunner.BuildWithSDK"/>
/// 파이프라인을 그대로 호출한다. Resources/ 폴더는 씬 참조와 무관하게 전량 빌드에 포함되므로,
/// 생성 콘텐츠는 .data 를 키워(→ 첫 프레임 지연 증가) 각 레버의 Δ가 측정 가능해진다.
///
/// 커밋되는 바이너리는 없다(전부 빌드 시 생성·gitignore). 콘텐츠는 LCG 시드 기반으로 완전 결정론적이라
/// 동일 입력에서 동일 산출물을 보장한다(System.Random / DateTime 미사용).
///
/// 생성 콘텐츠 ↔ 작동 레버 매핑:
///   - mipmap + DXT5 2048² 텍스처  → L6(mip strip) · L9(crunch, DXT 전용) · L11(대형 텍스처 외부화)
///   - DecompressOnLoad PCM 오디오  → L8(오디오 스트리밍)
///   - 절차 생성 메시(normals/tangents/uv2, .asset, Resources 안) → L7(optimize mesh data)
///   - 절차 생성 OBJ 메시(ModelImporter 경로, Assets/HeavyGen/ObjMeshes, Resources 밖)
///       + HeavyGenGallery 씬(전체 메시를 MeshFilter/MeshRenderer 로 렌더러 연결)
///       → stripUnusedMeshComponents(채널 스트리핑) 판정을 실제 렌더러 사용 여부 기준으로 실사용화하고,
///         씬 참조 자산이 sharedassets 로 패킹되는 실게임 경로를 만든다.
///   - NotoSansKR 폰트 사본 N종      → L10(CJK subset) · L12(폰트 deferral)
///   - (L2~L5 = 코드/로더 레버 → 콘텐츠 무관, wasm/loader에 항상 작동)
/// </summary>
public class HeavyBuildRunner
{
    private const string HeavyRoot = "Assets/Resources/HeavyGen";

    /// <summary>OBJ 메시 + 갤러리 씬 생성 루트(Resources 밖 — 대형 텍스처 외부화와 동일 사유로,
    /// 이 콘텐츠는 "씬 참조로만 빌드에 포함되는" 경로를 검증해야 하므로 Resources 강제 포함을 피한다).</summary>
    private const string HeavyGenRoot = "Assets/HeavyGen";
    private const string ObjMeshRoot = HeavyGenRoot + "/ObjMeshes";
    private const string GalleryScenePath = HeavyGenRoot + "/HeavyGenGallery.unity";

    /// <summary>
    /// E2EBuildRunner.BuildWithSDK() 가 제공하는 유일한 "부트 씬(scenes[0]) 뒤에 씬 1개를 추가"
    /// 훅(env var 이름은 DeployProbeBuildRunner 가 먼저 도입한 것을 그대로 재사용한다). 이 작업의
    /// 수정 허용 파일은 HeavyBuildRunner.cs 하나뿐이라 E2EBuildRunner.cs 에 전용 훅을 새로 만들 수
    /// 없다 — E2EBuildRunner.cs 73행에서 EditorBuildSettings.scenes 를 단일 원소로 덮어쓴 직후,
    /// 76~87행이 정확히 이 env var 를 읽어 index 1 로 append 한다(DeployProbeBuildRunner.cs 257-260행
    /// 참조, 동일 문제·동일 해법).
    ///
    /// IPreprocessBuildWithReport 로 빌드 직전에 EditorBuildSettings.scenes 를 바꾸는 방법은 이미
    /// 늦다는 점에 주의: AITWebGLBuilder.BuildWebGL() 이 BuildPipeline.BuildPlayer 호출 "전"에
    /// UnityUtil.GetBuildScenes() 로 씬 목록을 지역 변수(string[])에 스냅샷해 BuildPlayerOptions.scenes
    /// 에 고정하므로, IPreprocessBuildWithReport 콜백(BuildPipeline.BuildPlayer 내부에서 발화)에서
    /// EditorBuildSettings.scenes 를 바꿔도 이미 고정된 배열에는 반영되지 않는다. 따라서 DoExport 가
    /// 호출되기 전(E2EBuildRunner.BuildWithSDK 73행 직후)에 env var 로 값을 넘기는 이 경로가 유일하다.
    ///
    /// 참고(메인 세션 검토 포인트): env var 이름이 "DEPLOY_PROBE" 로 고정돼 있어 Heavy 픽스처 재사용은
    /// 의미상 부정확하다 — 두 픽스처가 같은 프로세스에서 순차 실행되지 않는 한(현재 CI 구조상 별도
    /// MenuItem/커맨드라인 진입점이라 발생하지 않음) 충돌은 없지만, 이 훅을 "AIT_EXTRA_SCENE_PATH" 등
    /// 픽스처 중립적인 이름으로 일반화하는 편이 장기적으로 안전하다(E2EBuildRunner.cs 수정 필요 — 이번
    /// 작업 범위 밖).
    /// </summary>
    private const string ExtraSceneEnvVar = "AIT_DEPLOY_PROBE_SCENE_PATH";

    [MenuItem("E2E/Build Heavy (perf fixture)")]
    public static void BuildHeavy()
    {
        Debug.Log("========================================");
        Debug.Log("Heavy Perf Fixture Build");
        Debug.Log("========================================");

        try
        {
            GenerateHeavyContent();
        }
        catch (System.Exception ex)
        {
            // 생성 실패는 빌드 전 단계이므로 CI가 명확히 검출하도록 exit 1.
            // sentryCapture 대상 아님(테스트 하네스 내부 오류).
            Debug.LogError("========================================");
            Debug.LogError($"Heavy content generation FAILED: {ex}");
            Debug.LogError("========================================");
            EditorApplication.Exit(1);
            return;
        }

        // perf full/fullmesh posture: 기본 자동 모드에선 꺼져 있는 opt-in 레버를 명시 활성화한다
        // (fontSubset 은 언어 선택 게이트, audioStreamTranscode/textureStreamJpeg 는 opt-in 기본 OFF).
        // fullmesh 는 full 에 meshCompression=1 만 더한 것 — 같은 픽스처에서 full↔fullmesh A/B 로
        // Mesh 압축 레버 단독 효과를 격리 측정하기 위한 posture 다.
        // dispatch 의 posture 입력이 unity-build.yml → AIT_PERF_POSTURE 로 전파된 것.
        var posture = System.Environment.GetEnvironmentVariable("AIT_PERF_POSTURE");
        if (posture == "full" || posture == "fullmesh")
        {
            var config = UnityUtil.GetEditorConf();
            config.fontSubsetLanguages = "ko";   // 자동 모드 언어 선택 게이트 통과 → 부팅 subset 발화
            config.audioStreamTranscode = 1;     // opt-in
            config.textureStreamJpeg = 1;        // opt-in
            string levers = "fontSubsetLanguages=ko, audioStreamTranscode=1, textureStreamJpeg=1";
            if (posture == "fullmesh")
            {
                config.meshCompression = 1;      // opt-in (Mesh 압축 레버 A/B 측정용)
                levers += ", meshCompression=1";
            }
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log($"[heavy] {posture} posture 적용: {levers}");
        }

        // 갤러리 씬(index 1)을 EditorBuildSettings.scenes 에 추가 예약 — ExtraSceneEnvVar 문서 참조.
        // GenerateHeavyContent() 가 이미 씬 저장 성공을 확인했지만(BuildGalleryScene 내부 검증),
        // 이 시점에 다시 한번 존재를 확인해 "씬 누락 → OBJ 세트 전량 누락"을 확실히 차단한다.
        if (!File.Exists(GalleryScenePath))
        {
            Debug.LogError("========================================");
            Debug.LogError($"[heavy] 갤러리 씬을 찾을 수 없어 OBJ 메시 세트가 빌드에서 누락됩니다: {GalleryScenePath}");
            Debug.LogError("========================================");
            EditorApplication.Exit(1);
            return;
        }
        System.Environment.SetEnvironmentVariable(ExtraSceneEnvVar, GalleryScenePath);
        Debug.Log($"[heavy] 갤러리 씬을 scenes[1] 로 추가 예약: {GalleryScenePath} (E2EBuildRunner 훅 재사용)");

        // 생성 콘텐츠가 임포트된 상태에서 검증된 E2E 빌드 파이프라인을 그대로 재사용.
        // (씬/SDK 설정/포트 오프셋/산출물 검증/exit code 처리 전부 E2EBuildRunner 소유)
        E2EBuildRunner.BuildWithSDK();
    }

    /// <summary>커맨드라인 진입점 (perf CI / run-local-tests.sh --heavy 에서 호출).</summary>
    public static void CommandLineHeavyBuild()
    {
        BuildHeavy();
    }

    private static void GenerateHeavyContent()
    {
        // 콘텐츠 규모는 환경변수로 조절 가능(로컬 빠른 검증 시 축소). 기본값은 perf 측정용.
        int textureCount = GetEnvInt("AIT_HEAVY_TEXTURES", 6);
        int textureSize = GetEnvInt("AIT_HEAVY_TEXTURE_SIZE", 2048);
        // L9(crunch) 측정용 압축성 텍스처. 노이즈 텍스처(GenerateTexture)는 DXT 블록이 전부
        // 달라 crunch 가 거의 무력(≈0 Δ)하므로, 저주파 콘텐츠 텍스처를 별도로 추가한다.
        int crunchTextureCount = GetEnvInt("AIT_HEAVY_CRUNCH_TEXTURES", 4);
        int audioCount = GetEnvInt("AIT_HEAVY_AUDIO", 2);
        int audioSeconds = GetEnvInt("AIT_HEAVY_AUDIO_SECONDS", 60);
        int meshCount = GetEnvInt("AIT_HEAVY_MESHES", 80);
        int meshGrid = GetEnvInt("AIT_HEAVY_MESH_GRID", 70); // (grid+1)^2 verts ≈ 5041
        int fontCopies = GetEnvInt("AIT_HEAVY_FONT_COPIES", 2);
        // OBJ 메시 세트(ModelImporter 경로, Resources 밖 — 갤러리 씬 참조로만 빌드에 포함).
        int objMeshCount = GetEnvInt("AIT_HEAVY_OBJ_MESHES", 20);
        int objMeshGrid = GetEnvInt("AIT_HEAVY_OBJ_MESH_GRID", 50); // (grid+1)^2 verts ≈ 2601, 개당 ~수백KB

        Debug.Log($"[heavy] generating: textures={textureCount}(+{crunchTextureCount} 압축성)@{textureSize}², " +
                  $"audio={audioCount}@{audioSeconds}s, meshes={meshCount}@~{(meshGrid + 1) * (meshGrid + 1)}v, " +
                  $"objMeshes={objMeshCount}@~{(objMeshGrid + 1) * (objMeshGrid + 1)}v, fontCopies={fontCopies}");

        // 결정론 보장: 매 빌드 전 생성 루트를 비우고 새로 만든다.
        if (AssetDatabase.IsValidFolder(HeavyRoot))
        {
            AssetDatabase.DeleteAsset(HeavyRoot);
        }
        EnsureFolder(HeavyRoot);
        EnsureFolder(HeavyRoot + "/Textures");
        EnsureFolder(HeavyRoot + "/Audio");
        EnsureFolder(HeavyRoot + "/Meshes");
        EnsureFolder(HeavyRoot + "/Fonts");

        // OBJ/갤러리 씬 루트도 동일 규약으로 비우고 새로 만든다(재실행 안전 — CI 영속 워크스페이스에서
        // 중복 누적 금지).
        if (AssetDatabase.IsValidFolder(HeavyGenRoot))
        {
            AssetDatabase.DeleteAsset(HeavyGenRoot);
        }
        EnsureFolder(HeavyGenRoot);
        EnsureFolder(ObjMeshRoot);

        // 주의: 이 루프를 StartAssetEditing/StopAssetEditing 배치로 감싸면 안 된다.
        // 배치 중에는 AssetDatabase 임포트가 지연되어, GenerateTexture/GenerateAudio 내부의
        // AssetImporter.GetAtPath()가 (아직 임포트 전이라) null 을 반환하고 importer.* 접근에서
        // NullReferenceException 이 난다. 각 Generate* 는 "파일 기록 → ForceSynchronousImport →
        // 임포터 설정 → SaveAndReimport" 를 자기 완결적으로 수행하므로 배치 없이 순차 호출한다.
        for (int i = 0; i < textureCount; i++) GenerateTexture(i, textureSize);
        for (int i = 0; i < crunchTextureCount; i++) GenerateCompressibleTexture(i, textureSize);
        for (int i = 0; i < audioCount; i++) GenerateAudio(i, audioSeconds);
        for (int i = 0; i < meshCount; i++) GenerateMesh(i, meshGrid);
        CopyFonts(fontCopies);

        // OBJ 메시 세트 생성(ModelImporter 경로) → 갤러리 씬에서 참조할 Mesh 목록을 확보.
        var objMeshes = new List<Mesh>(objMeshCount);
        for (int i = 0; i < objMeshCount; i++)
        {
            objMeshes.Add(GenerateObjMesh(i, objMeshGrid));
        }

        // 렌더러 연결 씬: 기존 .asset 메시 80개 전부 + 신규 OBJ 메시 전부를 배치.
        // 이 씬이 EditorBuildSettings.scenes 에 추가돼야(BuildHeavy() 의 ExtraSceneEnvVar 경로)
        // OBJ 메시(Resources 밖)가 실제로 빌드에 포함된다.
        BuildGalleryScene(meshCount, objMeshes);

        // L6(mip stripping) 측정 가능화: 모든 Quality 레벨의 텍스처 mip 제한을 설정한다.
        // 이 설정 단독으로는 .data 가 변하지 않는다(mipStripping=false 면 mip 전량 빌드 포함 →
        // baseline on-wire 불변). L6 레버(PlayerSettings.mipStripping=true)가 켜졌을 때 비로소
        // "어떤 Quality 레벨도 안 쓰는 최상위 mip"으로 분류되어 빌드에서 제거된다.
        ApplyMipLimitToAllQualityLevels(GetEnvInt("AIT_HEAVY_MIP_LIMIT", 1));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        LogHeavyFootprint();
    }

    // ---- 텍스처: mipmap + WebGL DXT5 (L6/L9/L11) ----
    private static void GenerateTexture(int index, int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        // 비압축성(노이즈) 픽셀 → gzip on-wire 신호 강함. 인덱스 시드로 결정론적.
        uint state = 0x9E3779B9u ^ (uint)(index * 0x85EBCA77u + 1u);
        for (int p = 0; p < pixels.Length; p++)
        {
            state = NextLcg(state);
            byte r = (byte)(state >> 24);
            byte g = (byte)(state >> 16);
            byte b = (byte)(state >> 8);
            pixels[p] = new Color32(r, g, b, 255);
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        string assetPath = $"{HeavyRoot}/Textures/heavy_tex_{index:D2}.png";
        File.WriteAllBytes(assetPath, png);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

        var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        if (importer == null)
            throw new System.Exception(
                $"[heavy] TextureImporter 가 null: {assetPath} (ForceSynchronousImport 후에도 임포트 안 됨 — " +
                "StartAssetEditing 배치로 감싸면 임포트가 지연되어 이 NRE 가 난다)");
        importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = true;            // L6 (mip stripping) 대상
        importer.isReadable = false;
        importer.textureCompression = TextureImporterCompression.Compressed;
        // WebGL 플랫폼 오버라이드로 DXT5 강제 → L9(crunch, DXT 전용)·L11(대형 텍스처) 대상.
        // ASTC면 crunch 레버가 작동하지 않으므로 명시적으로 DXT5 고정.
        var ps = new TextureImporterPlatformSettings
        {
            name = "WebGL",
            overridden = true,
            maxTextureSize = size,
            format = TextureImporterFormat.DXT5,
            textureCompression = TextureImporterCompression.Compressed,
            crunchedCompression = false,          // 베이스라인: 비-crunch (L9이 켜는 레버)
        };
        importer.SetPlatformTextureSettings(ps);
        importer.SaveAndReimport();
    }

    // ---- 압축성 텍스처: 저주파 타일 그라디언트 → DXT 블록 중복↑ → L9(crunch) 측정 가능화 ----
    // GenerateTexture 의 노이즈는 DXT 4×4 블록이 전부 달라 crunch(블록 스트림 재압축)가 거의
    // 무력하다(≈0 Δ). crunch 가 유의미한 절감을 내려면 인접·반복 블록이 중복돼야 하므로,
    // 큰 타일 단위의 부드러운 그라디언트(저주파)로 생성한다. 인덱스 시드로 완전 결정론적.
    // 임포트 설정은 노이즈 텍스처와 동일(DXT5 오버라이드 + 비-crunch 베이스라인)하게 두어,
    // L9 레버가 이 오버라이드를 DXT5→DXT5Crunched 로 in-place 갱신할 때만 크기가 줄도록 한다.
    private static void GenerateCompressibleTexture(int index, int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];

        // 타일 베이스 색을 좁은 범위(저채도)로 미리 결정 → 타일 간 색 유사 → 블록 중복↑.
        const int tile = 128;                          // 128² 타일 = 32×32 DXT 블록의 반복 단위
        int tilesPerRow = (size + tile - 1) / tile;
        var tileColors = new Color32[tilesPerRow * tilesPerRow];
        uint state = 0x6C078965u ^ (uint)(index * 0x9E3779B9u + 1u);
        for (int t = 0; t < tileColors.Length; t++)
        {
            state = NextLcg(state);
            // 64..127 의 좁은 대역 → 타일 간 차이가 작아 crunch 친화적.
            byte r = (byte)(64 + ((state >> 24) & 0x3F));
            byte g = (byte)(64 + ((state >> 16) & 0x3F));
            byte b = (byte)(64 + ((state >> 8) & 0x3F));
            tileColors[t] = new Color32(r, g, b, 255);
        }

        for (int y = 0; y < size; y++)
        {
            int ty = y / tile;
            float gy = (float)(y % tile) / tile;       // 타일 내 세로 위치 0..1
            for (int x = 0; x < size; x++)
            {
                int tx = x / tile;
                float gx = (float)(x % tile) / tile;   // 타일 내 가로 위치 0..1
                var c = tileColors[ty * tilesPerRow + tx];
                // 타일 내부는 0.75..1.0 의 완만한 명도 그라디언트(저주파) → DXT 후 crunch 친화.
                float shade = 0.75f + 0.125f * (gx + gy);
                byte r = (byte)Mathf.Clamp(c.r * shade, 0f, 255f);
                byte g = (byte)Mathf.Clamp(c.g * shade, 0f, 255f);
                byte b = (byte)Mathf.Clamp(c.b * shade, 0f, 255f);
                pixels[y * size + x] = new Color32(r, g, b, 255);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        string assetPath = $"{HeavyRoot}/Textures/heavy_ctex_{index:D2}.png";
        File.WriteAllBytes(assetPath, png);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

        var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        if (importer == null)
            throw new System.Exception(
                $"[heavy] TextureImporter 가 null: {assetPath} (ForceSynchronousImport 후에도 임포트 안 됨 — " +
                "StartAssetEditing 배치로 감싸면 임포트가 지연되어 이 NRE 가 난다)");
        importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = true;            // L6(mip stripping) 대상에 동일 포함
        importer.isReadable = false;
        importer.textureCompression = TextureImporterCompression.Compressed;
        var ps = new TextureImporterPlatformSettings
        {
            name = "WebGL",
            overridden = true,
            maxTextureSize = size,
            format = TextureImporterFormat.DXT5,
            textureCompression = TextureImporterCompression.Compressed,
            crunchedCompression = false,          // 베이스라인: 비-crunch (L9이 켜는 레버)
        };
        importer.SetPlatformTextureSettings(ps);
        importer.SaveAndReimport();
    }

    // ---- 오디오: DecompressOnLoad PCM (L8) ----
    private static void GenerateAudio(int index, int seconds)
    {
        const int sampleRate = 44100;
        const int channels = 2;
        int frames = sampleRate * seconds;
        // 16-bit PCM WAV. sine 스윕 + 약한 노이즈 → .data 내 비중 확보(L8 외부화 대상).
        byte[] wav = BuildWav(frames, channels, sampleRate, index);

        string assetPath = $"{HeavyRoot}/Audio/heavy_audio_{index:D2}.wav";
        File.WriteAllBytes(assetPath, wav);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

        var importer = (AudioImporter)AssetImporter.GetAtPath(assetPath);
        if (importer == null)
            throw new System.Exception(
                $"[heavy] AudioImporter 가 null: {assetPath} (ForceSynchronousImport 후에도 임포트 안 됨 — " +
                "StartAssetEditing 배치로 감싸면 임포트가 지연되어 이 NRE 가 난다)");
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad; // L8: 대용량 AudioClip 외부화 대상
        settings.compressionFormat = AudioCompressionFormat.PCM; // PCM → .data 비중 큼(신호 강함)
        importer.defaultSampleSettings = settings;
        // preloadAudioData 는 명시 설정하지 않는다(임포트 기본값이 true). Unity 6 에서
        // AudioImporter.preloadAudioData 는 obsolete-as-error(CS0619)로 빌드를 깨고
        // (per-platform SampleSettings 로 이전됨), 2021.3 에는 그 대체 필드가 없어 버전 간
        // 통일이 불가하다. L8 측정 대상인 .data 비중은 loadType/compressionFormat 이 결정하므로
        // preload 명시는 불필요하다.
        importer.SaveAndReimport();
    }

    // ---- 메시: 절차 생성 그리드, normals/tangents/uv2 포함 (L7, SetMeshCompression 경로) ----
    // Resources/ 안(.asset)이라 씬 연결 없이도 빌드에 강제 포함되지만, 렌더러 미연결이면
    // stripUnusedMeshComponents 의 "채널 사용" 판정이 신뢰 불가하다(레버가 항상 안전하게 보수적으로
    // 판단해 아무 채널도 못 지움). 아래 BuildGalleryScene 이 이 메시 80개 전부를 실제
    // MeshFilter/MeshRenderer 로 씬에 연결해 채널 스트리핑 판정을 실사용화한다 — uv2 는 그 판정을
    // 검증하는 "미사용 채널" 신호로 남긴다(씬에서도 실제로 읽히지 않는 채널).
    private static void GenerateMesh(int index, int grid)
    {
        int dim = grid + 1;
        int vcount = dim * dim;
        var verts = new Vector3[vcount];
        var normals = new Vector3[vcount];
        var uv = new Vector2[vcount];
        var uv2 = new Vector2[vcount];
        var tangents = new Vector4[vcount];

        uint state = 0x27D4EB2Fu ^ (uint)(index * 0x165667B1u + 1u);
        for (int y = 0; y <= grid; y++)
        {
            for (int x = 0; x <= grid; x++)
            {
                int vi = y * dim + x;
                state = NextLcg(state);
                float h = ((state >> 8) & 0xFFFF) / 65535f; // 결정론적 높이 노이즈
                float fx = (float)x / grid;
                float fy = (float)y / grid;
                verts[vi] = new Vector3(fx - 0.5f, h * 0.2f, fy - 0.5f);
                normals[vi] = Vector3.up;
                uv[vi] = new Vector2(fx, fy);
                uv2[vi] = new Vector2(fy, fx); // L7(optimize mesh data)이 미사용 채널 정리
                tangents[vi] = new Vector4(1, 0, 0, -1);
            }
        }

        var tris = new List<int>(grid * grid * 6);
        for (int y = 0; y < grid; y++)
        {
            for (int x = 0; x < grid; x++)
            {
                int v0 = y * dim + x;
                int v1 = v0 + 1;
                int v2 = v0 + dim;
                int v3 = v2 + 1;
                tris.Add(v0); tris.Add(v2); tris.Add(v1);
                tris.Add(v1); tris.Add(v2); tris.Add(v3);
            }
        }

        var mesh = new Mesh { name = $"heavy_mesh_{index:D2}" };
        if (vcount > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = verts;
        mesh.normals = normals;
        mesh.uv = uv;
        mesh.uv2 = uv2;
        mesh.tangents = tangents;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();

        AssetDatabase.CreateAsset(mesh, $"{HeavyRoot}/Meshes/heavy_mesh_{index:D2}.asset");
    }

    // ---- OBJ 메시: 절차 생성 Wavefront 텍스트, ModelImporter 경로 (L7 확장 대상) ----
    // v/vn/vt/f 만 사용하는 사각 격자 삼각형화(GenerateMesh 와 동일 수식·LCG 스타일 재사용). 법선은
    // GenerateMesh 와 동일하게 전부 위쪽 고정(Vector3.up)이라 vn 라인 1개만 공유 참조한다(파일 크기
    // 절감, 유효한 OBJ). Resources 밖(Assets/HeavyGen/ObjMeshes)이라 BuildGalleryScene 의 씬 참조가
    // 유일한 빌드 포함 경로다. 임포터 기본 설정(압축 Off 등)은 건드리지 않는다 — 레버가 조정할 대상이므로
    // 원본 상태를 유지해야 한다.
    private static Mesh GenerateObjMesh(int index, int grid)
    {
        string assetPath = $"{ObjMeshRoot}/heavy_obj_{index:D2}.obj";
        string text = BuildObjText(index, grid);
        // BOM 없는 UTF-8: 주석의 한글이 깨지지 않으면서도(가독성) OBJ 파서 호환성을 해치지 않는다
        // (v/vn/vt/f 데이터 라인 자체는 CultureInfo.InvariantCulture 로 순수 ASCII 숫자만 사용).
        File.WriteAllText(assetPath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

        var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
            throw new System.Exception(
                $"[heavy] ModelImporter 가 null: {assetPath} (OBJ 가 ModelImporter 경로로 임포트되지 않음 — " +
                "이 메시 세트 전체가 빌드에서 누락된다)");

        var mesh = LoadImportedMesh(assetPath);
        if (mesh == null)
            throw new System.Exception($"[heavy] OBJ 임포트 후 Mesh 서브에셋을 찾지 못함: {assetPath}");

        return mesh;
    }

    /// <summary>OBJ(.obj) Wavefront 텍스트를 직접 조립한다(수식 기반 완전 결정론, 로케일 무관).</summary>
    private static string BuildObjText(int index, int grid)
    {
        int dim = grid + 1;
        var sb = new StringBuilder(dim * dim * 48 + grid * grid * 2 * 40);
        sb.Append("# heavy_obj_").Append(index.ToString("D2")).Append(" - 절차 생성 grid=")
          .Append(grid).Append(" (LCG 결정론)\n");
        sb.Append("vn 0.0000 1.0000 0.0000\n"); // 공유 단일 노멀(GenerateMesh 와 동일하게 전부 위쪽 고정)

        // GenerateMesh 와 다른 상수(+7 오프셋)로 시드를 분리해 asset 메시와 값이 겹치지 않게 한다.
        uint state = 0x27D4EB2Fu ^ (uint)(index * 0x165667B1u + 7u);
        for (int y = 0; y <= grid; y++)
        {
            for (int x = 0; x <= grid; x++)
            {
                state = NextLcg(state);
                float h = ((state >> 8) & 0xFFFF) / 65535f; // 결정론적 높이 노이즈
                float fx = (float)x / grid;
                float fy = (float)y / grid;
                float vx = fx - 0.5f;
                float vy = h * 0.2f;
                float vz = fy - 0.5f;

                sb.Append("v ")
                  .Append(vx.ToString("F4", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(vy.ToString("F4", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(vz.ToString("F4", CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("vt ")
                  .Append(fx.ToString("F4", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(fy.ToString("F4", CultureInfo.InvariantCulture)).Append('\n');
            }
        }

        for (int y = 0; y < grid; y++)
        {
            for (int x = 0; x < grid; x++)
            {
                int v0 = y * dim + x + 1; // OBJ 인덱스는 1-based
                int v1 = v0 + 1;
                int v2 = v0 + dim;
                int v3 = v2 + 1;
                AppendObjFace(sb, v0, v2, v1);
                AppendObjFace(sb, v1, v2, v3);
            }
        }

        return sb.ToString();
    }

    /// <summary>OBJ 삼각형 face 라인 1개를 추가한다(v/vt 는 정점 인덱스, vn 은 공유 인덱스 1 고정).</summary>
    private static void AppendObjFace(StringBuilder sb, int a, int b, int c)
    {
        sb.Append("f ")
          .Append(a).Append('/').Append(a).Append("/1 ")
          .Append(b).Append('/').Append(b).Append("/1 ")
          .Append(c).Append('/').Append(c).Append("/1\n");
    }

    /// <summary>ModelImporter 로 임포트된 자산에서 Mesh 서브에셋을 찾는다.</summary>
    private static Mesh LoadImportedMesh(string assetPath)
    {
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (obj is Mesh mesh) return mesh;
        }
        return null;
    }

    // ---- 렌더러 연결 씬: 기존 asset 메시 80개 + 신규 OBJ 메시 전부를 배치 ----
    // (a) stripUnusedMeshComponents 의 채널 사용 판정을 실제 렌더러 기준으로 실사용화하고,
    // (b) 씬 참조 자산이 sharedassets 로 패킹되는 실게임 경로를 만든다. 이 씬을 EditorBuildSettings.scenes
    // 에 추가하는 것은 BuildHeavy() 의 책임(ExtraSceneEnvVar 훅) — 여기서는 씬 파일만 만들고 저장 성공을
    // 즉시 검증한다(OBJ 메시가 Resources 밖이라 이 씬이 유일한 빌드 포함 경로이므로 실패를 조용히 넘기면
    // 안 된다).
    private static void BuildGalleryScene(int assetMeshCount, List<Mesh> objMeshes)
    {
        // 결정론 보장: 매 빌드 전 기존 갤러리 씬을 지우고 새로 만든다(HeavyRoot 와 동일 규약).
        if (File.Exists(GalleryScenePath))
        {
            File.Delete(GalleryScenePath);
            string metaPath = GalleryScenePath + ".meta";
            if (File.Exists(metaPath)) File.Delete(metaPath);
            AssetDatabase.Refresh();
        }

        // 비부트 씬(index 1) — Camera/Light 불필요, 런타임 로드 목적이 아니라 메시 렌더러 연결(빌드
        // 포함 + 채널 스트리핑 판정 실사용화)이 목적이다(DeployProbeBuildRunner 의 비부트 씬과 동일 철학).
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 공유 머티리얼 1개: 빌트인 기본 머티리얼(렌더 파이프라인/Unity 버전 무관하게 항상 존재하는
        // GetBuiltinExtraResource 리소스 — 별도 .mat 에셋 생성 불필요).
        var sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");

        int placed = 0;
        for (int i = 0; i < assetMeshCount; i++)
        {
            string meshPath = $"{HeavyRoot}/Meshes/heavy_mesh_{i:D2}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
                throw new System.Exception($"[heavy] 갤러리 씬 배치 실패 — 메시 애셋 로드 불가: {meshPath}");
            PlaceMeshGameObject($"AssetMesh_{i:D2}", mesh, sharedMaterial, placed++);
        }

        for (int i = 0; i < objMeshes.Count; i++)
        {
            if (objMeshes[i] == null)
                throw new System.Exception($"[heavy] 갤러리 씬 배치 실패 — OBJ 메시 index {i} 가 null");
            PlaceMeshGameObject($"ObjMesh_{i:D2}", objMeshes[i], sharedMaterial, placed++);
        }

        EditorSceneManager.SaveScene(scene, GalleryScenePath);

        if (!File.Exists(GalleryScenePath))
        {
            // 씬 추가가 실패하면 OBJ 메시 세트 전체가 빌드에서 누락된다(Resources 밖이라 이 씬 참조가
            // 유일한 포함 경로) — 명확한 예외로 CI가 즉시 검출하게 한다.
            throw new System.Exception($"[heavy] 갤러리 씬 저장 실패: {GalleryScenePath}");
        }

        Debug.Log($"[heavy] gallery scene saved: {GalleryScenePath} " +
                  $"({placed}개 메시 배치: asset {assetMeshCount} + obj {objMeshes.Count})");
    }

    /// <summary>메시 GameObject 1개를 생성해 격자 배치한다(렌더링 결과는 무관 — 씬 참조 확립이 목적).</summary>
    private static void PlaceMeshGameObject(string name, Mesh mesh, Material material, int index)
    {
        var go = new GameObject(name);
        var meshFilter = go.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;
        var meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;

        const int cols = 10;
        const float spacing = 2f;
        go.transform.position = new Vector3((index % cols) * spacing, 0f, (index / cols) * spacing);
    }

    // ---- 폰트: NotoSansKR 사본 N종 (L10/L12) ----
    // 원본이 패키지 비임포트 폴더(Runtime/Fonts~/)로 이동해 더 이상 AssetDatabase 자산이 아니므로
    // (AssetDatabase.CopyAsset 불가) raw File.Copy 후 ImportAsset 으로 대상만 임포트한다.
    private static void CopyFonts(int copies)
    {
        if (copies <= 0) return;

        string srcPath = FindNotoSansKrPath();
        if (string.IsNullOrEmpty(srcPath))
        {
            Debug.LogWarning("[heavy] NotoSansKR 폰트를 찾지 못해 폰트 사본 생성을 건너뜀 (L10/L12 신호 약화).");
            return;
        }

        for (int i = 0; i < copies; i++)
        {
            string dst = $"{HeavyRoot}/Fonts/heavy_font_{i:D2}.otf";
            try
            {
                File.Copy(srcPath, Path.GetFullPath(dst), overwrite: true);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[heavy] 폰트 사본 실패: {srcPath} → {dst} ({e.Message})");
            }
        }
    }

    /// <summary>패키지 비임포트 원본(Runtime/Fonts~/NotoSansKR-Regular.otf)의 실 파일시스템 경로를
    /// 해석한다. UPM/embedded 설치 모두 해석(AITFontSubsetProcessor.ResolveToolSourceDir 와 동일
    /// 관용구: PackageInfo.FindForAssembly resolvedPath 우선 → CallerFilePath 폴백). 폰트가 더 이상
    /// AssetDatabase 로 검색 가능한 임포트 자산이 아니므로 File.Exists 기반으로 확인한다.</summary>
    private static string FindNotoSansKrPath()
    {
        try
        {
            var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(HeavyBuildRunner).Assembly);
            if (pkg != null && !string.IsNullOrEmpty(pkg.resolvedPath))
            {
                string viaPackage = Path.Combine(pkg.resolvedPath, "Runtime", "Fonts~", "NotoSansKR-Regular.otf");
                if (File.Exists(viaPackage)) return viaPackage;
            }
        }
        catch
        {
            // PackageInfo 미해석(Assets 내 임베드 개발) → 소스 파일 위치 폴백.
        }

        string here = CallerDir();
        if (string.IsNullOrEmpty(here)) return null;
        string viaCaller = Path.GetFullPath(Path.Combine(here, "..", "Runtime", "Fonts~", "NotoSansKR-Regular.otf"));
        return File.Exists(viaCaller) ? viaCaller : null;
    }

    private static string CallerDir([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
        => string.IsNullOrEmpty(thisFile) ? null : Path.GetDirectoryName(thisFile);

    // ---- WAV(RIFF/PCM 16-bit) 바이트 생성 ----
    private static byte[] BuildWav(int frames, int channels, int sampleRate, int seed)
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

            uint state = 0xC2B2AE35u ^ (uint)(seed * 0x9E3779B1u + 1u);
            double phase = 0.0;
            double freq = 220.0 + seed * 55.0;
            double phaseInc = 2.0 * System.Math.PI * freq / sampleRate;
            for (int f = 0; f < frames; f++)
            {
                state = NextLcg(state);
                double noise = (((state >> 12) & 0xFFFF) / 65535.0 - 0.5) * 0.2;
                double s = System.Math.Sin(phase) * 0.6 + noise;
                phase += phaseInc;
                short sample = (short)Mathf.Clamp((float)(s * short.MaxValue), short.MinValue, short.MaxValue);
                for (int c = 0; c < channels; c++) w.Write(sample);
            }
            w.Flush();
            return ms.ToArray();
        }
    }

    // ---- 유틸 ----
    private static uint NextLcg(uint state)
    {
        // numerical recipes LCG — 결정론적, 시드 의존.
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

    private static void LogHeavyFootprint()
    {
        long total = SumDirectorySize(HeavyRoot) + SumDirectorySize(HeavyGenRoot);
        Debug.Log($"[heavy] generated source footprint: {total / (1024.0 * 1024.0):F1} MB on disk " +
                  $"(빌드 .data 기여는 압축 포맷에 따라 상이; gitignore 대상)");
    }

    private static long SumDirectorySize(string relativeRoot)
    {
        long total = 0;
        string abs = Path.GetFullPath(relativeRoot);
        if (Directory.Exists(abs))
        {
            foreach (string file in Directory.GetFiles(abs, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta")) continue;
                total += new FileInfo(file).Length;
            }
        }
        return total;
    }

    // ---- Quality mip 제한 (L6 mip stripping 레버 측정 가능화) ----
    // Unity 의 mip stripping(PlayerSettings.mipStripping=true)은 "어떤 Quality 레벨도 필요로 하지
    // 않는 mip"만 빌드에서 제거한다. 기본 Unity Quality 는 모든 레벨이 mip 제한 0(전체 해상도)이라
    // 최상위 mip 이 항상 "사용됨"으로 분류 → mipStripping 플래그가 no-op 이 된다(진단 §9.32).
    // 따라서 픽스처가 모든 레벨에 mip 제한 ≥1 을 설정해야, 레버가 켜졌을 때 mip0 이 "미사용"으로
    // 분류되어 실제로 제거된다(2048² DXT5 의 ~1/4 절감). limit≤0 이면 no-op(설정 비활성).
    //
    // baseline 안전성: 이 설정은 .data 의 on-wire 바이트를 바꾸지 않는다. mipStripping=false 인
    // baseline/타 레버 빌드는 mip 피라미드를 전량 포함하며(런타임 업로드 base 만 mip1 로 이동),
    // 오직 L6 레버 빌드(mipStripping=true)에서만 mip0 이 제거된다.
    private static void ApplyMipLimitToAllQualityLevels(int limit)
    {
        if (limit <= 0)
            return;

        int originalLevel = QualitySettings.GetQualityLevel();
        int levelCount = QualitySettings.names.Length;
        for (int i = 0; i < levelCount; i++)
        {
            QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
#if UNITY_2022_2_OR_NEWER
            QualitySettings.globalTextureMipmapLimit = limit;
#else
            QualitySettings.masterTextureLimit = limit;
#endif
        }
        QualitySettings.SetQualityLevel(originalLevel, applyExpensiveChanges: false);

        Debug.Log($"[heavy] Quality mip limit = {limit} → {levelCount} 레벨 전부 적용 " +
                  "(L6 mip stripping 이 mip0 제거 가능; baseline on-wire 불변)");
    }

    private static int GetEnvInt(string name, int defaultValue)
    {
        string value = System.Environment.GetEnvironmentVariable(name);
        return (!string.IsNullOrEmpty(value) && int.TryParse(value, out int r)) ? r : defaultValue;
    }
}
