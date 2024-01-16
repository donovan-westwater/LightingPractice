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
    Texture2DArray stor;
    // Start is called before the first frame update
    IEnumerator Start()
    {
        RenderTexture test = new RenderTexture(256, 256, 0);
        test.graphicsFormat = GraphicsFormat.R8G8B8A8_SRGB;
        test.enableRandomWrite = true;
        test.autoGenerateMips = false;
        test.useMipMap = true;
        test.volumeDepth = 8;
        test.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        test.Create();
        testMips.SetTexture(testMips.FindKernel("CSMain"), "Result", test);
        testMips.Dispatch(testMips.FindKernel("CSMain"), 32, 32, 8);

        test.GenerateMips();
        return SaveRTWrapper(test, "StrokeGenerationAndRendering/MipGenTest");
    }
    IEnumerator SaveRTWrapper(RenderTexture rt3D, string pathWithoutAssetsAndExtension)
    {
        stor = new Texture2DArray(rt3D.width, rt3D.height, rt3D.volumeDepth, rt3D.graphicsFormat, TextureCreationFlags.MipChain);
        AsyncGPUReadbackRequest[] requests = new AsyncGPUReadbackRequest[rt3D.mipmapCount];
        for (int m = 0; m < rt3D.mipmapCount; m++)
        {
            requests[m] = SaveRT3DToTexture3DAsset(rt3D, pathWithoutAssetsAndExtension, m);
        }
        List<int> finshedReqs = new List<int>();
        int rIndex = 0;
        while (finshedReqs.Count < rt3D.mipmapCount)
        {
            if (requests[rIndex].done && !finshedReqs.Contains(rIndex))
            {
                finshedReqs.Add(rIndex);
            }
            rIndex++;
            if (rIndex >= requests.Length) rIndex = 0;
            yield return new WaitForEndOfFrame();
        }
        AssetDatabase.CreateAsset(stor, $"Assets/{pathWithoutAssetsAndExtension}.asset");
        AssetDatabase.SaveAssetIfDirty(stor);
    }
    AsyncGPUReadbackRequest SaveRT3DToTexture3DAsset(RenderTexture rt3D, string pathWithoutAssetsAndExtension,int mips)
    {
        int width = rt3D.width, height = rt3D.height, depth = rt3D.volumeDepth;
        int mipWidth = width >> mips, mipHeight = height >> mips;
        var a = new NativeArray<int>(width * height * depth, Allocator.Persistent, NativeArrayOptions.ClearMemory); //change if format is not 8 bits (i was using R8_UNorm) (create a struct with 4 bytes etc)
        NativeArray<int> outputA = new NativeArray<int>(width * height * depth, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        AsyncGPUReadbackRequest reqStore = AsyncGPUReadback.RequestIntoNativeArray(ref a, rt3D, mips,0,mipWidth,0,mipHeight,0,depth,rt3D.graphicsFormat, (_) =>
        {
            //Texture2DArray output = new Texture2DArray(width, height, depth, rt3D.graphicsFormat, TextureCreationFlags.MipChain);
            //NativeArray<float>.Copy(a, 0, outputA, 0, 1);
            //output.SetPixelData(outputA, 0, 0);
            for (int index = 0; index < depth; index++)
            {
                var tmpA = a.GetSubArray(index * mipWidth * mipHeight, mipWidth * mipHeight);
                stor.SetPixelData(tmpA, mips, index);
            }
            stor.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            //AssetDatabase.CreateAsset(output, $"Assets/{pathWithoutAssetsAndExtension}.asset");
            //AssetDatabase.SaveAssetIfDirty(output);
            a.Dispose();
            outputA.Dispose();
            //Graphics.CopyTexture(stor, stor);
            //stor.Apply();
            //rt3D.Release();
        });
        return reqStore;
    }
}
