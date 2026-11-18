Shader "Betta/Water"
{
    Properties
    {
        _Top ("Top", Color) = (0.16, 0.42, 0.52, 1)
        _Bottom ("Bottom", Color) = (0.02, 0.10, 0.15, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry-100" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Top;
                half4 _Bottom;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.uv = IN.uv;
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 c = lerp(_Bottom.rgb, _Top.rgb, smoothstep(0.0, 1.0, IN.uv.y));
                float2 d = IN.uv - 0.5;
                c *= 1.0 - 0.25 * dot(d, d);
                return half4(c, 1.0);
            }
            ENDHLSL
        }
    }
}
