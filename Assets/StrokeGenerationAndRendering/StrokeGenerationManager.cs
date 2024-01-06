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
    uint rng_state = 1245;
    Texture3D TAM;
    RenderTexture[] outArray = new RenderTexture[8]; //Each one is a mipMap layer
    public Material testMat;
    struct Stroke
    {
        public Vector2 normPos;
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
        //NEW CODE 1/5/2024
        //outArray[index].useMipMap = true;
        //outArray[index].autoGenerateMips = false;
        //NEW CODE 1/5/2024 END
        outArray[index].Create();
        return outArray[index];
    }
    // Start is called before the first frame update
    void Start()
    {
        //Create Asset to store our art map in
        TAM = new Texture3D(highestRes, highestRes,8 , TextureFormat.ARGB32,1);
        CreateRenderTexture(0);
        CreateRenderTexture(1);
        resetShader.SetTexture(resetShader.FindKernel("CSReset"), Shader.PropertyToID("ResetResults"), outArray[0]);
        resetShader.Dispatch(resetShader.FindKernel("CSReset"), 32, 32, 1);
        //Graphics.CopyTexture(outArray[0], outArray[1]);
        //We dispatch to the shader in sequental layers, starting from the bottom mips to the top
        //We wait for each to finish before moving on
        //strokeGenShader.SetTexture(strokeGenShader.FindKernel("CSMain"), Shader.PropertyToID("_Results"),outArray[1]);
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
        strokeBuffer = new ComputeBuffer(1, sizeof(float)*4);
        Stroke[] inital = new Stroke[1];
        inital[0].normLength = 0.0f;
        inital[0].normPos = Vector2.zero;
        inital[0].isVertical = 0;
        strokeBuffer.SetData(inital);
        //strokeGenShader.SetBuffer(strokeGenShader.FindKernel("CSMain"), "mipGoals", mipGoalsBuffer);
        //strokeGenShader.SetBuffer(strokeGenShader.FindKernel("CSMain"), "mipPixels", pixelCountBuffer);
        strokeGenShader.SetFloat("goalVal", 0.875f);//.875
        strokeGenShader.SetInt("rng_state", (int)rng_state);
        //strokeGenShader.Dispatch(strokeGenShader.FindKernel("CSMain"), 32, 32, 1);
        //TEST CODE TO APPLY A SINGLE STROKE TO TEXTURE!
        strokeGenShader.SetBuffer(strokeGenShader.FindKernel("CSGatherStrokes"), "mipGoals", mipGoalsBuffer);
        strokeGenShader.SetBuffer(strokeGenShader.FindKernel("CSGatherStrokes"), "mipPixels", pixelCountBuffer);
        strokeGenShader.SetBuffer(strokeGenShader.FindKernel("CSGatherStrokes"), "finalStroke", strokeBuffer);
        strokeGenShader.SetBuffer(strokeGenShader.FindKernel("CSApplyStroke"), "mipGoals", mipGoalsBuffer);
        strokeGenShader.SetBuffer(strokeGenShader.FindKernel("CSApplyStroke"), "mipPixels", pixelCountBuffer);
        strokeGenShader.SetBuffer(strokeGenShader.FindKernel("CSApplyStroke"), "finalStroke", strokeBuffer);
        //strokeGenShader.SetTexture(strokeGenShader.FindKernel("CSGatherStrokes"), Shader.PropertyToID("_Results"), outArray[1]);
        //strokeGenShader.SetTexture(strokeGenShader.FindKernel("CSApplyStroke"), Shader.PropertyToID("_Results"), outArray[1]);
        //CREATE COMPUTE BUFFER FOR STROKE STRUCT OF FINAL STROKE
        CommandBuffer comBuff = new CommandBuffer();
        comBuff.SetExecutionFlags(CommandBufferExecutionFlags.None);

        //comBuff.CreateAsyncGraphicsFence();
        comBuff.SetComputeFloatParam(strokeGenShader, Shader.PropertyToID("maxCanidateToneVal"), 0f);
        comBuff.DispatchCompute(strokeGenShader,strokeGenShader.FindKernel("CSGatherStrokes"), 1, 1, 1);
        comBuff.DispatchCompute(strokeGenShader, strokeGenShader.FindKernel("CSApplyStroke"), 32, 32, 1);

        //GraphicsFence applyFence = comBuff.CreateAsyncGraphicsFence();
        //comBuff.WaitOnAsyncGraphicsFence(applyFence);
        RenderTexture colorPyramid = new RenderTexture(highestRes, highestRes,0);
        colorPyramid.dimension = TextureDimension.Tex2D;
        colorPyramid.useMipMap = true;
        colorPyramid.autoGenerateMips = false;
        for (int textNo = 1; textNo < outArray.Length; textNo++)
        {
            CreateRenderTexture(textNo);
            Graphics.CopyTexture(outArray[textNo - 1], outArray[textNo]);
            strokeGenShader.SetFloat("goalVal", 1f - (1f/8f) * (textNo));
            strokeGenShader.SetTexture(strokeGenShader.FindKernel("CSGatherStrokes"), Shader.PropertyToID("_Results"), outArray[textNo]);
            strokeGenShader.SetTexture(strokeGenShader.FindKernel("CSApplyStroke"), Shader.PropertyToID("_Results"), outArray[textNo]);
            int strokeN = 0;
            while (strokeN < 700)
            {
                //NEW CODE start 1/5/24
                Graphics.CopyTexture(outArray[textNo], 0, 0, colorPyramid, 0, 0);
                colorPyramid.GenerateMips();
                strokeGenShader.SetTexture(strokeGenShader.FindKernel("CSGatherStrokes"), Shader.PropertyToID("colorPyramid"), colorPyramid);
                //new code end
                Graphics.ExecuteCommandBuffer(comBuff);
                strokeBuffer.GetData(inital);
                Debug.Log("Stroke choice: " + inital[0].normPos + " " + inital[0].normLength);
                strokeN++;
                rng_state = rng_state * 747796405u + 2891336453u;
                strokeGenShader.SetInt("rng_state", (int)rng_state);
            }
            
        }
            //Graphics.ExecuteCommandBufferAsync(comBuff, 0);

        pixelCountBuffer.GetData(pixelCounts);
        mipGoalsBuffer.GetData(mipGoals);
        //strokeBuffer.GetData(inital);
        //Debug.Log("Stroke choice: " + inital[0].normPos + " " + inital[0].normLength);
        for(int pc =0; pc < mipGoals.Length; pc++)
        {
            Debug.Log(mipGoals[pc]);
        }
        
        //AssetDatabase.CreateAsset(outArray[0], "Assets/StrokeGenerationAndRendering/Test.renderTexture");
        //testMat.SetTexture("_MainTex", outArray[0]);
        //AssetDatabase.CreateAsset(TAM, "Assets/StrokeGenerationAndRendering/TAM.asset");

        for(int toneN = 0;toneN <  outArray.Length;toneN++)
        {
            string suffix = "_Tone" + (toneN + 1);
            string name = "StrokeGenerationAndRendering/TAM" + suffix;
            SaveRT3DToTexture3DAsset(outArray[toneN], name);
            outArray[toneN].Release();
        }
        //SaveRT3DToTexture3DAsset(outArray[1], "StrokeGenerationAndRendering/TAM");
        //SaveRT3DToTexture3DAsset(outArray[2], "StrokeGenerationAndRendering/TAM_Tone2");
        //outArray[0].Release();
        //outArray[1].Release();
        //outArray[2].Release();
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
