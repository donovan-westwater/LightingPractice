using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject {
    [SerializeField]
    //Custom triangle shader
    Shader shader = default;
	
	//Material for full screen triangle shader
	[System.NonSerialized]
	Material material;
	//Bloom settings
	[System.Serializable]
	public struct BloomSettings
	{

		[Range(0f, 16f)]
		public int maxIterations; //Controls how many layers we use

		[Min(1f)]
		public int downscaleLimit; //At what size do we stop sampling?

        public bool bicubicUpsampling; //Switches to a different filtering method for bloom

        [Min(0f)]
		public float threshold; //Controls cutoff point for bloom effect

		[Range(0f, 1f)]
		public float thresholdKnee; //Controls the steepness of the bloom cutoff curve or knee curve

		[Min(0f)]
		public float intensity; //Controls intensity of the effect

		public bool fadeFireflies; //Reduces the flickering caused by moving the camera around
	}

	[SerializeField]
	BloomSettings bloom = default;

	public BloomSettings Bloom => bloom;
	public Material Material
	{
		get
		{
			if (material == null && shader != null)
			{
				material = new Material(shader);
				material.hideFlags = HideFlags.HideAndDontSave;
			}
			return material;
		}
	}

}
