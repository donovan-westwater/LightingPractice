using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CanEditMultipleObjects]
[CustomEditorForRenderPipeline(typeof(Light), typeof(CustomRenderPipelineAsset))]
public class CustomLightEditor : LightEditor
{
    //We want to expose hidden settings for the light objects
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        //If the light is a spotlight, expose the inner spot angle
        if(!settings.lightType.hasMultipleDifferentValues 
            && (LightType)settings.lightType.enumValueIndex == LightType.Spot)
        {
            settings.DrawInnerAndOuterSpotAngle();
            settings.ApplyModifiedProperties();
        }
    }
}
