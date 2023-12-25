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
    public int highestRes = 512;
    Texture3D TAM;
    RenderTexture[] outArray = new RenderTexture[8]; //Each one is a mipMap layer
    public Material testMat;
    struct Stroke
    {
        public Vector2 normPos;
        public float normLength;
        //Add more strange stroke behavior later via compliler derectives in functions/ this struct
    };
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
        resetShader.SetTexture(resetShader.FindKernel("CSReset"), Shader.PropertyToID("ResetResults"), outArray[0]);
        resetShader.Dispatch(resetShader.FindKernel("CSReset"), 32, 32, 1);
        //We dispatch to the shader in sequental layers, starting from the bottom mips to the top
        //We wait for each to finish before moving on
        strokeGenShader.SetTexture(strokeGenShader.FindKernel("CSMain"), Shader.PropertyToID("_Results"),outArray[0]);
        strokeGenShader.SetInt(Shader.PropertyToID("resolution"), highestRes);
        ComputeBuffer pixelCountBuffer, mipGoalsBuffer, strokeBuffer;
        uint[] pixelCounts = Enumerable.Repeat(0u, 8).ToArray();
        pixelCountBuffer = new ComputeBuffer(pixelCounts.Length, sizeof(uint));
        pixelCountBuffer.SetData(pixelCounts);
        float[] mipGoals = Enumerable.Repeat(1.0f, 8).ToArray();
        mipGoalsBuffer = new ComputeBuffer(mipGoals.Length, sizeof(float));
        mipGoalsBuffer.SetData(mipGoals);
        //Passing in a single stroke in an array because I have lost my mind and can't find any better solutions
        //THe maddness has taken hold and now we enter the lands of insanity
        strokeBuffer = new ComputeBuffer(1, sizeof(float)*3);
        Stroke[] inital = new Stroke[1];
        inital[0].normLength = 0.0f;
        inital[0].normPos = Vector2.zero;
        strokeBuffer.SetData(inital);
        //strokeGenShader.SetBuffer(strokeGenShader.FindKernel("CSMain"), "mipGoals", mipGoalsBuffer);
        //strokeGenShader.SetBuffer(strokeGenShader.FindKernel("CSMain"), "mipPixels", pixelCountBuffer);
        strokeGenShader.SetFloat("goalVal", 0.80f);//.875
        //strokeGenShader.Dispatch(strokeGenShader.FindKernel("CSMain"), 32, 32, 1);
        //TEST CODE TO APPLY A SINGLE STROKE TO TEXTURE!
        strokeGenShader.SetBuffer(strokeGenShader.FindKernel("CSGatherStrokes"), "mipGoals", mipGoalsBuffer);
        strokeGenShader.SetBuffer(strokeGenShader.FindKernel("CSGatherStrokes"), "mipPixels", pixelCountBuffer);
        strokeGenShader.SetBuffer(strokeGenShader.FindKernel("CSGatherStrokes"), "finalStroke", strokeBuffer);
        strokeGenShader.SetBuffer(strokeGenShader.FindKernel("CSApplyStroke"), "mipGoals", mipGoalsBuffer);
        strokeGenShader.SetBuffer(strokeGenShader.FindKernel("CSApplyStroke"), "mipPixels", pixelCountBuffer);
        strokeGenShader.SetBuffer(strokeGenShader.FindKernel("CSApplyStroke"), "finalStroke", strokeBuffer);
        strokeGenShader.SetTexture(strokeGenShader.FindKernel("CSGatherStrokes"), Shader.PropertyToID("_Results"), outArray[0]);
        strokeGenShader.SetTexture(strokeGenShader.FindKernel("CSApplyStroke"), Shader.PropertyToID("_Results"), outArray[0]);
        //CREATE COMPUTE BUFFER FOR STROKE STRUCT OF FINAL STROKE
        CommandBuffer comBuff = new CommandBuffer();
        comBuff.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);

        comBuff.DispatchCompute(strokeGenShader,strokeGenShader.FindKernel("CSGatherStrokes"), 1, 1, 1);
        comBuff.CreateAsyncGraphicsFence();
        comBuff.DispatchCompute(strokeGenShader, strokeGenShader.FindKernel("CSApplyStroke"), 32, 32, 1);
        comBuff.CreateAsyncGraphicsFence();

        Graphics.ExecuteCommandBufferAsync(comBuff, 0);
        Graphics.ExecuteCommandBufferAsync(comBuff, 0);

        pixelCountBuffer.GetData(pixelCounts);
        mipGoalsBuffer.GetData(mipGoals);
        strokeBuffer.GetData(inital);
        Debug.Log("Stroke choice: " + inital[0].normPos + " " + inital[0].normLength);
        for(int pc =0; pc < mipGoals.Length; pc++)
        {
            Debug.Log(mipGoals[pc]);
        }
        
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
