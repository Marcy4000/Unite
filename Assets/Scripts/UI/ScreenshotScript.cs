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
        string fileName = $"{fileNamePrefix}_{timestamp}.png";
        string filePath;

        // Different paths based on platform
        if (Application.isEditor)
        {
            // Keep current behavior in editor
            filePath = Path.Combine(Application.dataPath, folderPath, fileName);
        }
        else if (Application.platform == RuntimePlatform.Android)
        {
            // On Android, save to gallery (ScreenCapture handles the path)
            filePath = fileName;
        }
        else
        {
            // On other platforms, save to Screenshot folder next to the executable
            string screenshotFolder = Path.Combine(Application.dataPath, "..", "Screenshots");
            Directory.CreateDirectory(screenshotFolder);
            filePath = Path.Combine(screenshotFolder, fileName);
        }

        if (Application.platform == RuntimePlatform.Android)
        {
            // Android gallery save
            Texture2D ss = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            ss.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            ss.Apply();

            // Save the screenshot to Gallery/Photos
            NativeGallery.SaveImageToGallery(ss, "PokemonUniteRecScreenshots", fileName, (success, path) => Debug.Log("Media save result: " + success + " " + path));

            // To avoid memory leaks
            Destroy(ss);
        }
        else
        {
            // Regular file save for other platforms
            ScreenCapture.CaptureScreenshot(filePath);
        }

        Debug.Log($"Screenshot saved to: {filePath}");

        yield return null;

        canvas.SetActive(true);
    }
}
