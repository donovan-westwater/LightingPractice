#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED
//Does a basic directional light calculation with surface normal
float3 IncomingLight(Surface surface, Light light) {
	//saturate clamps value between 0 and 1
	return saturate(dot(surface.normal, light.direction)) * light.color;
}
//Calculates lighting based on light source
float3 GetLighting(Surface surface, Light light) {
	return IncomingLight(surface, light) * surface.color;
}
//Calculates lighting using the surface normals
float3 GetLighting(Surface surface) {
	return GetLighting(surface, GetDirectionalLight());
}


#endif