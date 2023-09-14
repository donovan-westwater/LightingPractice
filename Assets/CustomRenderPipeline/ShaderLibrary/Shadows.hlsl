#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#if defined(_DIRECTIONAL_PCF3)
#define DIRECTIONAL_FILTER_SAMPLES 4
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
#define DIRECTIONAL_FILTER_SAMPLES 9
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
#define DIRECTIONAL_FILTER_SAMPLES 16
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif
//Sample the other lights
#if defined(_OTHER_PCF3)
#define OTHER_FILTER_SAMPLES 4
#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_OTHER_PCF5)
#define OTHER_FILTER_SAMPLES 9
#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_OTHER_PCF7)
#define OTHER_FILTER_SAMPLES 16
#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_SHADOW_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_SHADOW_OTHER_LIGHT_COUNT 16
#define MAX_CASCADE_COUNT 4

//Shadow atlas specfic function
TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
TEXTURE2D_SHADOW(_OtherShadowAtlas);
//Define a sampler state explictly for sampling the shadow map
//need special way to sample the shadow map
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
	int _CascadeCount;
	float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
	float4 _CascadeData[MAX_CASCADE_COUNT];
	float4x4 _DirectionalShadowMatrices[MAX_SHADOW_DIRECTIONAL_LIGHT_COUNT*MAX_CASCADE_COUNT];
	float4x4 _OtherShadowMatrices[MAX_SHADOW_OTHER_LIGHT_COUNT]; //Meant to access to other Shadow atlas
	float4 _OtherShadowTiles[MAX_SHADOW_OTHER_LIGHT_COUNT]; //tile bias data for other lights
	float4 _ShadowAtlasSize;
	float4 _ShadowDistanceFade;
CBUFFER_END

struct DirectionalShadowData {
	float strength;
	int tileIndex;
	float normalBias;
	int shadowMaskChannel;
};
struct ShadowMask {
	bool always;
	bool distance;
	float4 shadows;
};
struct ShadowData {
	int cascadeIndex;
	float cascadeBlend;
	float strength;
	ShadowMask shadowMask;
};
//shadowmask data for point and spot lights
struct OtherShadowData{
	float strength;
	int tileIndex;
	int shadowMaskChannel;
};

float FadedShadowStrength(float distance, float scale, float fade){
	return saturate((1.0 - distance * scale) * fade); //Smooth transition between shadow and non shadow
}

ShadowData GetShadowData(Surface surfaceWS) {
	ShadowData data;
	data.shadowMask.always = false;
	data.shadowMask.distance = false;
	data.shadowMask.shadows = 1.0;
	data.cascadeBlend = 1.0;
	data.strength = FadedShadowStrength(surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y);
	//Figure out which cascade should be picked to render
	int i;
	for (i = 0; i < _CascadeCount; i++) {
		float4 sphere = _CascadeCullingSpheres[i];
		float distanceSqr = DistanceSquared(surfaceWS.position, sphere.xyz);
		if (distanceSqr < sphere.w){
			float fade = FadedShadowStrength(distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z);
			if (i == _CascadeCount - 1) {
				data.strength *= fade;
			}
			else {
				data.cascadeBlend = fade;
			}
			break;
		}
	}
	if (i == _CascadeCount && _CascadeCount > 0) {
		data.strength = 0.0;
	}
	#if defined(_CASCADE_BLEND_DITHER)
	else if (data.cascadeBlend < surfaceWS.dither) {
		i += 1;
	}
	#endif
	#if !defined(_CASCADE_BLEND_SOFT)
	data.cascadeBlend = 1.0;
	#endif
	data.cascadeIndex = i;
	return data;
}
//Sample the shadow atlas taking a position in shadow atlas space as input
//Attenuation determines how shadowed the point is
float SampleDirectionalShadowAtlas(float3 positionSTS) {
	return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}
//Multi sample and anti alias atlas to get soft shadows
float FilterDirectionalShadow(float3 positionSTS) {
#if defined(DIRECTIONAL_FILTER_SETUP)
	float weights[DIRECTIONAL_FILTER_SAMPLES];
	float2 positions[DIRECTIONAL_FILTER_SAMPLES];
	float4 size = _ShadowAtlasSize.yyxx;
	DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);
	float shadow = 0;
	for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++) {
		shadow += weights[i] * SampleDirectionalShadowAtlas(
			float3(positions[i].xy, positionSTS.z)
		);
	}
	return shadow;
#else
	return SampleDirectionalShadowAtlas(positionSTS);
