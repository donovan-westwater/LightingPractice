using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

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
        test.volumeDepth = 8;
        test.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        test.Create();

        testMips.SetTexture(testMips.FindKernel("CSMain"), "Result", test);
        testMips.Dispatch(testMips.FindKernel("CSMain"), 32, 32, 8);

        test.GenerateMips();

        SaveRT3DToTexture3DAsset(test,"StrokeGenerationAndRendering/MipGenTest",0);
    }
    void SaveRT3DToTexture3DAsset(RenderTexture rt3D, string pathWithoutAssetsAndExtension,int mips)
    {
        int width = rt3D.width, height = rt3D.height, depth = rt3D.volumeDepth;
        int mipWidth = width >> mips, mipHeight = height >> mips;
        var a = new NativeArray<float>(width * height * depth, Allocator.Persistent, NativeArrayOptions.ClearMemory); //change if format is not 8 bits (i was using R8_UNorm) (create a struct with 4 bytes etc)
        NativeArray<float> outputA = new NativeArray<float>(width * height * depth, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        AsyncGPUReadback.RequestIntoNativeArray(ref a, rt3D, mips,0,mipWidth,0,mipHeight,0,depth,rt3D.graphicsFormat, (_) =>
        {
            Texture2DArray output = new Texture2DArray(width, height, depth, rt3D.graphicsFormat, TextureCreationFlags.None);
            //NativeArray<float>.Copy(a, 0, outputA, 0, 1);
            //output.SetPixelData(outputA, 0, 0);
            for (int index = 0; index < depth; index++)
            {
                var tmpA = a.GetSubArray(index * mipWidth * mipHeight, mipWidth * mipHeight);
                output.SetPixelData(tmpA, mips, index);
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
