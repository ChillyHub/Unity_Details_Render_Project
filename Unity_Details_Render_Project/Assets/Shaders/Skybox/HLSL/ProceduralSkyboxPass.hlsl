#ifndef PROCEDURAL_SKYBOX_PASS_INCLUDED
#define PROCEDURAL_SKYBOX_PASS_INCLUDED

#include "../../ShaderLibrary/Utility.hlsl"
#include "../../ShaderLibrary/Atmosphere.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 baseUV : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 viewPosWS : TEXCOORD1;
    float3 baseUV : TEXCOORD2;
    UNITY_VERTEX_OUTPUT_STEREO
};

float3 GetBaseSkyboxColor(PerMaterial d, float3 sunDir, float3 moonDir, float3 viewDir, float3 uv)
{
    half3 horDayColor = lerp(d.sunColor, d.horizDayColor, smoothstep(0.0, 0.4, sunDir.y));
    half3 horNightColor = d.horizNightColor;
    half3 dayColor = lerp(d.horizDayColor, d.dayColor, smoothstep(0.0, 0.6, abs(uv.y)));
    half3 nightColor = lerp(d.horizNightColor, d.nightColor, smoothstep(0.0, 0.6, abs(uv.y)));
    dayColor = lerp(dayColor, horDayColor, smoothstep(0.2, 0.0, abs(uv.y)) * saturate(dot(-sunDir, viewDir)));
    nightColor = lerp(nightColor, horNightColor, smoothstep(0.2, 0.0, abs(uv.y)) * saturate(dot(-moonDir, viewDir)));
    return lerp(nightColor, dayColor, smoothstep(-0.5, 0.5, dot(sunDir, float3(0.0, 1.0, 0.0))));
}

float3 GetMieScatteringColor(PerMaterial d, float3 lightDir, float3 moonDir, float3 viewDir, float clamp = 0.0)
{
    float tSIn, tSOut, tMIn, tMOut;
    GetSphereIntersection(tSIn, tSOut,
        lightDir, float3(0.0, 0.0, 0.0), float3(0.0, -d.radius, 0.0), d.radius + d.thickness);
    GetSphereIntersection(tMIn, tMOut,
        moonDir, float3(0.0, 0.0, 0.0), float3(0.0, -d.radius, 0.0), d.radius + d.thickness);

    float lenSun = tSOut;
    float lenMoon = tMOut;
    float cosThetaSun = dot(-lightDir, viewDir);
    float cosThetaMoon = dot(-moonDir, viewDir);
    float3 coefSun = pow(float3(d.scatteringRedWave, d.scatteringGreenWave, d.scatteringBlueWave) * d.scattering, 10.0);
    float3 coefMoon = pow(float3(d.scatteringMoon, d.scatteringMoon, d.scatteringMoon), 10.0);
    half3 scatteringSun = d.sunColor * GetMiePhaseFunction(cosThetaSun, d.gDayMie) * (1.0 - exp(-coefSun * lenSun));
    half3 scatteringMoon = d.moonColor * GetMiePhaseFunction(cosThetaMoon, d.gNightMie) * (1.0 - exp(-coefMoon * lenMoon));

    half3 finalColor = scatteringSun * d.dayScatteringFac + scatteringMoon * d.nightScatteringFac;
    
    UNITY_BRANCH
    if (clamp > 0.1)
    {
        finalColor = min(finalColor, lerp(d.sunColor, d.moonColor, d.isNight));
    }
    return finalColor;
}

float3 GetMieScatteringRayMarching(PerMaterial d, float3 lightDir, float3 viewDir, float clamp = 0.0)
{
    UNITY_BRANCH
    if (clamp > 0.1)
    {
        return d.sunColor;
    }
    
    float tVIn, tVOut;
    GetSphereIntersection(tVIn, tVOut,
        -viewDir, float3(0.0, 0.0, 0.0), float3(0.0, -d.radius, 0.0), d.radius + d.thickness);

    const float sampleCounts = 30.0;
    float stepLen = tVOut / sampleCounts;
    float viewD = stepLen * 0.5;
    float cosTheta = dot(-lightDir, viewDir);
    float gMie = lerp(d.gDayMie, d.gNightMie, d.isNight);
    float scatteringFac = lerp(d.dayScatteringFac, d.nightScatteringFac, d.isNight);
    float3 coef = pow(float3(d.scatteringRedWave, d.scatteringGreenWave, d.scatteringBlueWave) * d.scattering, 10.0);

    float3 currPos = -viewDir * stepLen * 0.5;
    float3 scattering = float3(0.0, 0.0, 0.0);

    UNITY_LOOP
    for (float i = 0.0; i < sampleCounts; ++i)
    {
        float tLIn, tLOut;
        GetSphereIntersection(tLIn, tLOut, lightDir, currPos, float3(0.0, -d.radius, 0.0), d.radius + d.thickness);

        float lightLen = tLOut;
        scattering += exp(-coef * (viewD + lightLen)) * stepLen;

        currPos += -viewDir * stepLen;
        viewD += stepLen;
    }

    scattering *= d.sunColor * coef * GetMiePhaseFunction(cosTheta, gMie);
    
    half3 finalColor = scattering * scatteringFac;
    
    UNITY_BRANCH
    if (clamp > 0.1)
    {
        finalColor = min(finalColor, lerp(d.sunColor, d.moonColor, d.isNight) * 0.5);
    }
    return finalColor;
}

