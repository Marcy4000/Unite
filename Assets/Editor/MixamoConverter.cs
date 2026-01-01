using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class MixamoConverter : EditorWindow
{
    private AnimationClip sourceClip;
    private string outputName = "Fixed_Animation";

    [MenuItem("Tools/Mixamo Animation Converter")]
    static void Open()
    {
        GetWindow<MixamoConverter>("Mixamo Converter");
    }

    void OnGUI()
    {
        GUILayout.Label("Mixamo Animation Converter", EditorStyles.boldLabel);

        sourceClip = (AnimationClip)EditorGUILayout.ObjectField(
            "Source Animation",
            sourceClip,
            typeof(AnimationClip),
            false
        );

        outputName = EditorGUILayout.TextField("Output Name", outputName);

        GUI.enabled = sourceClip != null;

        if (GUILayout.Button("Convert Animation"))
            Convert();

        GUI.enabled = true;
    }

    void Convert()
    {
        var boneMap = GenerateBoneMap();
        var newClip = new AnimationClip();
        EditorUtility.CopySerialized(sourceClip, newClip);

        foreach (var binding in AnimationUtility.GetCurveBindings(sourceClip))
        {
            var curve = AnimationUtility.GetEditorCurve(sourceClip, binding);

            var newBinding = binding;
            newBinding.path = RemapPath(binding.path, boneMap);

            AnimationUtility.SetEditorCurve(newClip, newBinding, curve);
        }

        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(sourceClip))
        {
            var curve = AnimationUtility.GetObjectReferenceCurve(sourceClip, binding);

            var newBinding = binding;
            newBinding.path = RemapPath(binding.path, boneMap);

            AnimationUtility.SetObjectReferenceCurve(newClip, newBinding, curve);
        }

        string path = AssetDatabase.GetAssetPath(sourceClip);
        string dir = System.IO.Path.GetDirectoryName(path);
        string outPath = $"{dir}/{outputName}.anim";

        AssetDatabase.CreateAsset(newClip, AssetDatabase.GenerateUniqueAssetPath(outPath));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Done", "Animation converted successfully.", "OK");
    }

    static string RemapPath(string oldPath, Dictionary<string, string> map)
    {
        var segments = oldPath.Split('/');
        for (int i = 0; i < segments.Length; i++)
        {
            if (map.TryGetValue(segments[i], out var replacement))
                segments[i] = replacement;
        }
        return string.Join("/", segments);
    }

    static Dictionary<string, string> GenerateBoneMap()
    {
        var map = new Dictionary<string, string>
        {
            ["mixamorig:Hips"] = "reference/Bip001/Bip001 Pelvis",
            ["mixamorig:Spine"] = "Bip001 Spine",
            ["mixamorig:Spine1"] = "Bip001 Spine1",
            ["mixamorig:Spine2"] = "Bip001 Spine2",
            ["mixamorig:Neck"] = "Bip001 Neck",
            ["mixamorig:Head"] = "Bip001 Head",
            ["mixamorig:LeftShoulder"] = "Bip001 L Clavicle",
            ["mixamorig:LeftArm"] = "Bip001 L UpperArm",
            ["mixamorig:LeftForeArm"] = "Bip001 L Forearm",
            ["mixamorig:LeftHand"] = "Bip001 L Hand",
            ["mixamorig:RightShoulder"] = "Bip001 R Clavicle",
            ["mixamorig:RightArm"] = "Bip001 R UpperArm",
            ["mixamorig:RightForeArm"] = "Bip001 R Forearm",
            ["mixamorig:RightHand"] = "Bip001 R Hand",
            ["mixamorig:LeftUpLeg"] = "Bip001 L Thigh",
            ["mixamorig:LeftLeg"] = "Bip001 L Calf",
            ["mixamorig:LeftFoot"] = "Bip001 L Foot",
            ["mixamorig:LeftToeBase"] = "Bip001 L Toe0",
            ["mixamorig:RightUpLeg"] = "Bip001 R Thigh",
            ["mixamorig:RightLeg"] = "Bip001 R Calf",
            ["mixamorig:RightFoot"] = "Bip001 R Foot",
            ["mixamorig:RightToeBase"] = "Bip001 R Toe0",
        };

        string[] fingers = { "Thumb", "Index", "Middle", "Ring", "Pinky" };
        string[] fingerIds = { "Finger0", "Finger1", "Finger2", "Finger3", "Finger4" };

        foreach (var side in new[] { ("Left", "L"), ("Right", "R") })
        {
            for (int f = 0; f < fingers.Length; f++)
            {
                map[$"mixamorig:{side.Item1}Hand{fingers[f]}1"] = $"Bip001 {side.Item2} {fingerIds[f]}";
                map[$"mixamorig:{side.Item1}Hand{fingers[f]}2"] = $"Bip001 {side.Item2} {fingerIds[f]}1";
                map[$"mixamorig:{side.Item1}Hand{fingers[f]}3"] = $"Bip001 {side.Item2} {fingerIds[f]}2";
            }
        }

        return map;
    }
}
