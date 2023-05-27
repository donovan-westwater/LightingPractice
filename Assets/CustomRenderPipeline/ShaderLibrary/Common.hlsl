//Add include guards since we are in global scope
//Basically works like a singleton
#ifndef CUSTOM_COMMON_INCLUDED
#define CUSTOM_COMMON_INCLUDED
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "UnityInput.hlsl"
#define UNITY_MATRIX_M unity_ObjectToWorld //Package below doesnt have object to world. Matrix_M is defined instead
#define UNITY_MATRIX_I_M unity_WorldToObject //See above
#define UNITY_MATRIX_V unity_MatrixV //See above
#define UNITY_MATRIX_VP unity_MatrixVP //See above
#define UNITY_MATRIX_P glstate_matrix_projection //See above
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl" //Redfines macros to access instanced data arrays instead
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

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