float3 DrawSun(PerMaterial d, float3 lightDir, float3 viewDir, float clamp = 0.0)
{
    UNITY_BRANCH
    if (clamp > 0.1)
    {
        return float3(0.0, 0.0, 0.0);
    }
    
    float visual = smoothstep(-0.01, 0.0, dot(viewDir, float3(0.0, -1.0, 0.0)));
    return d.sunColor * GetMiePhaseFunction(dot(-lightDir, viewDir), d.gSun) * visual;
}

float3 DrawMoon(PerMaterial d, float3 moonDir, float3 viewDir, float clamp = 0.0)
{
    UNITY_BRANCH
    if (clamp > 0.1)
    {
        return float3(0.0, 0.0, 0.0);
    }

    float cosT = dot(-moonDir, viewDir);
    float cosC = cos(0.08);
    float sinC = sin(0.08);

    UNITY_BRANCH
    if (cosT < cosC)
    {
        return float3(0.0, 0.0, 0.0);
    }
    
    float3 rightDir = cross(-moonDir, float3(0.0, 1.0, 0.0));
    float3 upDir = cross(rightDir, -moonDir);
    float3 frontDir = cross(rightDir, upDir);

    float3x3 WToL = float3x3(rightDir, upDir, frontDir);
    float3 viewDirLS = mul(WToL, viewDir);

    float u = -asin(viewDirLS.x) / (sinC * PI);
    float v = -viewDirLS.y / sinC;
    float2 uvColor = float2(u, v);
    float2 uvAlpha = viewDirLS.xy / sinC * 0.5 + 0.5;

    float3 color = SampleMoonDiffuseTexture(uvColor);
    float alpha = SampleMoonAlphaTexture(uvAlpha);
    return d.moonColor * color * alpha;
}

float3 DrawClouds(PerMaterial d, float3 sunDir, float3 moonDir, float3 uv, out half cloudClamp)
{
    float u = atan2(uv.x, uv.z) * INV_TWO_PI + 0.5;
    float v = asin(uv.y) * INV_HALF_PI;
    float2 uvAtlas1 = float2(frac(u * 2.0 + _Time.y * d.cloudsSpeed * 0.01), saturate((v - 0.02) * 6.0));
    half4 clouds1 = SampleCloudsAtlas1Texture(uvAtlas1);

    float2 uvNoise = float2(frac(u * 16.0) + _Time.y * d.cloudsSpeed * 0.4, saturate((v - 0.02) * 6.0));
    half noise = SampleCloudsNoiseTexture(uvNoise);

    half light = clouds1.r;
    half edge = clouds1.g * (saturate(dot(sunDir, uv)) + saturate(dot(moonDir, uv)));
    half display = step(d.cloudsThreshold + noise * 0.05, clouds1.b);
    half clamp = clouds1.a;

    half3 colorAtlas1 = (d.cloudsColor * light + edge * d.sunColor) * display * clamp * 0.6;
    cloudClamp = clamp * display;

    half panoramic =
        SampleCloudsPanoramicTexture(float2(frac(u + _Time.y * d.cloudsSpeed * 0.001), saturate((v - 0.02) * 3.0)));

    half3 color = colorAtlas1 +
        panoramic * (1.0 - cloudClamp) * smoothstep(-0.5, 0.5, dot(sunDir, float3(0.0, 1.0, 0.0)));

    return color;
}

float3 DrawStars(PerMaterial d, float3 uv, float clamp = 0.0)
{
    UNITY_BRANCH
    if (clamp > 0.1)
    {
        return float3(0.0, 0.0, 0.0);
    }
    
    half3 starsColor = SampleStarsTexture(uv).ggg;
    float theta = _Time.y * d.starsFlashSpeed * 0.25;
    float x = uv.x * cos(theta) - uv.z * sin(theta);
    float z = uv.z * cos(theta) + uv.x * sin(theta);
    half starsNoise = SampleStarsNoiseTexture(float3(x, uv.y, z));

    return starsColor * starsNoise;
}

Varyings ProceduralSkyboxPassVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.viewPosWS = GetCameraPositionWS();
    output.baseUV = input.baseUV;

    return output;
}

float4 ProceduralSkyboxPassFragment(Varyings input) : SV_Target
{
    PerMaterial data = GetPerMaterial();
    float3 viewDir = normalize(input.viewPosWS - input.positionWS);
    float3 sunDir = _SunDir;
    float3 moonDir = _MoonDir;

    // TODO: Get Base Skybox Color
    float3 base = GetBaseSkyboxColor(data, sunDir, moonDir, viewDir, input.baseUV);

    // TODO: Draw Clouds and Stars
    float clamp;
    float3 cloud = DrawClouds(data, sunDir, moonDir, input.baseUV, clamp);
    float3 star = DrawStars(data, input.baseUV, clamp);

    // TODO: Get Mie Scattering Color
    //float3 mie = GetMieScatteringRayMarching(data, lightDir, viewDir);
    float3 mie = GetMieScatteringColor(data, sunDir, moonDir, viewDir, clamp);

    // TODO: Draw Sun or Moon
    float3 sun = DrawSun(data, sunDir, viewDir, clamp);
    float3 moon = DrawMoon(data, moonDir, viewDir, clamp);
    
    float3 color = base + mie + sun + moon + cloud + star;
    color = base + mie + sun + moon + cloud + star;
    
    return float4(color * data.exposure, 1.0);
}

#endif
