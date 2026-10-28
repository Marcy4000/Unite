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

    public static int SelectTargetFrameRate(double refreshRate)
    {
        return double.IsNaN(refreshRate) || double.IsInfinity(refreshRate) || refreshRate < 1d
            ? 60 : Mathf.Max(1, Mathf.RoundToInt((float)refreshRate));
    }

    public static bool ApplyMobileFrameRatePolicy()
    {
        if (!IsMobileDisplay) return false;

        double refreshRate = Screen.currentResolution.refreshRateRatio.value;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var display = new AndroidJavaClass("com.nottimistudios.unite.UniteAndroidDisplay"))
            {
                var activity = UnityEngine.Android.AndroidApplication.currentActivity;
                refreshRate = display.CallStatic<float>("getHighestRefreshRate", activity);
                display.CallStatic("apply", activity);
            }
        }
        catch (AndroidJavaException exception)
        {
            Debug.LogWarning($"Could not request Android refresh rate; using Unity display rate: {exception.Message}");
        }
#endif
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = SelectTargetFrameRate(refreshRate);
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
