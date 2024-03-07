//Add include guards since we are in global scope
//Basically works like a singleton
#ifndef CUSTOM_COMMON_INCLUDED
#define CUSTOM_COMMON_INCLUDED
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "UnityInput.hlsl"
#define UNITY_MATRIX_M unity_ObjectToWorld //Package below doesnt have object to world. Matrix_M is defined instead
#define UNITY_MATRIX_I_M unity_WorldToObject //See above
#define UNITY_MATRIX_V unity_MatrixV //See above
#define UNITY_MATRIX_VP unity_MatrixVP //See above
#define UNITY_MATRIX_P glstate_matrix_projection //See above
#if defined(_SHADOW_MASK_DISTANCE) || defined(_SHADOW_MASK_ALWAYS)
	#define SHADOWS_SHADOWMASK
#endif
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl" //Redfines macros to access instanced data arrays instead
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

SAMPLER(sampler_linear_clamp);
SAMPLER(sampler_point_clamp);

bool IsOrthographicCamera() {
	return unity_OrthoParams.w;
}
//Since we can't use the w of the params, we use clip space depth instead
float OrthographicDepthBufferToLinear(float rawDepth) {
#if UNITY_REVERSED_Z
	rawDepth = 1.0 - rawDepth;
#endif
	return (_ProjectionParams.z - _ProjectionParams.y) * rawDepth + _ProjectionParams.y;
}

#include "Fragment.hlsl"

float Square(float v) {
	return v * v;
}
float DistanceSquared(float3 pA, float3 pB) {
	return dot(pA - pB, pA - pB);
}
//Clips out objects between LOD levels with fade
void ClipLOD(Fragment fragment, float fade) {
	#if defined(LOD_FADE_CROSSFADE)
	//Uses noise to create a dithered fade between LOD layers
	float dither = InterleavedGradientNoise(fragment.positionSS, 0);
	//Add to the  fade when the lod level is negative (LOD 0)
	clip(fade + (fade < 0.0 ? dither : -dither));
	#endif
}
float3 DecodeNormal(float4 sample, float scale) {
#if defined(UNITY_NO_DXT5nm) //DXT5nm is a compression format
	return UnpackNormalRGB(sample, scale);
#else
	return UnpackNormalmapRGorAG(sample, scale);
#endif
}
//Convert from tangent space to world space. Used for normal maps
float3 NormalTangentToWorld(float3 normalTS, float3 normalWS, float4 tangentWS) {
	float3x3 tangentToWorld =
		CreateTangentToWorld(normalWS, tangentWS.xyz, tangentWS.w);
	return TransformTangentToWorld(normalTS, tangentToWorld);
}
/*
//Functions that handles the actual transform process from local to world
float3 TransformObjectToWorld(float3 positionOS) {
	return mul(unity_ObjectToWorld, float4(positionOS, 1.0)).xyz;
}
//Functions that handles the actual transform process from world to View space
float4 TransformWorldToView(float3 positionWS) {
	return mul(unity_MatrixVP, float4(positionWS, 1.0));
}
*/
#endif