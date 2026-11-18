Shader "Betta/Body"
{
    Properties
    {
        _BodyColor ("Body", Color) = (0.59, 0.04, 0.08, 1)
        _PatternColor ("Pattern", Color) = (1, 1, 1, 1)
        _SheenColor ("Sheen", Color) = (0.3, 0.55, 1, 1)
        _Sheen ("Sheen Amount", Range(0, 1)) = 0.15
        _Pattern ("Pattern", Float) = 0
        _Seed ("Seed", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "BettaCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BodyColor;
            half4 _PatternColor;
            half4 _SheenColor;
            float _Sheen;
            float _Pattern;
            float _Seed;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

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

            half4 frag(Varyings IN, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float sgn = IS_FRONT_VFACE(face, 1.0, -1.0);
                float3 N = normalize(IN.normalWS) * sgn;
                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                Light light = GetMainLight();
                float3 L = light.direction;
                half3 lc = light.color;
                float3 p = IN.positionOS;            // x = toward the head, y = up
                float objScale = length(GetObjectToWorldMatrix()._m00_m10_m20);

                // ---- base color: light belly -> body color -> dark back
                half3 bc = _BodyColor.rgb;
                float t = saturate((p.y + 0.14) / 0.29);
                half3 belly = saturate(bc * 1.05 + 0.03);
                half3 c = t < 0.5 ? lerp(belly, bc, t / 0.5) : lerp(bc, bc * 0.5, (t - 0.5) / 0.5);

                if (_Pattern > 1.5 && _Pattern < 2.5)          // marble (koi)
                {
                    float n = BettaFbm(p.xy * 6.0 + _Seed * 0.37);
                    c = lerp(c, _PatternColor.rgb, saturate((n - 0.50) / 0.04));
                }
                bool dragon = _Pattern > 2.5;
                if (dragon) c = lerp(c, _PatternColor.rgb, saturate((0.30 - p.x) / 0.10) * 0.85);

                // ---- shingled scales: each one rises toward its free (rear) edge, then drops
                float sx = p.x * 38.0, sz = p.y * 42.0;
                float row = floor(sz);
                float odd = row - 2.0 * floor(row * 0.5);
                float shift = sx + odd * 0.5;
                float2 cell = float2(frac(shift), frac(sz));
                float d = distance(cell, float2(1.0, 0.5));
                float rise = saturate((d - 0.15) / 0.45) * step(d, 0.6);
                float scalesOn = saturate((0.25 - p.x) / 0.06);              // no scales on the head
                float aa = saturate(1.6 - max(fwidth(sx), fwidth(sz)) * 2.2); // fade when tiny on screen
                float sh = rise * scalesOn * aa;
                float edgeDark = lerp(1.0, 0.5, saturate((sh - 0.72) / 0.28));
                float spark = lerp(0.82, 1.25, BettaHash(float2(floor(shift), row) + _Seed * 0.1));
                c *= edgeDark * lerp(1.0, spark, scalesOn * aa);

                // ---- face: gill cover line, upturned mouth, darker mottled head, dark gill membrane
                float gx = 0.235 + 0.07 * p.y * p.y / 0.0121;
                float gd = p.x - gx;
                float zin = 1.0 - saturate((abs(p.y) - 0.09) / 0.03);
                float gline = lerp(0.6, 1.0, saturate((abs(gd) - 0.004) / 0.012));
                float gmix = 1.0 - zin * (1.0 - gline);
                float my = p.x * 0.25 + (0.012 - 0.125);
                float mline = lerp(0.4, 1.0, saturate((abs(p.y - my) - 0.002) / 0.005));
                float msel = lerp(1.0, mline, saturate((p.x - 0.455) / 0.02));
                float headness = 1.0 - scalesOn;
                float headDark = lerp(1.0, 0.6, saturate((p.x - 0.2) / 0.1));
                float mot = lerp(0.65, 1.15, saturate((BettaFbm(p.xy * 22.0 + 3.0) - 0.3) / 0.4));
                float beard = saturate((p.y + 0.04) / -0.035) * (1.0 - saturate((abs(p.x - 0.235) - 0.03) / 0.035));
                c *= gmix * msel * headDark * lerp(1.0, mot, headness) * (1.0 - 0.5 * beard);

                // ---- surface relief: scales + raised gill plate + skin texture - mouth groove
                float plate = saturate((gd + 0.002) / 0.014) * zin;
                float skinAA = saturate(1.5 - fwidth(p.x * 140.0) * 1.5);
                float skin = BettaNoise(p.xy * 140.0) * headness * skinAA;
                float height = sh + plate * 0.7 + skin * 0.25 - (1.0 - msel) * 1.2;
                float3 Nb = BettaBump(IN.positionWS, N, height * 0.002 * objScale);
                N = normalize(lerp(N, Nb, dragon ? 0.6 : 0.5));

                // ---- lighting (approximation of the Cycles material)
                float NdotV = saturate(dot(N, V));
                float rough = lerp(0.3, 0.5, headness);
                float metallic = dragon ? 0.45 : 0.25;
                float wrap = saturate((dot(N, L) + 0.3) / 1.3);
                half3 amb = _BettaAmbient.rgb;
                half3 col = c * (1.0 - metallic * 0.6) * (amb + lc * wrap);

                float3 H = normalize(L + V);
                float NdotL = saturate(dot(N, L));
                float NdotH = saturate(dot(N, H));
                half3 specTint = 0.3 + 0.7 * _SheenColor.rgb;
                half3 film = BettaThinFilm(NdotV, 300.0 + 500.0 * _Sheen);
                half3 specCol = lerp(half3(0.08, 0.08, 0.08) * specTint, c * specTint, metallic) * lerp(1.0, film * 1.4, 0.35 + 0.4 * _Sheen);
                col += specCol * lc * BettaGGX(NdotH, rough) * NdotL * 0.25;
                // clear coat: a thin sharp highlight
                float fres = 0.04 + 0.96 * pow(1.0 - NdotV, 5.0);
                col += lc * BettaGGX(NdotH, 0.12) * NdotL * 0.3 * fres * 0.25 * lerp(1.0, 0.3, headness);
                // reflections of the surroundings (scales reflect the sheen color)
                float3 R = reflect(-V, N);
                col += BettaEnv(R) * specCol * (0.35 + fres) * (1.0 - rough * 0.8);
                col += BettaEnv(R) * fres * 0.3 * lerp(1.0, 0.3, headness);
                // iridescent scales
                col += _SheenColor.rgb * _Sheen * scalesOn * aa * spark * pow(NdotH, 12.0) * 0.6 * lc;
                // back (rim) light
                col += c * _BettaRimColor.rgb * pow(saturate(dot(N, _BettaRimDir.xyz)), 2.0) * 0.6;

                return half4(BettaFinish(col), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Off
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth

            struct AttributesD { float4 positionOS : POSITION; };
            struct VaryingsD { float4 positionCS : SV_POSITION; };

            VaryingsD vertDepth(AttributesD IN)
            {
                VaryingsD o;
                o.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return o;
            }

            half fragDepth(VaryingsD IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
