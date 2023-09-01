#ifndef CUSTOM_SCREEN_SPACE_FOG_PASS_INCLUDED
#define CUSTOM_SCREEN_SPACE_FOG_PASS_INCLUDED

#include "Fog.hlsl"

TEXTURE2D(_SourceTex);
SAMPLER(sampler_SourceTex);
TEXTURE2D(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);

struct Attributes
{
    float4 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 screenUV : VAR_SCREEN_UV;
    float3 viewRay : VAR_VIEW_RAY;
};

Varyings ScreenSpaceFogPassVertex(Attributes input)
{
    Varyings output;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.screenUV = input.baseUV;

    float depth = 1.0;
    #if defined(UNITY_REVERSED_Z)
    depth = 0.0;
    #endif

    // In order to recreate world position from depth map
    float3 positionWS = ComputeWorldSpacePosition(input.baseUV, depth, UNITY_MATRIX_I_VP);
    output.viewRay = positionWS - _WorldSpaceCameraPos.xyz;

    return output;
}

float4 ScreenSpaceFogPassFragment(Varyings input) : SV_Target
{
    float4 rgba = SAMPLE_TEXTURE2D(_SourceTex, sampler_SourceTex, input.screenUV);
    float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, input.screenUV);
    depth = Linear01Depth(depth, _ZBufferParams);

    UNITY_BRANCH
    if (depth > 0.999)
    {
        return rgba;
    }

    Light light = GetMainLight();
    float3 viewPos = _WorldSpaceCameraPos.xyz;
    float3 viewDir = -normalize(input.viewRay);
    float3 positionWS = viewPos + depth * input.viewRay;

    FogData fd = GetFogData(_SunDirection);
    float3 color = GetFogColor(fd, rgba.rgb, viewDir, positionWS, viewPos, input.screenUV);
    
    return float4(color, rgba.a);
}

#endif