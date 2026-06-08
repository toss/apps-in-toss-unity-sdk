using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

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
///   - 절차 생성 메시(normals/tangents/uv2) → L7(optimize mesh data)
///   - NotoSansKR 폰트 사본 N종      → L10(CJK subset) · L12(폰트 deferral)
///   - (L2~L5 = 코드/로더 레버 → 콘텐츠 무관, wasm/loader에 항상 작동)
/// </summary>
public class HeavyBuildRunner
{
    private const string HeavyRoot = "Assets/Resources/HeavyGen";

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
        int audioCount = GetEnvInt("AIT_HEAVY_AUDIO", 2);
        int audioSeconds = GetEnvInt("AIT_HEAVY_AUDIO_SECONDS", 60);
        int meshCount = GetEnvInt("AIT_HEAVY_MESHES", 80);
        int meshGrid = GetEnvInt("AIT_HEAVY_MESH_GRID", 70); // (grid+1)^2 verts ≈ 5041
        int fontCopies = GetEnvInt("AIT_HEAVY_FONT_COPIES", 2);

        Debug.Log($"[heavy] generating: textures={textureCount}@{textureSize}², audio={audioCount}@{audioSeconds}s, " +
                  $"meshes={meshCount}@~{(meshGrid + 1) * (meshGrid + 1)}v, fontCopies={fontCopies}");

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

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < textureCount; i++) GenerateTexture(i, textureSize);
            for (int i = 0; i < audioCount; i++) GenerateAudio(i, audioSeconds);
            for (int i = 0; i < meshCount; i++) GenerateMesh(i, meshGrid);
            CopyFonts(fontCopies);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

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
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad; // L8: 대용량 AudioClip 외부화 대상
        settings.compressionFormat = AudioCompressionFormat.PCM; // PCM → .data 비중 큼(신호 강함)
        importer.defaultSampleSettings = settings;
        importer.preloadAudioData = true;
        importer.SaveAndReimport();
    }

    // ---- 메시: 절차 생성 그리드, normals/tangents/uv2 포함 (L7) ----
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

    // ---- 폰트: NotoSansKR 사본 N종 (L10/L12) ----
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
            if (!AssetDatabase.CopyAsset(srcPath, dst))
            {
                Debug.LogWarning($"[heavy] 폰트 사본 실패: {srcPath} → {dst}");
            }
        }
    }

    private static string FindNotoSansKrPath()
    {
        // 패키지/프로젝트 어디에 있든 AssetDatabase로 탐색.
        foreach (string guid in AssetDatabase.FindAssets("NotoSansKR t:Font"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.EndsWith(".otf") || p.EndsWith(".ttf")) return p;
        }
        // 폴백: 알려진 SharedScripts 패키지 경로.
        string known = "Packages/im.toss.sdk-test-scripts/Runtime/Resources/Fonts/NotoSansKR-Regular.otf";
        return File.Exists(Path.GetFullPath(known)) ? known : null;
    }

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
        long total = 0;
        string abs = Path.GetFullPath(HeavyRoot);
        if (Directory.Exists(abs))
        {
            foreach (string file in Directory.GetFiles(abs, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta")) continue;
                total += new FileInfo(file).Length;
            }
        }
        Debug.Log($"[heavy] generated source footprint: {total / (1024.0 * 1024.0):F1} MB on disk " +
                  $"(빌드 .data 기여는 압축 포맷에 따라 상이; gitignore 대상)");
    }

    private static int GetEnvInt(string name, int defaultValue)
    {
        string value = System.Environment.GetEnvironmentVariable(name);
        return (!string.IsNullOrEmpty(value) && int.TryParse(value, out int r)) ? r : defaultValue;
    }
}
