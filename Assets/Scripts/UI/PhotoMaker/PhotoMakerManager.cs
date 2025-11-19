using JSAM;
using System.Collections;
using System.Collections.Generic;
using System.IO; // Added for Path
using System.Text.RegularExpressions; // Added for Regex
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;

public class PhotoMakerManager : MonoBehaviour
{
    [System.Serializable]
    public class TrainerLayoutData
    {
        public string Clothes;
        public Vector3 Position;
        public Vector3 Rotation;
        public string Animation;
    }

    [System.Serializable]
    private class TrainerLayoutWrapper
    {
        public string LayoutName = "Default Layout"; // Added
        public string ImagePath = ""; // Added
        public List<TrainerLayoutData> trainers = new List<TrainerLayoutData>();
    }

    [SerializeField] private GameObject trainerModelPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private TMP_Dropdown trainerAnimationsDropdown;
    [SerializeField] private TMP_InputField trainerClothesInputField;
    [SerializeField] private Button trainerInitializeButton;
    [SerializeField] private Button nextTrainerButton;
    [SerializeField] private Button prevTrainerButton;
    [SerializeField] private Button spawnTrainerButton;
    [SerializeField] private Button despawnTrainerButton;
    [SerializeField] private TMP_Text currentTrainerText;
    [SerializeField] private ClothesSelector clothesSelector;
    [SerializeField] private GameObject clothesMenu;
    [SerializeField] private DialogueTrigger dialogueTrigger;

    [SerializeField] private GameObject trainerCardHolder;
    [SerializeField] private Image trainerCardBackground, trainerCardFrame;
    [SerializeField] private RenderTexture photoPreviewRenderTexture;
    [SerializeField] private TrainerCardItem[] backgrounds;
    [SerializeField] private TrainerCardItem[] frames;

    [SerializeField] private FixedJoystick positionJoystick, rotationJoystick;

    private bool trainerCardEditorMode = false;
    private AsyncOperationHandle<Sprite> trainerBackgroundHandle;
    private AsyncOperationHandle<Sprite> trainerFrameHandle;

    private List<TrainerModel> trainerModels = new List<TrainerModel>();
    private int currentTrainerIndex = -1;
    private List<string> currentModelAnimationsNames = new List<string>();

    private float joystickSensitivity = 1f;

    private void Start()
    {
        trainerAnimationsDropdown.onValueChanged.AddListener(OnTrainerAnimationChanged);
        trainerInitializeButton.onClick.AddListener(OnTrainerClothesChanged);
        nextTrainerButton.onClick.AddListener(SelectNextTrainer);
        prevTrainerButton.onClick.AddListener(SelectPreviousTrainer);
        spawnTrainerButton.onClick.AddListener(SpawnTrainer);
        despawnTrainerButton.onClick.AddListener(DespawnTrainer);

        SpawnTrainer();

        if (LoadingScreen.Instance != null)
        {
            StartCoroutine(InitializeRoutine());
        }
        else
        {
            clothesMenu.SetActive(false);
        }
    }

    private IEnumerator InitializeRoutine()
    {
        clothesMenu.SetActive(true);

        yield return new WaitForSeconds(0.15f);

        clothesMenu.SetActive(false);

        dialogueTrigger.TriggerDialogue();

        LoadingScreen.Instance.HideGenericLoadingScreen();
    }

    private void OnTrainerAnimationChanged(int index)
    {
        if (currentTrainerIndex == -1 || trainerModels.Count == 0) return;
        if (!trainerModels[currentTrainerIndex].IsInitialized) return;

        trainerModels[currentTrainerIndex].ActiveAnimator.Play(currentModelAnimationsNames[index]);
    }

    private void OnTrainerClothesChanged()
    {
        if (currentTrainerIndex == -1 || trainerModels.Count == 0) return;

        trainerModels[currentTrainerIndex].InitializeClothes(PlayerClothesInfo.Deserialize(trainerClothesInputField.text));
        UpdateAnimationList();
    }

