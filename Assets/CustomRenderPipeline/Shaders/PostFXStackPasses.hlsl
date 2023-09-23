#ifndef CUSTOM_POST_FX_PASSES_INCLUDED
#define CUSTOM_POST_FX_PASSES_INCLUDED

TEXTURE2D(_PostFXSource);
SAMPLER(sampler_linear_clamp);

float4 _PostFXSource_TexelSize;

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
	//This is a 9x1 horizontal vector for the filter
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
		float offset = offsets[i] * 2.0 * GetSourceTexelSize().y;
		color += GetSource(input.screenUV + float2(0.0, offset)).rgb * weights[i];
	}
	return float4(color, 1.0);
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