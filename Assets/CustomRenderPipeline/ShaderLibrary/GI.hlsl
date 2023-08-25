#ifndef CUSTOM_GI_INCLUDED
#define CUSTOM_GI_INCLUDED
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);

TEXTURE2D(unity_ShadowMask);
SAMPLER(samplerunity_ShadowMask);

TEXTURE3D_FLOAT(unity_ProbeVolumeSH); //used for LPPV
SAMPLER(samplerunity_ProbeVolumeSH);
//Samplers for reflecting light from skybox
TEXTURECUBE(unity_SpecCube0);
SAMPLER(samplerunity_SpecCube0);
//Sturct used when sampling baked light
struct GI {
	float3 diffuse;
	float3 specular;
	ShadowMask shadowMask;
};
//Sample the cubemap for reflections
float3 SampleEnvironment(Surface surfaceWS) {
	//Calculate reflection coordinates for cubemap (skybox)
	float3 uvw = reflect(-surfaceWS.viewDirection,surfaceWS.normal);
	//Sample cube map and return its color values
	//We are going to be using a 3d texture coord
	float4 environment = SAMPLE_TEXTURECUBE_LOD(
		unity_SpecCube0, samplerunity_SpecCube0, uvw, 0.0);
	return environment.rgb;
}
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
float4 SampleBakedShadows(float2 lightMapUV, Surface surfaceWS) {
#if defined(LIGHTMAP_ON)
	return SAMPLE_TEXTURE2D(
		unity_ShadowMask, samplerunity_ShadowMask, lightMapUV
	);
#else
	//Sample the light probes to determine how occluded things are
	if (unity_ProbeVolumeParams.x) {
		return SampleProbeOcclusion(
			TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
			surfaceWS.position, unity_ProbeVolumeWorldToObject,
			unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
			unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
		);
	}
	else {
		return unity_ProbesOcclusion;//Returns sample from occlusion probe
	}
#endif
}
float3 SampleLightProbe(Surface surfaceWS) {
	#if defined(LIGHTMAP_ON)
	return 0.0;
	#else
	//LPPV sampling section
	if (unity_ProbeVolumeParams.x) {
		return SampleProbeVolumeSH4(
			TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
			surfaceWS.position, surfaceWS.normal,
			unity_ProbeVolumeWorldToObject,
			unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
			unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
		);
	}
	else {
		float4 coefficients[7];
		coefficients[0] = unity_SHAr;
		coefficients[1] = unity_SHAg;
		coefficients[2] = unity_SHAb;
		coefficients[3] = unity_SHBr;
		coefficients[4] = unity_SHBg;
		coefficients[5] = unity_SHBb;
		coefficients[6] = unity_SHC;
		return max(0.0, SampleSH9(coefficients, surfaceWS.normal)); //Do a light calc finding the max coefficent
	}
	#endif
}

//Light map sampling
GI GetGI(float2 lightMapUV, Surface surfaceWS) {
	GI gi;
	gi.diffuse = SampleLightMap(lightMapUV) + SampleLightProbe(surfaceWS);
	gi.specular = SampleEnvironment(surfaceWS);
	gi.shadowMask.always = false;
	gi.shadowMask.distance = false;
	gi.shadowMask.shadows = SampleBakedShadows(lightMapUV,surfaceWS);
#if defined(_SHADOW_MASK_ALWAYS)
	gi.shadowMask.always = true;
	gi.shadowMask.shadows = SampleBakedShadows(lightMapUV, surfaceWS);
#elif defined(_SHADOW_MASK_DISTANCE)
	gi.shadowMask.distance = true;
	gi.shadowMask.shadows = SampleBakedShadows(lightMapUV,surfaceWS);
#endif
	return gi;
}
#endif

