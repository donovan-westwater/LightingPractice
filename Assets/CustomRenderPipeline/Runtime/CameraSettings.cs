using System;
using UnityEngine;
using UnityEngine.Rendering;

//These are settings that are meant to specify settings for inidividual cmaeras
//Great for camera overlays, where we want to have want to make sure the bottom layer is clean
//before blending
[Serializable]
public class CameraSettings
{

	[Serializable]
	public struct FinalBlendMode
	{

		public BlendMode source, destination;
	}
	//Here we make sure the first / bottom camera always has a positve alpha. Avoids using bad data
	public FinalBlendMode finalBlendMode = new FinalBlendMode
	{
		source = BlendMode.One,
		destination = BlendMode.Zero
	};

	public bool overridePostFX = false;

	public PostFXSettings postFXSettings = default;
	//Caemra rendering layer mask - makes so objects only show up on certian cameras
	[RenderingLayerMaskField]
	public int renderingLayerMask = -1;
	public bool maskLights = false; //Disables the light mask
	public bool copyDepth = true; //Uses the depth pass for that specfic camera
	public bool copyColor = true;
	public enum RenderScaleMode { Inherit,Multiply,Override};
	public RenderScaleMode renderScaleMode = RenderScaleMode.Inherit;
	[Range(CameraRenderer.renderScaleMin, CameraRenderer.renderScaleMax)]
	public float renderScale = 1f;
	public bool allowFXAA = false;
	public bool keepAlpha = false;
	public float GetRenderScale(float scale)
	{
		return
			renderScaleMode == RenderScaleMode.Inherit ? scale :
			renderScaleMode == RenderScaleMode.Override ? renderScale :
			scale * renderScale;
	}
}