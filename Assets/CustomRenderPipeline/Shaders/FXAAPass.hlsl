#ifndef CUSTOM_FXAA_PASS_INCLUDED
#define CUSTOM_FXAA_PASS_INCLUDED
float4 _FXAAConfig;

#if defined(FXAA_QUALITY_LOW)
#define EXTRA_EDGE_STEPS 3
#define EDGE_STEP_SIZES 1.5, 2.0, 2.0
#define LAST_EDGE_STEP_GUESS 8.0
#elif defined(FXAA_QUALITY_MEDIUM)
#define EXTRA_EDGE_STEPS 8
#define EDGE_STEP_SIZES 1.5, 2.0, 2.0, 2.0, 2.0, 2.0, 2.0, 4.0
#define LAST_EDGE_STEP_GUESS 8.0
#else
#define EXTRA_EDGE_STEPS 10
#define EDGE_STEP_SIZES 1.0, 1.0, 1.0, 1.0, 1.5, 2.0, 2.0, 2.0, 2.0, 4.0
#define LAST_EDGE_STEP_GUESS 8.0
#endif

static const float edgeStepSizes[EXTRA_EDGE_STEPS] = { EDGE_STEP_SIZES };
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
		float lumaGradient, otherLuma;
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
	if(gradientP < gradientN){
		edge.pixelStep = -edge.pixelStep;
		edge.lumaGradient = gradientN;
		edge.otherLuma = lumaN;
	}
	else {
		edge.lumaGradient = gradientP;
		edge.otherLuma = lumaP;
	}
	//edge.pixelStep = gradientP < gradientN ? -edge.pixelStep : edge.pixelStep;
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
float GetEdgeBlendFactor(LumaNeighborhood luma, FXAAEdge edge, float2 uv) {
	//We want to trace along the edge to get a better idea of what to blend
	//Sample the pixel between the current and next pixel
	float2 edgeUV = uv;
	float2 uvStep = 0.0;
	if (edge.isHorizontal) {
		edgeUV.y += 0.5 * edge.pixelStep;
		uvStep.x = GetSourceTexelSize().x;
	}
	else {
		edgeUV.x += 0.5 * edge.pixelStep;
		uvStep.y = GetSourceTexelSize().y;
	}
	//Deteremine contrast between orginal sample and new samples
	//If the contrast is too great we are off the edge
	float edgeLuma = 0.5 * (luma.m + edge.otherLuma);
	float gradientThreshold = 0.25 * edge.lumaGradient;
	//get the gradient between offset and org edge and check if it meets a threshold
	//This tells us if we are in the positive direction
	float2 uvP = edgeUV + uvStep;
	float lumaDeltaP = GetLuma(uvP) - edgeLuma;
	bool atEndP = abs(lumaDeltaP) >= gradientThreshold;
	//repeat until we break or walk the whole edge
	int i;
	for (i = 0; i < EXTRA_EDGE_STEPS && !atEndP; i++) {
		uvP += uvStep*edgeStepSizes[i];
		lumaDeltaP = GetLuma(uvP) - edgeLuma;
		atEndP = abs(lumaDeltaP) >= gradientThreshold;
		if (!atEndP) uvP += uvStep*LAST_EDGE_STEP_GUESS;
	}
	//Do the same in the negative direction
	float2 uvN = edgeUV - uvStep;
	float lumaDeltaN = GetLuma(uvN) - edgeLuma;
	bool atEndN = abs(lumaDeltaN) >= gradientThreshold;
	for (i = 0; i < EXTRA_EDGE_STEPS && !atEndN; i++) {
		uvN -= uvStep * edgeStepSizes[i];;
		lumaDeltaN = GetLuma(uvN) - edgeLuma;
		atEndN = abs(lumaDeltaN) >= gradientThreshold;
		if (!atEndN) uvN -= uvStep * LAST_EDGE_STEP_GUESS;;
	}
	//Get the distnace to the end from the negative and postive directions
	float distanceToEndP, distanceToEndN;
	if (edge.isHorizontal) {
		distanceToEndP = uvP.x - uv.x;
		distanceToEndN = uv.x - uvN.x;
	}
	else {
		distanceToEndP = uvP.y - uv.y;
		distanceToEndN = uv.y - uvN.y;
	}
	//Get the final distance
	float distanceToNearestEnd;
	bool deltaSign;
	if (distanceToEndP <= distanceToEndN) {
		distanceToNearestEnd = distanceToEndP;
		deltaSign = lumaDeltaP >= 0;
	}
	else {
		distanceToNearestEnd = distanceToEndN;
		deltaSign = lumaDeltaN >= 0;
	}
	//return 10.0*distanceToNearestEnd;
	if (deltaSign == (luma.m - edgeLuma >= 0)) {
		return 0.0;
	}
	else {
		return 0.5 - distanceToNearestEnd / (distanceToEndP + distanceToEndN);
	}
}
float4 FXAAPassFragment(Varyings input) : SV_TARGET{
	LumaNeighborhood luma = GetLumaNeighborhood(input.screenUV);
if (canSkipFXAA(luma)) return GetSource(input.screenUV);
	
	FXAAEdge edge = GetFXAAEdge(luma);
	//We blend between the neighboring pixel and the middle pixel
	float blendFactor = max(GetSubpixelBlendFactor(luma)
	,GetEdgeBlendFactor(luma, edge, input.screenUV));
	//blendFactor = GetSubpixelBlendFactor(luma);
	//return blendFactor;
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