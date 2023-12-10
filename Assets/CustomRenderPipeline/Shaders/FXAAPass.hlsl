#ifndef CUSTOM_FXAA_PASS_INCLUDED
#define CUSTOM_FXAA_PASS_INCLUDED
float4 _FXAAConfig;

//Manages the convolution process for getting the Luma Neighborhood
struct LumaNeighborhood {
	float m, n, e, s, w;
	float highest, lowest;
	float range;
};

//We want to get the lumanice for a given pixel but also for its neighbors
float GetLuma(float2 uv, float uOffset = 0.0, float vOffset = 0.0) {
	uv += float2(uOffset, vOffset)*GetSourceTexelSize().xy;
#if defined(FXAA_ALPHA_CONTAINS_LUMA)
	//Use the lumanice calculation we made in the colorgrading step
	return GetSource(uv).a;
#else
	//Use green since we are more sensitive to it and it is cheaper than a luminance conversion
	return GetSource(uv).g;
#endif 
}
bool canSkipFXAA(LumaNeighborhood lum) {
	return lum.range < _FXAAConfig.x;
}
//We want to get the luminance around the main pixel
//We use this to get the contrast between the vertical and horizontal directions
LumaNeighborhood GetLumaNeighborhood(float2 uv) {
	LumaNeighborhood luma;
	luma.m = GetLuma(uv);
	luma.n = GetLuma(uv, 0.0, 1.0);
	luma.s = GetLuma(uv, 0.0, -1.0);
	luma.w = GetLuma(uv, -1.0, 0.0);
	luma.e = GetLuma(uv, 1.0, 0.0);
	luma.highest = max(max(max(max(luma.m, luma.n), luma.e), luma.s), luma.w);
	luma.lowest = min(min(min(min(luma.m, luma.n), luma.e), luma.s), luma.w);
	luma.range = luma.highest - luma.lowest;
	return luma;

}
float4 FXAAPassFragment(Varyings input) : SV_TARGET{
	LumaNeighborhood luma = GetLumaNeighborhood(input.screenUV);
	if (canSkipFXAA(luma)) return 0.0;
	return luma.range;
}

#endif