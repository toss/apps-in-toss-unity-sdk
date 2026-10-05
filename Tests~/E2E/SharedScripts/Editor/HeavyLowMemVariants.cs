using System.IO;
using UnityEditor;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;

/// <summary>
/// 저메모리 기기 대응 계획(P0-2 / P0-5 / 예외 처리 비용)을 재는 perf 픽스처 변형. 등록 규약은 <see cref="HeavyBuildVariants"/> 참조.
/// perf.yml 의 workflow_dispatch <c>variant</c> 입력(→ AIT_PERF_VARIANT)에 이름을 적어 쓴다.
///
///   tex-stream   : 부팅과 무관한 2048² 스프라이트 4장(장마다 고유 색)을 Resources 밖에 만들고, 그것을 참조하는 Resources 프리팹을 둔다.
///                  textureStreaming=1 로 4장이 외부화(스텁 + StreamingAssets 사본)되고, HeavyTexStreamProbe 가 부팅 직후 프리팹을 인스턴스화해
///                  AITStreamingTexture 의 복원 경로(LoadImage 제자리 복원)와 그 메모리를 실제로 태운다. 기존 heavy 텍스처는 전부 /Resources/ 밑이라
///                  외부화 대상이 0장이어서 이 경로가 측정되지 않았다.
///   tex-stream-gpu : tex-stream 에 AIT_TEXTURE_STREAM_KEEP_GPU_FORMAT=1 을 더한다(원본 ASTC 블록 스트리밍 + LoadRawTextureData 복원 경로 측정).
///   exc-explicit : WebGL 예외 처리를 ExplicitlyThrownExceptionsOnly 로(SDK 기본은 FullWithStacktrace). 코드 크기·성능 비용 비교용.
///                  posture 접미사 "-explicit" 과 같은 효과를 변형으로도 쓸 수 있게 한 것이다.
///   lto600       : 코드 최적화를 DiskSizeLTO 로 강제한다(AIT_WEBGL_CODE_OPTIMIZATION 환경변수를 이 프로세스에 설정). 6000.0 은 기본으로 LTO 를 건너뛰는
///                  버전(링크 OOM 위험)이라 그 게이트를 풀어 재는 변형이다. 다른 버전에서는 이미 기본이라 효과가 같다.
///                  빌드 후 build-validation.json 의 wasmCodeBytes 와 "[AIT-CodeSize]" 로그로 code 섹션 크기를 비교한다.
/// </summary>
public static class HeavyLowMemVariants
{
    /// <summary>tex-stream 텍스처 폴더(Resources 밖 — 외부화 게이트가 /Resources/ 는 제외한다). Assets/HeavyGen 은 gitignore 대상.</summary>
    internal const string TexStreamDir = "Assets/HeavyGen/TexStream";

    /// <summary>tex-stream 프리팹. Resources 안이라 씬 참조 없이도 빌드에 실리고, 런타임 프로브가 Resources.Load 로 꺼낸다.</summary>
    internal const string TexStreamPrefabPath = "Assets/Resources/HeavyTexStream.prefab";

    /// <summary>HeavyTexStreamProbe 가 Resources.Load 하는 이름(확장자 없음). 두 곳이 같아야 한다.</summary>
    internal const string TexStreamResourceName = "HeavyTexStream";

    internal const int TexStreamCount = 4;
    internal const int TexStreamSize = 2048;

    /// <summary>
    /// 노이즈 줄 간격. 이 간격마다 한 줄을 비압축성 노이즈로 채워 PNG 가 외부화 최소 크기(기본 512KB)를 넘게 한다.
    /// 2048² 에서 128줄 × 2048 × RGB 3바이트 ≈ 786KB 가 deflate 로 줄지 않는다. 나머지는 단색이라 PNG 가 작게 유지된다.
    /// </summary>
    internal const int NoiseRowStride = 16;

    [HeavyVariant("tex-stream")]
    public static void ApplyTexStream(AITEditorScriptObject config)
    {
        if (config == null) throw new System.ArgumentNullException(nameof(config));

        // 이전 빌드 잔여물을 지우고 새로 만든다(결정론).
        AssetDatabase.DeleteAsset(TexStreamDir);
        AssetDatabase.DeleteAsset(TexStreamPrefabPath);
        EnsureFolder(TexStreamDir);
        EnsureFolder("Assets/Resources");

        var sprites = new Sprite[TexStreamCount];
        for (int i = 0; i < TexStreamCount; i++)
        {
            string path = $"{TexStreamDir}/texstream_{i:D2}.png";
            Color32[] pixels = BuildTexStreamPixels(i, TexStreamSize);
            var tex = new Texture2D(TexStreamSize, TexStreamSize, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
                throw new System.Exception($"[heavy] TextureImporter 가 null: {path}");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.alphaIsTransparency = false;
            importer.maxTextureSize = TexStreamSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();

            sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprites[i] == null)
                throw new System.Exception($"[heavy] 스프라이트 로드 실패: {path}");
        }

        // 프리팹: 스프라이트 4장을 가까이 둔다. 크기를 줄여(화면에 거의 안 보임) E2E 화면을 가리지 않되 렌더러는 살아 있게 한다.
        var root = new GameObject(TexStreamResourceName);
        for (int i = 0; i < TexStreamCount; i++)
        {
            var child = new GameObject($"TexStream{i:D2}");
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = new Vector3((i - 1.5f) * 0.4f, -3f, 0f);
            child.transform.localScale = Vector3.one * 0.004f;
            child.AddComponent<SpriteRenderer>().sprite = sprites[i];
        }
        PrefabUtility.SaveAsPrefabAsset(root, TexStreamPrefabPath);
        Object.DestroyImmediate(root);

        // 강제 활성: 자동(-1)은 후속 배치에서 "메모리 중립인 경우만" 으로 바뀔 수 있어, 이 변형은 외부화 경로를 반드시 태워야 하므로 명시 1 로 둔다.
        config.textureStreaming = 1;
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[heavy] tex-stream: {TexStreamSize}² 스프라이트 {TexStreamCount}장 + 프리팹({TexStreamPrefabPath}) 생성, textureStreaming=1");
    }

