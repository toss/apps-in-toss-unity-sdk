Shader "PocBench/LitNoise" {
  SubShader {
    Tags { "RenderType"="Opaque" "Queue"="Geometry" }
    Pass {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_instancing
      #include "UnityCG.cginc"
      struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
      struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 lp : TEXCOORD1; float3 col : TEXCOORD2; float3 wp : TEXCOORD3; };
      float hash11 (float x) { return frac(sin(x * 127.1) * 43758.5453); }
      float3 hue (float h) { return saturate(abs(frac(h + float3(0.0, 0.6667, 0.3333)) * 6.0 - 3.0) - 1.0); }
      float hash21 (float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }
      float vnoise (float2 p) {
        float2 i = floor(p), f = frac(p);
        f = f * f * (3.0 - 2.0 * f);
        return lerp(lerp(hash21(i), hash21(i + float2(1,0)), f.x), lerp(hash21(i + float2(0,1)), hash21(i + float2(1,1)), f.x), f.y);
      }
      v2f vert (appdata v) {
        UNITY_SETUP_INSTANCE_ID(v);
        v2f o;
        float3 c = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
        float h = hash11(dot(c, float3(1.0, 7.31, 13.7)));
        float a = _Time.y * (0.8 + h * 2.4) + h * 6.2831;
        float s, co;
        sincos(a, s, co);
        float3 p = v.vertex.xyz;
        p = float3(co * p.x + s * p.z, p.y, -s * p.x + co * p.z);
        float3 nn = float3(co * v.normal.x + s * v.normal.z, v.normal.y, -s * v.normal.x + co * v.normal.z);
        float4 w = mul(unity_ObjectToWorld, float4(p, 1.0));
        o.wp = w.xyz;
        o.pos = mul(UNITY_MATRIX_VP, w);
        o.n = normalize(mul((float3x3)unity_ObjectToWorld, nn));
        o.lp = p;
        o.col = lerp(float3(1,1,1), hue(h * 3.0 + _Time.y * 0.05), 0.85);
        return o;
      }
      fixed4 frag (v2f i) : SV_Target {
        float3 n = normalize(i.n);
        float3 l = normalize(float3(0.4, 0.8, 0.3));
        float d = saturate(dot(n, l)) * 0.8 + 0.2;
        float3 v = normalize(_WorldSpaceCameraPos - i.wp);
        float2 q = i.lp.xy * 3.0 + i.lp.z * 2.0 + _Time.y * 0.6;
        float h = 0.0, amp = 0.5;
        for (int k = 0; k < 4; k++) { h += amp * vnoise(q); q = q * 2.03 + 7.1; amp *= 0.5; }
        float rim = pow(1.0 - saturate(dot(n, v)), 3.0);
        float3 col = i.col * (0.35 + h * 1.1) * d + float3(0.15, 0.3, 0.9) * rim;
        return fixed4(col, 1);
      }
      ENDCG
    }
  }
}
