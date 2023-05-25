//Add include guards since we are in global scope
//Basically works like a singleton
#ifndef CUSTOM_UNLIT_PASS_INCLUDED
#define CUSTOM_UNLIT_PASS_INCLUDED
#include "../ShaderLibrary/Common.hlsl"

//We want to be able to bundle draw calls to CPU and GPU as batches
CBUFFER_START(UnityPerMaterial)
	float4 _BaseColor;//color used for unlit shader. Is assigned in Custom-Unlit
CBUFFER_END

//output is for homogenous clip space
float4 UnlitPassVertex(float3 positionOS: POSITION) : SV_POSITION{
	float3 positionWS = TransformObjectToWorld(positionOS);
	float4 positionView = TransformWorldToHClip(positionWS);
	return positionView;
}
//: XXXXX statements indicates what we mean with the value we return
//In this case, output is for render target
float4 UnlitPassFragment() : SV_TARGET{
	return _BaseColor;
}

#endif