    private void UpdateAnimationList()
    {
        if (currentTrainerIndex == -1 || trainerModels.Count == 0) return;

        currentModelAnimationsNames.Clear();

        if (!trainerModels[currentTrainerIndex].IsInitialized) return;

        foreach (var clip in trainerModels[currentTrainerIndex].ActiveAnimator.runtimeAnimatorController.animationClips)
        {
            currentModelAnimationsNames.Add(clip.name);
        }

        trainerAnimationsDropdown.ClearOptions();
        trainerAnimationsDropdown.AddOptions(currentModelAnimationsNames);

        string currentAnimation = trainerModels[currentTrainerIndex].ActiveAnimator.GetCurrentAnimatorClipInfo(0)[0].clip.name;
        int currentIndex = currentModelAnimationsNames.IndexOf(currentAnimation);
        trainerAnimationsDropdown.value = currentIndex != -1 ? currentIndex : 0;
    }

    private void SelectNextTrainer()
    {
        if (trainerModels.Count == 0) return;
        currentTrainerIndex = (currentTrainerIndex + 1) % trainerModels.Count;
        UpdateUI();
    }

    private void SelectPreviousTrainer()
    {
        if (trainerModels.Count == 0) return;
        currentTrainerIndex = (currentTrainerIndex - 1 + trainerModels.Count) % trainerModels.Count;
        UpdateUI();
    }

    private void SpawnTrainer()
    {
        GameObject newTrainerObj = Instantiate(trainerModelPrefab, spawnPoint.position, spawnPoint.rotation);
        TrainerModel newTrainer = newTrainerObj.GetComponent<TrainerModel>();
        newTrainer.onClothesInitialized += UpdateAnimationList;
        newTrainer.InitializeClothes(new PlayerClothesInfo());
        trainerModels.Add(newTrainer);
        currentTrainerIndex = trainerModels.Count - 1;
        UpdateUI();
    }

    private void DespawnTrainer()
    {
        if (trainerModels.Count == 0 || currentTrainerIndex == -1) return;

        Destroy(trainerModels[currentTrainerIndex].gameObject);
        trainerModels.RemoveAt(currentTrainerIndex);

        if (trainerModels.Count == 0)
            currentTrainerIndex = -1;
        else
            currentTrainerIndex = Mathf.Clamp(currentTrainerIndex, 0, trainerModels.Count - 1);

        UpdateUI();
    }

    private void UpdateUI()
    {
        bool hasTrainer = trainerModels.Count > 0 && currentTrainerIndex != -1;
        trainerAnimationsDropdown.interactable = hasTrainer;
        trainerClothesInputField.interactable = hasTrainer;
        trainerInitializeButton.interactable = hasTrainer;
        nextTrainerButton.interactable = hasTrainer && trainerModels.Count > 1;
        prevTrainerButton.interactable = hasTrainer && trainerModels.Count > 1;
        despawnTrainerButton.interactable = hasTrainer;

        if (hasTrainer)
        {
            currentTrainerText.text = $"Trainer {currentTrainerIndex + 1}";
            trainerClothesInputField.text = trainerModels[currentTrainerIndex].PlayerClothesInfo.Serialize();
            UpdateAnimationList();

            if (trainerCardEditorMode && trainerCardHolder != null)
            {
                var card = trainerModels[currentTrainerIndex].PlayerClothesInfo.TrainerCardInfo;
                _ = LoadTrainerCardSpritesAsync(card.BackgroundIndex, card.FrameIndex);
            }
        }
        else
        {
            currentTrainerText.text = "No trainers";
            trainerAnimationsDropdown.ClearOptions();
            trainerClothesInputField.text = "";
        }
    }

    private void Update()
    {
        if (currentTrainerIndex == -1 || trainerModels.Count == 0) return;

        if (trainerModels[currentTrainerIndex].IsInitialized)
        {
            Vector3 position = new Vector3(positionJoystick.Horizontal, 0, positionJoystick.Vertical);
            Vector3 rotation = new Vector3(0, rotationJoystick.Horizontal, 0);
            trainerModels[currentTrainerIndex].transform.position += position * Time.deltaTime * joystickSensitivity;
            trainerModels[currentTrainerIndex].transform.Rotate(-rotation * Time.deltaTime * 100f * joystickSensitivity);
        }
    }

