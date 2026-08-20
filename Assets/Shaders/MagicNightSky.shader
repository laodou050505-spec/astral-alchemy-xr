Shader "AstralAlchemy/MagicNightSky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.012, 0.006, 0.06, 1)
        _HorizonColor ("Horizon", Color) = (0.12, 0.035, 0.22, 1)
        _StarColor ("Stars", Color) = (1, 0.78, 0.34, 1)
        _NebulaColor ("Nebula", Color) = (0.32, 0.08, 0.48, 1)
        _StarDensity ("Star Density", Range(0.96, 0.999)) = 0.988
        _NebulaStrength ("Nebula Strength", Range(0, 0.5)) = 0.16
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Opaque" "PreviewType"="Skybox" }
        Cull Front
        ZWrite Off
        ZTest LEqual
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _ZenithColor, _HorizonColor, _StarColor, _NebulaColor;
            float _StarDensity, _NebulaStrength;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 position : SV_POSITION; float3 direction : TEXCOORD0; };
            float Hash31(float3 p) { p = frac(p * .1031); p += dot(p, p.yzx + 33.33); return frac((p.x + p.y) * p.z); }
            v2f vert(appdata v) { v2f o; o.position = UnityObjectToClipPos(v.vertex); o.direction = normalize(mul((float3x3)unity_ObjectToWorld, v.vertex.xyz)); return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.direction);
                float height = saturate(d.y * .56 + .48);
                float3 color = lerp(_HorizonColor.rgb, _ZenithColor.rgb, smoothstep(0, 1, height));
                float cloudA = sin(d.x * 4.2 + d.z * 2.8 + sin(d.y * 8.0)) * .5 + .5;
                float cloudB = sin(d.x * 11.0 - d.z * 6.0 + sin(d.y * 15.0)) * .5 + .5;
                float band = saturate(cloudA * .72 + cloudB * .28);
                band *= smoothstep(.06, .32, height) * smoothstep(.95, .34, height);
                color += _NebulaColor.rgb * band * band * _NebulaStrength * 1.35;
                float3 cell = floor(d * 390.0);
                float starNoise = Hash31(cell);
                float star = smoothstep(_StarDensity, 1.0, starNoise);
                float blinkMask = step(.68, Hash31(cell.yzx));
                float blinkRate = 1.0 + Hash31(cell.zxy) * 3.2;
                float twinkle = lerp(1.0, .55 + .45 * sin(_Time.y * blinkRate + starNoise * 23.0), blinkMask);
                float3 starTint = lerp(float3(.46, .72, 1.0), _StarColor.rgb, Hash31(cell.zxy));
                color += starTint * star * twinkle * 1.7;
                return fixed4(color, 1);
            }
            ENDCG
        }
    }
}
