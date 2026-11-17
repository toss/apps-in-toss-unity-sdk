using System;
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

    const string ColdDir = "Assets/PocBench/Resources/PocBench/Cold";
    public const int ColdCount = 40;

    // S 단계용 셰이더. 빌드마다 다른 상수(@SALT_*@)가 연산식에 들어가 컴파일 결과가 매번 달라진다.
    // 기기 셰이더 캐시가 이 셰이더를 가질 수 없게 하는 용도다.
    const string ColdShaderTemplate = @"Shader ""PocBench/ColdLit"" {
  Properties { _Color (""Color"", Color) = (1,1,1,1) }
  SubShader {
    Tags { ""RenderType""=""Opaque"" ""Queue""=""Geometry"" }
    Pass {
      Tags { ""LightMode""=""ForwardBase"" }
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile _ COLD_A
      #pragma multi_compile _ COLD_B
      #pragma multi_compile _ COLD_C
      #pragma multi_compile _ COLD_D
      #pragma multi_compile _ COLD_M1 COLD_M2
      #include ""UnityCG.cginc""
      // build salt @SALT_ID@
      #define SALT_A @SALT_A@
      #define SALT_B @SALT_B@
      #define SALT_C @SALT_C@
      #define SALT_D @SALT_D@
      fixed4 _Color;
      struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 wp : TEXCOORD1; float2 uv : TEXCOORD2; };
      v2f vert (appdata_base v) {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.n = UnityObjectToWorldNormal(v.normal);
        o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
        o.uv = v.texcoord.xy;
        return o;
      }
      float hash21 (float2 p) { return frac(sin(dot(p, float2(SALT_A, SALT_B))) * SALT_C); }
      float vnoise (float2 p) {
        float2 i = floor(p), f = frac(p);
        f = f * f * (3.0 - 2.0 * f);
        return lerp(lerp(hash21(i), hash21(i + float2(1,0)), f.x), lerp(hash21(i + float2(0,1)), hash21(i + float2(1,1)), f.x), f.y);
      }
      fixed4 frag (v2f i) : SV_Target {
        float3 n = normalize(i.n);
        float3 v = normalize(_WorldSpaceCameraPos - i.wp);
        float3 l = normalize(_WorldSpaceLightPos0.xyz);
        float h = 0.0, amp = 0.5;
        float2 q = i.uv * 6.0 + SALT_D;
      #if defined(COLD_A)
        for (int k = 0; k < 5; k++) { h += amp * vnoise(q); q = q * 2.03 + SALT_B; amp *= 0.5; }
        n = normalize(n + (h - 0.5) * 0.6);
      #endif
        float ndl = saturate(dot(n, l));
        float3 hv = normalize(l + v);
        float shin = 16.0 + SALT_A;
        float spec = pow(saturate(dot(n, hv)), shin);
      #if defined(COLD_B)
        spec *= 0.5 + 0.5 * sin(dot(i.wp, float3(SALT_A, SALT_B, SALT_C)) + SALT_D);
      #endif
        float3 col = _Color.rgb * (0.15 + ndl) + spec * 0.6;
      #if defined(COLD_C)
        float fr = pow(1.0 - saturate(dot(n, v)), 3.0 + frac(SALT_C));
        col += fr * float3(frac(SALT_A), frac(SALT_B), frac(SALT_D));
      #endif
      #if defined(COLD_D)
        col = lerp(col, col * col * (3.0 - 2.0 * col), 0.5 + 0.5 * sin(SALT_C * ndl + SALT_D));
      #endif
      #if defined(COLD_M1)
        col = col.gbr * 0.5 + col * 0.5 + sin(i.wp * SALT_B) * 0.02;
      #elif defined(COLD_M2)
        float g = dot(col, float3(0.299, 0.587, 0.114));
        col = lerp(float3(g, g, g), col, 1.0 + frac(SALT_A) + 0.25 * sin(g * SALT_D));
      #endif
        return fixed4(col, 1);
      }
      ENDCG
    }
  }
}
";

    // 5개 키워드 그룹(2x2x2x2x3 = 48변형) 중 40개를 고르게 뽑아 머티리얼을 만든다.
    static void GenerateCold()
    {
        if (Directory.Exists(ColdDir)) Directory.Delete(ColdDir, true);
        Directory.CreateDirectory(ColdDir);
        var rnd = new System.Random(Guid.NewGuid().GetHashCode());
        string F(double lo, double hi) => (lo + rnd.NextDouble() * (hi - lo)).ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
        var src = ColdShaderTemplate
            .Replace("@SALT_ID@", DateTime.UtcNow.Ticks.ToString() + "-" + Guid.NewGuid().ToString("N"))
            .Replace("@SALT_A@", F(12.0, 13.0))
            .Replace("@SALT_B@", F(78.0, 79.0))
            .Replace("@SALT_C@", F(43758.0, 43759.0))
            .Replace("@SALT_D@", F(1.0, 2.0));
        var shaderPath = ColdDir + "/PocCold.shader";
        File.WriteAllText(shaderPath, src);
        AssetDatabase.ImportAsset(shaderPath, ImportAssetOptions.ForceSynchronousImport);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (shader == null) { Debug.LogWarning("[PocBench] cold shader import failed; skipping S stage"); return; }
        string[] bin = { "COLD_A", "COLD_B", "COLD_C", "COLD_D" };
        string[] tri = { null, "COLD_M1", "COLD_M2" };
        for (int i = 0; i < ColdCount; i++)
        {
            int idx = i * 48 / ColdCount;
            int bits = idx % 16, m = idx / 16;
            var mat = new Material(shader);
            for (int b = 0; b < bin.Length; b++)
                if ((bits & (1 << b)) != 0) mat.EnableKeyword(bin[b]);
            if (tri[m] != null) mat.EnableKeyword(tri[m]);
            mat.color = Color.HSVToRGB(i / (float)ColdCount, 0.6f, 0.9f);
            AssetDatabase.CreateAsset(mat, $"{ColdDir}/S_{i:D2}.mat");
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[PocBench] Generated {ColdCount} cold-shader materials (salted) in {ColdDir}");
    }

    public static void Generate()
    {
        GenerateCold();
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
