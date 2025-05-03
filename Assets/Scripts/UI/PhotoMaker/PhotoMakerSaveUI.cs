using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PhotoMakerSaveUI : MonoBehaviour
{
    [SerializeField] private PhotoMakerManager photoMakerManager; // Reference to the manager
    [SerializeField] private ScreenshotScript screenshotScript; // Reference to the screenshot script
    [SerializeField] private GameObject saveSlotPrefab;
    [SerializeField] private Transform saveSlotContainer;
    [SerializeField] private GameObject saveSlotUI; // The root UI object to show/hide

    [SerializeField] private TMP_InputField saveSlotNameInputField;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button cancelButton;

    private List<GameObject> instantiatedSaveSlots = new List<GameObject>(); // Keep track of created slots

    private void Awake()
    {
        saveButton.onClick.AddListener(() => StartCoroutine(HandleSaveButtonClickCoroutine()));
        cancelButton.onClick.AddListener(HandleCancelButtonClick);
        saveSlotUI.SetActive(false); // Start hidden
    }

    public void Show()
    {
        saveSlotUI.SetActive(true);
        saveSlotNameInputField.text = ""; // Clear input field
        PopulateSaveSlots();
    }

    public void Hide()
    {
        saveSlotUI.SetActive(false);
    }

    private void PopulateSaveSlots()
    {
        // Clear existing slots
        foreach (GameObject slot in instantiatedSaveSlots)
        {
            Destroy(slot);
        }
        instantiatedSaveSlots.Clear();

        // Find all layout files
        string[] layoutFiles = Directory.GetFiles(Application.persistentDataPath, "*_layout.json");

        // Sort files by creation time, newest first (optional)
        System.Array.Sort(layoutFiles, (x, y) => File.GetCreationTime(y).CompareTo(File.GetCreationTime(x)));

        for (int i = 0; i < layoutFiles.Length; i++)
        {
            string filePath = layoutFiles[i];
            // Extract the sanitized name used in the filename
            string sanitizedName = Path.GetFileNameWithoutExtension(filePath).Replace("_layout", "");

            // Try to get the original name and image path from the JSON
            if (photoMakerManager.TryGetLayoutInfo(sanitizedName, out string originalLayoutName, out string imagePath))
            {
                GameObject slotInstance = Instantiate(saveSlotPrefab, saveSlotContainer);
                PhotoMakerSaveSlotUI slotUI = slotInstance.GetComponent<PhotoMakerSaveSlotUI>();

                if (slotUI != null)
                {
                    // Load the associated image if path exists, otherwise use null/default
                    Sprite previewSprite = null;
                    if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                    {
                        previewSprite = LoadSpriteFromFile(imagePath);
                    }

                    // Initialize the slot UI with original name, sprite, and sanitized name as identifier
                    slotUI.Initialize(originalLayoutName, previewSprite, i); // Pass index for now, adjust if needed

                    // Use the sanitized name for loading/deleting actions
                    string currentSanitizedName = sanitizedName; // Capture loop variable
                    slotUI.OnLoadButtonClick += (index) => HandleLoadButtonClick(currentSanitizedName);
                    slotUI.OnDeleteButtonClick += (index) => HandleDeleteButtonClick(currentSanitizedName);

                    instantiatedSaveSlots.Add(slotInstance);
                }
                else
                {
                    Debug.LogError("Save Slot Prefab is missing PhotoMakerSaveSlotUI component.");
                    Destroy(slotInstance); // Clean up invalid instance
                }
            }
            else
            {
                Debug.LogWarning($"Could not read layout info for file: {filePath}. Skipping.");
                // Optionally delete corrupted/unreadable files here
                // File.Delete(filePath);
            }
        }
    }

    private void HandleLoadButtonClick(string sanitizedLayoutName)
    {
        Debug.Log($"Load button clicked for: {sanitizedLayoutName}");
        photoMakerManager.LoadLayout(sanitizedLayoutName);
        Hide();
    }

    private void HandleDeleteButtonClick(string sanitizedLayoutName)
    {
        Debug.Log($"Delete button clicked for: {sanitizedLayoutName}");
        string filePath = Path.Combine(Application.persistentDataPath, $"{sanitizedLayoutName}_layout.json");
        string imagePath = ""; // Variable to store the image path

        // Retrieve image path *before* deleting the JSON, as TryGetLayoutInfo reads the JSON
        if (photoMakerManager.TryGetLayoutInfo(sanitizedLayoutName, out _, out imagePath))
        {
            // Image path retrieved successfully
            Debug.Log($"Found associated image path: {imagePath}");
        }
        else
        {
            Debug.LogWarning($"Could not retrieve layout info (including image path) for {sanitizedLayoutName}. Image might not be deleted.");
            // Image path remains empty or invalid if info couldn't be read
        }

        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
                Debug.Log($"Deleted layout file: {filePath}");

                // Now, attempt to delete the associated image if the path is valid and the file exists
                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                {
                    try
                    {
                        File.Delete(imagePath);
                        Debug.Log($"Deleted associated image: {imagePath}");
                    }
                    catch (System.Exception imgEx)
                    {
                        Debug.LogError($"Error deleting associated image {imagePath}: {imgEx.Message}");
                    }
                }
                else if (!string.IsNullOrEmpty(imagePath))
                {
                    Debug.LogWarning($"Associated image file not found at path: {imagePath}");
                }
                // If imagePath was empty from the start, no deletion attempt is needed.

                PopulateSaveSlots(); // Refresh the list
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error deleting file {filePath}: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"Layout file not found for deletion: {filePath}");
            // Attempt to delete orphan image file if path was retrieved? Optional.
            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                try
                {
                    File.Delete(imagePath);
                    Debug.Log($"Deleted potentially orphaned associated image: {imagePath}");
                }
                catch (System.Exception imgEx)
                {
                    Debug.LogError($"Error deleting potentially orphaned image {imagePath}: {imgEx.Message}");
                }
            }
            PopulateSaveSlots(); // Refresh even if file was already gone
        }
    }

    private IEnumerator HandleSaveButtonClickCoroutine()
    {
        string layoutName = saveSlotNameInputField.text;
        if (string.IsNullOrWhiteSpace(layoutName))
        {
            Debug.LogWarning("Layout name cannot be empty.");
            // Optionally show a message to the user
            yield break; // Exit coroutine
        }

        // Sanitize name for file paths (reuse manager's logic if possible, or duplicate here)
        string sanitizedName = photoMakerManager.SanitizeFileName(layoutName);
        if (string.IsNullOrWhiteSpace(sanitizedName))
        {
            Debug.LogError("Cannot save layout: Sanitized layout name is empty.");
            yield break;
        }

        // Define the path for the screenshot PNG file
        string imageFileName = $"{sanitizedName}_preview.png";
        string imagePath = Path.Combine(Application.persistentDataPath, imageFileName);

        // Trigger screenshot capture and wait for it to complete
        // Assuming ScreenshotScript has a method like CaptureAndSaveScreenshotCoroutine
        if (screenshotScript != null)
        {
            yield return StartCoroutine(screenshotScript.CaptureAndSaveScreenshotCoroutine(imagePath));
        }
        else
        {
            Debug.LogError("ScreenshotScript reference not set in PhotoMakerSaveUI.");
            imagePath = ""; // Ensure no invalid path is saved if screenshot fails
        }

        // Save layout data along with the image path
        photoMakerManager.SaveCurrentLayout(layoutName, imagePath);

        saveSlotNameInputField.text = ""; // Clear input field
        PopulateSaveSlots(); // Refresh list to show the new save
    }

    private void HandleCancelButtonClick()
    {
        Hide();
    }

    // Helper function to load a Sprite from a file path
    private Sprite LoadSpriteFromFile(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            byte[] fileData = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2); // Create small texture to load data into
            if (texture.LoadImage(fileData)) // LoadImage auto-resizes the texture
            {
                // Create sprite from texture
                return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to load image from {path}: {ex.Message}");
        }

        return null; // Return null if loading failed
    }
}
