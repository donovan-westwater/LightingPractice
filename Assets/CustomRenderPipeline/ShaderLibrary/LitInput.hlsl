#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED
#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)
//This input file is meant to Meta Pass to determine how surfaces should be affected by diffuse reflectiveity
//Because this is a new meta pass for surfaces, we need a new input file to define the properties
TEXTURE2D(_BaseMap);
TEXTURE2D(_EmissionMap); //Texture for emissive materials
TEXTURE2D(_MaskMap); //Texture for MODS mask map (metallic, occlusion, detail, smoothness)
SAMPLER(sampler_BaseMap);
//We want to be able to bundle draw calls to CPU and GPU as batches (if we were using cbuffer)
//Since cbuffer wont work with per object material properties, then we need to setup GPU instacing instead
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST) //UV scaling and transforms can be per instances!
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)//color used for unlit shader. Is assigned in Custom-Unlit
UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor) //Map for emissive materials
UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff) //For cutting holes in objects via alpha
UNITY_DEFINE_INSTANCED_PROP(float, _Metallic) //Simulating metalic surfaces
UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness) //Simualating smooth surfaces
UNITY_DEFINE_INSTANCED_PROP(float,_Fresnel) //Controls the amount of fresnel reflection there is 
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

float2 TransformBaseUV(float2 baseUV) {
	float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	return baseUV * baseST.xy + baseST.zw;
}

float4 GetBase(float2 baseUV) {
	float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV);
	float4 color = INPUT_PROP(_BaseColor);
	return map * color;
}
float4 GetMask(float2 baseUV) {
	return SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, baseUV);
}
float3 GetEmission(float2 baseUV) {
	float4 map = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, baseUV);
	float4 color = INPUT_PROP(_EmissionColor);
	return map.rgb * color.rgb;
}
float GetCutoff(float2 baseUV) {
	return INPUT_PROP(_Cutoff);
}
float GetFresnel(float2 baseUV) {
	return INPUT_PROP(_Fresnel);
}
float GetMetallic(float2 baseUV) {
	float metallic = INPUT_PROP(_Metallic);
	metallic *= GetMask(baseUV).r;
	return metallic;
}

float GetSmoothness(float2 baseUV) {
	float smoothness = INPUT_PROP(_Smoothness);
	smoothness *= GetMask(baseUV).a;
	return smoothness;
}

#endif