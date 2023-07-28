#ifndef CUSTOM_META_PASS_INCLUDED
#define CUSTOM_META_PASS_INCLUDED

#include "../ShaderLibrary/CustomSurface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/CustomLight.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"

bool4 unity_MetaFragmentControl;
float unity_OneOverOutputBoost;
float unity_MaxOutputValue;
//We need the surface includes because we are dealing with surface infomation
//Here we want to map the light map coords back to the xy object space positions
//We are doing this for adding surface to the Global illumination system since it doesn't have that right now!
struct Attributes {
	float3 positionOS : POSITION;
	float2 baseUV : TEXCOORD0;
	float2 lightMapUV : TEXCOORD1;
};

struct Varyings {
	float4 positionCS : SV_POSITION;
	float2 baseUV : VAR_BASE_UV;
};
//Only need to know object space positon and base UV for vert
Varyings MetaPassVertex(Attributes input) {
	Varyings output;
	//Here is where we transform the lightmap UV to object space
	input.positionOS.xy = 
		input.lightMapUV * unity_LightmapST.xy + unity_LightmapST.zw;
	input.positionOS.z = input.positionOS.z > 0.0 ? FLT_MIN : 0.0; //OpenGL needs this to work. Still ned object space vertex
	output.positionCS = TransformWorldToHClip(input.positionOS);
	output.baseUV = TransformBaseUV(input.baseUV);
	return output;
}

float4 MetaPassFragment(Varyings input) : SV_TARGET{
	float4 base = GetBase(input.baseUV);
	Surface surface;
	//Initalize surface to 0 and only setup the values needed for BRDF
	ZERO_INITIALIZE(Surface, surface);
	surface.color = base.rgb;
	surface.metallic = GetMetallic(input.baseUV);
	surface.smoothness = GetSmoothness(input.baseUV);
	BRDF brdf = GetBRDF(surface);
	float4 meta = 0.0;
	if (unity_MetaFragmentControl.x) {
		meta = float4(brdf.diffuse, 1.0);
		meta.rgb += brdf.specular * brdf.roughness * 0.5f; //indirect light for specular but rough materials
		meta.rgb = min(
		PositivePow(meta.rgb,unity_OneOverOutputBoost),unity_MaxOutputValue);

	}
	return meta;
}

#endif