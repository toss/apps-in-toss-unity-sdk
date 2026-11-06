using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using AppsInToss;
using AppsInToss.Editor;

/// <summary>
/// 엔진 초기화(callMain)·wasm 크기 단축 후보를 재는 perf 변형. minimal posture 에서도 그대로 적용된다
/// (HeavyBuildRunner 가 모든 posture 에서 BuildWithSDK 직전에 변형을 적용한다). 등록 규약은 <see cref="HeavyBuildVariants"/> 참조.
/// SDK 기본값은 바꾸지 않는다. 프로젝트 설정 파일(GraphicsSettings)을 고치는 변형은 CI 체크아웃이 다음 run 에서 되돌린다.
///
///   gfx-builtin-off  : GraphicsSettings 의 Built-in Shader Settings(Deferred, Deferred Reflections, Screen Space Shadows,
///                      Legacy Deferred, Depth Normals, Motion Vectors, Light Halo, Lens Flare)를 No Support 로, 비디오 셰이더를
///                      Never 로 둔다. 빈 씬 빌드의 unity_builtin_extra 에 이 셰이더들이 실려 부팅 때 역직렬화된다.
///   shaders-trim     : Always Included Shaders 에서 Legacy Shaders/Diffuse(+VertexLit 폴백)와 Hidden/CubeBlur·CubeCopy·CubeBlend 를 뺀다.
///                      Sprites/Default·UI/Default 등 나머지는 남긴다.
///   envrefl-16       : 새로 만드는 씬의 기본 반사 큐브맵 해상도를 128 → 16 으로(RenderSettings.defaultReflectionResolution).
///                      빈 씬에서도 스카이박스 반사 큐브맵(128² × 6면 × 밉, 약 520KB)이 sharedassets0 에 구워져 부팅 때 올라간다.
///                      E2EBuildRunner 가 빌드 직전에 씬을 새로 만들므로 newSceneCreated 훅으로 건다.
///   envrefl-off      : 새로 만드는 씬의 기본 반사를 Custom(텍스처 없음)으로 둬 스카이박스 반사 큐브맵을 굽지 않는다.
///                      envrefl-16 으로 sharedassets0 크기가 그대로였던 것을 확인하는 대조군이다.
///   lz4-off          : productionProfile.enableLZ4Compression=false(BuildOptions.CompressWithLz4 끔). .data 안의 data.unity3d 가
///                      LZ4 로 미리 압축돼 바깥 brotli/gzip 이 거의 못 줄이고(297KB→197KB, 풀어 둔 내용물은 ~139KB),
///                      부팅 때 LZ4 해제가 한 번 더 돈다.
///   symbols-embedded : productionProfile.debugSymbolsExternal=false → WebGLDebugSymbolMode.Embedded. wasm 에 name section 이 들어가
///                      함수 이름 단위로 코드 크기와 부팅 프로파일을 귀속할 수 있다(측정 전용, 전송량은 늘어난다).
///   sdk-keep         : Assets/link.xml 로 AppsInTossSDK·AppsInToss.Helpers 를 통째로 보존한다(Runtime/SDK/link.xml 이 의도한 범위 +
///                      Helpers). minimal 빈 씬에서는 SDK 어셈블리를 참조하는 코드가 없어 IL2CPP 가 두 어셈블리를 통째로 지우고
///                      RuntimeInitializeOnLoad 훅도 돌지 않는다(global-metadata 이미지 목록으로 확인). SDK 를 실제로 쓰는 게임처럼
///                      SDK 코드가 실린 빌드에서 SDK 몫을 재기 위한 변형이다. minimal posture 가 지운 Assets/link.xml 자리에 쓴다.
///   nsjson-trim      : SDK 패키지의 Runtime/SDK/link.xml 에서 Newtonsoft.Json 어셈블리 단위 preserve="all" 만 떼고 타입 단위 보존은
///                      남긴 사본으로 이 빌드 동안만 바꾼다(에디터 종료 시 원복, CI 체크아웃도 되돌린다). 어셈블리 통째 보존이
///                      XmlNodeConverter·DataSet 변환기를 거쳐 System.Xml·System.Data 를 끌어오는지 재는 변형이다. sdk-keep 과 함께 쓴다.
///   codeopt-speed    : WebGL Code Optimization 을 실행 속도 쪽(6000.0: RuntimeSpeed, 6000.x: RuntimeSpeedLTO, 2021/2022: Speed)으로.
///                      SDK 기본(DiskSizeLTO, -Oz 계열)이 엔진 초기화 CPU 를 늘리는지 보는 대조군이다.
/// </summary>
public static class EngineInitVariants
{
    internal static readonly string[] BuiltinShaderSettingProps =
    {
        "m_Deferred", "m_DeferredReflections", "m_ScreenSpaceShadows", "m_LegacyDeferred",
        "m_DepthNormals", "m_MotionVectors", "m_LightHalo", "m_LensFlare",
    };

