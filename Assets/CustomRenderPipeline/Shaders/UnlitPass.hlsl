//Add include guards since we are in global scope
//Basically works like a singleton
#ifndef CUSTOM_UNLIT_PASS_INCLUDED
#define CUSTOM_UNLIT_PASS_INCLUDED
#include "../ShaderLibrary/Common.hlsl"

//We want to be able to bundle draw calls to CPU and GPU as batches (if we were using cbuffer)
//Since cbuffer wont work with per object material properties, then we need to setup GPU instacing instead
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4,_BaseColor)//color used for unlit shader. Is assigned in Custom-Unlit
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

struct Attributes {
	float3 positionOS : POSITION;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};
//Custom output
struct Varyings {
	float4 positionCS : SV_POSITION;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};
//output is for homogenous clip space
Varyings UnlitPassVertex(Attributes input){
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input); //For GPU instancing
	UNITY_TRANSFER_INSTANCE_ID(input, output); //copy index from input to output for GPU instancing
	float3 positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(positionWS);
	return output;
}
//: XXXXX statements indicates what we mean with the value we return
//In this case, output is for render target
float4 UnlitPassFragment(Varyings i) : SV_TARGET{
	UNITY_SETUP_INSTANCE_ID(i);
	return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
}

#endif