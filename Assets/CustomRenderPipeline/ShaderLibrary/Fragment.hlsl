#ifndef FRAGMENT_INCLUDED
#define FRAGMENT_INCLUDED

TEXTURE2D(_CameraDepthTexture);
TEXTURE2D(_CameraColorTexture);
//Organizes our screen space, fragment infomation
struct Fragment {
	float2 positionSS;
	float2 screenUV; //UV coords of the fragment in the full screen tri
	float depth;
	float bufferDepth; //Stores depth from camera texture
};

Fragment GetFragment(float4 positionSS) {
	Fragment f;
	f.positionSS = positionSS.xy;
	f.screenUV = f.positionSS / _ScreenParams.xy;
	f.depth = IsOrthographicCamera() ? 
		OrthographicDepthBufferToLinear(positionSS.z) : positionSS.w;
	f.bufferDepth = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture,sampler_point_clamp,f.screenUV,0);
	//Need to account for perspective
	f.bufferDepth = IsOrthographicCamera() ?
		OrthographicDepthBufferToLinear(f.bufferDepth) :
		LinearEyeDepth(f.bufferDepth, _ZBufferParams);
	return f;
}
//Sample color buffer from texture
float4 GetBufferColor(Fragment fragment, float2 uvOffset = float2(0.0, 0.0)) {
	float2 uv = fragment.screenUV + uvOffset;
	return SAMPLE_TEXTURE2D_LOD(_CameraColorTexture, sampler_linear_clamp, uv, 0);
}
#endif