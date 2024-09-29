using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using System.IO;
using System.Linq;
using UnityEngine;

public class MultiPlatformBuildScript
{
    private static string buildFolder = "C:/Users/Marcy/Documents/ClickTeamProjects/UnityProjects/Unite/Build";

    [MenuItem("Build/Build All Platforms")]
    public static void BuildAllPlatforms()
    {
        // Ensure Addressables are built before any platform build
        BuildAddressables();

        // Build for each platform
        BuildForWindows();
        BuildForAndroid();
        BuildForWebGL();
        BuildForLinuxServer();
    }

    [MenuItem("Build/Build Windows")]
    public static void BuildForWindows()
    {
        string targetDir = Path.Combine(buildFolder, "Windows");
        BuildPlayer(BuildTarget.StandaloneWindows64, targetDir, "Pokemon Unite Recreation.exe");
    }

    [MenuItem("Build/Build Android")]
    public static void BuildForAndroid()
    {
        string targetDir = Path.Combine(buildFolder, "Android");
        BuildPlayer(BuildTarget.Android, targetDir, "PokemonUniteRecreation.apk");
    }

    [MenuItem("Build/Build WebGL")]
    public static void BuildForWebGL()
    {
        string targetDir = Path.Combine(buildFolder, "WebGL");
        BuildPlayer(BuildTarget.WebGL, targetDir);
    }

    [MenuItem("Build/Build Linux Server")]
    public static void BuildForLinuxServer()
    {
        string targetDir = Path.Combine(buildFolder, "LinuxServer");
        BuildPlayer(BuildTarget.StandaloneLinux64, targetDir, "", BuildOptions.None, true);
    }

    private static void BuildPlayer(BuildTarget target, string outputDir, string outputFileName = "", BuildOptions options = BuildOptions.None, bool isServer = false)
    {
        // Make sure the build directory exists
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // Set up build options
        BuildPlayerOptions buildOptions = new BuildPlayerOptions
        {
            scenes = GetScenesFromBuildSettings(),
            locationPathName = Path.Combine(outputDir, outputFileName),
            target = target,
            options = options,
        };

        // If it's a server build, use StandaloneBuildSubtarget.Server
        if (isServer && target == BuildTarget.StandaloneLinux64)
        {
            buildOptions.subtarget = (int)StandaloneBuildSubtarget.Server;
        }

        // Build the player
        var report = BuildPipeline.BuildPlayer(buildOptions);

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"Build for {target} succeeded: {outputDir}");
        }
        else
        {
            Debug.LogError($"Build for {target} failed: {report.summary.result}");
        }
    }

    private static string[] GetScenesFromBuildSettings()
    {
        // Get all enabled scenes from the Build Settings
        return EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
    }

    public static void BuildAddressables()
    {
        Debug.Log("Building Addressables...");
        AddressableAssetSettings.BuildPlayerContent();
        Debug.Log("Addressables build complete.");
    }
}
