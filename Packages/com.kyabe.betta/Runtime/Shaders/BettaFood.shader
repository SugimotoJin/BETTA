Shader "Betta/Food"
{
    Properties
    {
        _Color ("Color", Color) = (0.45, 0.22, 0.08, 1)
        _Fade ("Fade", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "BettaCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _Fade;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            // colour: normal alpha blend; alpha channel: accumulate coverage (transparent desktop window)
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite On

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
                // a dry pellet: rough, slightly speckled
                float n = BettaNoise(IN.positionOS.xy * 9.0 + IN.positionOS.z * 5.0);
                half3 c = _Color.rgb * (0.75 + 0.5 * n);
                float3 N = normalize(IN.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                Light light = GetMainLight();
                float ndl = saturate(dot(N, light.direction)) * 0.8 + 0.2;
                half3 col = c * (_BettaAmbient.rgb + light.color * ndl);
                col += c * _BettaRimColor.rgb * pow(saturate(dot(N, _BettaRimDir.xyz)), 2.0) * 0.4;
                col += 0.04 * pow(1.0 - saturate(dot(N, V)), 4.0);
                return half4(BettaFinish(col), saturate(_Fade));
            }
            ENDHLSL
        }
    }
}
