//Add include guards since we are in global scope
//Basically works like a singleton
#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED
#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/CustomSurface.hlsl"
#include "../ShaderLibrary/CustomLight.hlsl"
#include "../ShaderLibrary/CustomLighting.hlsl"
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
	float3 normalOS : NORMAL;
	float2 baseUV : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};
//Custom output
struct Varyings {
	float4 positionCS : SV_POSITION;
	float3 normalWS : VAR_NORMAL;
	float2 baseUV : VAR_BASE_UV; //Basically declaring that it has no special meaning
	UNITY_VERTEX_INPUT_INSTANCE_ID
};
//output is for homogenous clip space
Varyings LitPassVertex(Attributes input){
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input); //For GPU instancing
	UNITY_TRANSFER_INSTANCE_ID(input, output); //copy index from input to output for GPU instancing
	float3 positionWS = TransformObjectToWorld(input.positionOS);
	output.normalWS = TransformObjectToWorldNormal(input.normalOS);
	output.positionCS = TransformWorldToHClip(positionWS);
	float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	output.baseUV = input.baseUV * baseST.xy + baseST.zw; //Transform UVs
	return output;
}
//: XXXXX statements indicates what we mean with the value we return
//In this case, output is for render target
float4 LitPassFragment(Varyings input) : SV_TARGET{
	UNITY_SETUP_INSTANCE_ID(input);
	float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV); //Samples texture
	float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor); //Get color from instance
	float4 base = baseMap * baseColor;
	//base.rgb = normalize(input.normalWS); //Smooth out interpolation distortion
	#if defined(_CLIPPING)
		clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff)); //Discard frag if 0 or less
	#endif 
	Surface surface;
	surface.normal = normalize(input.normalWS);
	surface.color = base.rgb;
	float3 color = GetLighting(surface);
	surface.alpha = base.a;

	return float4(color, surface.alpha);
}

#endif