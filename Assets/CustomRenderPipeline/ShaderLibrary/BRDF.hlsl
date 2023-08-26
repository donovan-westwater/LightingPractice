#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED
//Keepingn BRDF seperate from surfaces to split diffuse and specular elements
struct BRDF {
	float3 diffuse;
	float3 specular;
	float roughness;
	float perceptualRoughness;
	float fresnel; //Value used for fresnal reflections. Turns surface into perfect mirror
};
//This the lowest level of reflectivity on avaerage for metals
#define MIN_REFLECTIVITY 0.04

//Reflectivity varies for materials
float OneMinusReflectivity(float metallic) {
	float range = 1.0 - MIN_REFLECTIVITY;
	return range - metallic * range;
}
//calculates spec strength based on view dir and formula: roughness^2/(d^2*max(0.1,(L * H)^2)*normal)
//d = (normal*(light v + view v <- all normalized))^2 * (roughness-1)^2 + 1.0001
float SpecularStrength(Surface surface, BRDF brdf, Light light) {
	float3 h = SafeNormalize(light.direction + surface.viewDirection); //use safe normalize to avoid / by 0
	float nh2 = Square(saturate(dot(surface.normal, h)));
	float lh2 = Square(saturate(dot(light.direction, h)));
	float r2 = Square(brdf.roughness);
	float d2 = Square(nh2 * (r2 - 1.0) + 1.00001);
	float normalization = brdf.roughness * 4.0 + 2.0;
	return r2 / (d2 * max(0.1, lh2) * normalization);
}
float3 DirectBRDF(Surface surface, BRDF brdf, Light light) {
	return SpecularStrength(surface, brdf, light) * brdf.specular + brdf.diffuse;
}
BRDF GetBRDF(Surface surface, bool applyAlphaToDiffuse = false) {
	BRDF brdf;
	float oneMinusReflectivity = OneMinusReflectivity(surface.metallic);
	brdf.diffuse = surface.color * oneMinusReflectivity;
	if (applyAlphaToDiffuse) {
		brdf.diffuse *= surface.alpha; //premultiply alpha blending for better diffuse reflections 
	} 
	brdf.specular = lerp(MIN_REFLECTIVITY, surface.color, surface.metallic); //Amount of outgoing light cannot exceed incoming light
	//Used to simulate muddy reflections via using lower mip levels
	brdf.perceptualRoughness =
		PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
	brdf.roughness = PerceptualRoughnessToRoughness(brdf.perceptualRoughness);
	brdf.fresnel = saturate(surface.smoothness + 1.0 - oneMinusReflectivity);
	return brdf;
}
#endif