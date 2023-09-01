Shader "Hidden/Custom/PostProcess/Screen Space Fog"
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
			Name "Screen Space Fog Pass"
			
			HLSLPROGRAM

			#pragma target 2.0

			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _SHADOWS_SOFT
			
			#pragma vertex ScreenSpaceFogPassVertex
			#pragma fragment ScreenSpaceFogPassFragment
			
			#include "HLSL/ScreenSpaceFogPass.hlsl"

			ENDHLSL
		}
	}
}