    [HeavyVariant("tex-stream-gpu")]
    public static void ApplyTexStreamGpu(AITEditorScriptObject config)
    {
        if (config == null) throw new System.ArgumentNullException(nameof(config));

        ApplyTexStream(config);
        // 같은 프로세스 안에서 빌드가 돌므로 환경변수가 AITLargeTextureExternalizer.ResolveKeepGpuFormat 입력으로 그대로 전달된다(lto600 과 같은 방식).
        System.Environment.SetEnvironmentVariable(AITTextureStreamPlanner.KeepGpuFormatEnvVar, "1");
        Debug.Log($"[heavy] tex-stream-gpu: tex-stream + {AITTextureStreamPlanner.KeepGpuFormatEnvVar}=1 (원본 ASTC 블록 스트리밍)");
    }

    [HeavyVariant("exc-explicit")]
    public static void ApplyExcExplicit(AITEditorScriptObject config)
    {
        if (config == null) throw new System.ArgumentNullException(nameof(config));

        // SDK 빌드(AITBuildInitializer)는 설정 저장값(1 = ExplicitlyThrownExceptionsOnly)으로 PlayerSettings 를 덮어쓰므로 둘 다 맞춘다.
        config.exceptionSupport = 1;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        Debug.Log("[heavy] exc-explicit: exceptionSupport=ExplicitlyThrownExceptionsOnly");
    }

    [HeavyVariant("lto600")]
    public static void ApplyLto600(AITEditorScriptObject config)
    {
        if (config == null) throw new System.ArgumentNullException(nameof(config));

        // 같은 프로세스 안에서 빌드가 돌므로 환경변수 설정이 AITBuildInitializer 의 ResolveDecision 입력으로 그대로 전달된다.
        System.Environment.SetEnvironmentVariable(AITWebGLCodeOptimization.EnvOverrideKey, AITWebGLCodeOptimization.DiskSizeLTO);
        Debug.Log($"[heavy] lto600: {AITWebGLCodeOptimization.EnvOverrideKey}={AITWebGLCodeOptimization.DiskSizeLTO} " +
                  $"(Unity {Application.unityVersion}, LTO 위험 게이트={AITWebGLCodeOptimization.IsLtoRiskyVersion(Application.unityVersion)} 를 우회)");
    }

    /// <summary>
    /// tex-stream 텍스처 픽셀. 장마다 고유한 베이스 색 + <see cref="NoiseRowStride"/> 줄마다 한 줄의 결정론적 노이즈.
    /// 노이즈 줄은 y % stride == 0 이다(복원 여부를 (0,0) 픽셀과 (0,1) 픽셀이 다른지로 확인할 수 있다).
    /// </summary>
    internal static Color32[] BuildTexStreamPixels(int index, int size)
    {
        Color32 baseColor = TexStreamBaseColor(index);
        var pixels = new Color32[size * size];
        uint state = 0xC2B2AE35u ^ (uint)(index * 0x9E3779B1u + 7u);
        for (int y = 0; y < size; y++)
        {
            bool noisy = y % NoiseRowStride == 0;
            int row = y * size;
            for (int x = 0; x < size; x++)
            {
                if (noisy)
                {
                    state = state * 1664525u + 1013904223u;
                    pixels[row + x] = new Color32((byte)(state >> 24), (byte)(state >> 16), (byte)(state >> 8), 255);
                }
                else
                {
                    pixels[row + x] = baseColor;
                }
            }
        }
        return pixels;
    }

    /// <summary>장마다 서로 다른 베이스 색(4장 이상에서도 중복되지 않도록 인덱스에서 직접 계산).</summary>
    internal static Color32 TexStreamBaseColor(int index)
    {
        byte r = (byte)(32 + (index * 67) % 192);
        byte g = (byte)(48 + (index * 109) % 160);
        byte b = (byte)(64 + (index * 151) % 128);
        return new Color32(r, g, b, 255);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
