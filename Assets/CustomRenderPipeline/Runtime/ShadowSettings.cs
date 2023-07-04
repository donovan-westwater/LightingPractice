using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[System.Serializable]
public class ShadowSettings
{
    //Defines how far away we render shadows at the maximum
    [Min(0.001f)]
    public float maxDistance = 100f;

    [Range(0.001f, 1f)]
    public float distanceFade = 0.1f;
    //What is the texture size for the shadow
    public enum TextureSize
    {
        _256 = 256, _512 = 512, _1024 = 1024,
        _2048 = 2048, _4096 = 4096, _8192 = 8192
    }
    //We define a single texture to handle the shadow maps
    [System.Serializable]
    public struct Directional
    {
        //Handles the size of the shadow map texture
        public TextureSize atlasSize;
        //Settings for shadow cascades
        //Each cascade adds more detail to the shadows by zooming out further
        //Start close to camera then zoom out
        //How many renders of the shadow shall we use?
        [Range(1, 4)]
        public int cascadeCount;

        [Range(0f, 1f)]
        public float cascadeRatio1, cascadeRatio2, cascadeRatio3;
        //Last cascade cover entire screen so it doesn't need a ratio (its 1)
        
        //Accessing ratios
        public Vector3 CascadeRatios =>
            new Vector3(cascadeRatio1, cascadeRatio2, cascadeRatio3);
    }

    public Directional directional = new Directional
    {
        atlasSize = TextureSize._1024,
        cascadeCount = 4,
        cascadeRatio1 = 0.1f,
        cascadeRatio2 = 0.25f,
        cascadeRatio3 = 0.5f
    };

}
