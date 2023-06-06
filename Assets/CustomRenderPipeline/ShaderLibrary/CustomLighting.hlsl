#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED
//Does a basic directional light calculation with surface normal
float3 IncomingLight(Surface surface, Light light) {
	//saturate clamps value between 0 and 1
	return saturate(dot(surface.normal, light.direction)) * light.color;
}
//Calculates lighting based on light source
float3 GetLighting(Surface surface, BRDF brdf, Light light) {
	return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}
//Calculates lighting using the surface normals
float3 GetLighting(Surface surface, BRDF brdf) {
	float3 color = 0.0;
	for (int i = 0; i < GetDirectionalLightCount(); i++) {
		color += GetLighting(surface, brdf,GetDirectionalLight(i));
	}
	return color;
}


#endif