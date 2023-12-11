using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class StrokeGenerationManager : MonoBehaviour
{
    public ComputeShader strokeGenShader;
    public int highestRes = 256;
    Texture2DArray TAM;
    RenderTexture[] outArray = new RenderTexture[8]; //Each one is a mipMap layer
    // Start is called before the first frame update
    void Start()
    {
        //Create Asset to store our art map in
        TAM = new Texture2DArray(highestRes, highestRes, 8, TextureFormat.ARGB32, 8, false);
        //Setup the highest res mipMap layer
        outArray[0] = new RenderTexture(highestRes, highestRes, 8);
        outArray[0].dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        outArray[0].enableRandomWrite = true;
        outArray[0].Create();
        //We dispatch to the shader in sequental layers, starting from the bottom mips to the top
        //We wait for each to finish before moving on
        strokeGenShader.SetTexture(0, Shader.PropertyToID("_Results"),outArray[0]);
        strokeGenShader.SetInt(Shader.PropertyToID("resolution"), highestRes);
        strokeGenShader.SetInt(Shader.PropertyToID("previousResolution"), highestRes/2);
        strokeGenShader.Dispatch(0, 32, 32, 2);
        //Retrive map from GPU so we don't have to do this again
        var rtTmp = RenderTexture.active;
        Graphics.SetRenderTarget(outArray[0], 0, CubemapFace.Unknown, 0);
        //RenderTexture.active = outArray[0];
        Texture2D test = new Texture2D(highestRes, highestRes);
        test.ReadPixels(new Rect(0, 0, outArray[0].width, outArray[0].height), 0, 0);
        test.Apply();
        RenderTexture.active = rtTmp;
        TAM.SetPixels(test.GetPixels(0, 0, test.width, test.height),0,0);
        //Test for getting the 2nd one
        rtTmp = RenderTexture.active;
        Graphics.SetRenderTarget(outArray[0], 0,CubemapFace.Unknown,1);
        test = new Texture2D(highestRes, highestRes);
        test.ReadPixels(new Rect(0, 0, outArray[0].width, outArray[0].height), 0, 0);
        test.Apply();
        RenderTexture.active = rtTmp;
        TAM.SetPixels(test.GetPixels(0, 0, test.width, test.height), 1, 0);
        AssetDatabase.CreateAsset(TAM, "Assets/StrokeGenerationAndRendering/TAM.asset");
        outArray[0].Release();
    }

}
