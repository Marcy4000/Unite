using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

public class ScreenshotScript : MonoBehaviour
{
    public string folderPath = "Screenshots"; // Folder to save screenshots
    public string fileNamePrefix = "Screenshot"; // Prefix for screenshot files

    public bool useNewCode;

    private Camera cam;

    void Start()
    {
        // Ensure the folder exists
        folderPath = Path.Combine(Application.dataPath, folderPath);
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        // Get the camera component
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogError("ScreenshotScript must be attached to a camera!");
        }
    }

    void Update()
    {
        if (Keyboard.current.f2Key.wasPressedThisFrame && cam != null)
        {
            TakeScreenshot();
        }
    }

    public void TakeScreenshot()
    {
        if (useNewCode)
        {
            StartCoroutine(TakeScreenshowNew());
        }
        else
        {
            TakeScreenshotOld();
        }
    }

    void TakeScreenshotOld()
    {
        // Create a temporary RenderTexture for capturing
        int width = Screen.width;
        int height = Screen.height;
        RenderTexture renderTexture = new RenderTexture(width, height, 24);

        // Ensure compatibility with Universal Render Pipeline
        var cameraData = cam.GetUniversalAdditionalCameraData();
        if (cameraData != null)
        {
            // Set up RenderTexture for all cameras in the stack
            var allCameras = cameraData.cameraStack;
            foreach (var overlayCam in allCameras)
            {
                if (overlayCam != null)
                {
                    overlayCam.targetTexture = renderTexture;
                }
            }
        }

        // Render the stack to the RenderTexture
        cam.targetTexture = renderTexture;
        RenderTexture.active = renderTexture;
        cam.Render();

        // Create Texture2D from RenderTexture
        Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB9e5Float, false);
        screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenshot.Apply();

        // Save screenshot as PNG
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filePath = Path.Combine(folderPath, $"{fileNamePrefix}_{timestamp}.png");
        File.WriteAllBytes(filePath, screenshot.EncodeToPNG());
        Debug.Log($"Screenshot saved to: {filePath}");

        // Cleanup
        cam.targetTexture = null;
        RenderTexture.active = null;

        if (cameraData != null)
        {
            foreach (var overlayCam in cameraData.cameraStack)
            {
                if (overlayCam != null)
                {
                    overlayCam.targetTexture = null;
                }
            }
        }

        Destroy(renderTexture);
        Destroy(screenshot);
    }

    IEnumerator TakeScreenshowNew()
    {
        GameObject canvas = GameObject.Find("Canvas");

        canvas.SetActive(false);

        yield return null;

        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filePath = Path.Combine(folderPath, $"{fileNamePrefix}_{timestamp}.png");

        if (Application.platform == RuntimePlatform.Android)
            ScreenCapture.CaptureScreenshot($"{fileNamePrefix}_{timestamp}.png");
        else
            ScreenCapture.CaptureScreenshot(filePath);

        Debug.Log($"Screenshot saved to: {filePath}");

        yield return null;

        canvas.SetActive(true);
    }
}
