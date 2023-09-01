#ifndef CUSTOM_FOG_INCLUDED
#define CUSTOM_FOG_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

CBUFFER_START(UnityPerMaterial)
    half3 _FogColorDay;
    half3 _FogColorNight;
    float _Density;

    float _HeightFogStart;
    float _HeightFogDensity;
    
    float _DistanceFogMaxLength;
    float _DistanceFogDensity;
    
    half3 _DayScatteringColor;
    half3 _NightScatteringColor;
    float _Scattering;
    float _ScatteringRedWave;
    float _ScatteringGreenWave;
    float _ScatteringBlueWave;
    float _ScatteringMoon;
    float _ScatteringFogDensity;
     
    float _DayScatteringFac;
    float _NightScatteringFac;
    float _gDayMie;
    float _gNightMie;

    float _DynamicFogHeight;
    float _DynamicFogDensity;

    
    float3 _SunDirection;
    float3 _MoonDirection;
CBUFFER_END

struct FogData
{
    half3 fogColor;
    float density;
    float heightFogStart;
    float heightFogDensity;
    float distanceFogMaxLength;
    float distanceFogDensity;
    half3 dayScatteringColor;
    half3 nightScatteringColor;
    float scattering;
    float scatteringRedWave;
    float scatteringGreenWave;
    float scatteringBlueWave;
    float scatteringMoon;
    float scatteringFogDensity;
    float dayScatteringFac;
    float nightScatteringFac;
    float gDayMie;
    float gNightMie;
    float dynamicFogHeight;
    float dynamicFogDensity;
};

FogData GetFogData()
{
    FogData o;
    o.fogColor = _FogColorDay;
    o.density = _Density;
    o.heightFogStart = _HeightFogStart;
    o.heightFogDensity = _HeightFogDensity;
    o.distanceFogMaxLength = _DistanceFogMaxLength;
    o.distanceFogDensity = _DistanceFogDensity;
    o.dayScatteringColor = _DayScatteringColor;
    o.nightScatteringColor = _NightScatteringColor;
    o.scattering = _Scattering;
    o.scatteringRedWave = _ScatteringRedWave;
    o.scatteringGreenWave = _ScatteringGreenWave;
    o.scatteringBlueWave = _ScatteringBlueWave;
    o.scatteringMoon = _ScatteringMoon;
    o.scatteringFogDensity = _ScatteringFogDensity;
    o.dayScatteringFac = _DayScatteringFac;
    o.nightScatteringFac = _NightScatteringFac;
    o.gDayMie = _gDayMie;
    o.gNightMie = _gNightMie;
    o.dynamicFogHeight = _DynamicFogHeight;
    o.dynamicFogDensity = _DynamicFogDensity;

    return o;
}

FogData GetFogData(float3 sunDir)
{
    FogData o = GetFogData();
    float3 fogColor = lerp(_FogColorNight, _FogColorDay, smoothstep(-0.2, 0.2, dot(sunDir, float3(0.0, 1.0, 0.0))));
    o.fogColor = fogColor;

    return o;
}

float GetMiePhaseFunction(float cosTheta, float g)
{
    float g2 = g * g;
    float cos2 = cosTheta * cosTheta;
    float num = 3.0 * (1.0 - g2) * (1.0 + cos2);
    float denom = rcp(8.0 * PI * (2.0 + g2) * pow(abs(1.0 + g2 - 2.0 * g * cosTheta), 1.5));
    return num * denom;
}

half3 GetTransmitColor(FogData fd, half3 sourceColor, float len)
{
    return exp(-pow(abs(fd.scattering), 10.0) * len) * sourceColor;
}

half3 GetHeightFogColor(FogData fd, float3 positionWS, float3 viewPosWS)
{
    return fd.heightFogDensity *
        smoothstep(0.0, abs(viewPosWS.y - fd.heightFogStart), viewPosWS.y - positionWS.y) * fd.fogColor;
}

half3 GetDistanceFogColor(FogData fd, float len)
{
    return fd.distanceFogDensity * smoothstep(0.0, fd.distanceFogMaxLength, len) * fd.fogColor;
}

