#ifndef CUSTOM_FXAA_PASS_INCLUDED
#define CUSTOM_FXAA_PASS_INCLUDED
float4 _FXAAConfig;

//Manages the convolution process for getting the Luma Neighborhood
struct LumaNeighborhood {
	float m, n, e, s, w,ne,nw,se,sw;
	float highest, lowest;
	float range;
};
//Store info about detected edge
struct FXAAEdge {
	bool isHorizontal;
	float pixelStep;//size of the pixel to blend
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
	//We want to take into account global contrast but also relative contrast
	//RElative contrast is based on the highest luma, so it changes depending on the neighborhood
	return lum.range < max( _FXAAConfig.x,_FXAAConfig.y * lum.highest);
}
//Determine if it is horizontal based on which has the higher contrast
bool IsHorizontalEdge(LumaNeighborhood luma) {
	//Basically combining the contrasts between the top and bottom
	//First the values directly above, then comparing the diagonals above and below
	//values above and below middle are weighted more
	float h = 2.0 * abs(luma.n + luma.s - 2.0 * luma.m) + //Combine contrasts above and below middle
		abs(luma.ne + luma.se - 2.0 * luma.e) + //Combine contrasts above and below the east value
		abs(luma.nw + luma.sw - 2.0 * luma.w); //Combine contrasts above and below the west value
	//Basically combining the contrasts between the sides, with the sides directly next to the 
	//middle are weighted more
	float v = 2.0 * abs(luma.e + luma.w - 2.0 * luma.m) + //Combine contrasts to sides of middle
		abs(luma.ne + luma.nw - 2.0 * luma.n) + //Combine contrasts to the sides to the north
		abs(luma.se + luma.sw - 2.0 * luma.s); //combine contrasts to the sides to the south
	return h >= v;
}
FXAAEdge GetFXAAEdge(LumaNeighborhood luma) {
	FXAAEdge edge;
	edge.isHorizontal = IsHorizontalEdge(luma);
	float lumaP, lumaN;
	if (edge.isHorizontal) {
		edge.pixelStep = GetSourceTexelSize().y;
		lumaP = luma.n;
		lumaN = luma.s;
		
	}
	else {
		edge.pixelStep = GetSourceTexelSize().x;
		lumaP = luma.w;
		lumaN = luma.e;
	}
	//We want to figure out which way to blend the pixel
	//We want to blend in the direction of the higher contrast
	float gradientP = abs(lumaP - luma.m);
	float gradientN = abs(lumaN - luma.m);
	edge.pixelStep = gradientP < gradientN ? -edge.pixelStep : edge.pixelStep;
	return edge;
}
//We want to get the luminance around the main pixel
//We use this to get the contrast between the vertical and horizontal directions
LumaNeighborhood GetLumaNeighborhood(float2 uv) {
	LumaNeighborhood luma;
	luma.m = GetLuma(uv);
	luma.n = GetLuma(uv, 0.0, 1.0);
	luma.ne = GetLuma(uv, 1.0, 1.0);
	luma.nw = GetLuma(uv, -1.0, 1.0);
	luma.s = GetLuma(uv, 0.0, -1.0);
	luma.se = GetLuma(uv, 1.0, -1.0);
	luma.sw = GetLuma(uv, -1.0, -1.0);
	luma.w = GetLuma(uv, -1.0, 0.0);
	luma.e = GetLuma(uv, 1.0, 0.0);
	luma.highest = max(max(max(max(luma.m, luma.n), luma.e), luma.s), luma.w);
	luma.lowest = min(min(min(min(luma.m, luma.n), luma.e), luma.s), luma.w);
	luma.range = luma.highest - luma.lowest;
	return luma;

}
//The we want to guess at what the line would look like at higher resolution. We
//do this by guessing what pixels were lost in the res reduction process
//We blend the pixels together to get the result, based on the contrast of the neighborhood
float GetSubpixelBlendFactor(LumaNeighborhood luma) {
	//This is a low pass filter which filters out the high contrast values via an average
	float filter = luma.n + luma.e + luma.s + luma.w;
	filter *= 2.0; //weighted in favor of the closer values
	filter += luma.ne + luma.nw + luma.se + luma.sw;
	filter *= 1.0 / 12; //2*4 + 4 = 12
	//This is a high pass filter eliminating the low values by comapring with the middle
	filter = abs(filter - luma.m);
	filter = saturate(filter / luma.range); //Normalize the results
	//Use a smoothstep to reduce the strength of the filter
	filter = smoothstep(0, 1, filter);
	return filter * filter * _FXAAConfig.z;
}
float4 FXAAPassFragment(Varyings input) : SV_TARGET{
	LumaNeighborhood luma = GetLumaNeighborhood(input.screenUV);
	if (canSkipFXAA(luma)) return GetSource(input.screenUV);
	
	FXAAEdge edge = GetFXAAEdge(luma);
	//We blend between the neighboring pixel and the middle pixel
	float blendFactor = GetSubpixelBlendFactor(luma);
	float2 blendUV = input.screenUV;
	if (edge.isHorizontal) {
		blendUV.y += blendFactor * edge.pixelStep;
	}
	else {
		blendUV.x += blendFactor * edge.pixelStep;
	}
	return GetSource(blendUV);
	//return edge.pixelStep < 0.0 ? float4(1.0, 0.0, 0.0, 0.0) : 1.0;
	//return GetSubpixelBlendFactor(luma);
}

#endif