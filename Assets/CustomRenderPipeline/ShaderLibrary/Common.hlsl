//Add include guards since we are in global scope
//Basically works like a singleton
#ifndef CUSTOM_COMMON_INCLUDED
#define CUSTOM_COMMON_INCLUDED
#include "UnityInput.hlsl"
//Functions that handles the actual transform process from local to world
float3 TransformObjectToWorld(float3 positionOS) {
	return mul(unity_ObjectToWorld, float4(positionOS, 1.0)).xyz;
}
//Functions that handles the actual transform process from world to View space
float4 TransformWorldToView(float3 positionWS) {
	return mul(unity_MatrixVP, float4(positionWS, 1.0));
}
#endif