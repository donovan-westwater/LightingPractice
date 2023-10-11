#ifndef CUSTOM_POST_FX_PASSES_INCLUDED
#define CUSTOM_POST_FX_PASSES_INCLUDED
//Including the filters
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
TEXTURE2D(_PostFXSource);
TEXTURE2D(_PostFXSource2);
SAMPLER(sampler_linear_clamp);

bool _BloomBicubicUpsampling;

float4 _PostFXSource_TexelSize;

float4 _BloomThreshold;

float _BloomIntensity;

float _ExposureBias;

float _WhitePoint;

float4 _ColorAdjustments;

float4 _ColorFilter;

float4 _WhiteBalance;

float4 _SplitToningShadows, _SplitToningHighlights;

float4 _ChannelMixerRed, _ChannelMixerGreen, _ChannelMixerBlue;

float4 _SMHShadows, _SMHMidtones, _SMHHighlights, _SMHRange;

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
//Same as Combine but it interplates between the low and high resolutions instead of adding
float4 BloomScatterPassFragment(Varyings input) : SV_TARGET{
	float3 lowRes;
	if (_BloomBicubicUpsampling) {
		lowRes = GetSourceBicubic(input.screenUV).rgb;
	}
	else {
		lowRes = GetSource(input.screenUV).rgb;
	}
	float3 highRes = GetSource2(input.screenUV).rgb;
	return float4(lerp(highRes, lowRes, _BloomIntensity), 1.0);
}
//Same as Scatter add high res light and then subtract it with a bloom treshold applied
float4 BloomScatterFinalPassFragment(Varyings input) : SV_TARGET{
	float3 lowRes;
	if (_BloomBicubicUpsampling) {
		lowRes = GetSourceBicubic(input.screenUV).rgb;
	}
	else {
		lowRes = GetSource(input.screenUV).rgb;
	}
	float3 highRes = GetSource2(input.screenUV).rgb;
	lowRes += highRes - ApplyBloomThreshold(highRes);
	return float4(lerp(highRes, lowRes, _BloomIntensity), 1.0);
}
//Widens the filter to 6x6 to more agressively blur brightspots to avoid flickering
float4 BloomPrefilterFirefliesPassFragment(Varyings input) : SV_TARGET{
	float3 color = 0.0;
	float weightSum = 0.0;
	float2 offsets[] = {
		float2(0.0, 0.0),
		float2(-1.0, -1.0), float2(-1.0, 1.0), float2(1.0, -1.0), float2(1.0, 1.0)
	};
	for (int i = 0; i < 5; i++) {
		float3 c =
			GetSource(input.screenUV + offsets[i] * GetSourceTexelSize().xy * 2.0).rgb;
		
		c = ApplyBloomThreshold(c);
		float w = 1.0 / (Luminance(c) + 1.0);
		color += c * w; //Get the lumincance to figure out how much we need to blur
		weightSum += w;
	}
	color /= weightSum;
	return float4(color, 1.0);
}
float Luminance(float3 color, bool useACES) {
	return useACES ? AcesLuminance(color) : Luminance(color);
}
float3 ColorGradePostExposure(float3 color) {
	return color * _ColorAdjustments.x;
}
//Alters the color tempeature. We convert to LMS and apply the white balance coefficents to alter the temp
float3 ColorGradeWhiteBalance(float3 color) {
	color = LinearToLMS(color);
	color *= _WhiteBalance.rgb;
	return LMSToLinear(color);
}
//Applies split toning to the color. Uses the  Adobe split toning function
float3 ColorGradeSplitToning(float3 color, bool useACES) {
	color = PositivePow(color, 1.0 / 2.2);
	float t = saturate(Luminance(saturate(color), useACES) + _SplitToningShadows.w);
	float3 shadows = lerp(0.5, _SplitToningShadows.rgb, 1.0 - t); //Limit the colors to just the regions they are meant for
	float3 highlights = lerp(0.5, _SplitToningHighlights.rgb, t); //Same idea as above
	color = SoftLight(color, shadows); //Soft bled between the color and shadow tint
	color = SoftLight(color, highlights); //Soft blend btween the color and hightlight tint
	return PositivePow(color, 2.2);
}
//Transform the color into a vector whose values are relative to the center of the brightness spectrum
//When then scale these values to increase or decreese our constrast (i.e how far apart the colors are from 
//the center) From there, we add back the midgray to transform back into our global space
float3 ColorGradingContrast(float3 color, bool useACES) {

	//We convert from linear color space to ACEScc's logrithmic color space
	//ACEScg is the linear subset of ACES
	color = useACES ? ACES_to_ACEScc(unity_to_ACES(color)) : LinearToLogC(color);
	color = (color - ACEScc_MIDGRAY) * _ColorAdjustments.y + ACEScc_MIDGRAY;
	//Convert back at the end
	return useACES ? ACES_to_ACEScg(ACEScc_to_ACES(color)) : LogCToLinear(color);
}
//Just adds a color filter. Pretty self explanitory when you see the code
float3 ColorGradeColorFilter(float3 color) {
	return color * _ColorFilter.rgb;
}
//We Shift the hue of the color by moving around the color wheel
//We can do this by converting into HSV, and then adding to the H value
//This gets us our rotation around the wheel
//We make sure it can wrap around at the ends of the wheel, and convert back
//Remember that + is clockwise around the color wheel
float3 ColorGradingHueShift(float3 color) {
	color = RgbToHsv(color);
	float hue = color.x + _ColorAdjustments.z;
	color.x = RotateHue(hue, 0.0, 1.0);
	return HsvToRgb(color);
}
//Use the same strat here as we do with contrast
//convert to lumincance, get relative lunminace, and then adjust the luminance to change saturation
//(long, medium, short) wavelengths
//White balance is blue / yellow
//Tint is red/green
float3 ColorGradingSaturation(float3 color, bool useACES) {
	float luminance = Luminance(color,useACES);
	return (color - luminance) * _ColorAdjustments.w + luminance;
}
//Mix channels together to create a new color
//Creates a 3 x 3 matrix to transform a color by mutipling it by other channels
float3 ColorGradingChannelMixer(float3 color) {
	return mul(
		float3x3(_ChannelMixerRed.rgb, _ChannelMixerGreen.rgb, _ChannelMixerBlue.rgb),
		color
	);
}
//Multiply the colors for each range based on the luminance of the color
//We interplotate between the ranges with a smoothstep function
//We create weights to control how much each smh color affects the results
float3 ColorGradingShadowsMidtonesHighlights(float3 color, bool useACES) {
	float luminance = Luminance(color,useACES);
	float shadowsWeight = 1.0 - smoothstep(_SMHRange.x, _SMHRange.y, luminance);
	float highlightsWeight = smoothstep(_SMHRange.z, _SMHRange.w, luminance);
	float midtonesWeight = 1.0 - shadowsWeight - highlightsWeight;
	return
		color * _SMHShadows.rgb * shadowsWeight +
		color * _SMHMidtones.rgb * midtonesWeight +
		color * _SMHHighlights.rgb * highlightsWeight;
}
//Color correction and color grading step
float3 ColorGrade(float3 color, bool useACES = false) {
	color = ColorGradePostExposure(color);
	color = ColorGradeWhiteBalance(color);
	color = ColorGradingContrast(color, useACES);
	color = ColorGradeColorFilter(color);
	color = max(color, 0.0);
	color = ColorGradeSplitToning(color, useACES);
	color = ColorGradingChannelMixer(color);
	color = max(color, 0.0);
	color = ColorGradingShadowsMidtonesHighlights(color, useACES);
	color = ColorGradingHueShift(color);
	color = ColorGradingSaturation(color, useACES);
	color = max(useACES ? ACEScg_to_ACES(color) : color, 0.0); //Negative colors don't exist
	return color;
}
float4 _ColorGradingLUTParameters;
bool _ColorGradingLUTInLogC;
//Grab color from the lookup table
float3 GetColorGradedLUT(float2 uv, bool useACES = false) {
	float3 color = GetLutStripValue(uv, _ColorGradingLUTParameters);
	return ColorGrade(_ColorGradingLUTInLogC ? LogCToLinear(color) : color, useACES);
}
//We still want to color grade even if there is no tone mapping
float4 ColorGradingNonePassFragment(Varyings input) : SV_TARGET{
	float3 color = GetColorGradedLUT(input.screenUV);
	return float4(color,1.0);
}
//Tone mapping function that reduces the brightness of the image so more uniform images have more colors
//Uses a non linear conversion that mainly reduces high values. uses c/(1+c) to reduce the colors
float4 ColorGradingReinhardPassFragment(Varyings input) : SV_TARGET{
	float3 color = GetColorGradedLUT(input.screenUV);
	//Grad againest high value colors
	color.rgb /= color.rgb + 1.0;
	return float4(color,1.0);
}
//Uses a tone mapping function that is more configuriable
//The function is: (x(ax + cb) + de)/(x(ax+b)+df) - e/f
//c is the color channel, the e is the exposure bias, and w is the white point
//First used in Ucharted 2: https://www.slideshare.net/ozlael/hable-john-uncharted2-hdr-lighting
//NeutralTonemap comes from this: https://github.com/Unity-Technologies/Graphics/blob/master/com.unity.postprocessing/PostProcessing/Shaders/Colors.hlsl
//All of the properties are set there in the function
float4 ColorGradingNeutralPassFragment(Varyings input) : SV_TARGET{
	float3 color = GetColorGradedLUT(input.screenUV);
	//Grad againest high value colors
	color.rgb = NeutralTonemap(color.rgb); //This is Unity's version of the function
	return float4(color,1.0);
}
//ACES Tonemapping, which is the standard used by film. Shifts the HUE and Brightness more
float4 ColorGradingACESPassFragment(Varyings input) : SV_TARGET{
	float3 color = GetColorGradedLUT(input.screenUV,true);
	//Grad againest high value colors
	color.rgb = AcesTonemap(color.rgb); //This is Unity's version of the function
	return float4(color,1.0);
}
float4 ColorGradingNeutralCustomPassFragment(Varyings input) : SV_TARGET{
	float3 color = GetColorGradedLUT(input.screenUV);
	//Grade against high value colors
	// Tonemap
	float a = 0.2;
	float b = 0.29;
	float c = 0.24;
	float d = 0.272;
	float e = _ExposureBias;
	float f = 0.3;
	float whiteLevel = _WhitePoint;
	float whiteClip = 1.0;

	float3 whiteScale = (1.0).xxx / NeutralCurve(whiteLevel, a, b, c, d, e, f);
	color.rgb = NeutralCurve(color.rgb * whiteScale, a, b, c, d, e, f);
	color.rgb *= whiteScale;

	// Post-curve white point adjustment
	color.rgb /= whiteClip.xxx;

	return float4(color,1.0);
}
float4 CopyPassFragment(Varyings input) : SV_TARGET{
	float4 color = GetSource(input.screenUV);
	//Shitty tone mapper test
	//float Y = 0.2126 * color.x + 0.7152 * color.y + 0.0722 * color.z;
	//float y = Y*3.9;
	//color = pow(color / Y, 0.6) * y;
	return color;
}
TEXTURE2D(_ColorGradingLUT);
//Time to apply the post process effects to the image via the LUT
float3 ApplyColorGradingLUT(float3 color) {
	return ApplyLut2D(
		TEXTURE2D_ARGS(_ColorGradingLUT, sampler_linear_clamp),
		saturate(_ColorGradingLUTInLogC ? LinearToLogC(color) : color),
		_ColorGradingLUTParameters.xyz
	);
}

float4 FinalPassFragment(Varyings input) : SV_TARGET{
	float4 color = GetSource(input.screenUV);
	color.rgb = ApplyColorGradingLUT(color.rgb);
	return color;
}
#endif