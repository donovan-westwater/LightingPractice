using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class StrokeGenerationManager : MonoBehaviour
{
    public ComputeShader strokeGenShader;
    public ComputeShader resetShader;
    public ComputeShader debugShader;
    public int highestRes = 512;
    public bool randomizeWithTime = false;
    uint rng_state = 1245;
    [Range(0,45)]
    public float angleMax = 45f;
    [Range(-45, 0)]
    public float angleMin = -45f;
    Texture3D TAM;
    RenderTexture[] outArray = new RenderTexture[8]; //Each one is a mipMap layer
    Texture2DArray stor;
    public Material testMat;
    struct Stroke
    {
        public Vector2 normPos;
        public Vector2 xySlope;
        public float normLength;
        public int isVertical;     
        //Add more strange stroke behavior later via compliler derectives in functions/ this struct
    };
    RenderTexture CreateRenderTexture(int index)
    {
        //Setup the highest res mipMap layer
        outArray[index] = new RenderTexture(highestRes, highestRes, 0);
        outArray[index].dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        outArray[index].volumeDepth = 8;
        outArray[index].enableRandomWrite = true;
        outArray[index].Create();
        return outArray[index];
    }
    // Start is called before the first frame update
    IEnumerator Start()
    {
        if(randomizeWithTime) rng_state *= (uint)System.DateTime.Now.Millisecond;
        //Create Asset to store our art map in
        TAM = new Texture3D(highestRes, highestRes,8 , TextureFormat.ARGB32,1);
        CreateRenderTexture(0);
        CreateRenderTexture(1);
        resetShader.SetTexture(resetShader.FindKernel("CSReset"), Shader.PropertyToID("ResetResults"), outArray[0]);
        resetShader.Dispatch(resetShader.FindKernel("CSReset"), (int)(highestRes/8), (int)(highestRes / 8), 1);
        strokeGenShader.SetInt(Shader.PropertyToID("resolution"), highestRes);
        strokeGenShader.SetVector(Shader.PropertyToID("angleRange"), new Vector4(angleMin, angleMax, 0, 0));
        ComputeBuffer pixelCountBuffer, mipGoalsBuffer, strokeBuffer;
        uint[] pixelCounts = Enumerable.Repeat(0u, 8).ToArray();
        pixelCountBuffer = new ComputeBuffer(pixelCounts.Length, sizeof(uint));
        pixelCountBuffer.SetData(pixelCounts);
        float[] mipGoals = Enumerable.Repeat(1.0f, 8).ToArray();
        mipGoalsBuffer = new ComputeBuffer(mipGoals.Length, sizeof(float));
        mipGoalsBuffer.SetData(mipGoals);
        //Passing in a single stroke in an array because I have lost my mind and can't find any better solutions
        //THe maddness has taken hold and now we enter the lands of insanity
        strokeBuffer = new ComputeBuffer(1, sizeof(float)*6);
        Stroke[] inital = new Stroke[1];
        inital[0].normLength = 0.0f;
        inital[0].normPos = Vector2.zero;
        inital[0].isVertical = 0;
        inital[0].xySlope = Vector2.zero;
        strokeBuffer.SetData(inital);
        //Send buffers over to shader
        strokeGenShader.SetFloat("goalVal", 0.875f);
        strokeGenShader.SetInt("rng_state", (int)rng_state);
        strokeGenShader.SetBuffer(strokeGenShader.FindKernel("CSGatherStrokes"), "mipGoals", mipGoalsBuffer);
        strokeGenShader.SetBuffer(strokeGenShader.FindKernel("CSGatherStrokes"), "mipPixels", pixelCountBuffer);
        strokeGenShader.SetBuffer(strokeGenShader.FindKernel("CSGatherStrokes"), "finalStroke", strokeBuffer);
        strokeGenShader.SetBuffer(strokeGenShader.FindKernel("CSApplyStroke"), "mipGoals", mipGoalsBuffer);
        strokeGenShader.SetBuffer(strokeGenShader.FindKernel("CSApplyStroke"), "mipPixels", pixelCountBuffer);
        strokeGenShader.SetBuffer(strokeGenShader.FindKernel("CSApplyStroke"), "finalStroke", strokeBuffer);
        //Create command buffer so we just have to sumbit the buffer repeatedly intead of redoing the commands
        //We reset the canidate values and then gather the best canidate strokes, then apply the finalist
        CommandBuffer comBuff = new CommandBuffer();
        comBuff.SetExecutionFlags(CommandBufferExecutionFlags.None);
        comBuff.SetComputeFloatParam(strokeGenShader, Shader.PropertyToID("maxCanidateToneVal"), 0f);
        comBuff.SetComputeIntParam(strokeGenShader, Shader.PropertyToID("maxCanidateToneVal_uint"), (int)0u);
        comBuff.DispatchCompute(strokeGenShader,strokeGenShader.FindKernel("CSGatherStrokes"), 1, 1, 1);
        comBuff.DispatchCompute(strokeGenShader, strokeGenShader.FindKernel("CSApplyStroke")
            , (int)(highestRes / 8), (int)(highestRes / 8), 1);
        //Create color pyramid used to test canidate strokes via mips
        RenderTexture colorPyramid = new RenderTexture(highestRes, highestRes,0);
        colorPyramid.dimension = TextureDimension.Tex2DArray;
        colorPyramid.graphicsFormat = GraphicsFormat.R8G8B8A8_SRGB;
        colorPyramid.volumeDepth = 8;
        colorPyramid.useMipMap = true;
        colorPyramid.autoGenerateMips = false;
        colorPyramid.Create();
        for (int cI = 0; cI < 8; cI++)
        {
            Graphics.CopyTexture(outArray[0], cI, 0, colorPyramid, cI, 0);
        }
        //Skip zero since it is just going to be white
        for (int textNo = 1; textNo < outArray.Length; textNo++)
        {
            CreateRenderTexture(textNo);
            //Copy the results of the previous texture to perseve continunity
            Graphics.CopyTexture(outArray[textNo - 1], outArray[textNo]);
            //Set the current goal for the tone, split into 8 tones
            strokeGenShader.SetFloat("goalVal", 1f - (1f/8f) * (textNo));
            strokeGenShader.SetTexture(strokeGenShader.FindKernel("CSGatherStrokes"), Shader.PropertyToID("_Results"), outArray[textNo]);
            strokeGenShader.SetTexture(strokeGenShader.FindKernel("CSApplyStroke"), Shader.PropertyToID("_Results"), outArray[textNo]);
            int strokeN = 0;
            //Apply the strokes 1 at a time, building up until we reach the correct tone
            while (strokeN < 700)
            {
                strokeGenShader.SetTexture(strokeGenShader.FindKernel("CSGatherStrokes"), Shader.PropertyToID("colorPyramid"), colorPyramid);
                Graphics.ExecuteCommandBuffer(comBuff);
                for (int cI = 0; cI < 8; cI++)
                {
                    Graphics.CopyTexture(outArray[textNo], cI, 0, colorPyramid, cI, 0);
                }
                colorPyramid.GenerateMips();
                strokeBuffer.GetData(inital);
                Debug.Log("Stroke choice: " + inital[0].normPos + " " + inital[0].normLength);
                strokeN++;
                rng_state = rng_state * 747796405u + 2891336453u;
                strokeGenShader.SetInt("rng_state", (int)rng_state);
            }
        }
        //Combine the custom mips into a single array
        for(int toneN = 0;toneN <  outArray.Length;toneN++)
        {
            outArray[toneN] = CombineCustomMipsIntoTexture(outArray[toneN]);
        }
        RenderTexture outputArray = CombineTexturesIntoArray(outArray);
        return SaveRTWrapper(outputArray, "StrokeGenerationAndRendering/TAM_FINAL");
    }
    RenderTexture CombineCustomMipsIntoTexture(RenderTexture texArray)
    {
        RenderTextureDescriptor descriptor = texArray.descriptor;
        descriptor.useMipMap = true;
        descriptor.autoGenerateMips = false;
        descriptor.mipCount = texArray.volumeDepth;
        descriptor.dimension = TextureDimension.Tex2D;
        descriptor.volumeDepth = 1;
        RenderTexture output = new RenderTexture(descriptor);
        //Need to go through the texture in the array and add it to the output texture at differnt mip levels
        for(int i = 0; i < texArray.volumeDepth; i++)
        {
            Graphics.CopyTexture(texArray, i, 0, 0, 0, texArray.width >> i, texArray.height >> i
            , output, 0, i, 0, 0);

        }
        texArray.Release();
        return output;
    }
    RenderTexture CombineTexturesIntoArray(RenderTexture[] singleTexs)
    {
        RenderTextureDescriptor descriptor = singleTexs[0].descriptor;
        descriptor.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        descriptor.volumeDepth = 8;
        RenderTexture outputArray = new RenderTexture(descriptor);    
        for (int i = 0; i < singleTexs.Length; i++)
        {
            Graphics.CopyTexture(singleTexs[i], 0, outputArray, i);
            //for (int k = 0;k < singleTexs[i].mipmapCount; k++) { 
            //    Graphics.CopyTexture(singleTexs[i], 0,k, outputArray, i,k);
            //}
        }
        return outputArray;
    }
    void SaveRT3DToTexture3DAsset(RenderTexture rt3D, string pathWithoutAssetsAndExtension,TextureCreationFlags flag)
    {
        int width = rt3D.width, height = rt3D.height, depth = rt3D.volumeDepth;
        var a = new NativeArray<float>(width * height * depth, Allocator.Persistent, NativeArrayOptions.ClearMemory); //change if format is not 8 bits (i was using R8_UNorm) (create a struct with 4 bytes etc)
        NativeArray<float> outputA = new NativeArray<float>(width * height * depth, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        AsyncGPUReadback.RequestIntoNativeArray(ref a, rt3D, 0, (_) =>
        {
            Texture2DArray output = new Texture2DArray(width, height, depth, rt3D.graphicsFormat, TextureCreationFlags.None);
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
    IEnumerator SaveRTWrapper(RenderTexture rt3D, string pathWithoutAssetsAndExtension)
    {
        stor = new Texture2DArray(rt3D.width, rt3D.height, rt3D.volumeDepth, rt3D.graphicsFormat, TextureCreationFlags.MipChain);
        AsyncGPUReadbackRequest[] requests = new AsyncGPUReadbackRequest[rt3D.mipmapCount];
        for (int m = 0; m < rt3D.mipmapCount; m++)
        {
            requests[m] = SaveRTWithAllMips(rt3D, pathWithoutAssetsAndExtension, m);
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
        rt3D.Release();
    }
    AsyncGPUReadbackRequest SaveRTWithAllMips(RenderTexture rt3D, string pathWithoutAssetsAndExtension, int mips)
    {
        int width = rt3D.width, height = rt3D.height, depth = rt3D.volumeDepth;
        int mipWidth = width >> mips, mipHeight = height >> mips;
        var a = new NativeArray<int>(width * height * depth, Allocator.Persistent, NativeArrayOptions.ClearMemory); //change if format is not 8 bits (i was using R8_UNorm) (create a struct with 4 bytes etc)
        NativeArray<int> outputA = new NativeArray<int>(width * height * depth, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        AsyncGPUReadbackRequest reqStore = AsyncGPUReadback.RequestIntoNativeArray(ref a, rt3D, mips, 0, mipWidth, 0, mipHeight, 0, depth, rt3D.graphicsFormat, (_) =>
        {
            for (int index = 0; index < depth; index++)
            {
                var tmpA = a.GetSubArray(index * mipWidth * mipHeight, mipWidth * mipHeight);
                stor.SetPixelData(tmpA, mips, index);
            }
            stor.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            a.Dispose();
            outputA.Dispose();
        });
        return reqStore;
    }
}