half3 GetScatteringFogColor(FogData fd, float3 sunDir, float3 moonDir, float3 viewDir, float len)
{
    float sunCos = dot(viewDir, -sunDir);
    float moonCos = dot(viewDir, -moonDir);
    float sunMiePhase = GetMiePhaseFunction(sunCos, fd.gDayMie);
    float moonMiePhase = GetMiePhaseFunction(moonCos, fd.gNightMie);
    float3 coefSun = pow(
        float3(fd.scatteringRedWave, fd.scatteringGreenWave, fd.scatteringBlueWave) * fd.scattering, 10.0);
    float3 coefMoon = pow(fd.scatteringMoon, 10.0);
    half3 sunScattering = sunMiePhase * fd.dayScatteringColor * (1.0 - exp(-coefSun * len));
    half3 moonScattering = moonMiePhase * fd.nightScatteringColor * (1.0 - exp(-coefMoon * len));
    sunScattering *= smoothstep(-0.4, 0.0, dot(sunDir, float3(0.0, 1.0, 0.0)));
    moonScattering *= smoothstep(-0.4, 0.0, dot(moonDir, float3(0.0, 1.0, 0.0)));

    return fd.scatteringFogDensity * (fd.dayScatteringFac * sunScattering + fd.nightScatteringFac * moonScattering);
}

float2 Hash(float2 st, int seed)
{
    float2 s = float2(dot(st, float2(127.1, 311.7)) + seed, dot(st, float2(269.5, 183.3)) + seed);
    return -1 + 2 * frac(sin(s) * 43758.5453123);
}

float RandomNoise(float2 st, int seed)
{
    st.x -= _Time.y;

    float2 p = floor(st);
    float2 f = frac(st);
 
    float w00 = dot(Hash(p, seed), f);
    float w10 = dot(Hash(p + float2(1, 0), seed), f - float2(1, 0));
    float w01 = dot(Hash(p + float2(0, 1), seed), f - float2(0, 1));
    float w11 = dot(Hash(p + float2(1, 1), seed), f - float2(1, 1));
				
    float2 u = f * f * (3 - 2 * f);
 
    return lerp(lerp(w00, w10, u.x), lerp(w01, w11, u.x), u.y);
}

half3 GetDynamicFogColor(FogData fd, float3 positionWS, float3 viewPosWS, float2 baseUV)
{
    float noise = RandomNoise(baseUV, 3);
    return fd.fogColor * smoothstep(0.0, 2.0 * fd.dynamicFogHeight,
        (viewPosWS.y + fd.dynamicFogHeight - positionWS.y)) * noise * fd.dynamicFogDensity;
}

half3 GetFogColor(FogData fd, float3 sourceColor, float3 viewDirWS, float3 positionWS, float3 viewPosWS,
    float2 screenUV)
{
    // TODO: Transmit Color
    float len = length(positionWS - viewPosWS);
    half3 excintion = GetTransmitColor(fd, sourceColor, len);
    
    // TODO: Get Height Fog Color
    half3 heightFog = GetHeightFogColor(fd, positionWS, viewPosWS);

    // TODO: Get Distance Fog Color
    half3 distanceFog = GetDistanceFogColor(fd, len);

    // TODO: Get Scattering Fog Color
    half3 inScattering = GetScatteringFogColor(fd, _SunDirection, _MoonDirection, viewDirWS, len);

    half3 dynamicFog = GetDynamicFogColor(fd, positionWS, viewPosWS, screenUV);

    return fd.density * (excintion + heightFog + distanceFog + inScattering + dynamicFog);
}

half3 GetFogColor(FogData fd, float3 sourceColor, float2 positionNDC, float deviceDepth, float2 screenUV)
{
    float3 positioWS = ComputeWorldSpacePosition(positionNDC, deviceDepth, unity_MatrixInvVP);
    float3 viewPosWS = GetCameraPositionWS();
    float3 viewDirWS = normalize(viewPosWS - positioWS);

    return GetFogColor(fd, sourceColor, viewDirWS, positioWS, viewPosWS, screenUV);
}

#endif