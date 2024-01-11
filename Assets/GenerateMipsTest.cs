using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GenerateMipsTest : MonoBehaviour
{
    public ComputeShader testMips;
    // Start is called before the first frame update
    void Start()
    {
        RenderTexture test = new RenderTexture(256, 256, 0);
        test.enableRandomWrite = true;
        test.autoGenerateMips = false;
        test.useMipMap = true;
        test.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
        test.Create();

        testMips.SetTexture(testMips.FindKernel("CSMain"), "Result", test);
        testMips.Dispatch(testMips.FindKernel("CSMain"), 32, 32, 1);

        test.GenerateMips();
        Texture2D outTex = new Texture2D(256, 256);
        var rtTmp = RenderTexture.active;
        RenderTexture.active = test;
        outTex.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
        RenderTexture.active = rtTmp;
        //Graphics.CopyTexture(test, outTex);
        outTex.Apply();
        System.IO.File.WriteAllBytes("Assets/StrokeGenerationAndRendering/MipGenTest" + ".png", outTex.EncodeToPNG());
    }
}
