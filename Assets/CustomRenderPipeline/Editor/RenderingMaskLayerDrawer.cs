using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[CustomPropertyDrawer(typeof(RenderingLayerMaskFieldAttribute))]
public class RenderingMaskLayerDrawer : PropertyDrawer
{
	public static void Draw(
		Rect position, SerializedProperty property, GUIContent label
	)
	{
		//SerializedProperty property = settings.renderingLayerMask;
		EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
		EditorGUI.BeginChangeCheck();
		//We set our max to -1 to represent an everything selection
		//We only need to do this when the property is unsigned
		int mask = property.intValue;
		bool isUint = property.type == "uint";
		if (isUint && mask == int.MaxValue)
		{
			mask = -1;
		}
		//Use the rendering layer names for consistency
		mask = EditorGUI.MaskField(
			position, label, mask,
			GraphicsSettings.currentRenderPipeline.renderingLayerMaskNames
		);
		if (EditorGUI.EndChangeCheck())
		{
			property.intValue = isUint && mask == -1 ? int.MaxValue : mask;
		}
		EditorGUI.showMixedValue = false;
	}
	public override void OnGUI(
		Rect position, SerializedProperty property, GUIContent label
	)
	{
		Draw(position, property, label);
	}
	public static void Draw(SerializedProperty property, GUIContent label)
	{
		Draw(EditorGUILayout.GetControlRect(), property, label);
	}
}
