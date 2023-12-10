#ifndef CUSTOM_FXAA_PASS_INCLUDED
#define CUSTOM_FXAA_PASS_INCLUDED

float GetLuma(float2 uv) {
	return sqrt(Luminance(GetSource(uv))); //Apply gamma adjustment to get more dark values. Better on eyes
}

float4 FXAAPassFragment(Varyings input) : SV_TARGET{
	return GetLuma(input.screenUV);
}

#endif