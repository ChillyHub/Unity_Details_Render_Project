Shader "Custom/Skybox/Procedural Skybox"
{
	Properties
	{
		[Header(Color Setting)][Space]
		[HDR] _SunColor("Sun Color", Color) = (1.0, 1.0, 0.2, 1.0)
		[HDR] _DayColor("Day Color", Color) = (0.4, 0.8, 1.0, 1.0)
		[HDR] _HorizDayColor("Horiz Day Color", Color) = (0.8, 0.9, 1.0, 1.0)
		[HDR] _NightColor("Night Color", Color) = (0.0, 0.1, 0.3, 1.0)
		[HDR] _HorizNightColor("Horiz Night Color", Color) = (0.1, 0.2, 0.3, 1.0)
		[HDR] _MoonColor("Moon Color", Color) = (0.6, 0.8, 1.0, 1.0)
		_Scattering("Scattering Golbal", Range(0.0, 1.0)) = 0.5
		_ScatteringRedWave("Scattering Red Wave", Range(0.0, 1.0)) = 1.0
		_ScatteringGreenWave("Scattering Green Wave", Range(0.0, 1.0)) = 1.0
		_ScatteringBlueWave("Scattering Blue Wave", Range(0.0, 1.0)) = 1.0
		_ScatteringMoon("Scattering Moon", Range(0.0, 1.0)) = 0.5
		_Exposure("Exposure", Range(0.0, 10.0)) = 1.0
		
		[Header(Physic Setting)][Space]
    	_dayScatteringFac("Day Scattering Fac", Range(0.0, 1.0)) = 1.0
    	_nightScatteringFac("Night Scattering Fac", Range(0.0, 1.0)) = 0.5
    	_gDayMie("Day Mie g", Range(0.5, 0.9999)) = 0.75
    	_gNightMie("Night Mie g", Range(0.5, 0.9999)) = 0.75
    	_gSun("Sun Mie g", Range(0.999, 1.0)) = 0.999
		
		[Header(Moon Texture)][Space]
		_MoonDiffuse("Moon Diffuse Texture", 2D) = "black" {}
		_MoonAlpha("Moon Alpha Texture", 2D) = "black" {}
		
		[Header(Cloud Setting)][Space]
		_CloudsAtlas1("Clouds Atlas 1", 2D) = "black" {}
		_CloudsAtlas2("Clouds Atlas 2", 2D) = "black" {}
		_CloudsAtlas3("Clouds Atlas 3", 2D) = "black" {}
		_CloudsPanoramic("Clouds Panoramic", 2D) = "black" {}
		_CloudsNoise("Clouds Noise", 2D) = "black" {}
		[HDR] _CloudsColor("Clouds Color", Color) = (1.0, 1.0, 1.0, 1.0)
		_CloudsSpeed("Clouds Speed", Range(-10.0, 10.0)) = 1.0
		_CloudsThreshold("Clouds Display Threshold", Range(0.0, 1.0)) = 0.0
		
		[Header(Stars Setting)][Space]
		_StarsTex("Stars Texture", Cube) = "black" {}
		_StarsNoise("Stars Noise", Cube) = "black" {}
		_StarsFlashSpeed("Stars Flash Speed", Range(0.0, 1.0)) = 0.5
	}
	SubShader
	{
		Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
		
		Cull Off
		ZWrite Off

		Pass
		{
			HLSLPROGRAM

			#pragma target 4.5
			
			#pragma vertex ProceduralSkyboxPassVertex
			#pragma fragment ProceduralSkyboxPassFragment

			#include "HLSL/ProceduralSkyboxInput.hlsl"
			#include "HLSL/ProceduralSkyboxPass.hlsl"
			
			ENDHLSL
		}
	}
}
