Shader "AstralAlchemy/ElementEnergyShell"
{
    Properties
    {
        _Color ("Energy Color", Color) = (0.5, 0.3, 1, 0.08)
        _EmissionColor ("Rim Emission", Color) = (0.5, 0.3, 1, 1)
        _RimPower ("Rim Power", Range(1, 8)) = 3.2
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Back
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            fixed4 _EmissionColor;
            float _RimPower;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 position : SV_POSITION; float3 worldNormal : TEXCOORD0; float3 viewDir : TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPos);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float rim = pow(1.0 - saturate(dot(normalize(i.worldNormal), normalize(i.viewDir))), _RimPower);
                float3 color = _Color.rgb * .35 + _EmissionColor.rgb * rim * 1.1;
                return fixed4(color, saturate(_Color.a + rim * .24));
            }
            ENDCG
        }
    }
}
