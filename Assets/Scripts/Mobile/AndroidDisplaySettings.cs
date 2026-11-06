using System.Collections;
using UnityEngine;

/// <summary>Restores Android immersive mode after startup, focus changes and resume.</summary>
public sealed class AndroidDisplaySettings : MonoBehaviour
{
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        var display = new GameObject(nameof(AndroidDisplaySettings));
        DontDestroyOnLoad(display);
        display.AddComponent<AndroidDisplaySettings>();
    }
#endif

    private Coroutine pendingApply;

    public static bool IsMobileDisplay => Application.platform == RuntimePlatform.Android ||
        Application.platform == RuntimePlatform.IPhonePlayer;

    public const string FrameRateKey = "Display.TargetFrameRate";
    public const string RefreshRateKey = "Display.RefreshRate";
    public static int PreferredFrameRate => SelectTargetFrameRate(PlayerPrefs.GetInt(FrameRateKey, 60));
    // Zero lets Android choose its display mode. FPS and physical refresh are separate.
    public static int PreferredRefreshRate => Mathf.Clamp(PlayerPrefs.GetInt(RefreshRateKey, 0), 0, 360);

    public static int SelectTargetFrameRate(double requestedFrameRate)
    {
        return double.IsNaN(requestedFrameRate) || double.IsInfinity(requestedFrameRate) || requestedFrameRate < 15d
            ? 60 : Mathf.RoundToInt((float)System.Math.Min(360d, requestedFrameRate));
    }

    public static bool ApplyMobileFrameRatePolicy()
    {
        if (!IsMobileDisplay) return false;

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var display = new AndroidJavaClass("com.nottimistudios.unite.UniteAndroidDisplay"))
            {
                var activity = UnityEngine.Android.AndroidApplication.currentActivity;
                display.CallStatic("apply", activity, PreferredRefreshRate);
            }
        }
        catch (AndroidJavaException exception)
        {
            Debug.LogWarning($"Could not request Android refresh rate; using Unity display rate: {exception.Message}");
        }
#endif
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = PreferredFrameRate;
        return true;
    }

    private void Start()
    {
        ScheduleApply();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            ScheduleApply();
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused)
            ScheduleApply();
    }

    private void ScheduleApply()
    {
        if (!IsMobileDisplay)
            return;

        if (pendingApply != null)
            StopCoroutine(pendingApply);

        pendingApply = StartCoroutine(ApplyAfterUnityLayout());
    }

    private IEnumerator ApplyAfterUnityLayout()
    {
        if (Application.platform == RuntimePlatform.Android)
            Screen.fullScreen = true;
        // Fullscreen changes are deferred. Let Unity finish before applying native flags.
        yield return null;

        ApplyMobileFrameRatePolicy();
        pendingApply = null;
    }
}
