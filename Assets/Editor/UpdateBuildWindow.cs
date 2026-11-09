using System;
using UnityEditor;
using UnityEngine;

public sealed class UpdateBuildWindow : EditorWindow
{
    private Editor settingsEditor;
    private Editor tuningEditor;
    private Vector2 scroll;
    private static bool actionQueued;

    [MenuItem("Build/Updates/Release tools")]
    public static void Open() => GetWindow<UpdateBuildWindow>("Update delivery");

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Active platform: " + UpdateBuildPipeline.Target, EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Set a public HTTPS content origin before release builds. Content downloads and balance checks happen at boot. Switch platforms in Build Profiles before building each target.", MessageType.Info);
        Editor.CreateCachedEditor(UpdateBuildPipeline.Settings, null, ref settingsEditor);
        settingsEditor.OnInspectorGUI();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Balance coefficients and optional move overrides", EditorStyles.boldLabel);
        Editor.CreateCachedEditor(UpdateBuildPipeline.Tuning, null, ref tuningEditor);
        tuningEditor.OnInspectorGUI();
        EditorGUILayout.HelpBox("Pokémon stats come from PokemonBase assets. Move defaults preserve compiled cooldown behavior; move overrides use the asset IDs printed in the exported JSON. Increment Patch Version whenever rules change.", MessageType.None);
        Button("Export balance JSON", UpdateBuildPipeline.ExportBalance);
        Button("Prepare Addressables configuration only", UpdateBuildPipeline.PrepareContent);
        Button("Build Addressables baseline", UpdateBuildPipeline.BuildBaseline);
        Button("Build Addressables content update…", UpdateBuildPipeline.BuildContentUpdate);
        Button("Export manifest using existing Addressables", UpdateBuildPipeline.ExportReleaseManifest);
        Button("Build player using existing Addressables", () => UpdateBuildPipeline.BuildPlayer(EditorUserBuildSettings.activeBuildTarget));
        Button("Build Linux dedicated server using existing Addressables", () => UpdateBuildPipeline.BuildPlayer(BuildTarget.StandaloneLinux64, true));
        EditorGUILayout.HelpBox("Outputs: Build/Updates/Upload and Build/Players. Upload bundles, catalogs and patch JSON first; latest.json last. Artifacts are local build archives, not public files. Nothing here uploads automatically.", MessageType.Info);
        EditorGUILayout.HelpBox("Baseline builds are synchronous Unity operations. Preparation has its own progress bar; Unity shows bundle/shader progress next. The editor can remain busy for many minutes. Stage logs are in Build/Updates/Logs.", MessageType.Info);
        if (GUILayout.Button("Open output folder")) { System.IO.Directory.CreateDirectory(UpdateBuildPipeline.OutputRoot); EditorUtility.RevealInFinder(UpdateBuildPipeline.OutputRoot); }
        EditorGUILayout.EndScrollView();
    }
    private static void Button(string label, Action action)
    {
        using (new EditorGUI.DisabledScope(actionQueued || UpdateBuildProgress.IsRunning || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
        {
            if (!GUILayout.Button(label)) return;
            actionQueued = true;
            // Let IMGUI finish its layout/repaint before starting a blocking Unity build/progress dialog.
            EditorApplication.delayCall += () =>
            {
                try { UpdateBuildProgress.Run(label, action); }
                catch (OperationCanceledException exception) { Debug.Log(exception.Message); }
                catch (Exception exception) { Debug.LogException(exception); EditorUtility.DisplayDialog("Update build failed", exception.Message, "OK"); }
                finally { actionQueued = false; }
            };
        }
    }
    private void OnDisable()
    {
        if (settingsEditor != null) DestroyImmediate(settingsEditor);
        if (tuningEditor != null) DestroyImmediate(tuningEditor);
    }
}
