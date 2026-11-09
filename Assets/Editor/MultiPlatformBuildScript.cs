using UnityEditor;

// Kept as the entry point for existing menu/CI integrations. Addressables exports are now explicit.
public static class MultiPlatformBuildScript
{
    [MenuItem("Build/Build Windows")]
    public static void BuildForWindows() => UpdateBuildPipeline.BuildPlayer(BuildTarget.StandaloneWindows64);
    [MenuItem("Build/Build Android")]
    public static void BuildForAndroid() => UpdateBuildPipeline.BuildPlayer(BuildTarget.Android);
    [MenuItem("Build/Build WebGL")]
    public static void BuildForWebGL() => UpdateBuildPipeline.BuildPlayer(BuildTarget.WebGL);
    [MenuItem("Build/Build Linux Server")]
    public static void BuildForLinuxServer() => UpdateBuildPipeline.BuildPlayer(BuildTarget.StandaloneLinux64, true);
    public static void BuildAddressables() => UpdateBuildPipeline.BuildBaseline();

    [MenuItem("Build/Build All Platforms")]
    public static void BuildAllPlatforms() => UpdatePlayerBuildQueue.Start();
}
