#ifndef CUSTOM_GRASS_INPUT_INCLUDED
#define CUSTOM_GRASS_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#include "CustomLighting.hlsl"

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

CBUFFER_START(UnityPerMaterial)
half3 _TopColor;
half3 _RootColor;
half3 _ReflectColor;
float _WindDirection;
float _WindFrequency;
float _WindIntensity;
CBUFFER_END

float _ArgsOffset;
float4x4 _InteractionMatrixVP0;
float4x4 _InteractionMatrixVP1;
float4x4 _InteractionMatrixVP2;
float4x4 _InteractionMatrixVP3;
float _RecordDistance0;
float _RecordTextureSize0;

TEXTURE2D(_WindFieldTexture0);
SAMPLER(sampler_WindFieldTexture0);
TEXTURE2D(_WindFieldTexture1);
SAMPLER(sampler_WindFieldTexture1);
TEXTURE2D(_WindFieldTexture2);
SAMPLER(sampler_WindFieldTexture2);
TEXTURE2D(_WindFieldTexture3);
SAMPLER(sampler_WindFieldTexture3);
TEXTURE2D(_SDFTexture0);
SAMPLER(sampler_SDFTexture0);
TEXTURE2D(_SDFTexture1);
SAMPLER(sampler_SDFTexture1);
TEXTURE2D(_SDFTexture2);
SAMPLER(sampler_SDFTexture2);
TEXTURE2D(_SDFTexture3);
SAMPLER(sampler_SDFTexture3);

StructuredBuffer<float3> _DetailsPositionsBuffer;
StructuredBuffer<float4> _DetailsTransformsBuffer;
StructuredBuffer<float4> _DetailsColorsBuffer;
StructuredBuffer<float4> _SHArBuffer;
StructuredBuffer<float4> _SHAgBuffer;
StructuredBuffer<float4> _SHAbBuffer;
StructuredBuffer<float4> _SHBrBuffer;
StructuredBuffer<float4> _SHBgBuffer;
StructuredBuffer<float4> _SHBbBuffer;
StructuredBuffer<float4> _SHCBuffer;
StructuredBuffer<float4> _SHOcclusionProbesBuffer;
StructuredBuffer<uint4> _IndicesDistancesTypesLodsBuffer;
StructuredBuffer<uint> _DrawIndirectArgs;

#endif
