using System.Collections;
using System.Collections.Generic;
using UnityEngine;

partial class CustomRenderPipelineAsset
{

#if UNITY_EDITOR
	//Reconfiguring the rendering layer names
	//This holds the custom names
	static string[] renderingLayerNames;

	static CustomRenderPipelineAsset()
	{
		renderingLayerNames = new string[31]; //32 will overflow and get back to 0
		for (int i = 0; i < renderingLayerNames.Length; i++)
		{
			renderingLayerNames[i] = "Layer " + (i + 1);
		}
	}

	public override string[] renderingLayerMaskNames => renderingLayerNames;

#endif
}
