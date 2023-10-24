using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

[CanEditMultipleObjects]
[CustomEditorForRenderPipeline(typeof(Light), typeof(CustomRenderPipelineAsset))]
public class CustomLightEditor : LightEditor
{
    //We need a custom Gui
    static GUIContent renderingLayerMaskLabel =
        new GUIContent("Rendering Layer Mask", "Functional version of above property.");
 
    //We want to expose hidden settings for the light objects
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        RenderingMaskLayerDrawer.Draw(settings.renderingLayerMask, renderingLayerMaskLabel);
        //If the light is a spotlight, expose the inner spot angle
        if(!settings.lightType.hasMultipleDifferentValues 
            && (LightType)settings.lightType.enumValueIndex == LightType.Spot)
        {
            settings.DrawInnerAndOuterSpotAngle();
            //settings.ApplyModifiedProperties();
        }
        settings.ApplyModifiedProperties();
        //Warn the user that they are using a culling mask other than everything
        var light = target as Light;
        if (light.cullingMask != -1)
        {
            EditorGUILayout.HelpBox(
                light.type == LightType.Directional ?
                    "Culling Mask only affects shadows." :
                    "Culling Mask only affects shadow unless Use Lights Per Objects is on.",
                MessageType.Warning
            );
        }
    }
}
