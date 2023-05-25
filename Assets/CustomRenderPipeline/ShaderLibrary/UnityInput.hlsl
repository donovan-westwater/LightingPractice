//Add include guards since we are in global scope
//Basically works like a singleton
#ifndef CUSTOM_UNITY_INPUT_INCLUDED
#define CUSTOM_UNITY_INPUT_INCLUDED
//Buffer that handles unity matrices used in every draw call
CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld; //Matrix for local to world space transforms
	float4x4 unity_WorldToObject; //Matrix for world to object transform
	float4 unity_LODFade; //Needs to be included even if not used
	real4 unity_WorldTransformParams; //World Transform Params
CBUFFER_END

float4x4 unity_MatrixVP; //View matrix or world to camera matrix
float4x4 unity_MatrixV;
float4x4 glstate_matrix_projection;
#endif