#endif
}
//Sample other shadow atlas
float SampleOtherShadowAtlas(float3 positionSTS) {
	return SAMPLE_TEXTURE2D_SHADOW(
		_OtherShadowAtlas, SHADOW_SAMPLER, positionSTS
	);
}
//Dither the shadow atlas to prevent glitches
//Multi sample and anti alias atlas to get soft shadows
float FilterOtherShadow(float3 positionSTS) {
#if defined(OTHER_FILTER_SETUP)
	real weights[OTHER_FILTER_SAMPLES];
	real2 positions[OTHER_FILTER_SAMPLES];
	float4 size = _ShadowAtlasSize.wwzz;
	OTHER_FILTER_SETUP(size, positionSTS.xy, weights, positions);
	float shadow = 0;
	for (int i = 0; i < OTHER_FILTER_SAMPLES; i++) {
		shadow += weights[i] * SampleOtherShadowAtlas(
			float3(positions[i].xy, positionSTS.z)
		);
	}
	return shadow;
#else
	return SampleOtherShadowAtlas(positionSTS);
#endif
}
float GetBakedShadow(ShadowMask mask, int channel) {
	float shadow = 1.0;
	if (mask.always || mask.distance) {
		shadow = mask.shadows.r;
		if (channel >= 0) {
			shadow = mask.shadows[channel];
		}
	}
	return shadow;
}
//Handles the case where there is only the shadow mask with no realtime shadows
float GetBakedShadow(ShadowMask mask, int channel, float strength) {
	if (mask.always || mask.distance) {
		return lerp(1.0, GetBakedShadow(mask,channel), strength);
	}
	return 1.0;
}
//Real time shadow function
float GetCascadedShadow(
	DirectionalShadowData directional, ShadowData global, Surface surfaceWS
) {
	float3 normalBias = surfaceWS.interpolatedNormal *
		(directional.normalBias * _CascadeData[global.cascadeIndex].y);
	float3 positionSTS = mul(
		_DirectionalShadowMatrices[directional.tileIndex],
		float4(surfaceWS.position + normalBias, 1.0)
	).xyz;
	float shadow = FilterDirectionalShadow(positionSTS);
	if (global.cascadeBlend < 1.0) {
		normalBias = surfaceWS.interpolatedNormal *
			(directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
		positionSTS = mul(
			_DirectionalShadowMatrices[directional.tileIndex + 1],
			float4(surfaceWS.position + normalBias, 1.0)
		).xyz;
		shadow = lerp(
			FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend
		);
	}
	return shadow;
}
//Manages switches between baked and real time
float MixBakedAndRealtimeShadows(
	ShadowData global, float shadow, int shadowMaskChannel ,float strength
) {
	float baked = GetBakedShadow(global.shadowMask, shadowMaskChannel);
	if (global.shadowMask.always) {
		shadow = lerp(1.0, shadow, global.strength);
		shadow = min(baked, shadow);
		return lerp(1.0, shadow, strength);
	}
	if (global.shadowMask.distance) {
		//Combines backed and realtime lighting together
		//Baked light comes in the further we go
		shadow = lerp(baked, shadow, global.strength);
		return lerp(1.0, shadow, strength);
	}
	return lerp(1.0, shadow, strength * global.strength);
}
//Returns the atteuation of the shadows given the data and a surface
// NOTE: Seems to be having issues with multiple lights
//We add some bias to help deal with shadow acne
float GetDirectionalShadowAttenuation(DirectionalShadowData directional,ShadowData global, Surface surfaceWS) {
	#if !defined(_RECEIVE_SHADOWS)
		return 1.0;
	#endif
		float shadow;
		if (directional.strength * global.strength <= 0.0) {
			shadow = GetBakedShadow(global.shadowMask, 
				directional.shadowMaskChannel,abs(directional.strength));
		}
		else {
			shadow = GetCascadedShadow(directional, global, surfaceWS);
			shadow = MixBakedAndRealtimeShadows(global, shadow, 
				directional.shadowMaskChannel,directional.strength);
		}
		return shadow;
}
//Real-time shadow function for point and spotlight
//Use the shadow atlas to determine shadows
float GetOtherShadow(
	OtherShadowData other, ShadowData global, Surface surfaceWS
) {
	float4 tileData = _OtherShadowTiles[other.tileIndex];
	//We don't use a shadow casecade to blend and we use perspective projection
	float3 normalBias = surfaceWS.interpolatedNormal * tileData.w;;
	float4 positionSTS = mul(
		_OtherShadowMatrices[other.tileIndex],
		float4(surfaceWS.position + normalBias, 1.0)
	);
	return FilterOtherShadow(positionSTS.xyz / positionSTS.w);
}

//Get attenuation for shadowMask for point and spot lights
float GetOtherShadowAttenuation(
	OtherShadowData other, ShadowData global, Surface surfaceWS
) {
#if !defined(_RECEIVE_SHADOWS)
	return 1.0;
#endif

	float shadow;
	//Global is used to skip sampling of realtime shadows
	if (other.strength > 0.0 * global.strength <= 0.0) {
		shadow = GetBakedShadow(
			global.shadowMask, other.shadowMaskChannel, abs(other.strength)
		);
	}
	else {
		//Everything that isn't baked
		shadow = GetOtherShadow(other, global, surfaceWS);
		shadow = MixBakedAndRealtimeShadows(
			global, shadow, other.shadowMaskChannel, other.strength
		);
	}
	return shadow;
}
#endif