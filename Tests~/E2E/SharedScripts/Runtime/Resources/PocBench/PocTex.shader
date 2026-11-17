Shader "PocBench/Tex" {
  Properties { _MainTex ("Tex", 2D) = "white" {} }
  SubShader {
    Tags { "RenderType"="Opaque" "Queue"="Geometry" }
    Pass {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "UnityCG.cginc"
      sampler2D _MainTex;
      struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
      v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; return o; }
      fixed4 frag (v2f i) : SV_Target { return tex2D(_MainTex, i.uv); }
      ENDCG
    }
  }
}
