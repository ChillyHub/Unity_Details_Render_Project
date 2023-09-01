#ifndef CUSTOM_GRASS_MOTION_INCLUDED
#define CUSTOM_GRASS_MOTION_INCLUDED

struct Wind
{
    float3 direction;
    float4 frequency;
    float4 phase;
    float4 amplitude;
    float intensity;
};

// GPU Gems3 Chapter 16
float4 SmoothCurve(float4 x)
{
    return x * x * ( 3.0 - 2.0 * x );
}

float4 TriangleWave(float4 x)
{
    return abs(frac(x + 0.5) * 2.0 - 1.0 );
}

float4 SmoothTriangleWave(float4 x)
{
    return SmoothCurve(TriangleWave(x));
}

float3 MainBending(float3 vertexWS, float3 objectWS, Wind wind, float time, float bendingMinHeight = 0.0, float bendingMaxHeight = 1.0)
{
    float intensity = smoothstep(bendingMinHeight, bendingMaxHeight, vertexWS.y - objectWS.y);

    float4 waveInput = time * wind.frequency + wind.phase;
    float4 waveOutput = SmoothTriangleWave(waveInput) * wind.amplitude * intensity;

    float wave = (waveOutput.x + waveOutput.y + waveOutput.z + waveOutput.w) * wind.intensity;
    float3 offset = wind.direction * wave;
    float3 stretchVertexWS = vertexWS + offset;
    float3 originWS = float3(vertexWS.x, objectWS.y, vertexWS.z);
    float actualLen = length(vertexWS - originWS);
    float3 actualVertexWS = normalize(stretchVertexWS - originWS) * actualLen + originWS;

    return actualVertexWS;
}

float3 DetailBending(float3 positionWS)
{
    return float3(0.0, 0.0, 0.0);
}

float3 ExtrusionBending(float3 vertexWS, float3 objectWS, float sdf, float2 sdfDxy, float meterPerResolution)
{
    if (sdf > 3.0)
    {
        return vertexWS;
    }
    
    float2 offsetXZ = normalize(sdfDxy) * -sdf * meterPerResolution + 0.02;
    float3 offset = float3(offsetXZ.x, 0.0, offsetXZ.y);
    float3 stretchVertexWS = vertexWS + offset;
    float3 originWS = float3(vertexWS.x, objectWS.y, vertexWS.z);
    float actualLen = length(vertexWS - originWS);
    float3 actualVertexWS = normalize(stretchVertexWS - originWS) * actualLen + originWS;

    return actualVertexWS;
}

#endif
