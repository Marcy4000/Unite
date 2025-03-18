using UnityEngine;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.IMGUI.Controls;

[CustomEditor(typeof(LightProbeGenerator))]
public class LightProbeGenEditor : Editor
{
	private BoxBoundsHandle _boundsHandle = new BoxBoundsHandle();
	private bool _editBounds = false;
	private GUIStyle _buttonStyle;
	private GUILayoutOption[] _editBoundsOptions;
	private LightProbeGenerator _gen;
	private GUIContent _editModeButton;


	public override void OnInspectorGUI()
	{
		GUIContent content = new GUIContent(EditorGUIUtility.IconContent("EditCollider").image, 
			EditorGUIUtility.TrTextContent("Edit bounding volume.\n\n - Hold Alt after clicking control handle to pin center in place." +
            "\n - Hold Shift after clicking control handle to scale uniformly.").text);

		_buttonStyle = new GUIStyle(GUI.skin.button);

        EditorGUILayout.BeginHorizontal();
		GUILayout.Space(70);
		GUILayout.FlexibleSpace();

        if (_editBounds = GUILayout.Toggle(_editBounds, content, _buttonStyle, _editBoundsOptions))
        {
            SceneView.RepaintAll();
        }

		GUILayout.BeginVertical();
		GUILayout.Space(10);

		GUILayout.Label("Edit Bounding Volume");

		GUILayout.FlexibleSpace();
		GUILayout.EndVertical();
		GUILayout.FlexibleSpace();

		EditorGUILayout.EndHorizontal();

		DrawPropertiesExcluding(serializedObject, "m_Script");
		EditorGUILayout.Separator();

		EditorGUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();

		if (GUILayout.Button("Generate", GUILayout.Width(160), GUILayout.Height(40)))
		{
			(target as LightProbeGenerator).GenProbes();
		}

		EditorGUILayout.Separator();

		if (GUILayout.Button("Clear", GUILayout.Width(160), GUILayout.Height(40)))
		{
			(target as LightProbeGenerator).ClearProbes();
		}

		GUILayout.FlexibleSpace();
		EditorGUILayout.EndHorizontal();
	}

	public void OnSceneGUI()
	{
		if (_editBounds)
        {
			_boundsHandle.center = _gen.LightProbeVolumes.ProbeVolume.center;
			_boundsHandle.size = _gen.LightProbeVolumes.ProbeVolume.size;
			_boundsHandle.wireframeColor = Color.red;
			_boundsHandle.handleColor = Color.green;
		}

		if (_editBounds)
		{
			EditorGUI.BeginChangeCheck();
			_boundsHandle.DrawHandle();
		}

		if (Tools.current == Tool.Move && !_editBounds)
		{
			Undo.RecordObject(target, "ProbeGenerator Move");

			_gen.LightProbeVolumes.ProbeVolume.center = Handles.PositionHandle(
					_gen.LightProbeVolumes.ProbeVolume.center, Quaternion.identity);
		}

		if (Tools.current == Tool.Rotate && !_editBounds)
		{
			Undo.RecordObject(target, "ProbeGenerator Rotation");

			_gen.LightProbeVolumes.Rotation = Handles.RotationHandle(_gen.LightProbeVolumes.Rotation, _gen.LightProbeVolumes.ProbeVolume.center);
		}

		if (Tools.current == Tool.Scale && !_editBounds)
		{
			Undo.RecordObject(target, "ProbeGenerator Scale");

			_gen.LightProbeVolumes.ProbeVolume.extents = Handles.ScaleHandle(
							_gen.LightProbeVolumes.ProbeVolume.extents,
							_gen.LightProbeVolumes.ProbeVolume.center,
							Quaternion.identity,
							5.0f);
		}

        if (EditorGUI.EndChangeCheck() && _editBounds)
        {
			Undo.RecordObject(target, "Change Bounds");

			Bounds newBounds = new Bounds();
			newBounds.center = _boundsHandle.center;
			newBounds.size = _boundsHandle.size;
			_gen.LightProbeVolumes.ProbeVolume.center = newBounds.center;
			_gen.LightProbeVolumes.ProbeVolume.extents = newBounds.extents;
        }
	}

    private void OnEnable()
    {
		_gen = target as LightProbeGenerator;
		Tools.hidden = true;
		_editBoundsOptions = new GUILayoutOption[]
		{
			GUILayout.Width(35),
			GUILayout.Height(25)
		};
	}

    private void OnDisable()
    {
		Tools.hidden = false;
    }
}