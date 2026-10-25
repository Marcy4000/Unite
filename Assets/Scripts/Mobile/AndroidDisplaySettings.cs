using System.Collections;
using UnityEngine;

/// <summary>Restores Android immersive mode after startup, focus changes and resume.</summary>
public sealed class AndroidDisplaySettings : MonoBehaviour
{
#if UNITY_ANDROID && !UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        var display = new GameObject(nameof(AndroidDisplaySettings));
        DontDestroyOnLoad(display);
        display.AddComponent<AndroidDisplaySettings>();
    }
#endif

    private Coroutine pendingApply;

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
        if (Application.platform != RuntimePlatform.Android)
            return;

        if (pendingApply != null)
            StopCoroutine(pendingApply);

        pendingApply = StartCoroutine(ApplyAfterUnityLayout());
    }

    private IEnumerator ApplyAfterUnityLayout()
    {
        Screen.fullScreen = true;
        // Fullscreen changes are deferred. Let Unity finish before applying native flags.
        yield return null;

        try
        {
            using (var display = new AndroidJavaClass("com.nottimistudios.unite.UniteAndroidDisplay"))
                display.CallStatic("apply", UnityEngine.Android.AndroidApplication.currentActivity);
        }
        catch (AndroidJavaException exception)
        {
            Debug.LogError($"Could not configure Android fullscreen/safe area: {exception.Message}");
        }

        pendingApply = null;
    }
}
