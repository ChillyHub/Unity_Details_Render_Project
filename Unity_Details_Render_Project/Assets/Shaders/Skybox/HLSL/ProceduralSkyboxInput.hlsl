#ifndef CUSTOM_PROCEDURAL_SKYBOX_INPUT_INCLUDED
#define CUSTOM_PROCEDURAL_SKYBOX_INPUT_INCLUDED

#include "../../ShaderLibrary/Utility.hlsl"

TEXTURE2D(_MoonDiffuse);
SAMPLER(sampler_MoonDiffuse);
TEXTURE2D(_MoonAlpha);
SAMPLER(sampler_MoonAlpha);
TEXTURE2D(_CloudsAtlas1);
SAMPLER(sampler_CloudsAtlas1);
TEXTURE2D(_CloudsAtlas2);
SAMPLER(sampler_CloudsAtlas2);
TEXTURE2D(_CloudsAtlas3);
SAMPLER(sampler_CloudsAtlas3);
TEXTURE2D(_CloudsPanoramic);
SAMPLER(sampler_CloudsPanoramic);
TEXTURE2D(_CloudsNoise);
SAMPLER(sampler_CloudsNoise);
TEXTURECUBE(_StarsTex);
SAMPLER(sampler_StarsTex);
TEXTURECUBE(_StarsNoise);
SAMPLER(sampler_StarsNoise);

CBUFFER_START(UnityPerMaterial)
    half3 _SunColor;
    half3 _DayColor;
    half3 _HorizDayColor;
    half3 _NightColor;
    half3 _HorizNightColor;
    half3 _MoonColor;
    float _Scattering;
    float _ScatteringRedWave;
    float _ScatteringGreenWave;
    float _ScatteringBlueWave;
    float _ScatteringMoon;
    float _Exposure;
    
    float _dayScatteringFac;
    float _nightScatteringFac;
    float _gDayMie;
    float _gNightMie;
    float _gSun;

    float _SkyTime;

    half3 _CloudsColor;
    float _CloudsSpeed;
    float _CloudsThreshold;

    float _StarsFlashSpeed;

    float3 _SunDir;
    float3 _MoonDir;
CBUFFER_END

const static float sThickness = 10000.0;
const static float sRadius = 75000.0;

struct PerMaterial
{
    half3 sunColor;
    half3 dayColor;
    half3 horizDayColor;
    half3 nightColor;
    half3 horizNightColor;
    half3 moonColor;
    float scattering;
    float scatteringRedWave;
    float scatteringGreenWave;
    float scatteringBlueWave;
    float scatteringMoon;
    float exposure;
    
    float dayScatteringFac;
    float nightScatteringFac;
    float gDayMie;
    float gNightMie;
    float gSun;

    float thickness;
    float radius;

    float skyTime;
    float isNight;

    half3 cloudsColor;
    float cloudsSpeed;
    float cloudsThreshold;

    float starsFlashSpeed;
};

PerMaterial GetPerMaterial()
{
    PerMaterial o;
    o.sunColor = _SunColor;
    o.dayColor = _DayColor;
    o.horizDayColor = _HorizDayColor;
    o.nightColor = _NightColor;
    o.horizNightColor = _HorizNightColor;
    o.moonColor = _MoonColor;
    o.scattering = _Scattering;
    o.scatteringRedWave = _ScatteringRedWave;
    o.scatteringGreenWave = _ScatteringGreenWave;
    o.scatteringBlueWave = _ScatteringBlueWave;
    o.scatteringMoon = _ScatteringMoon;
    o.exposure = _Exposure;
    
    o.dayScatteringFac = _dayScatteringFac;
    o.nightScatteringFac = _nightScatteringFac;
    o.gDayMie = _gDayMie;
    o.gNightMie = _gNightMie;
    o.gSun = _gSun;

    o.thickness = sThickness;
    o.radius = sRadius;

    o.skyTime = _SkyTime;
    o.isNight = 0.0;

    o.cloudsColor = _CloudsColor;
    o.cloudsSpeed = _CloudsSpeed;
    o.cloudsThreshold = _CloudsThreshold;

    o.starsFlashSpeed = _StarsFlashSpeed;

    return o;
}

half3 SampleMoonDiffuseTexture(float2 uv)
{
    return SAMPLE_TEXTURE2D(_MoonDiffuse, sampler_MoonDiffuse, uv).aaa;
}

half SampleMoonAlphaTexture(float2 uv)
{
    return SAMPLE_TEXTURE2D(_MoonAlpha, sampler_MoonAlpha, uv).a;
}

half4 SampleCloudsAtlas1Texture(float2 uv)
{
    float u = frac(uv.x * 4.0);
    float v = (floor(uv.x * 4.0) + uv.y) * 0.25;

    return SAMPLE_TEXTURE2D(_CloudsAtlas1, sampler_CloudsAtlas1, float2(u, v));
}

half4 SampleCloudsAtlas2Texture(float2 uv)
{
    float u = frac(uv.x * 4.0);
    float v = (floor(uv.x * 4.0) + uv.y) * 0.25;

    return SAMPLE_TEXTURE2D(_CloudsAtlas2, sampler_CloudsAtlas2, float2(u, v));
}

half4 SampleCloudsAtlas3Texture(float2 uv)
{
    float u = frac(uv.x * 4.0);
    float v = (floor(uv.x * 4.0) + uv.y) * 0.25;

    return SAMPLE_TEXTURE2D(_CloudsAtlas3, sampler_CloudsAtlas3, float2(u, v));
}

half SampleCloudsPanoramicTexture(float2 uv)
{
    return SAMPLE_TEXTURE2D(_CloudsPanoramic, sampler_CloudsPanoramic, uv).a;
}

half SampleCloudsNoiseTexture(float2 uv)
{
    return SAMPLE_TEXTURE2D(_CloudsNoise, sampler_CloudsNoise, uv).r;
}

half2 SampleStarsTexture(float3 uv)
{
    return SAMPLE_TEXTURECUBE(_StarsTex, sampler_StarsTex, uv).rg;
}

half SampleStarsNoiseTexture(float3 uv)
{
    return SAMPLE_TEXTURECUBE(_StarsNoise, sampler_StarsNoise, uv).r;
}

#endif