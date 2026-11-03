using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

[CustomEditor(typeof(LightProbeGenerator))]
public class LightProbeGenEditor : Editor
{
    private readonly BoxBoundsHandle boundsHandle = new BoxBoundsHandle();
    private bool editBounds;
    private bool previousToolsHidden;
    private LightProbeGenerator generator;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var content = new GUIContent(EditorGUIUtility.IconContent("EditCollider").image,
            "Edit bounding volume. Hold Alt to pin the center; Shift to scale uniformly.");
        EditorGUI.BeginChangeCheck();
        editBounds = GUILayout.Toggle(editBounds, content, GUI.skin.button, GUILayout.Width(35), GUILayout.Height(25));
        if (EditorGUI.EndChangeCheck())
            SceneView.RepaintAll();

        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.HelpBox("Subdivisions are whole-number intervals per axis. Random Count is the number of candidates before filtering. Mesh filtering uses enabled renderers on Obstacle Layers; closed, outward-facing meshes work best.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Generate", GUILayout.Height(40)))
                generator.GenProbes();
            if (GUILayout.Button("Clear", GUILayout.Height(40)))
                generator.ClearProbes();
        }
    }

    public void OnSceneGUI()
    {
        if (generator.LightProbeVolumes == null)
            return;
        var area = generator.LightProbeVolumes;
        Bounds bounds = area.ProbeVolume;
        Quaternion rotation = area.Rotation;
        EditorGUI.BeginChangeCheck();
        if (editBounds)
        {
            using (new Handles.DrawingScope(Matrix4x4.TRS(bounds.center, rotation, Vector3.one)))
            {
                boundsHandle.center = Vector3.zero;
                boundsHandle.size = bounds.size;
                boundsHandle.wireframeColor = Color.red;
                boundsHandle.handleColor = Color.green;
                boundsHandle.DrawHandle();
                bounds.center += rotation * boundsHandle.center;
                bounds.size = boundsHandle.size;
            }
        }
        else if (Tools.current == Tool.Move)
            bounds.center = Handles.PositionHandle(bounds.center, rotation);
        else if (Tools.current == Tool.Rotate)
            rotation = Handles.RotationHandle(rotation, bounds.center);
        else if (Tools.current == Tool.Scale)
            bounds.size = Handles.ScaleHandle(bounds.size, bounds.center, rotation, HandleUtility.GetHandleSize(bounds.center));

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(generator, "Edit Light Probe Volume");
            Vector3 size = bounds.size;
            bounds.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            area.ProbeVolume = bounds;
            area.Rotation = rotation;
            EditorUtility.SetDirty(generator);
            PrefabUtility.RecordPrefabInstancePropertyModifications(generator);
        }
    }

    private void OnEnable()
    {
        generator = (LightProbeGenerator)target;
        previousToolsHidden = Tools.hidden;
        Tools.hidden = true;
    }

    private void OnDisable()
    {
        Tools.hidden = previousToolsHidden;
    }
}
