using UnityEngine;
using UnityEngine.SceneManagement;

public partial class SettingsManager
{
    private const string OpenWorldVisitKey = "OpenWorldLobby.VisitInProgress";
    private bool openWorldVisit;
    private bool openWorldPaused;
    private float openWorldWarmup;
    private float openWorldSampleTime;
    private int openWorldSampleFrames;
    private int openWorldSlowSamples;

    private static bool IsLowEndOpenWorldDevice(bool mobile, int memoryMb, int processors, int graphicsMemoryMb)
    {
        return (memoryMb > 0 && memoryMb < 4096) ||
            (processors > 0 && processors < (mobile ? 6 : 3)) ||
            (!mobile && graphicsMemoryMb > 0 && graphicsMemoryMb < 1024);
    }

    private void InitializeOpenWorldSafety()
    {
        bool interrupted = PlayerPrefs.GetInt(OpenWorldVisitKey, 0) != 0;
        PlayerPrefs.DeleteKey(OpenWorldVisitKey);
        if (interrupted || IsLowEndOpenWorldDevice(Application.isMobilePlatform, SystemInfo.systemMemorySize,
            SystemInfo.processorCount, SystemInfo.graphicsMemorySize))
            SetAutoJoinOpenWorldLobby(false);
        PlayerPrefs.Save();
        Application.lowMemory += OnOpenWorldLowMemory;
        SceneManager.activeSceneChanged += OnOpenWorldSceneChanged;
    }

    public static void BeginOpenWorldVisit()
    {
        PlayerPrefs.SetInt(OpenWorldVisitKey, 1);
        PlayerPrefs.Save();
        if (Instance == null) return;
        Instance.openWorldVisit = true;
        Instance.ResetOpenWorldSamples();
    }

    private void ResetOpenWorldSamples()
    {
        openWorldWarmup = 20f;
        openWorldSampleTime = 0f;
        openWorldSampleFrames = 0;
        openWorldSlowSamples = 0;
    }

    private void OnOpenWorldSceneChanged(Scene previous, Scene current)
    {
        if (current.name == "ChineseLobby") return;
        openWorldVisit = false;
        PlayerPrefs.DeleteKey(OpenWorldVisitKey);
        PlayerPrefs.Save();
    }

    private void OnOpenWorldLowMemory()
    {
        if (openWorldVisit) SetAutoJoinOpenWorldLobby(false);
    }

    private void Update()
    {
        if (!openWorldVisit || openWorldPaused || !Application.isFocused || !AutoJoinOpenWorldLobby ||
            SceneManager.GetActiveScene().name != "ChineseLobby") return;
        if (openWorldWarmup > 0f)
        {
            openWorldWarmup -= Time.unscaledDeltaTime;
            return;
        }
        ObserveOpenWorldFrame(Time.unscaledDeltaTime);
    }

    private void ObserveOpenWorldFrame(float elapsed)
    {
        openWorldSampleTime += elapsed;
        openWorldSampleFrames++;
        if (openWorldSampleTime < 10f) return;
        openWorldSlowSamples = openWorldSampleFrames / openWorldSampleTime < 20f ? openWorldSlowSamples + 1 : 0;
        openWorldSampleTime = 0f;
        openWorldSampleFrames = 0;
        if (openWorldSlowSamples >= 3) SetAutoJoinOpenWorldLobby(false);
    }

    private void OnApplicationPause(bool paused)
    {
        openWorldPaused = paused;
        if (!openWorldVisit) return;
        if (paused) PlayerPrefs.DeleteKey(OpenWorldVisitKey);
        else PlayerPrefs.SetInt(OpenWorldVisitKey, 1);
        PlayerPrefs.Save();
        ResetOpenWorldSamples();
    }

    private void OnApplicationFocus(bool focused)
    {
        ResetOpenWorldSamples();
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.DeleteKey(OpenWorldVisitKey);
        PlayerPrefs.Save();
    }
}
