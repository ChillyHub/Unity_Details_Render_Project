Shader "Hidden/Custom/Depth/DepthMipmap"
{
	Properties
	{
		
	}
	SubShader
	{
		Cull Off
		ZWrite Off
		ZTest Always

		Pass
		{
			Name "Depth Mipmap Pass"
			
			HLSLPROGRAM

			#pragma target 2.0

			#pragma vertex DepthMipmapVertex
			#pragma fragment DepthMipmapFragment

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

			float4 _MainTex_TexelSize;

			struct Attributes
			{
			    float4 positionOS : POSITION;
			    float2 baseUV : TEXCOORD0;
			    UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct Varyings
			{
			    float4 positionCS : SV_POSITION;
			    float2 currUV : TEXCOORD0;
			    UNITY_VERTEX_OUTPUT_STEREO
			};

			Varyings DepthMipmapVertex(Attributes input)
			{
			    Varyings output;
			    UNITY_SETUP_INSTANCE_ID(input);
			    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

			    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
			    output.currUV = input.baseUV;

			    return output;
			}

			half4 DepthMipmapFragment(Varyings input) : SV_Target
			{
			    float2 offset = float2(_MainTex_TexelSize.x / 2.0, _MainTex_TexelSize.y / 2.0);

				float depth1 = SAMPLE_DEPTH_TEXTURE(_MainTex, sampler_MainTex, input.currUV + offset * float2(-1.0, -1.0));
				float depth2 = SAMPLE_DEPTH_TEXTURE(_MainTex, sampler_MainTex, input.currUV + offset * float2(-1.0,  1.0));
				float depth3 = SAMPLE_DEPTH_TEXTURE(_MainTex, sampler_MainTex, input.currUV + offset * float2( 1.0, -1.0));
				float depth4 = SAMPLE_DEPTH_TEXTURE(_MainTex, sampler_MainTex, input.currUV + offset * float2( 1.0,  1.0));

				#if UNITY_REVERSED_Z
					return half4(min(min(depth1, depth2), min(depth3, depth4)), 0.0, 0.0, 1.0);
				#else
					return half4(max(max(depth1, depth2), max(depth3, depth4)), 0.0, 0.0, 1.0);
				#endif
			}

			ENDHLSL
		}
	}
}