    public void SetJoystickSensitivity(float value)
    {
        joystickSensitivity = value;
    }

    public void LoadMainMenu()
    {
        LobbyController.Instance.ReturnToLobby(false);
    }

    // Helper function to create safe filenames - MAKE PUBLIC
    public string SanitizeFileName(string name)
    {
        string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
        string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
        return Regex.Replace(name, invalidRegStr, "_");
    }

    public void SaveCurrentLayout(string layoutName, string imagePath = "")
    {
        if (currentTrainerIndex == -1 || trainerModels.Count == 0 || string.IsNullOrWhiteSpace(layoutName))
        {
            Debug.LogError("Cannot save layout: No trainers available or layout name is empty.");
            return;
        }

        string sanitizedName = SanitizeFileName(layoutName); // Use the public method
        if (string.IsNullOrWhiteSpace(sanitizedName))
        {
            Debug.LogError("Cannot save layout: Sanitized layout name is empty.");
            return;
        }

        string filePath = Path.Combine(Application.persistentDataPath, $"{sanitizedName}_layout.json");
        TrainerLayoutWrapper wrapper = new TrainerLayoutWrapper
        {
            LayoutName = layoutName, // Store the original name
            ImagePath = imagePath   // Store the image path
        };

        foreach (var trainer in trainerModels)
        {
            TrainerLayoutData data = new TrainerLayoutData
            {
                Clothes = trainer.PlayerClothesInfo.Serialize(),
                Position = trainer.transform.position,
                Rotation = trainer.transform.rotation.eulerAngles,
                Animation = trainer.ActiveAnimator.GetCurrentAnimatorClipInfo(0)[0].clip.name
            };
            wrapper.trainers.Add(data);
        }

        string json = JsonUtility.ToJson(wrapper, true);
        Debug.Log($"Saving layout '{layoutName}' to: {filePath}");
        System.IO.File.WriteAllText(filePath, json);
    }

    // Make sure TryGetLayoutInfo is public if it isn't already
    public bool TryGetLayoutInfo(string sanitizedLayoutName, out string foundLayoutName, out string foundImagePath)
    {
        foundLayoutName = "";
        foundImagePath = "";
        if (string.IsNullOrWhiteSpace(sanitizedLayoutName)) return false;

        string filePath = Path.Combine(Application.persistentDataPath, $"{sanitizedLayoutName}_layout.json");
        if (!System.IO.File.Exists(filePath)) return false;

        try
        {
            string json = System.IO.File.ReadAllText(filePath);
            TrainerLayoutWrapper wrapper = JsonUtility.FromJson<TrainerLayoutWrapper>(json);
            foundLayoutName = wrapper.LayoutName;
            foundImagePath = wrapper.ImagePath;
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error reading layout info for '{sanitizedLayoutName}': {ex.Message}");
            return false;
        }
    }

    public void LoadLayout(string sanitizedLayoutName) // Ensure parameter is the sanitized name
    {
        if (string.IsNullOrWhiteSpace(sanitizedLayoutName))
        {
            Debug.LogError("Cannot load layout: Sanitized layout name is empty.");
            return;
        }

        string filePath = Path.Combine(Application.persistentDataPath, $"{sanitizedLayoutName}_layout.json");
        if (!System.IO.File.Exists(filePath))
        {
            Debug.LogWarning($"Layout file '{sanitizedLayoutName}' not found at: {filePath}");
            return;
        }

        Debug.Log($"Loading layout '{sanitizedLayoutName}' from: {filePath}");

        int previousIndex = currentTrainerIndex;
        for (int i = trainerModels.Count - 1; i >= 0; i--)
        {
            currentTrainerIndex = i;
            DespawnTrainer();
        }
        currentTrainerIndex = -1;

        string json = System.IO.File.ReadAllText(filePath);
        TrainerLayoutWrapper wrapper = JsonUtility.FromJson<TrainerLayoutWrapper>(json);

        if (wrapper.trainers.Count == 0)
        {
            UpdateUI();
            return;
        }

        foreach (var data in wrapper.trainers)
        {
            SpawnTrainer();
            var trainer = trainerModels[currentTrainerIndex];

            StartCoroutine(LoadTrainer(trainer, data));
        }

        currentTrainerIndex = 0;
        UpdateUI();
    }

