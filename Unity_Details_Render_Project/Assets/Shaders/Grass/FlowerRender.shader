Shader "Cutom/Grass/FlowerRenderShader"
{
	Properties
	{
		_MainTex("Main Texture", 2D) = "white" {}
		_WindDirection("Wind Direction", Float) = 0
		_WindFrequency("Wind Frequency", Range(0, 3)) = 1
		_WindIntensity("Wind Intensity", Range(0, 3)) = 1
	}
	SubShader
	{
		Tags { "RenderType"="AlphaTest" "RenderPipline"="UniversalPipeline" }
		LOD 300
		
		AlphaTest Greater 0.1

		Pass
		{
			Name "FlowerForward"
			Tags { "LightMode"="UniversalForward" }
			
			Cull Off
			
			
			HLSLPROGRAM

			#pragma target 4.5

			// Shader stages
			#pragma vertex FlowerPassVertex
			#pragma fragment FlowerPassFragment

			// Material Keywords


			// Universal Pipeline keywords
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _FORWARD_PLUS

			// GPU Instancing
			#pragma multi_compile_instancing


			#include "HLSL/FlowerForwardPass.hlsl"

			ENDHLSL
		}
	}
}