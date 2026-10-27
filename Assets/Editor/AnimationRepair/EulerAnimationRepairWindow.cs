using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public sealed class EulerAnimationRepairWindow : EditorWindow
{
    [SerializeField] private List<AnimationClip> clips = new List<AnimationClip>();
    [SerializeField] private EulerAnimationRepair.SourceOrder sourceOrder;
    [SerializeField] private float sampleRate;
    [SerializeField] private string outputFolder = "Assets/Animations/Corrected";
    [SerializeField] private AnimatorController controller;
    [SerializeField] private bool replaceController;
    private Vector2 scroll;

    [MenuItem("Tools/Animation/Repair Extracted Euler Animations...")]
    public static void Open()
    {
        var window = GetWindow<EulerAnimationRepairWindow>("Euler Animation Repair");
        window.minSize = new Vector2(480, 440);
        window.AddSelection();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Repair incorrect Euler rotation order", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Use only when you know the exported angles' true rotation order. XYZ fixes the test Vaporeon exports. NaN tangents are repaired in all copies. Quaternion-only clips are copied without rotation conversion and listed in the Console. Originals are never overwritten.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add selected clips / folders / controllers")) AddSelection();
            if (GUILayout.Button("Clear", GUILayout.Width(60))) clips.Clear();
        }
        EditorGUILayout.LabelField($"{clips.Count(c => c != null)} input clips");
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(120));
        for (int i = 0; i < clips.Count; i++)
            clips[i] = (AnimationClip)EditorGUILayout.ObjectField(clips[i], typeof(AnimationClip), false);
        EditorGUILayout.EndScrollView();
        sourceOrder = (EulerAnimationRepair.SourceOrder)EditorGUILayout.EnumPopup("True source rotation order", sourceOrder);
        sampleRate = EditorGUILayout.FloatField(new GUIContent("Bake FPS (0 = source)", "Keep 0 for already sampled exports. Includes original key times as well as the frame grid."), sampleRate);
        outputFolder = EditorGUILayout.TextField("Output folder", outputFolder);
        controller = (AnimatorController)EditorGUILayout.ObjectField("Controller (optional)", controller, typeof(AnimatorController), false);
        using (new EditorGUI.DisabledScope(controller == null))
            replaceController = EditorGUILayout.Toggle("Replace controller references", replaceController);
        if (replaceController && controller != null)
            EditorGUILayout.HelpBox("Updates this controller's states and referenced blend trees, including shared trees. Controller reference changes support Undo. No other controllers or scene objects are scanned.", MessageType.Warning);
        using (new EditorGUI.DisabledScope(clips.All(c => c == null) || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Create corrected copies", GUILayout.Height(30))) Repair();
        }
    }

    private void AddSelection()
    {
        var found = new HashSet<AnimationClip>(clips.Where(c => c != null));
        foreach (var selected in Selection.objects)
        {
            if (selected is AnimationClip clip) found.Add(clip);
            else if (selected is AnimatorController selectedController)
            {
                controller = selectedController;
                foreach (var animation in selectedController.animationClips) found.Add(animation);
            }
            else
            {
                string path = AssetDatabase.GetAssetPath(selected);
                var paths = AssetDatabase.IsValidFolder(path)
                    ? AssetDatabase.FindAssets("t:AnimationClip", new[] { path }).Select(AssetDatabase.GUIDToAssetPath)
                    : new[] { path }.AsEnumerable();
                foreach (string assetPath in paths.Where(p => !string.IsNullOrEmpty(p)).Distinct())
                    foreach (var animation in AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<AnimationClip>())
                        if (!animation.name.StartsWith("__preview__", StringComparison.Ordinal)) found.Add(animation);
            }
        }
        clips = found.OrderBy(AssetDatabase.GetAssetPath).ThenBy(c => c.name).ToList();
        Repaint();
    }

    private void Repair()
    {
        try { EulerAnimationRepair.ValidateFolder(outputFolder); }
        catch (Exception error) { EditorUtility.DisplayDialog("Invalid output folder", error.Message, "OK"); return; }
        if (!EditorUtility.DisplayDialog("Repair Euler animation copies?",
            $"Interpret Euler angles as {sourceOrder} and bake quaternion copies. Clips without Euler tracks will also be copied, without changing their rotations, and listed in the Console.\n\nOriginal clips will remain unchanged."
            + (replaceController && controller != null ? $"\nReferences in {controller.name} and its blend trees will be updated." : ""), "Create copies", "Cancel")) return;

        var replacements = new Dictionary<AnimationClip, AnimationClip>();
        int skipped = 0, failed = 0;
        var inputs = clips.Where(c => c != null).Distinct().ToArray();
        try
        {
            for (int i = 0; i < inputs.Length; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Repairing Euler animation", inputs[i].name, (float)i / inputs.Length)) break;
                try
                {
                    bool convertEuler = EulerAnimationRepair.RequiresEulerConversion(inputs[i]);
                    var corrected = EulerAnimationRepair.CreateCorrectedCopy(inputs[i], outputFolder, sourceOrder, sampleRate);
                    replacements.Add(inputs[i], corrected);
                    if (!convertEuler) skipped++;
                }
                catch (Exception error) { failed++; Debug.LogError($"Animation repair: {error.Message}", inputs[i]); }
            }
            int references = replaceController && controller != null
                ? EulerAnimationRepair.ReplaceControllerClips(controller, replacements) : 0;
            AssetDatabase.SaveAssets();
            Debug.Log($"Animation repair: {replacements.Count} copies created in {outputFolder}, {replacements.Count - skipped} Euler conversions, {skipped} copied with conversion skipped (no Euler curves), {failed} failed, {references} controller references replaced.");
            EditorUtility.DisplayDialog("Animation repair finished", $"Created: {replacements.Count}\nEuler converted: {replacements.Count - skipped}\nCopied without conversion: {skipped}\nFailed: {failed}\nReferences replaced: {references}\n\nSee Console for skipped-conversion clip names. Originals were not changed.", "OK");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }
}
