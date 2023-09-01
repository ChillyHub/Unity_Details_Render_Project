#ifndef CUSTOM_GRASS_FORWARD_PASS_INCLUDED
#define CUSTOM_GRASS_FORWARD_PASS_INCLUDED

#include "DetailsInput.hlsl"
#include "GrassMotion.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    uint instanceID : SV_InstanceID;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float3 color : TEXCOORD2;
    float instanceID : TEXCOORD3;
    float fogFactor : TEXCOORD4;
    float3 vertexSH : TEXCOORD5;
    //#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord : TEXCOORD6;
    //#endif
    float distance : TEXCOORD7;
};

half3 SampleSHInstance(uint index, half3 normalWS)
{
    real4 SHCoefficients[7];
    SHCoefficients[0] = _SHArBuffer[index];
    SHCoefficients[1] = _SHAgBuffer[index];
    SHCoefficients[2] = _SHAbBuffer[index];
    SHCoefficients[3] = _SHBrBuffer[index];
    SHCoefficients[4] = _SHBgBuffer[index];
    SHCoefficients[5] = _SHBbBuffer[index];
    SHCoefficients[6] = _SHCBuffer[index];

    return max(half3(0, 0, 0), SampleSH9(SHCoefficients, normalWS));
}

float Mask(float sdf, float2 currUV)
{
    float2 a = 0.5 - abs(currUV - 0.5);
    float weight = saturate(min(a.x, a.y) * 10.0);
    return lerp(FLT_MAX, sdf, weight);
}

float2 Mask(float2 finalWindDir, float2 currUV)
{
    float2 a = 0.5 - abs(currUV - 0.5);
    float weight = saturate(min(a.x, a.y) * 10.0);
    return lerp(float2(0.0, 0.0), finalWindDir, weight);
}

Varyings GrassPassVertex(Attributes input)
{
    Varyings output;

    // Get instance data
    uint instanceID = input.instanceID + _DrawIndirectArgs[_ArgsOffset + 4];
    uint index = _IndicesDistancesTypesLodsBuffer[instanceID].x;
    float3 positionWS = _DetailsPositionsBuffer[index];

    uint dist = _IndicesDistancesTypesLodsBuffer[instanceID].y;
    output.distance = (float)dist / 256.0f;

    float4 transform = _DetailsTransformsBuffer[index];
    float3 scale = float3(transform.x, transform.y, transform.x);
    float rot = transform.z;

    float4x4 scaleMat = float4x4
    (
        scale.x,     0.0,     0.0, 0.0, 
            0.0, scale.y,     0.0, 0.0, 
            0.0,     0.0, scale.z, 0.0,
            0.0,     0.0,     0.0, 1.0
    );
    float4x4 rotateMat = float4x4
    (
         cos(rot), 0.0, sin(rot), 0.0, 
              0.0, 1.0,      0.0, 0.0, 
        -sin(rot), 0.0, cos(rot), 0.0,
              0.0, 0.0,      0.0, 1.0
    );
    float4x4 translateMat = float4x4
    (
        1.0, 0.0, 0.0, positionWS.x, 
        0.0, 1.0, 0.0, positionWS.y, 
        0.0, 0.0, 1.0, positionWS.z,
        0.0, 0.0, 0.0, 1.0
    );

    float4x4 objectToWorld = mul(translateMat, mul(rotateMat, scaleMat));
    float4x4 worldToObject = Inverse(objectToWorld);
    
    unity_SHAr = _SHArBuffer[index];
    unity_SHAg = _SHAgBuffer[index];
    unity_SHAb = _SHAbBuffer[index];
    unity_SHBr = _SHBrBuffer[index];
    unity_SHBg = _SHBgBuffer[index];
    unity_SHBb = _SHBbBuffer[index];
    unity_SHC  = _SHCBuffer[index];
    unity_ProbesOcclusion = _SHOcclusionProbesBuffer[index];

    // output.positionWS = input.positionOS.xyz;
    // output.positionWS *= scale;
    // output.positionWS += positionWS;
    output.positionWS = mul(objectToWorld, float4(input.positionOS.xyz, 1.0)).xyz;

    float4 interactCS0 = mul(_InteractionMatrixVP0, float4(output.positionWS, 1.0));
    float2 interactUV0 = interactCS0.xy * 0.5 + 0.5;
    float2 windDirection = SAMPLE_TEXTURE2D_LOD(_WindFieldTexture0, sampler_WindFieldTexture0, interactUV0, 0).xy;
    windDirection = Mask(windDirection, interactUV0);

    Wind wind;
    float3 defaultDir = float3(cos(_WindDirection * PI / 180.0), 0.0, sin(_WindDirection * PI / 180.0));
    float3 fieldDir = float3(windDirection.x, 0.0, windDirection.y);
    // wind.direction = float3(cos(_WindDirection * PI / 180.0), 0.0, sin(_WindDirection * PI / 180.0));
    wind.direction = lerp(defaultDir, fieldDir, step(0.00, Length2(fieldDir)));
    wind.frequency = float4(1.0, 2.0, 3.0, 4.0) * 0.1 * _WindFrequency;
    wind.phase = float4(0.2, 0.4, 0.6, 0.8) + positionWS.x + positionWS.z;
    wind.amplitude = float4(0.2, 0.6, 0.4, 1.0) * 0.6;
    wind.intensity = _WindIntensity;
    
    output.positionWS = MainBending(output.positionWS, positionWS, wind, _Time.y, 0.0, 2.0);

    //float meterPerResolution = 2.0 * _RecordDistance0 * rcp(_RecordTextureSize0);
    //float uvPerResolution = 1.0 * rcp(_RecordTextureSize0);
    //float2 uv1 = float2(uvPerResolution, 0.0);
    //float2 uv2 = float2(0.0, uvPerResolution);
    //float sdf = SAMPLE_TEXTURE2D_LOD(_SDFTexture0, sampler_SDFTexture0, interactUV0, 0).r;
    //float sdfDx = SAMPLE_TEXTURE2D_LOD(_SDFTexture0, sampler_SDFTexture0, interactUV0 + uv1, 0).r;
    //float sdfDy = SAMPLE_TEXTURE2D_LOD(_SDFTexture0, sampler_SDFTexture0, interactUV0 + uv2, 0).r;
    //sdf = Mask(sdf, interactUV0);
    //sdfDx = Mask(sdfDx, interactUV0 + uv1);
    //sdfDy = Mask(sdfDy, interactUV0 + uv2);
    //float2 sdfDxy = float2(sdfDx - sdf, sdfDy - sdf);

    //output.positionWS = ExtrusionBending(output.positionWS, positionWS, sdf, sdfDxy, meterPerResolution);
    
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.normalWS = SafeNormalize(mul(input.normalOS, (float3x3)worldToObject));

    float3 N = float3(0.0, 1.0, 0.0);
    float3 V = normalize(GetCameraPositionWS() - output.positionWS);
    float3 albedo = _TopColor + (_ReflectColor - _TopColor) * pow(1.0 - abs(dot(N, V)), 5.0);;
    albedo = lerp(_RootColor, albedo, input.positionOS.y);
    output.color = albedo;
    output.instanceID = input.instanceID;

    output.fogFactor = ComputeFogFactor(output.positionCS.z);
    
    // OUTPUT_SH(output.normalWS.xyz, output.vertexSH);
    output.vertexSH = SampleSH(output.normalWS);// SampleSHVertex(output.normalWS);

    VertexPositionInputs vi = (VertexPositionInputs)0;
    vi.positionWS = output.positionWS;
    vi.positionCS = output.positionCS;
    output.shadowCoord = GetShadowCoord(vi);

    return output;
}

