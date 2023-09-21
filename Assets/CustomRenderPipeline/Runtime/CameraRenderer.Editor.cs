using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

partial class CameraRenderer
{
	//Partial class splits class definaion across files for organization
	//Any partial functions not defined in code are ignored during compilation
	partial void DrawGizmosBeforeFX();

	partial void DrawGizmosAfterFX();

	partial void DrawUnsupportedShaders();

	partial void PrepareForSceneWindow();

	partial void PrepareBuffer();
	//This is editor only code, so put the complier directives here to ensure it can build
#if UNITY_EDITOR

	static ShaderTagId[] legacyShaderTagIds = {
		new ShaderTagId("Always"),
		new ShaderTagId("ForwardBase"),
		new ShaderTagId("PrepassBase"),
		new ShaderTagId("Vertex"),
		new ShaderTagId("VertexLMRGBM"),
		new ShaderTagId("VertexLM")
	};

	static Material errorMaterial;//used for unsupported mats

	string SampleName { get; set; }
	//Draw Gizmos for our custom pipeline
	partial void DrawGizmosBeforeFX()
	{
		if (Handles.ShouldRenderGizmos())
		{
			//Dont use image effects so we invoke both
			context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
			//context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
		}
	}
    partial void DrawGizmosAfterFX()
    {
		if (Handles.ShouldRenderGizmos())
		{
			context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
		}
	}
    partial void DrawUnsupportedShaders()
	{
		if (errorMaterial == null)
		{
			//Setup error material to draw when bad shaders are used
			errorMaterial =
				new Material(Shader.Find("Hidden/InternalErrorShader"));
		}
		//We are drawing legacy shaders starting with the first legacy pass
		//using default sorting settings for the camera
		//We dont care about the settings since these are all invalid!
		var drawingSettings = new DrawingSettings(
			legacyShaderTagIds[0], new SortingSettings(camera)
		)
		{
			overrideMaterial = errorMaterial
		};
		//Add the rest of the passes to the drawing settings
		for (int i = 1; i < legacyShaderTagIds.Length; i++)
		{
			drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
		}
		var filteringSettings = FilteringSettings.defaultValue;
		context.DrawRenderers(
			cullingResults, ref drawingSettings, ref filteringSettings
		);
	}

	partial void PrepareForSceneWindow()
	{
		if (camera.cameraType == CameraType.SceneView)
		{
			//UI wont show up ini scene view without this!
			ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
		}
	}
	//Make sure each camera gets their own scope
	//Want to make sure samples are attached to the correct cameras
	partial void PrepareBuffer()
	{
		Profiler.BeginSample("Editor Only");
		buffer.name = SampleName = camera.name;
		Profiler.EndSample();
	}

#else

	const string SampleName = bufferName;

#endif
}
