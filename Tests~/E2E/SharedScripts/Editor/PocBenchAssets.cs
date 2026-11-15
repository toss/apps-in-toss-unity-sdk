using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// WebGPU PoC 벤치마크 C 단계용 머티리얼 생성. 빌드 직전에 Standard / Standard (Specular setup) 셰이더의
/// 키워드 조합이 서로 다른 머티리얼 80개를 Resources 아래에 만든다. Resources 안의 머티리얼이 켜 둔 키워드
/// 조합만 플레이어 빌드에 셰이더 변형으로 포함되므로, 런타임에 키워드를 켜는 방식으로는 안 된다.
/// </summary>
public static class PocBenchAssets
{
    const string Dir = "Assets/PocBench/Resources/PocBench/Variants";
    public const int Count = 80;

    static readonly string[] Flags =
    {
        "_NORMALMAP", "_EMISSION", "_METALLICGLOSSMAP", "_DETAIL_MULX2", "_PARALLAXMAP", "_ALPHATEST_ON",
    };

    public static void Generate()
    {
        if (Directory.Exists(Dir)) Directory.Delete(Dir, true);
        Directory.CreateDirectory(Dir);
        var standard = Shader.Find("Standard");
        var specular = Shader.Find("Standard (Specular setup)");
        if (standard == null || specular == null)
        {
            Debug.LogWarning("[PocBench] Standard shader not found; skipping variant materials");
            return;
        }
        // 6개 플래그(64) x 블렌드 모드 3종(없음/ALPHABLEND/ALPHAPREMULTIPLY) = 192조합에서 80개를 고르게 뽑는다.
        for (int i = 0; i < Count; i++)
        {
            int idx = i * 192 / Count;
            int bits = idx % 64, blend = idx / 64;
            var mat = new Material(i % 2 == 0 ? standard : specular);
            var keywords = new System.Collections.Generic.List<string>();
            for (int b = 0; b < Flags.Length; b++)
                if ((bits & (1 << b)) != 0) keywords.Add(Flags[b]);
            if (blend == 1) keywords.Add("_ALPHABLEND_ON");
            if (blend == 2) keywords.Add("_ALPHAPREMULTIPLY_ON");
            if (i % 2 == 1 && (bits & 4) != 0) { keywords.Remove("_METALLICGLOSSMAP"); keywords.Add("_SPECGLOSSMAP"); }
            foreach (var k in keywords) mat.EnableKeyword(k);
            if (blend != 0) mat.renderQueue = 3000;
            else if ((bits & 32) != 0) mat.renderQueue = 2450;
            mat.color = Color.HSVToRGB(i / (float)Count, 0.6f, 0.9f);
            AssetDatabase.CreateAsset(mat, $"{Dir}/V_{i:D2}.mat");
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[PocBench] Generated {Count} variant materials in {Dir}");
    }
}
