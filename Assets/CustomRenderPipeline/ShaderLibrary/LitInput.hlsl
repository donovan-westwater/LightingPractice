#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED
#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)
//This input file is meant to Meta Pass to determine how surfaces should be affected by diffuse reflectiveity
//Because this is a new meta pass for surfaces, we need a new input file to define the properties
TEXTURE2D(_BaseMap);
TEXTURE2D(_EmissionMap); //Texture for emissive materials
TEXTURE2D(_MaskMap); //Texture for MODS mask map (metallic, occlusion, detail, smoothness)
SAMPLER(sampler_BaseMap);
TEXTURE2D(_DetailMap); //Texture for detail mask map
SAMPLER(sampler_DetailMap);
//We want to be able to bundle draw calls to CPU and GPU as batches (if we were using cbuffer)
//Since cbuffer wont work with per object material properties, then we need to setup GPU instacing instead
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST) //UV scaling and transforms can be per instances!
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)//color used for unlit shader. Is assigned in Custom-Unlit
UNITY_DEFINE_INSTANCED_PROP(float4, _DetailMap_ST) //Texture for detail map (small details mask map)
UNITY_DEFINE_INSTANCED_PROP(float, _DetailAlbedo)
UNITY_DEFINE_INSTANCED_PROP(float, _DetailSmoothness)
UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor) //Map for emissive materials
UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff) //For cutting holes in objects via alpha
UNITY_DEFINE_INSTANCED_PROP(float, _Metallic) //Simulating metalic surfaces
UNITY_DEFINE_INSTANCED_PROP(float, _Occlusion) //For handling detailed shadows
UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness) //Simualating smooth surfaces
UNITY_DEFINE_INSTANCED_PROP(float,_Fresnel) //Controls the amount of fresnel reflection there is 
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

float2 TransformBaseUV(float2 baseUV) {
	float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	return baseUV * baseST.xy + baseST.zw;
}
float2 TransformDetailUV(float2 detailUV) {
	float4 detailST = INPUT_PROP(_DetailMap_ST);
	return detailUV * detailST.xy + detailST.zw;
}
float4 GetDetail(float2 detailUV) {
	float4 map = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, detailUV);
	return map * 2.0 - 1.0; //maps to -1 to 1
}
float4 GetMask(float2 baseUV) {
	return SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, baseUV);
}
float4 GetBase(float2 baseUV,float2 detailUV = 0.0) {
	float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV);
	float4 color = INPUT_PROP(_BaseColor);
	//interploates albedo for the detail map
	float4 detail = GetDetail(detailUV).r * INPUT_PROP(_DetailAlbedo);
	float mask = GetMask(baseUV).b; //Factor in mask map into detail map
	map.rgb = lerp(sqrt(map.rgb), detail < 0.0 ? 0.0 : 1.0, abs(detail) * mask);
	map.rgb *= map.rgb;

	return map * color;
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
//Handles gaps and holes in surfaces that create shadows that cant be handled by traditional lighting
float GetOcclusion(float2 baseUV) {
	float strength = INPUT_PROP(_Occlusion);
	float occlusion = GetMask(baseUV).g;
	occlusion = lerp(occlusion, 1.0, strength);
	return occlusion;
}
float GetSmoothness(float2 baseUV,float2 detailUV = 0.0) {
	float smoothness = INPUT_PROP(_Smoothness);
	smoothness *= GetMask(baseUV).a;
	//Detail mask intergration
	float detail = GetDetail(detailUV).b * INPUT_PROP(_DetailSmoothness);
	float mask = GetMask(baseUV).b;
	smoothness = lerp(smoothness, detail < 0.0 ? 0.0 : 1.0, abs(detail)* mask);
	return smoothness;
}

#endif