    private IEnumerator LoadTrainer(TrainerModel trainerModel, TrainerLayoutData data)
    {
        trainerModel.transform.position = data.Position;
        trainerModel.transform.rotation = Quaternion.Euler(data.Rotation);

        yield return new WaitUntil(() => trainerModel.IsInitialized);

        trainerModel.InitializeClothes(PlayerClothesInfo.Deserialize(data.Clothes));

        yield return new WaitUntil(() => trainerModel.IsInitialized);

        bool animFound = false;
        foreach (var clip in trainerModel.ActiveAnimator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == data.Animation)
            {
                animFound = true;
                break;
            }
        }
        if (animFound)
        {
            trainerModel.ActiveAnimator.Play(data.Animation);
        }
        else
        {
            Debug.LogWarning($"Animation '{data.Animation}' not found for trainer. Playing default state.");
        }
    }

    public void LookAtCamera()
    {
        if (currentTrainerIndex == -1 || trainerModels.Count == 0) return;

        if (trainerModels[currentTrainerIndex].IsInitialized)
        {
            trainerModels[currentTrainerIndex].transform.LookAt(Camera.main.transform);
            trainerModels[currentTrainerIndex].transform.rotation = Quaternion.Euler(0, trainerModels[currentTrainerIndex].transform.rotation.eulerAngles.y, 0);
        }
    }

    public void OpenClothesMenu()
    {
        if (currentTrainerIndex == -1 || trainerModels.Count == 0) return;

        if (trainerModels[currentTrainerIndex].IsInitialized)
        {
            clothesMenu.SetActive(true);
            clothesSelector.SetNewClothesInfo(trainerModels[currentTrainerIndex].PlayerClothesInfo);
        }
    }

    public void CloseClothesMenu()
    {
        clothesMenu.SetActive(false);
        if (currentTrainerIndex == -1 || trainerModels.Count == 0) return;
        if (trainerModels[currentTrainerIndex].IsInitialized)
        {
            trainerModels[currentTrainerIndex].InitializeClothes(clothesSelector.LocalPlayerClothesInfo);
        }
    }

    private void OnDestroy()
    {
        if (trainerBackgroundHandle.IsValid())
        {
            Addressables.Release(trainerBackgroundHandle);
        }

        if (trainerFrameHandle.IsValid())
        {
            Addressables.Release(trainerFrameHandle);
        }
    }

    public void SetTrainerCardEditorMode(bool enabled)
    {
        trainerCardEditorMode = enabled;
        if (trainerCardHolder != null)
            trainerCardHolder.SetActive(enabled);

        if (enabled)
        {
            if (currentTrainerIndex != -1 && trainerModels.Count > 0 && trainerModels[currentTrainerIndex].IsInitialized)
            {
                var card = trainerModels[currentTrainerIndex].PlayerClothesInfo.TrainerCardInfo;
                _ = LoadTrainerCardSpritesAsync(card.BackgroundIndex, card.FrameIndex);
            }
        }
        else
        {
            if (trainerCardBackground != null) trainerCardBackground.sprite = null;
            if (trainerCardFrame != null) trainerCardFrame.sprite = null;

            if (trainerBackgroundHandle.IsValid())
            {
                Addressables.Release(trainerBackgroundHandle);
                trainerBackgroundHandle = default;
            }
            if (trainerFrameHandle.IsValid())
            {
                Addressables.Release(trainerFrameHandle);
                trainerFrameHandle = default;
            }
        }
    }


    private async Task LoadTrainerCardSpritesAsync(byte backgroundIndex, byte frameIndex)
    {
        if (trainerBackgroundHandle.IsValid())
        {
            if (trainerCardBackground != null) trainerCardBackground.sprite = null;
            Addressables.Release(trainerBackgroundHandle);
            trainerBackgroundHandle = default;
        }

        if (trainerFrameHandle.IsValid())
        {
            if (trainerCardFrame != null) trainerCardFrame.sprite = null;
            Addressables.Release(trainerFrameHandle);
            trainerFrameHandle = default;
        }

        if (backgrounds == null || backgrounds.Length == 0 || backgroundIndex >= backgrounds.Length) return;
        if (frames == null || frames.Length == 0 || frameIndex >= frames.Length) return;

        trainerBackgroundHandle = Addressables.LoadAssetAsync<Sprite>(backgrounds[backgroundIndex].itemSprite);
        trainerFrameHandle = Addressables.LoadAssetAsync<Sprite>(frames[frameIndex].itemSprite);

        await Task.WhenAll(trainerBackgroundHandle.Task, trainerFrameHandle.Task);

        if (trainerBackgroundHandle.Status == AsyncOperationStatus.Succeeded && trainerCardBackground != null)
        {
            trainerCardBackground.sprite = trainerBackgroundHandle.Result;
        }
        if (trainerFrameHandle.Status == AsyncOperationStatus.Succeeded && trainerCardFrame != null)
        {
            trainerCardFrame.sprite = trainerFrameHandle.Result;
        }
    }

    // Call this from your photo-capture flow when a photo is taken.
    // If trainer card editor mode is active, this will compose background + preview + frame and save a PNG.
    public void SaveTrainerCardPhotoIfInTrainerCardMode()
    {
        if (!trainerCardEditorMode) return;
        try
        {
            ComposeAndSaveTrainerCardPhoto();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error composing trainer card photo: {ex}");
        }
    }

    private void ComposeAndSaveTrainerCardPhoto()
    {
        if (trainerCardBackground == null || trainerCardBackground.sprite == null ||
            trainerCardFrame == null || trainerCardFrame.sprite == null ||
            photoPreviewRenderTexture == null)
        {
            Debug.LogWarning("Missing sprites/textures for trainer card composition.");
            return;
        }

        Sprite bgSprite = trainerCardBackground.sprite;
        Sprite frameSprite = trainerCardFrame.sprite;

        int w = (int)bgSprite.rect.width;
        int h = (int)bgSprite.rect.height;

        // Convert/resize photo RenderTexture to proper size if needed
        Texture2D photoTexture = PreparePhotoTextureFromRenderTexture(photoPreviewRenderTexture, w, h);

        // Render the full UI (background, photo, frame) into a single texture
        Texture2D composed = RenderCardUIToTexture(bgSprite, photoTexture, frameSprite, w, h);

        if (composed == null)
        {
            Debug.LogError("Failed to render trainer card UI to texture.");
            if (photoTexture != null) Object.Destroy(photoTexture);
            return;
        }

        // Save composed texture
        byte[] png = composed.EncodeToPNG();
        string fileName = SanitizeFileName($"TrainerCard_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
        string path = Path.Combine(Application.persistentDataPath, fileName);
        System.IO.File.WriteAllBytes(path, png);
        Debug.Log($"Saved trainer card photo to: {path}");

        Object.Destroy(composed);
        Object.Destroy(photoTexture);
    }

    // Prepare a Texture2D from RenderTexture, properly handling color space conversion and resizing to w x h
    private Texture2D PreparePhotoTextureFromRenderTexture(RenderTexture sourceRT, int w, int h)
    {
        if (sourceRT == null) return null;

        // Create an sRGB intermediate RT to convert from Linear (if source is Linear) to sRGB
        RenderTexture srgbRT = RenderTexture.GetTemporary(sourceRT.width, sourceRT.height, 0, 
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);

        // Blit from source to sRGB RT - this handles color space conversion
        Graphics.Blit(sourceRT, srgbRT);

        // Read pixels from sRGB RT
        RenderTexture current = RenderTexture.active;
        RenderTexture.active = srgbRT;
        Texture2D photoTex2D = new Texture2D(sourceRT.width, sourceRT.height, TextureFormat.RGBA32, false);
        photoTex2D.ReadPixels(new Rect(0, 0, sourceRT.width, sourceRT.height), 0, 0);
        photoTex2D.Apply();
        RenderTexture.active = current;
        
        RenderTexture.ReleaseTemporary(srgbRT);

        // Resize if needed
        if (photoTex2D.width != w || photoTex2D.height != h)
        {
            RenderTexture temp = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(photoTex2D, temp);
            RenderTexture.active = temp;
            Texture2D scaled = new Texture2D(w, h, TextureFormat.RGBA32, false);
            scaled.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            scaled.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(temp);
            Object.Destroy(photoTex2D);
            photoTex2D = scaled;
        }

        return photoTex2D;
    }

    // Render the UI (background sprite, photo texture, frame sprite) into a Texture2D sized w x h using a temporary Canvas+Camera.
    private Texture2D RenderCardUIToTexture(Sprite bgSprite, Texture2D photoTexture, Sprite frameSprite, int w, int h)
    {
        int tempLayer = 31; // use a high unused layer for isolation

        // Create a standard ARGB32 RenderTexture with default (sRGB) color space
        RenderTexture rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);

        // Create temporary camera
        GameObject camGO = new GameObject("TempUICam");
        Camera cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.clear;
        cam.orthographic = true;
        cam.orthographicSize = h * 0.5f;
        cam.cullingMask = 1 << tempLayer;
        cam.targetTexture = rt;
        cam.allowHDR = true;
        cam.allowMSAA = false;

        // Create Canvas
        GameObject canvasGO = new GameObject("TempUICanvas");
        canvasGO.layer = tempLayer;
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(w, h);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Background Image (bottom layer)
        GameObject bgGO = new GameObject("TempBG");
        bgGO.layer = tempLayer;
        bgGO.transform.SetParent(canvasGO.transform, false);
        Image bgImage = bgGO.AddComponent<Image>();
        bgImage.sprite = bgSprite;
        bgImage.preserveAspect = false;
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        // Photo RawImage (middle layer)
        GameObject photoGO = new GameObject("TempPhoto");
        photoGO.layer = tempLayer;
        photoGO.transform.SetParent(canvasGO.transform, false);
        RawImage raw = photoGO.AddComponent<RawImage>();
        raw.texture = photoTexture;
        RectTransform photoRT = photoGO.GetComponent<RectTransform>();
        photoRT.anchorMin = Vector2.zero;
        photoRT.anchorMax = Vector2.one;
        photoRT.offsetMin = Vector2.zero;
        photoRT.offsetMax = Vector2.zero;

        // Frame Image (top layer)
        GameObject frameGO = new GameObject("TempFrame");
        frameGO.layer = tempLayer;
        frameGO.transform.SetParent(canvasGO.transform, false);
        Image frameImage = frameGO.AddComponent<Image>();
        frameImage.sprite = frameSprite;
        frameImage.preserveAspect = false;
        RectTransform frameRT = frameGO.GetComponent<RectTransform>();
        frameRT.anchorMin = Vector2.zero;
        frameRT.anchorMax = Vector2.one;
        frameRT.offsetMin = Vector2.zero;
        frameRT.offsetMax = Vector2.zero;

        // Force a render
        cam.Render();

        // Read pixels from the render texture
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D result = new Texture2D(w, h, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        result.Apply();
        RenderTexture.active = prev;

        // Cleanup
        cam.targetTexture = null;
        Object.DestroyImmediate(camGO);
        Object.DestroyImmediate(canvasGO);
        RenderTexture.ReleaseTemporary(rt);

        return result;
    }
}
