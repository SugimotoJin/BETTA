Shader "Betta/Eye"
{
    Properties
    {
        _IrisColor ("Iris", Color) = (0.35, 0.15, 0.08, 1)
        _SkinColor ("Skin", Color) = (0.2, 0.02, 0.03, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "BettaCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _IrisColor;
            half4 _SkinColor;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                o.positionOS = IN.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float z = normalize(IN.positionOS).z;   // +Z points out of the head
                // the rim is covered by head skin; a thin dark reddish iris; black pupil
                half3 iris = _IrisColor.rgb;
                half3 c = _SkinColor.rgb;
                c = lerp(c, iris, smoothstep(0.5, 0.6, z));
                c = lerp(c, iris * 0.6, smoothstep(0.6, 0.74, z));
                c = lerp(c, half3(0.003, 0.003, 0.004), smoothstep(0.74, 0.8, z));

                float3 N = normalize(IN.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                Light light = GetMainLight();
                float ndl = dot(N, light.direction) * 0.5 + 0.5;
                float3 H = normalize(light.direction + V);
                half3 col = c * (_BettaAmbient.rgb + light.color * ndl * 0.7);
                col += light.color * BettaGGX(saturate(dot(N, H)), 0.12) * 0.04 * 0.25 * 4.0;   // wet, glossy cornea
                col += BettaEnv(reflect(-V, N)) * 0.15;
                return half4(BettaFinish(col), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            struct AttributesD { float4 positionOS : POSITION; };
            struct VaryingsD { float4 positionCS : SV_POSITION; };
            VaryingsD vertDepth(AttributesD IN) { VaryingsD o; o.positionCS = TransformObjectToHClip(IN.positionOS.xyz); return o; }
            half fragDepth(VaryingsD IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
