using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>Resume platform builds after Unity's platform-switch domain reloads.</summary>
[InitializeOnLoad]
public static class UpdatePlayerBuildQueue
{
    private const string Key = "Unite.UpdatePlayerBuildQueue";
    private static readonly BuildTarget[] Targets =
    {
        BuildTarget.StandaloneWindows64, BuildTarget.Android, BuildTarget.WebGL, BuildTarget.StandaloneLinux64
    };
    private static bool busy;
    static UpdatePlayerBuildQueue() { EditorApplication.update += Tick; }

    public static void Start()
    {
        if (Application.isBatchMode) throw new BuildFailedException("CI must use a separate -buildTarget invocation for each platform.");
        UpdateBuildPipeline.ValidateSettings(true);
        if (SessionState.GetInt(Key, -1) >= 0) throw new BuildFailedException("A player build queue is already running.");
        SessionState.SetInt(Key, 0);
        Debug.Log("Queued player builds using existing platform content archives. Missing archives or modules stop the queue.");
    }

    [MenuItem("Build/Updates/Cancel queued player builds")]
    public static void Cancel() { SessionState.SetInt(Key, -1); }

    private static void Tick()
    {
        int index = SessionState.GetInt(Key, -1);
        if (index < 0 || busy || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer) return;
        busy = true;
        try
        {
            var target = Targets[index];
            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target), target))
                    throw new BuildFailedException("Could not switch platform to " + target + ". Check installed build modules.");
                return; // The next editor tick/domain reload resumes this target.
            }
            UpdateBuildPipeline.BuildPlayer(target, target == BuildTarget.StandaloneLinux64);
            if (SessionState.GetInt(Key, -1) < 0) return;
            SessionState.SetInt(Key, index + 1 < Targets.Length ? index + 1 : -1);
            if (index + 1 == Targets.Length) Debug.Log("All queued player builds completed.");
        }
        catch (Exception exception) { Cancel(); Debug.LogException(exception); }
        finally { busy = false; }
    }
}
