#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#define MAX_SHADOW_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_CASCADE_COUNT 4

//Shadow atlas specfic function
TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
//Define a sampler state explictly for sampling the shadow map
//need special way to sample the shadow map
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
	float4x4 _DirectionalShadowMatrices[MAX_SHADOW_DIRECTIONAL_LIGHT_COUNT*MAX_CASCADE_COUNT];
CBUFFER_END

struct DirectionalShadowData {
	float strength;
	int tileIndex;
};
//Sample the shadow atlas taking a position in shadow atlas space as input
//Attenuation determines how shadowed the point is
//NOTICE: THIS FUNCTION ISNT WORKING?! ONLY RETURNS 0!
float SampleDirectionalShadowAtlas(float3 positionSTS) {
	return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}
//Returns the atteuation of the shadows given the data and a surface
float GetDirectionalShadowAttenuation(DirectionalShadowData data, Surface surfaceWS) {
	if (data.strength <= 0.0) return 1.0;
	float3 positionSTS = mul(_DirectionalShadowMatrices[data.tileIndex],
		float4(surfaceWS.position, 1.0)).xyz;
	float shadow = SampleDirectionalShadowAtlas(positionSTS);
	return lerp(1.0,shadow,data.strength);
}

#endif