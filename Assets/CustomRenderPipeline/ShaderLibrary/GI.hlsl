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
//Light map sampling
GI GetGI(float2 lightMapUV) {
	GI gi;
	gi.diffuse = SampleLightMap(lightMapUV);
	return gi;
}

#endif

