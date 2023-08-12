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
	float4 unity_ProbesOcclusion;//Light probes also have shadow mask data. This can be accessed via occulsion probes
	float4 unity_LightmapST; //Var for sampled lightmap
	float4 unity_DynamicLightmapST; //depricated lightmap var
	//Used for light probe sampling. Coefficents of a polynomial used for GI
	float4 unity_SHAr;
	float4 unity_SHAg;
	float4 unity_SHAb;
	float4 unity_SHBr;
	float4 unity_SHBg;
	float4 unity_SHBb;
	float4 unity_SHC;
	//Light probe volume sampling
	float4 unity_ProbeVolumeParams;
	float4x4 unity_ProbeVolumeWorldToObject;
	float4 unity_ProbeVolumeSizeInv;
	float4 unity_ProbeVolumeMin;
CBUFFER_END

float4x4 unity_MatrixVP; //View matrix or world to camera matrix
float4x4 unity_MatrixV;
float4x4 glstate_matrix_projection;
//View direction used for the reflection calculation
float3 _WorldSpaceCameraPos;
#endif