Shader "PocBench/Particle" {
  SubShader {
    Tags { "RenderType"="Transparent" "Queue"="Transparent" }
    Pass {
      Blend SrcAlpha One
      ZWrite Off
      Cull Off
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "UnityCG.cginc"
      struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
      struct v2f { float4 pos : SV_POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };
      v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.col = v.color; o.uv = v.uv; return o; }
      fixed4 frag (v2f i) : SV_Target {
        float2 d = i.uv - 0.5;
        float a = saturate(1.0 - dot(d, d) * 4.0);
        return fixed4(i.col.rgb, i.col.a * a * a);
      }
      ENDCG
    }
  }
}
