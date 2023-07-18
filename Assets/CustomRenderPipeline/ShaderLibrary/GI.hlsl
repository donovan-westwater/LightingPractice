#ifndef CUSTOM_GI_INCLUDED
#define CUSTOM_GI_INCLUDED
//Sturct used when sampling baked light
struct GI {
	float3 diffuse;
};
//Light map sampling
GI GetGI(float2 lightMapUV) {
	GI gi;
	gi.diffuse = float3(lightMapUV, 0.0);
	return gi;
}

#endif

