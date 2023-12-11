using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct CameraBufferSettings
{
	//Stands for Fast Apporixmate Anti Anliasing
	[Serializable]
	public struct FXAA
    {
		public bool enabled;
		[Range(0.0312f, 0.0833f)] //Based on the original algorthmn description for what values to use
		public float fixedThreshold;
		[Range(0.063f, 0.333f)] //Based on the original algorthmn description for what values to use
		public float relativeThreshold;
		[Range(0, 1)]
		public float subpixelBlending;
	}
	public FXAA fxaa;
	public bool allowHDR;//Allows a wider range of color intenisties

	public bool copyDepth, copyDepthReflections;

	public bool copyColor, copyColorReflections;
	[Range(CameraRenderer.renderScaleMin, CameraRenderer.renderScaleMax)]
	public float renderScale; //Controls the size of image buffers. Going above causes too many pixels to get skipped when downsampling
	public enum BicubicRescalingMode { Off, UpOnly, UpAndDown }

	public BicubicRescalingMode bicubicRescaling; //Upsamples to reduce aliasing
}
