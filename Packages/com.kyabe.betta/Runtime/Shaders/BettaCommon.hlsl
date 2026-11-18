#ifndef BETTA_COMMON_INCLUDED
#define BETTA_COMMON_INCLUDED

// Set from C#: Shader.SetGlobalColor / SetGlobalVector
float4 _BettaAmbient;     // ambient light color
float4 _BettaRimDir;      // direction TO the rim (back) light, world space
float4 _BettaRimColor;    // rim light color
float4 _BettaEnvTop;      // reflection environment: bright top
float4 _BettaEnvBottom;   // reflection environment: dark bottom
// Desktop overlay: post-processing can't keep the window's alpha, so the shaders tone-map themselves
float _BettaTonemap;      // 1 = apply exposure + filmic curve here
float _BettaExposure;     // linear exposure multiplier

half3 BettaFinish(half3 c)
{
    if (_BettaTonemap > 0.5)
    {
        float3 x = max(c * _BettaExposure * 0.8, 0.0);
        c = saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14));   // ACES (Narkowicz fit)
    }
    return c;
}

float BettaHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float BettaNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float a = BettaHash(i);
    float b = BettaHash(i + float2(1.0, 0.0));
    float c = BettaHash(i + float2(0.0, 1.0));
    float d = BettaHash(i + float2(1.0, 1.0));
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float BettaFbm(float2 p)
{
    float v = 0.0;
    float amp = 0.5;
    for (int k = 0; k < 4; k++)
    {
        v += amp * BettaNoise(p);
        p = p * 2.03 + 17.1;
        amp *= 0.5;
    }
    return v;
}

// reflection environment: bright from above (aquarium lamp), dark below
float3 BettaEnv(float3 R)
{
    float t = saturate(R.y * 0.5 + 0.5);
    return lerp(_BettaEnvBottom.rgb, _BettaEnvTop.rgb, smoothstep(0.35, 1.0, t));
}

// bump mapping from a scalar height (world units) using screen-space derivatives
float3 BettaBump(float3 posWS, float3 N, float h)
{
    float3 dpdx = ddx(posWS);
    float3 dpdy = ddy(posWS);
    float dhdx = ddx(h);
    float dhdy = ddy(h);
    float3 r1 = cross(dpdy, N);
    float3 r2 = cross(N, dpdx);
    float det = dot(dpdx, r1);
    if (abs(det) < 1e-20) return N;
    float3 grad = sign(det) * (dhdx * r1 + dhdy * r2);
    return normalize(abs(det) * N - grad);
}

// GGX specular distribution
float BettaGGX(float NdotH, float rough)
{
    float a = max(rough * rough, 0.002);
    float a2 = a * a;
    float d = NdotH * NdotH * (a2 - 1.0) + 1.0;
    return a2 / (PI * d * d);
}

// thin-film interference tint (soap-bubble colors) for iridescent scales and fins
float3 BettaThinFilm(float NdotV, float thickness)
{
    float x = thickness / 600.0 + NdotV * 1.5;
    return 0.5 + 0.5 * cos(6.2831 * (float3(0.0, 0.33, 0.67) + x));
}

#endif