half4 GrassPassFragment(Varyings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
{
    InputData inputData = (InputData)0;

    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.normalWS = IS_FRONT_VFACE(facing, input.normalWS, -input.normalWS);
    inputData.normalWS = lerp(inputData.normalWS, float3(0.0, 1.0, 0.0), pow(saturate(input.distance * 6.0), 0.5));
    inputData.viewDirectionWS = GetCameraPositionWS() - input.positionWS;

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
    #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
    #else
    inputData.shadowCoord = float4(0, 0, 0, 0);
    #endif

    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
    //inputData.vertexLighting;
    //inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.bakedGI = input.vertexSH; // SampleSH(inputData.normalWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input);
    //inputData.tangentToWorld;
    
    
    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = input.color;
    surfaceData.specular = 0.0f;
    surfaceData.metallic = 0.0f;
    surfaceData.smoothness = 0.0f;
    surfaceData.normalTS = half3(0.0, 1.0, 0.0);
    surfaceData.emission = 0.0f;
    surfaceData.occlusion = 1.0f;
    surfaceData.alpha = 1.0f;
    surfaceData.clearCoatMask = half(0.0);
    surfaceData.clearCoatSmoothness = half(0.0);

    half4 color = SimpleFragmentPBR(inputData, surfaceData);

    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = 1.0f;

    // color.rgb = input.vertexSH;

    // color.rgb = unity_SHAr;// SampleSH(inputData.normalWS);

    return color;

    // half dist = (float)_GrassDistancesIndicesBuffer[input.instanceID].x / 256;
    // return half4(dist, dist, dist, 1.0);
    
    float var = input.instanceID / 20000.0;
    return half4(var, var, var, 1.0);
    input.positionWS.y += 0.2;
    return half4(input.positionWS, 1.0);
}

#endif
