using UnityEngine;
using UnityEngine.Rendering.Universal;

public class DisableOnAndroid : MonoBehaviour
{
    [SerializeField] private Camera disableAAcamera;

    private void Start()
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            if (disableAAcamera != null)
            {
                Camera cam = GetComponent<Camera>();
                UniversalAdditionalCameraData uacm = cam.GetComponent<UniversalAdditionalCameraData>();
                uacm.antialiasing = AntialiasingMode.None;
                uacm.antialiasingQuality = AntialiasingQuality.Low;
                return;
            }

            gameObject.SetActive(false);
        }
    }
}
