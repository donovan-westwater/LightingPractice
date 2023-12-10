#ifndef CUSTOM_FXAA_PASS_INCLUDED
#define CUSTOM_FXAA_PASS_INCLUDED

float GetLuma(float2 uv) {
	
#if defined(FXAA_ALPHA_CONTAINS_LUMA)
	//Use the lumanice calculation we made in the colorgrading step
	return GetSource(uv).a;
#else
	//Use green since we are more sensitive to it and it is cheaper than a luminance conversion
	return GetSource(uv).g;
#endif 
}

float4 FXAAPassFragment(Varyings input) : SV_TARGET{
	return GetLuma(input.screenUV);
}

#endif