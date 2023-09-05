//Add include guards since we are in global scope
//Basically works like a singleton
#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED
//#include "../ShaderLibrary/Common.hlsl" This is redudant now
#include "../ShaderLibrary/CustomSurface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/CustomLight.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/CustomLighting.hlsl"
//Defining the macros used to for global illumination
 #if defined(LIGHTMAP_ON)
	#define GI_ATTRIBUTE_DATA float2 lightMapUV : TEXCOORD1;
	#define GI_VARYINGS_DATA float2 lightMapUV : VAR_LIGHT_MAP_UV;
	#define TRANSFER_GI_DATA(input, output)  \
		output.lightMapUV = input.lightMapUV * \
		unity_LightmapST.xy + unity_LightmapST.zw; //We need to apply the transfrom to access lightmapUVs correctly
	#define GI_FRAGMENT_DATA(input) input.lightMapUV
#else
	#define GI_ATTRIBUTE_DATA
	#define GI_VARYINGS_DATA
	#define TRANSFER_GI_DATA(input, output)
	#define GI_FRAGMENT_DATA(input) 0.0
#endif
//Want to support textures
// Cannot be per material
//TEXTURE2D(_BaseMap); This is redudant now
//SAMPLER(sampler_BaseMap); This is redudant now

//This is redudant now
//We want to be able to bundle draw calls to CPU and GPU as batches (if we were using cbuffer)
//Since cbuffer wont work with per object material properties, then we need to setup GPU instacing instead
//UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
//	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST) //UV scaling and transforms can be per instances!
//	UNITY_DEFINE_INSTANCED_PROP(float4,_BaseColor)//color used for unlit shader. Is assigned in Custom-Unlit
//	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff) //For cutting holes in objects via alpha
//	UNITY_DEFINE_INSTANCED_PROP(float, _Metallic) //Simulating metalic surfaces
//	UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness) //Simualating smooth surfaces
//UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

struct Attributes {
	float3 positionOS : POSITION;
	float3 normalOS : NORMAL;
	float2 baseUV : TEXCOORD0;
	float4 tangentOS : TANGENT;
	GI_ATTRIBUTE_DATA //Macro to used to add lightmap UV data only when needed
	UNITY_VERTEX_INPUT_INSTANCE_ID
};
//Custom output
struct Varyings {
	float4 positionCS : SV_POSITION;
	float3 positionWS : VAR_POSITION; //Used to calculate view dir
	float3 normalWS : VAR_NORMAL;
	float2 baseUV : VAR_BASE_UV; //Basically declaring that it has no special meaning
	float2 detailUV : VAR_DETAIL_UV;
	float4 tangentWS : VAR_TANGENT;
	GI_VARYINGS_DATA //Macro to used to add lightmap UV data only when needed
	UNITY_VERTEX_INPUT_INSTANCE_ID
};
//output is for homogenous clip space
Varyings LitPassVertex(Attributes input){
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input); //For GPU instancing
	UNITY_TRANSFER_INSTANCE_ID(input, output); //copy index from input to output for GPU instancing
	TRANSFER_GI_DATA(input,output) //Macro to used to add lightmap UV data only when needed
	float3 positionWS = TransformObjectToWorld(input.positionOS);
	output.normalWS = TransformObjectToWorldNormal(input.normalOS);
	output.positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(output.positionWS);
	//float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	output.baseUV = TransformBaseUV(input.baseUV); //Transform UVs
	output.detailUV = TransformDetailUV(input.baseUV);
	output.tangentWS =
		float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
	return output;
}
//: XXXXX statements indicates what we mean with the value we return
//In this case, output is for render target
float4 LitPassFragment(Varyings input) : SV_TARGET{
	UNITY_SETUP_INSTANCE_ID(input);
	//#if defined(LOD_FADE_CROSSFADE)
	//return -unity_LODFade.x;
	//#endif
	ClipLOD(input.positionCS.xy, unity_LODFade.x);
	//float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV); //Samples texture
	//float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor); //Get color from instance
	float4 base = GetBase(input.baseUV,input.detailUV);
	//base.rgb = normalize(input.normalWS); //Smooth out interpolation distortion
	#if defined(_CLIPPING)
		clip(base.a - GetCutoff(input.baseUV)); //Discard frag if 0 or less
	#endif 
	Surface surface;
	surface.position = input.positionWS; //pixel position for shadows
	surface.normal = normalize(input.normalWS);
	surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
	surface.depth = -TransformWorldToView(input.positionWS).z;
	surface.color = base.rgb;
	surface.metallic = GetMetallic(input.baseUV); //Get property frome lit shader
	surface.smoothness = GetSmoothness(input.baseUV,input.detailUV); //Get proeprty from lit shader
	surface.occlusion = GetOcclusion(input.baseUV);
	surface.fresnelStrength = GetFresnel(input.baseUV);
	surface.dither = InterleavedGradientNoise(input.positionCS.xy, 0);
	surface.alpha = base.a;
	surface.normal = NormalTangentToWorld(
		GetNormalTS(input.baseUV, input.detailUV), input.normalWS, input.tangentWS
	);
	surface.interpolatedNormal = input.normalWS;
	//struct used to calculate reflectiveness via the Biderectional Reflectance distribution function
#if defined(_PREMULTIPLY_ALPHA)
	BRDF brdf = GetBRDF(surface, true);
#else
	BRDF brdf = GetBRDF(surface);
#endif //Get the the lighting properties that result from a given surface
	GI gi = GetGI(GI_FRAGMENT_DATA(input), surface, brdf);
	float3 color = GetLighting(surface,brdf,gi);
	color += GetEmission(input.baseUV);
	return float4(color, surface.alpha);
}

#endif