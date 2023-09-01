Shader "Hidden/Custom/Interaction/PlaneWindField"
{
	Properties
	{
		
	}
	SubShader
	{
		Cull Back
		ZWrite Off
		ZTest Always

		Pass
		{
			Name "Plane Wind Field Pass"
			
			HLSLPROGRAM

			#pragma target 2.0

			#pragma vertex PlaneWindFieldVertex
			#pragma fragment PlaneWindFieldFragment

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

			TEXTURE2D(_HistoryWindFieldTexture);
			SAMPLER(sampler_HistoryWindFieldTexture);
			TEXTURE2D(_DepthMotionRecordTexture);
			SAMPLER(sampler_DepthMotionRecordTexture);
			TEXTURE2D(_SDFTexture);
			SAMPLER(sampler_SDFTexture);

			float4x4 _CurrMatrixVP;
			float4x4 _PrevMatrixVP;
			float2 _OffsetUV;

			float2 _CurrWindDirection;
			float _BlankingSpeed;
			float _RecordDistance;
			float _TextureSize;

			struct Attributes
			{
			    float4 positionOS : POSITION;
			    float2 baseUV     : TEXCOORD0;
			    UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct Varyings
			{
			    float4 positionCS : SV_POSITION;
				float2 currUV     : TEXCOORD0;
				float2 prevUV     : TEXCOORD1;
			    UNITY_VERTEX_OUTPUT_STEREO
			};

			float4 GetVertexPositionNDC(float4 positionCS)
			{
				float4 positionNDC;
				float4 ndc = positionCS * 0.5f;
				positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
				positionNDC.zw = positionCS.zw;
			
				return positionNDC;
			}

			Varyings PlaneWindFieldVertex(Attributes input)
			{
			    Varyings output;
			    UNITY_SETUP_INSTANCE_ID(input);
			    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
				
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				output.currUV = input.baseUV;

				float4 prevCS = mul(_PrevMatrixVP, float4(0.0, 0.0, 0.0, 1.0));
				float4 currCS = mul(_CurrMatrixVP, float4(0.0, 0.0, 0.0, 1.0));
				float2 offset = currCS.xy - prevCS.xy;

				#ifndef UNITY_UV_STARTS_AT_TOP
					_OffsetUV.y = 1.0 - _OffsetUV.y;
				#endif
				
				output.prevUV = input.baseUV + _OffsetUV;

			    return output;
			}

			float4 PlaneWindFieldFragment(Varyings input) : SV_Target
			{
				float2 currUV = input.currUV;
				float2 prevUV = input.prevUV;
				float weight = _BlankingSpeed;

				float4 recordDepthMotion = SAMPLE_TEXTURE2D(_DepthMotionRecordTexture, sampler_DepthMotionRecordTexture, currUV);
				float2 historyWindField = SAMPLE_TEXTURE2D(_HistoryWindFieldTexture, sampler_HistoryWindFieldTexture, prevUV).xy;
				float sdf = SAMPLE_TEXTURE2D(_SDFTexture, sampler_SDFTexture, currUV).r;

				if (AnyIsNaN(historyWindField))
				{
					historyWindField = float2(0.0, 0.0);
				}

				float velocity = (recordDepthMotion.xy * _RecordDistance * 2.0) * unity_DeltaTime.y;
				float resolutionPerMeter = _TextureSize * rcp(_RecordDistance * 2.0);
				float meterPerResolution = 2.0 * _RecordDistance * rcp(_TextureSize);
				float threshold = smoothstep(0.0, 0.2, sdf * meterPerResolution);
				
				float2 recordWindDir = lerp(float2(0.0, 0.0), velocity * 2.0, 1.0 - threshold);
				float2 prevWindDir = historyWindField.xy;
				float2 currWindDir = lerp(float2(0.0, 0.0), _CurrWindDirection, threshold) + recordWindDir;
				float2 finalWindDir = lerp(prevWindDir, currWindDir, weight);

				if (AnyIsNaN(finalWindDir))
				{
					finalWindDir = float2(0.0, 0.0);
				}

				return float4(finalWindDir, 0.0, 0.0);
				return float4((input.currUV - input.prevUV) * 10000.0, 0.0, 0.0);
			}

			ENDHLSL
		}
	}
}