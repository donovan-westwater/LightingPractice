using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//We can't edit camera settings directly. To add the settings we want
//make a custom camera compontent, only 1 per camera
//This will handle our custom settings
[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public class CustomRenderPipelineCamera : MonoBehaviour
{

	[SerializeField]
	CameraSettings settings = default;
	//?? = settings == null ? settings = new CameraSettings() : settings;
	public CameraSettings Settings => settings ?? (settings = new CameraSettings());
}
