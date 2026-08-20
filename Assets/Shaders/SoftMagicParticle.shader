Shader "AstralAlchemy/SoftMagicParticle"
{
    Properties { _Intensity ("Intensity", Range(.25, 4)) = 1.25 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest LEqual
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Intensity;
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 position : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.position = UnityObjectToClipPos(v.vertex); o.color = v.color; o.uv = v.uv; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 centered = i.uv * 2.0 - 1.0;
                float radius = length(centered);
                float alpha = smoothstep(1.0, .18, radius);
                alpha *= alpha;
                return fixed4(i.color.rgb * _Intensity, i.color.a * alpha);
            }
            ENDCG
        }
    }
}
