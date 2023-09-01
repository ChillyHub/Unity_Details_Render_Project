Shader "Hidden/Custom/Interaction/DepthMotionRecord"
{
	Properties
	{
		
	}
	SubShader
	{
		Pass
		{
			Name "Depth Motion Record Pass"
			
			Tags { "LightMode"="MotionVector" }
			
			HLSLPROGRAM

			#pragma target 2.0

			#pragma vertex DepthMotionRecordVertex
			#pragma fragment DepthMotionRecordFragment

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

			float4x4 _PrevMatrixVP;

			struct Attributes
			{
			    float4 positionOS : POSITION;
			    float3 prevPositionOS : TEXCOORD4;
			    UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct Varyings
			{
			    float4 positionCS : SV_POSITION;
				float4 currPositionNDC : TEXCOORD0;
				float4 prevPositionNDC : TEXCOORD1;
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

			Varyings DepthMotionRecordVertex(Attributes input)
			{
			    Varyings output;
			    UNITY_SETUP_INSTANCE_ID(input);
			    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

				float4 prevPositionCS = mul(_PrevMatrixVP, mul(unity_MatrixPreviousM,
					float4(step(0.5, unity_MotionVectorsParams.x) ? input.prevPositionOS : input.positionOS.xyz, 1.0)));

				#if UNITY_REVERSED_Z
                    output.positionCS.z -= unity_MotionVectorsParams.z * output.positionCS.w;
                #else
                    output.positionCS.z += unity_MotionVectorsParams.z * output.positionCS.w;
                #endif
			
				output.currPositionNDC = GetVertexPositionNDC(output.positionCS);
				output.prevPositionNDC = GetVertexPositionNDC(prevPositionCS);

			    return output;
			}

			half4 DepthMotionRecordFragment(Varyings input) : SV_Target
			{
				float2 currUV = input.currPositionNDC.xy / input.currPositionNDC.w;
				float2 prevUV = input.prevPositionNDC.xy / input.prevPositionNDC.w;
				float depth = input.currPositionNDC.z / input.currPositionNDC.w;

				#ifndef  UNITY_REVERSED_Z
					depth = 1.0 - depth;
				#endif
				
			    return half4(currUV - prevUV, depth, 0.0);
			}

			ENDHLSL
		}
	}
}