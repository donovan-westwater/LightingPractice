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
TEXTURE2D(_NormalMap);
TEXTURE2D(_DetailNormalMap);
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
UNITY_DEFINE_INSTANCED_PROP(float, _DetailNormalScale)
UNITY_DEFINE_INSTANCED_PROP(float, _NormalScale)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)
//More organized way to get and pass parameter infomation for the input file
struct InputConfig {
	float2 baseUV;
	float2 detailUV;
	bool useMask; //Do we enable the mask?
	bool useDetail; //Do we enable the detail map
};

InputConfig GetInputConfig(float2 baseUV, float2 detailUV = 0.0) {
	InputConfig c;
	c.baseUV = baseUV;
	c.detailUV = detailUV;
	c.useMask = false;
	c.useDetail = false;
	return c;
}
float2 TransformBaseUV(float2 baseUV) {
	float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	return baseUV * baseST.xy + baseST.zw;
}
float2 TransformDetailUV(float2 detailUV) {
	float4 detailST = INPUT_PROP(_DetailMap_ST);
	return detailUV * detailST.xy + detailST.zw;
}
float4 GetDetail(InputConfig c) {
	if (c.useDetail) {
		float4 map = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, c.detailUV);
		return map * 2.0 - 1.0; //maps to -1 to 1
	}
	return 0.0;
}
float4 GetMask(InputConfig c) {
	if (c.useMask) {
		return SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, c.baseUV);
	}
	return 1.0;
}
float4 GetBase(InputConfig c) {
	float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.baseUV);
	float4 color = INPUT_PROP(_BaseColor);
	//interploates albedo for the detail map
	if (c.useDetail) {
		float4 detail = GetDetail(c).r * INPUT_PROP(_DetailAlbedo);
		float mask = GetMask(c).b; //Factor in mask map into detail map
		map.rgb = lerp(sqrt(map.rgb), detail < 0.0 ? 0.0 : 1.0, abs(detail)* mask);
		map.rgb *= map.rgb;
	}
	return map * color;
}

float3 GetEmission(InputConfig c) {
	float4 map = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, c.baseUV);
	float4 color = INPUT_PROP(_EmissionColor);
	return map.rgb * color.rgb;
}
float GetCutoff(InputConfig c) {
	return INPUT_PROP(_Cutoff);
}
float GetFresnel(InputConfig c) {
	return INPUT_PROP(_Fresnel);
}
float GetMetallic(InputConfig c) {
	float metallic = INPUT_PROP(_Metallic);
	metallic *= GetMask(c).r;
	return metallic;
}
//Handles gaps and holes in surfaces that create shadows that cant be handled by traditional lighting
float GetOcclusion(InputConfig c) {
	float strength = INPUT_PROP(_Occlusion);
	float occlusion = GetMask(c).g;
	occlusion = lerp(occlusion, 1.0, strength);
	return occlusion;
}
float GetSmoothness(InputConfig c) {
	float smoothness = INPUT_PROP(_Smoothness);
	smoothness *= GetMask(c).a;
	//Detail mask intergration
	if (c.useDetail) {
		float detail = GetDetail(c).b * INPUT_PROP(_DetailSmoothness);
		float mask = GetMask(c).b;
		smoothness = lerp(smoothness, detail < 0.0 ? 0.0 : 1.0, abs(detail)* mask);
	}
	return smoothness;
}
float3 GetNormalTS(InputConfig c) {
	float4 map = SAMPLE_TEXTURE2D(_NormalMap, sampler_BaseMap, c.baseUV);
	float scale = INPUT_PROP(_NormalScale);
	float3 normal = DecodeNormal(map, scale);

	//Detail normal map
	if (c.useDetail) {
		map = SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailMap, c.detailUV);
		scale = INPUT_PROP(_DetailNormalScale) * GetMask(c).b;
		float3 detail = DecodeNormal(map, scale);
		normal = BlendNormalRNM(normal, detail);
	}
	return normal;
}
#endif