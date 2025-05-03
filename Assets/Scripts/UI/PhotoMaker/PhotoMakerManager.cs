using JSAM;
using System.Collections;
using System.Collections.Generic;
using System.IO; // Added for Path
using System.Text.RegularExpressions; // Added for Regex
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    [SerializeField] private FixedJoystick positionJoystick, rotationJoystick;

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
}

