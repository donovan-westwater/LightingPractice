#ifndef CUSTOM_UNLIT_INPUT_INCLUDED
#define CUSTOM_UNLIT_INPUT_INCLUDED
#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)
//This input file is meant to Meta Pass to determine how surfaces should be affected by diffuse reflectiveity
//Because this is a new meta pass for surfaces, we need a new input file to define the properties
TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
//We want to be able to bundle draw calls to CPU and GPU as batches (if we were using cbuffer)
//Since cbuffer wont work with per object material properties, then we need to setup GPU instacing instead
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST) //UV scaling and transforms can be per instances!
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)//color used for unlit shader. Is assigned in Custom-Unlit
UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeDistance) //When should particles disappears 
UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeRange) //When does the near plane end
UNITY_DEFINE_INSTANCED_PROP(float, _SoftParticlesDistance) //Same as near fade dist but with recognizes object depth
UNITY_DEFINE_INSTANCED_PROP(float, _SoftParticlesRange) //same as near fade range but recognizes object depth
UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff) //For cutting holes in objects via alpha
UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite) //Add a setting to prevent issues with base maps of varying alphas, We want to set alphas that
//Are not discarded to one
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

struct InputConfig {
	Fragment fragment;
	float4 color;
	float2 baseUV;
	float3 flipbookUVB;
	bool flipbookBlending;
	bool nearFade;
	bool softParticles;
};

InputConfig GetInputConfig(float4 positionSS, float2 baseUV) {
	InputConfig c;
	c.fragment = GetFragment(positionSS);
	c.color = 1.0;
	c.baseUV = baseUV;
	c.flipbookUVB = 0.0;
	c.flipbookBlending = false;
	c.nearFade = false;
	c.softParticles = false;
	return c;
}

float2 TransformBaseUV(float2 baseUV) {
	float4 baseST = INPUT_PROP(_BaseMap_ST);
	return baseUV * baseST.xy + baseST.zw;
}

float4 GetBase(InputConfig c) {
	float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.baseUV);
	//Blend between frames in the flipbook to create smoother animations
	if (c.flipbookBlending) {
		map = lerp(
			map, SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.flipbookUVB.xy),
			c.flipbookUVB.z
		);
	}
	if (c.nearFade) {
		float nearAttenuation = (c.fragment.depth - INPUT_PROP(_NearFadeDistance)) /
			INPUT_PROP(_NearFadeRange);
		map.a *= saturate(nearAttenuation);
	}
	if (c.softParticles) {
		//We compare the depth of the fragment and whatever is currently drawn the the depth buffer.
		//Using the delta we can then fade out the particles as they get close to geometery
		float depthDelta = c.fragment.bufferDepth - c.fragment.depth; 
		float softAttenuation = (depthDelta - INPUT_PROP(_SoftParticlesDistance))
			/ INPUT_PROP(_SoftParticlesRange);
		map.a *= saturate(softAttenuation);
	}
	float4 color = INPUT_PROP(_BaseColor);
	return map * color * c.color;
}
float3 GetEmission(InputConfig c) {
	return GetBase(c).rgb;
}
float GetCutoff(InputConfig c) {
	return INPUT_PROP(_Cutoff);
}
//DOesn't exist in the unlit pass so return zero 
float GetMetallic(InputConfig c) {
	return 0.0;
}
float GetFresnel(InputConfig c) {
	return 0.0;
}
float GetSmoothness(InputConfig c) {
	return 0.0;
}
//Final alpha to ensure the correct alpha is used for layered transparency
float GetFinalAlpha(float alpha) {
	return INPUT_PROP(_ZWrite) ? 1.0 : alpha;
}
#endif