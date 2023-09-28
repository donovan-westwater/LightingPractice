#ifndef CUSTOM_POST_FX_PASSES_INCLUDED
#define CUSTOM_POST_FX_PASSES_INCLUDED
//Including the filters
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
TEXTURE2D(_PostFXSource);
TEXTURE2D(_PostFXSource2);
SAMPLER(sampler_linear_clamp);

bool _BloomBicubicUpsampling;

float4 _PostFXSource_TexelSize;

float4 _BloomThreshold;

float _BloomIntensity;

float4 GetSourceTexelSize() {
	return _PostFXSource_TexelSize;
}
//We want to draw a single clipped triangle instead of a quad
struct Varyings {
	float4 positionCS : SV_POSITION;
	float2 screenUV : VAR_SCREEN_UV;
};
//We draw a single large tirangle instead of a quad
//This is done in the GPU directly
Varyings DefaultPassVertex(uint vertexID : SV_VertexID) {
	Varyings output;
	output.positionCS = float4(
		vertexID <= 1 ? -1.0 : 3.0,
		vertexID == 1 ? 3.0 : -1.0,
		0.0, 1.0
		);
	output.screenUV = float2(
		vertexID <= 1 ? 0.0 : 2.0,
		vertexID == 1 ? 2.0 : 0.0
		);
	if (_ProjectionParams.x < 0.0) {
		output.screenUV.y = 1.0 - output.screenUV.y;
	}
	return output;
}

float4 GetSource(float2 screenUV) {
	return SAMPLE_TEXTURE2D_LOD(_PostFXSource, sampler_linear_clamp, screenUV,0);
}
float4 GetSource2(float2 screenUV) {
	return SAMPLE_TEXTURE2D_LOD(_PostFXSource2, sampler_linear_clamp, screenUV, 0);
}
//Smooth out filtering but bicubic filterng (built-in to unity)
float4 GetSourceBicubic(float2 screenUV) {
	return SampleTexture2DBicubic(
		TEXTURE2D_ARGS(_PostFXSource, sampler_linear_clamp), screenUV,
		_PostFXSource_TexelSize.zwxy, 1.0, 0.0
	);
}
//We use the bloom vector to store differnt values in the formula
// s = Min(Max(0,b - t + tk),2tk)^2/4tk+0.00001
//This is s = Min(Max(0,b -Vect.y),vector.z)^2*vect.w
//w = Max(s,b - vect.x)/Max(b,.00001)
float3 ApplyBloomThreshold(float3 color) {
	float brightness = Max3(color.r, color.g, color.b);
	float soft = brightness + _BloomThreshold.y; //b - t + tk
	soft = clamp(soft, 0.0, _BloomThreshold.z); //2tk
	soft = soft * soft * _BloomThreshold.w; //1/4tk+0.00001
	float contribution = max(soft, brightness - _BloomThreshold.x);
	contribution /= max(brightness, 0.00001);
	return color * contribution;
}
//Applies the artistic controls to the post process effect
float4 BloomPrefilterPassFragment(Varyings input) : SV_TARGET{
	float3 color = ApplyBloomThreshold(GetSource(input.screenUV).rgb);
	return float4(color, 1.0);
}
//Creates a horizontal vector that when mulipled with a transpose, would create a kernal
//that would be used for bloom or other image effects
float4 BloomHorizontalPassFragment(Varyings input) : SV_TARGET{
	float3 color = 0.0;
	//This is a 1x9 horizontal vector for the filter
	//The Weights are derived from pascals triangle
	float offsets[] = {
		-4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0
	};
	float weights[] = {
		0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
		0.19459459, 0.12162162, 0.05405405, 0.01621622
	};
	for (int i = 0; i < 9; i++) {
		//First get the x or horizontal compoental, and get the texels to the left and right
		//Based on the down sample, we determine which pixel to grab, and how much each pixel should 
		//contribute to the average
		float offset = offsets[i] * 2.0 * GetSourceTexelSize().x;
		color += GetSource(input.screenUV + float2(offset, 0.0)).rgb * weights[i];
	}
	return float4(color, 1.0);
}
//Creates a vertical vector that when mulipled with a transpose, would create a kernal
//that would be used for bloom or other image effects
float4 BloomVerticalPassFragment(Varyings input) : SV_TARGET{
	float3 color = 0.0;
	//We can reduse the number of samples by sampling inbetwee gaussian sampling points
	//This is a 5x1 horizontal vector for the filter
	//The Weights are derived from pascals triangle
	float offsets[] = {
		-3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923
	};
	float weights[] = {
		0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027
	};
	for (int i = 0; i < 5; i++) {
		//First get the x or horizontal compoental, and get the texels to the left and right
		//Based on the down sample, we determine which pixel to grab, and how much each pixel should 
		//contribute to the average
		float offset = offsets[i] * GetSourceTexelSize().y; //Don't double because we want to fill the gaps and complete the gaussian
		color += GetSource(input.screenUV + float2(0.0, offset)).rgb * weights[i];
	}
	return float4(color, 1.0);
}
float4 BloomCombinePassFragment(Varyings input) : SV_TARGET{
	float3 lowRes = GetSourceBicubic(input.screenUV).rgb;
	if (_BloomBicubicUpsampling) {
		lowRes = GetSourceBicubic(input.screenUV).rgb;
	}
	else {
		lowRes = GetSource(input.screenUV).rgb;
	}
	float3 highRes = GetSource2(input.screenUV).rgb;
	return float4(lowRes * _BloomIntensity + highRes, 1.0);
}

float4 CopyPassFragment(Varyings input) : SV_TARGET{
	float4 color = GetSource(input.screenUV);
	//Shitty tone mapper test
	//float Y = 0.2126 * color.x + 0.7152 * color.y + 0.0722 * color.z;
	//float y = Y*3.9;
	//color = pow(color / Y, 0.6) * y;
	return color;
}
#endif