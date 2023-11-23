#ifndef FRAGMENT_INCLUDED
#define FRAGMENT_INCLUDED
//Organizes our screen space, fragment infomation
struct Fragment {
	float2 positionSS;
	float depth;
};

Fragment GetFragment(float4 positionSS) {
	Fragment f;
	f.positionSS = positionSS.xy;
	f.depth = IsOrthographicCamera() ? 
		OrthographicDepthBufferToLinear(positionSS.z) : positionSS.w;
	return f;
}

#endif