#ifndef CUSTOM_GI_INCLUDED
#define CUSTOM_GI_INCLUDED
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);
//Sturct used when sampling baked light
struct GI {
	float3 diffuse;
};
//Sample the lightmap
float3 SampleLightMap (float2 lightMapUV) {
#if defined(LIGHTMAP_ON)
return SampleSingleLightmap(TEXTURE2D_ARGS(unity_Lightmap, samplerunity_Lightmap)
	,lightMapUV
	, float4(1.0, 1.0, 0.0, 0.0),
	#if defined(UNITY_LIGHTMAP_FULL_HDR)
	false,
	#else
	true,
	#endif
	float4(LIGHTMAP_HDR_MULTIPLIER,LIGHTMAP_HDR_EXPONENT,0.0,0.0)); //Need to pass sampler state args
#else
return 0.0;
#endif
}

float3 SampleLightProbe(Surface surfaceWS) {
	#if defined(LIGHTMAP_ON)
	return 0.0;
	#else
	float4 coefficients[7];
	coefficients[0] = unity_SHAr;
	coefficients[1] = unity_SHAg;
	coefficients[2] = unity_SHAb;
	coefficients[3] = unity_SHBr;
	coefficients[4] = unity_SHBg;
	coefficients[5] = unity_SHBb;
	coefficients[6] = unity_SHC;
	return max(0.0, SampleSH9(coefficients, surfaceWS.normal)); //Do a light calc finding the max coefficent
	#endif
}

//Light map sampling
GI GetGI(float2 lightMapUV, Surface surfaceWS) {
	GI gi;
	gi.diffuse = SampleLightMap(lightMapUV) + SampleLightProbe(surfaceWS);
	return gi;
}

#endif

