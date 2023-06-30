#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED
//Does a basic directional light calculation with surface normal
float3 IncomingLight(Surface surface, Light light) {
	//saturate clamps value between 0 and 1
	float lIn = saturate(dot(surface.normal, light.direction)
	*light.attenuation) * light.color;
	//lIn = lerp(0.5,1,ceil(lIn)); Cell Shading mode!
	return lIn * light.color;
}
//Calculates lighting based on light source
float3 GetLighting(Surface surface, BRDF brdf, Light light) {
	return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}
//Calculates lighting using the surface normals
float3 GetLighting(Surface surfaceWS, BRDF brdf) {
	float3 color = 0.0;
	for (int i = 0; i < GetDirectionalLightCount(); i++) {
		color += GetLighting(surfaceWS, brdf,GetDirectionalLight(i,surfaceWS));
	}
	return color;
}


#endif