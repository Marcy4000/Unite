using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PhotoMakerManager : MonoBehaviour
{
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

    private List<TrainerModel> trainerModels = new List<TrainerModel>();
    private int currentTrainerIndex = -1;
    private List<string> currentModelAnimationsNames = new List<string>();

    private void Start()
    {
        trainerAnimationsDropdown.onValueChanged.AddListener(OnTrainerAnimationChanged);
        trainerInitializeButton.onClick.AddListener(OnTrainerClothesChanged);
        nextTrainerButton.onClick.AddListener(SelectNextTrainer);
        prevTrainerButton.onClick.AddListener(SelectPreviousTrainer);
        spawnTrainerButton.onClick.AddListener(SpawnTrainer);
        despawnTrainerButton.onClick.AddListener(DespawnTrainer);
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
        GameObject newTrainerObj = Instantiate(trainerModelPrefab, spawnPoint.position, Quaternion.identity);
        TrainerModel newTrainer = newTrainerObj.GetComponent<TrainerModel>();
        newTrainer.onClothesInitialized += UpdateAnimationList;
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
}
