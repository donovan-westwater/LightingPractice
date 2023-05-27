using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour
{
	static MaterialPropertyBlock block;
	static int baseColorId = Shader.PropertyToID("_BaseColor");

	[SerializeField]
	Color baseColor = Color.white;
	void Awake()
	{
		OnValidate();
	}
	//Called when values changed
	void OnValidate()
	{
		if (block == null)
		{
			block = new MaterialPropertyBlock();
		}
		block.SetColor(baseColorId, baseColor);
		GetComponent<Renderer>().SetPropertyBlock(block);
	}
}
