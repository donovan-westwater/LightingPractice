using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct CameraBufferSettings
{

	public bool allowHDR;//Allows a wider range of color intenisties

	public bool copyDepth, copyDepthReflections;

	public bool copyColor, copyColorReflections;
}
