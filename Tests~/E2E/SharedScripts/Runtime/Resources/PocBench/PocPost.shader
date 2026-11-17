Shader "PocBench/Post" {
  Properties { _MainTex ("Tex", 2D) = "white" {} }
  SubShader {
    Cull Off ZWrite Off ZTest Always
    Pass {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "UnityCG.cginc"
      sampler2D _MainTex;
      float4 _MainTex_TexelSize;
      struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
      v2f vert (appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }
      float hash21 (float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }
      float vnoise (float2 p) {
        float2 i = floor(p), f = frac(p);
        f = f * f * (3.0 - 2.0 * f);
        return lerp(lerp(hash21(i), hash21(i + float2(1,0)), f.x), lerp(hash21(i + float2(0,1)), hash21(i + float2(1,1)), f.x), f.y);
      }
      fixed4 frag (v2f i) : SV_Target {
        float2 t = _MainTex_TexelSize.xy * 1.5;
        float4 c = tex2D(_MainTex, i.uv) * 4.0;
        c += (tex2D(_MainTex, i.uv + float2(t.x, 0)) + tex2D(_MainTex, i.uv - float2(t.x, 0)) + tex2D(_MainTex, i.uv + float2(0, t.y)) + tex2D(_MainTex, i.uv - float2(0, t.y))) * 2.0;
        c += tex2D(_MainTex, i.uv + t) + tex2D(_MainTex, i.uv - t) + tex2D(_MainTex, i.uv + float2(t.x, -t.y)) + tex2D(_MainTex, i.uv + float2(-t.x, t.y));
        c *= (1.0 / 16.0);
        float2 q = i.uv * float2(10.0, 22.0) + _Time.y * 0.3;
        float n = 0.5 * vnoise(q) + 0.25 * vnoise(q * 2.1) + 0.125 * vnoise(q * 4.3);
        c.rgb += (n - 0.4) * 0.06;
        return c;
      }
      ENDCG
    }
  }
}
