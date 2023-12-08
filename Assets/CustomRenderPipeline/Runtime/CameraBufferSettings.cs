using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct CameraBufferSettings
{

	public bool allowHDR;//Allows a wider range of color intenisties

	public bool copyDepth, copyDepthReflections;

	public bool copyColor, copyColorReflections;
	[Range(CameraRenderer.renderScaleMin, CameraRenderer.renderScaleMax)]
	public float renderScale; //Controls the size of image buffers. Going above causes too many pixels to get skipped when downsampling
	public enum BicubicRescalingMode { Off, UpOnly, UpAndDown }

	public BicubicRescalingMode bicubicRescaling; //Upsamples to reduce aliasing
}