    internal static readonly HashSet<string> TrimmedAlwaysIncluded = new HashSet<string>
    {
        "Legacy Shaders/Diffuse", "Hidden/CubeBlur", "Hidden/CubeCopy", "Hidden/CubeBlend",
    };

    internal const int ReflectionResolution = 16;

    private static bool _reflectionHookInstalled;

    [HeavyVariant("gfx-builtin-off")]
    public static void ApplyGfxBuiltinOff(AITEditorScriptObject config)
    {
        var so = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
        var changed = new List<string>();
        foreach (string name in BuiltinShaderSettingProps)
        {
            SerializedProperty mode = so.FindProperty(name + ".m_Mode");
            if (mode == null) continue;
            if (mode.intValue != 0) changed.Add($"{name}:{mode.intValue}→0");
            mode.intValue = 0; // BuiltinShaderMode.Disabled (No Support)
        }
        SerializedProperty video = so.FindProperty("m_VideoShadersIncludeMode");
        if (video != null)
        {
            if (video.intValue != 0) changed.Add($"m_VideoShadersIncludeMode:{video.intValue}→0");
            video.intValue = 0; // VideoShadersIncludeMode.Never
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Debug.Log($"[AIT-Variant] gfx-builtin-off: {(changed.Count > 0 ? string.Join(", ", changed) : "변경 없음")}");
    }

    [HeavyVariant("shaders-trim")]
    public static void ApplyShadersTrim(AITEditorScriptObject config)
    {
        var so = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
        SerializedProperty list = so.FindProperty("m_AlwaysIncludedShaders");
        if (list == null || !list.isArray)
        {
            throw new System.InvalidOperationException("[AIT-Variant] shaders-trim: m_AlwaysIncludedShaders 를 찾지 못했습니다.");
        }

        var removed = new List<string>();
        var kept = new List<string>();
        for (int i = list.arraySize - 1; i >= 0; i--)
        {
            var shader = list.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
            string name = shader != null ? shader.name : "(null)";
            if (shader == null || TrimmedAlwaysIncluded.Contains(name))
            {
                // objectReference 가 남아 있으면 DeleteArrayElementAtIndex 가 먼저 null 로만 만든다 — 두 번 지운다.
                int before = list.arraySize;
                list.DeleteArrayElementAtIndex(i);
                if (list.arraySize == before) list.DeleteArrayElementAtIndex(i);
                removed.Add(name);
            }
            else
            {
                kept.Add(name);
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Debug.Log($"[AIT-Variant] shaders-trim: 제거 [{string.Join(", ", removed)}], 유지 [{string.Join(", ", kept)}]");
    }

    [HeavyVariant("envrefl-16")]
    public static void ApplyEnvReflection16(AITEditorScriptObject config)
    {
        if (!_reflectionHookInstalled)
        {
            _reflectionHookInstalled = true;
            EditorSceneManager.newSceneCreated += (scene, setup, mode) =>
            {
                RenderSettings.defaultReflectionResolution = ReflectionResolution;
                Debug.Log($"[AIT-Variant] envrefl-16: {scene.path} defaultReflectionResolution={RenderSettings.defaultReflectionResolution}");
            };
        }
        // 이미 열린 씬을 그대로 빌드하는 posture(game 등)에도 걸리도록 현재 씬에도 적용한다.
        RenderSettings.defaultReflectionResolution = ReflectionResolution;
        Debug.Log($"[AIT-Variant] envrefl-16: newSceneCreated 훅 설치, 현재 씬 defaultReflectionResolution={ReflectionResolution}");
    }

    private static bool _reflectionOffHookInstalled;

    [HeavyVariant("envrefl-off")]
    public static void ApplyEnvReflectionOff(AITEditorScriptObject config)
    {
        if (!_reflectionOffHookInstalled)
        {
            _reflectionOffHookInstalled = true;
            EditorSceneManager.newSceneCreated += (scene, setup, mode) => SetCustomReflectionNone("newSceneCreated");
            EditorSceneManager.sceneSaving += (scene, path) => SetCustomReflectionNone("sceneSaving " + path);
        }
        SetCustomReflectionNone("현재 씬");
    }

    private static void SetCustomReflectionNone(string where)
    {
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
#if UNITY_2022_1_OR_NEWER
        RenderSettings.customReflectionTexture = null;
#else
        RenderSettings.customReflection = null;
#endif
        Debug.Log($"[AIT-Variant] envrefl-off: {where} defaultReflectionMode={RenderSettings.defaultReflectionMode}");
    }

    [HeavyVariant("lz4-off")]
    public static void ApplyLz4Off(AITEditorScriptObject config)
    {
        if (config == null) throw new System.ArgumentNullException(nameof(config));
        if (config.productionProfile == null) config.productionProfile = AITBuildProfile.CreateProductionProfile();
        config.productionProfile.enableLZ4Compression = false;
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log("[AIT-Variant] lz4-off: productionProfile.enableLZ4Compression=false");
    }

    [HeavyVariant("symbols-embedded")]
    public static void ApplySymbolsEmbedded(AITEditorScriptObject config)
    {
        if (config == null) throw new System.ArgumentNullException(nameof(config));
        if (config.productionProfile == null) config.productionProfile = AITBuildProfile.CreateProductionProfile();
        config.productionProfile.debugSymbolsExternal = false;
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log("[AIT-Variant] symbols-embedded: productionProfile.debugSymbolsExternal=false (Embedded)");
    }

    internal const string SdkKeepLinkXmlPath = "Assets/link.xml";

    [HeavyVariant("sdk-keep")]
    public static void ApplySdkKeep(AITEditorScriptObject config)
    {
        if (System.IO.File.Exists(SdkKeepLinkXmlPath))
        {
            throw new System.InvalidOperationException($"[AIT-Variant] sdk-keep: {SdkKeepLinkXmlPath} 가 이미 있습니다(픽스처 link.xml 과 섞이지 않게 minimal posture 에서만 쓴다).");
        }
        System.IO.File.WriteAllText(SdkKeepLinkXmlPath,
            "<linker>\n" +
            "  <assembly fullname=\"AppsInTossSDK\" preserve=\"all\"/>\n" +
            "  <assembly fullname=\"AppsInToss.Helpers\" preserve=\"all\"/>\n" +
            "</linker>\n");
        AssetDatabase.ImportAsset(SdkKeepLinkXmlPath);
        Debug.Log($"[AIT-Variant] sdk-keep: {SdkKeepLinkXmlPath} 작성 (AppsInTossSDK, AppsInToss.Helpers preserve=all)");
    }

    internal const string SdkLinkXmlPackagePath = "Packages/im.toss.apps-in-toss-unity-sdk/Runtime/SDK/link.xml";
    internal const string NewtonsoftPreserveAll = "<assembly fullname=\"Newtonsoft.Json\" preserve=\"all\">";

    [HeavyVariant("nsjson-trim")]
    public static void ApplyNewtonsoftTrim(AITEditorScriptObject config)
    {
        string path = System.IO.Path.GetFullPath(SdkLinkXmlPackagePath);
        string original = System.IO.File.ReadAllText(path);
        if (!original.Contains(NewtonsoftPreserveAll))
        {
            throw new System.InvalidOperationException($"[AIT-Variant] nsjson-trim: {path} 에 Newtonsoft 어셈블리 preserve=all 항목이 없습니다.");
        }
        System.IO.File.WriteAllText(path, original.Replace(NewtonsoftPreserveAll, "<assembly fullname=\"Newtonsoft.Json\">"));
        EditorApplication.quitting += () =>
        {
            try { System.IO.File.WriteAllText(path, original); } catch (System.Exception) { /* CI 체크아웃이 되돌린다 */ }
        };
        Debug.Log($"[AIT-Variant] nsjson-trim: {path} 의 Newtonsoft.Json preserve=all 제거(빌드 동안만)");
    }

    [HeavyVariant("codeopt-speed")]
    public static void ApplyCodeOptSpeed(AITEditorScriptObject config)
    {
        string v = Application.unityVersion;
        string codeOpt = v.StartsWith("6000.0.") ? "RuntimeSpeed"   // 6000.0 은 LTO 링크 OOM 위험(speed posture 와 같은 선택)
            : v.StartsWith("6000.") ? "RuntimeSpeedLTO"
            : "Speed";
        System.Environment.SetEnvironmentVariable(AITWebGLCodeOptimization.EnvOverrideKey, codeOpt);
        Debug.Log($"[AIT-Variant] codeopt-speed: {AITWebGLCodeOptimization.EnvOverrideKey}={codeOpt}");
    }
}
