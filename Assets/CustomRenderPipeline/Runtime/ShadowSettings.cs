using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[System.Serializable]
public class ShadowSettings
{
    //Defines how far away we render shadows at the maximum
    [Min(0f)]
    public float maxDistance = 100f;
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
    }

    public Directional directional = new Directional
    {
        atlasSize = TextureSize._1024
    };
}
