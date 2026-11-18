Shader "Betta/Fin"
{
    Properties
    {
        _FinColor ("Fin", Color) = (0.75, 0.06, 0.1, 1)
        _EdgeColor ("Edge", Color) = (0.9, 0.16, 0.16, 1)
        _PatternColor ("Pattern", Color) = (1, 1, 1, 1)
        _SheenColor ("Sheen", Color) = (0.3, 0.55, 1, 1)
        _Sheen ("Sheen Amount", Range(0, 1)) = 0.15
        _Clear ("Clear", Range(0, 1)) = 0.2
        _Pattern ("Pattern", Float) = 0
        _Seed ("Seed", Float) = 1
        _RayCount ("Ray Count", Float) = 27
        _Outer ("Outer Fade", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            // color: normal alpha blend; alpha channel: accumulate coverage (for a transparent desktop window later)
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "BettaCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FinColor;
                half4 _EdgeColor;
                half4 _PatternColor;
                half4 _SheenColor;
                float _Sheen;
                float _Clear;
                float _Pattern;
                float _Seed;
                float _RayCount;
                float _Outer;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;   // x: colour ramp (1 = membrane edge, >1 crown spike), y: distance from root
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float2 uv2 : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                o.uv = IN.uv;
                o.uv2 = IN.uv2;
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
                float objScale = length(GetObjectToWorldMatrix()._m00_m10_m20);

                float u = IN.uv.x;
                float vEdge = saturate(IN.uv.y);   // 0 = root, 1 = end of this column (for the edge cut-off)
                float vr = IN.uv2.x;                // 1 at the membrane edge; crown spikes go beyond 1
                float v = saturate(vr);             // colour ramp / ray zones
                float vp = IN.uv2.y;                // physical distance: patterns continue across columns
                float rays = max(_RayCount, 2.0);
                // crown spike zone: 0 at the web edge -> 1 at the spike tip
                float spikeT = vr > 1.0 ? saturate(vEdge * (vr - 1.0) / max(vr - vEdge, 1e-4)) : 0.0;
                half3 fc = _FinColor.rgb, ec = _EdgeColor.rgb, pc = _PatternColor.rgb, sc = _SheenColor.rgb;

                // ---- color along the fin: deep at the root -> fin color -> edge color
                half3 mid = (fc + ec) * 0.5;
                half3 c = v < 0.45 ? lerp(fc * 0.9, fc, v / 0.45)
                        : (v < 0.82 ? lerp(fc, mid, (v - 0.45) / 0.37) : lerp(mid, ec, (v - 0.82) / 0.18));
                if (_Pattern > 0.5 && _Pattern < 1.5)            // butterfly band
                    c = lerp(c, pc, saturate((v - 0.56) / 0.04 + spikeT * 10.0));
                else if (_Pattern > 1.5 && _Pattern < 2.5)       // marble
                {
                    float n = BettaFbm(float2(u * 4.0, vp * 2.5) + float2(_Seed * 1.37 + rays, _Seed * 0.7));
                    c = lerp(c, pc, saturate((n - 0.52) / 0.04));
                }
                // color streaks running along the rays
                float n3 = BettaFbm(float2(u * rays * 1.6, vp * 1.3) + float2(_Seed * 0.91 + rays * 0.13, 0.0));
                c = lerp(c, ec, saturate((n3 - 0.42) / 0.2) * 0.55);

                // ---- rays: one near the root, two in the middle, four at the edge (branching)
                float rp = u * (rays - 1.0);
                float lf1 = saturate(1.6 - fwidth(rp) * 2.0);
                float lf2 = saturate(1.6 - fwidth(rp * 2.0) * 2.0);
                float lf4 = saturate(1.6 - fwidth(rp * 4.0) * 2.0);
                float r1 = pow(abs(cos(PI * rp)), 24.0) * lf1;
                float r2 = pow(abs(cos(PI * rp * 2.0)), 36.0) * lf2;
                float r3 = pow(abs(cos(PI * rp * 4.0)), 40.0) * lf4;
                float z1 = 1.0 - saturate((v - 0.35) / 0.2);
                float z3 = saturate((v - 0.65) / 0.17);
                float z2 = (1.0 - z1) * (1.0 - z3);
                float ray = max(max(r1 * z1, r2 * z2), r3 * z3);

                // rays brighter and glossier, membrane between them a little darker, streaky texture
                c *= lerp(1.0, 0.72 + 0.28 * fc, (1.0 - ray) * 0.8);
                half3 shineCol = 0.35 + 0.4 * sc;
                c = lerp(c, 1.0 - (1.0 - c) * (1.0 - shineCol), ray * 0.45);
                float nz2 = BettaNoise(float2(u * rays * 5.0, vp * 6.0) + _Seed * 2.3);
                c *= 0.9 + 0.2 * nz2;

                // ---- opacity: dense at the root, thinner at the edge, frayed tips
                float a = lerp(0.95, 0.62 - 0.3 * _Clear, v);
                a = min(1.0, a + ray * 0.2);
                a *= lerp(1.0 - 0.5 * _Outer, 1.0, saturate(min(u, 1.0 - u) / 0.2));
                float lim = 1.0 - 0.035 * BettaNoise(float2(u * rays * 9.0 + _Seed * 1.7, 0.5));
                float fv = max(fwidth(vEdge), 1e-4), fu = max(fwidth(u), 1e-4);
                a *= saturate((lim - vEdge) / fv * 0.7);
                // crown tail: past the web edge only the bare ray bundle remains, tapering to a thin point
                if (vr > 1.0)
                {
                    float cp = u * (rays - 1.0);
                    // thin ray spikes: width ~ 1/(pi*sqrt(n)) rays; never thinner than ~0.7 px -
                    // when far away the spike gets fainter instead of wider
                    float want = lerp(18.0, 110.0, spikeT);
                    float fw = max(fwidth(cp), 1e-4);
                    float nMax = 1.0 / ((PI * 0.7 * fw) * (PI * 0.7 * fw));
                    float sharp = max(2.0, min(want, nMax));
                    // each ray spike forks into two near its tip (branched rays)
                    float fork = 0.07 * smoothstep(0.5, 1.0, spikeT);
                    float spikeMask = max(pow(abs(cos(PI * (cp - fork))), sharp),
                                          pow(abs(cos(PI * (cp + fork))), sharp));
                    spikeMask *= sqrt(sharp / want);
                    a *= lerp(1.0, spikeMask, saturate((vr - 1.0) / 0.05));
                    a = saturate(a * lerp(1.0, 1.25, spikeT));
                }
                a *= saturate(u / fu * 0.7) * saturate((1.0 - u) / fu * 0.7);

                // ---- normal: pleats come from the mesh; rays are slightly raised
                float3 Nb = BettaBump(IN.positionWS, N, (ray + 0.3 * nz2) * 0.001 * objScale);
                N = normalize(lerp(N, Nb, 0.4));

                // ---- thin membrane lighting: 70% glossy surface, 30% light shining through
                float NdotV = abs(dot(N, V));
                float ndl = dot(N, L);
                half3 amb = _BettaAmbient.rgb;
                half3 diffuse = c * (amb + lc * (abs(ndl) * 0.6 + 0.4));
                float3 H = normalize(L + V);
                float NdotH = abs(dot(N, H));
                half3 film = BettaThinFilm(NdotV, 250.0 + 450.0 * _Sheen);
                half3 spec = lc * BettaGGX(NdotH, 0.45) * abs(ndl) * 0.25 * lerp(0.04, 0.5, ray) * lerp(1.0, film * 1.5, 0.3 + 0.5 * _Sheen);
                half3 trans = c * lc * (saturate(-ndl) + pow(saturate(dot(-V, L)), 3.0)) * 0.8;
                trans += c * _BettaRimColor.rgb * (abs(dot(N, _BettaRimDir.xyz)) * 0.5 + pow(saturate(dot(-V, _BettaRimDir.xyz)), 3.0)) * 0.7;
                half3 col = 0.7 * (diffuse + spec) + 0.3 * trans;
                // velvet sheen at grazing angles
                col += (0.6 + 0.4 * ec) * pow(1.0 - NdotV, 4.0) * 0.35 * (amb + lc * 0.5);
                col += BettaEnv(reflect(-V, N)) * 0.06 * pow(1.0 - NdotV, 3.0);

                return half4(BettaFinish(col), saturate(a));
            }
            ENDHLSL
        }
    }
}
