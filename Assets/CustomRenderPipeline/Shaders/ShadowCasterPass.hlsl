//Add include guards since we are in global scope
//Basically works like a singleton
#ifndef CUSTOM_SHADOW_CASTER_PASS_INCLUDED
#define CUSTOM_SHADOW_CASTER_PASS_INCLUDED
#include "../ShaderLibrary/Common.hlsl"
//We want to copy the light shader since we are just doing many of the same things its doing
//Want to support textures
// Cannot be per material
TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

//We want to be able to bundle draw calls to CPU and GPU as batches (if we were using cbuffer)
//Since cbuffer wont work with per object material properties, then we need to setup GPU instacing instead
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST) //UV scaling and transforms can be per instances!
	UNITY_DEFINE_INSTANCED_PROP(float4,_BaseColor)//color used for unlit shader. Is assigned in Custom-Unlit
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff) //For cutting holes in objects via alpha
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

struct Attributes {
	float3 positionOS : POSITION;
	float2 baseUV : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};
//Custom output
struct Varyings {
	float4 positionCS : SV_POSITION;
	float2 baseUV : VAR_BASE_UV; //Basically declaring that it has no special meaning
	UNITY_VERTEX_INPUT_INSTANCE_ID
};
//output is for homogenous clip space
Varyings ShadowCasterPassVertex(Attributes input){
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input); //For GPU instancing
	UNITY_TRANSFER_INSTANCE_ID(input, output); //copy index from input to output for GPU instancing
	float3 positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(positionWS);

	float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	output.baseUV = input.baseUV * baseST.xy + baseST.zw; //Transform UVs
	return output;
}

void ShadowCasterPassFragment(Varyings input){
	UNITY_SETUP_INSTANCE_ID(input);
	float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV); //Samples texture
	float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor); //Get color from instance
	float4 base = baseMap * baseColor;
	//base.rgb = normalize(input.normalWS); //Smooth out interpolation distortion
	#if defined(_CLIPPING)
		clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff)); //Discard frag if 0 or less
	#endif 
	
}

#endif