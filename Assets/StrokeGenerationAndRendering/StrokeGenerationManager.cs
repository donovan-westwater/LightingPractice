using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class StrokeGenerationManager : MonoBehaviour
{
    public ComputeShader strokeGenShader;
    public int highestRes = 256;
    Texture3D TAM;
    RenderTexture[] outArray = new RenderTexture[8]; //Each one is a mipMap layer
    public Material testMat;
    // Start is called before the first frame update
    void Start()
    {
        //Create Asset to store our art map in
        TAM = new Texture3D(highestRes, highestRes,8 , TextureFormat.ARGB32,1);
        //Setup the highest res mipMap layer
        outArray[0] = new RenderTexture(highestRes, highestRes, 0);
        outArray[0].dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        outArray[0].volumeDepth = 8;
        outArray[0].enableRandomWrite = true;
        outArray[0].Create();
        //We dispatch to the shader in sequental layers, starting from the bottom mips to the top
        //We wait for each to finish before moving on
        strokeGenShader.SetTexture(0, Shader.PropertyToID("_Results"),outArray[0]);
        strokeGenShader.SetInt(Shader.PropertyToID("resolution"), highestRes);
        strokeGenShader.Dispatch(0, 32, 32, 1);
        //Retrive map from GPU so we don't have to do this again
        var rtTmp = RenderTexture.active;
        Graphics.SetRenderTarget(outArray[0], 0, CubemapFace.Unknown, 0);
        //RenderTexture.active = outArray[0];
        Texture2D test = new Texture2D(highestRes, highestRes);
        test.ReadPixels(new Rect(0, 0, outArray[0].width, outArray[0].height), 0, 0);
        test.Apply();
        RenderTexture.active = rtTmp;
        Color[] tex1C = test.GetPixels();
        //TAM.SetPixels(test.GetPixels(0, 0, test.width, test.height),0,0);
        //Test for getting the 2nd one
        rtTmp = RenderTexture.active;
        Graphics.SetRenderTarget(outArray[0], 0,CubemapFace.Unknown,1);
        test = new Texture2D(highestRes, highestRes);
        test.ReadPixels(new Rect(0, 0, outArray[0].width, outArray[0].height), 0, 0);
        test.Apply();
        Color[] tex2C = test.GetPixels();
        Color c = test.GetPixel(0, 0);
        if (c.r > 0) Debug.Log("2nd level is working");
        RenderTexture.active = rtTmp;
        //TAM.SetPixels(test.GetPixels(0, 0, test.width, test.height), 1, 0);
        Color[] totalC = TAM.GetPixels(0);
        int i = 0;
        int j;
        for (j =0;j < tex1C.Length; j++)
        {
            totalC[i] = tex1C[j];
            i++;
        }
        for (j = 0; j < tex2C.Length; j++)
        {
            if (tex2C[j].r > 0) Debug.Log("RED");
            totalC[i] = tex2C[j];
            i++;
        }
        TAM.SetPixels(totalC, 0);
        TAM.Apply();
        //AssetDatabase.CreateAsset(outArray[0], "Assets/StrokeGenerationAndRendering/Test.renderTexture");
        //testMat.SetTexture("_MainTex", outArray[0]);
        //AssetDatabase.CreateAsset(TAM, "Assets/StrokeGenerationAndRendering/TAM.asset");
        SaveRT3DToTexture3DAsset(outArray[0], "StrokeGenerationAndRendering/TAM");
        outArray[0].Release();
    }
    //Subarray section is broken. FIX!
    void SaveRT3DToTexture3DAsset(RenderTexture rt3D, string pathWithoutAssetsAndExtension)
    {
        int width = rt3D.width, height = rt3D.height, depth = rt3D.volumeDepth;
        var a = new NativeArray<float>(width * height * depth, Allocator.Persistent, NativeArrayOptions.ClearMemory); //change if format is not 8 bits (i was using R8_UNorm) (create a struct with 4 bytes etc)
        NativeArray<float> outputA = new NativeArray<float>(width * height * depth, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        AsyncGPUReadback.RequestIntoNativeArray(ref a, rt3D, 0, (_) =>
        {
            Texture2DArray output = new Texture2DArray(width, height, depth, rt3D.graphicsFormat, TextureCreationFlags.None);
            //NativeArray<float>.Copy(a, 0, outputA, 0, 1);
            //output.SetPixelData(outputA, 0, 0);
            for(int index = 0; index < depth; index++)
            {
                var tmpA = a.GetSubArray(index * width * height,width * height);
                output.SetPixelData(tmpA, 0, index);
            }
            output.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            AssetDatabase.CreateAsset(output, $"Assets/{pathWithoutAssetsAndExtension}.asset");
            AssetDatabase.SaveAssetIfDirty(output);
            a.Dispose();
            outputA.Dispose();
            rt3D.Release();
        });
    }
}
