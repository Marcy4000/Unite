using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PhotoMakerManager : MonoBehaviour
{
    [SerializeField] private TrainerModel[] trainerModels;
    [SerializeField] private TMP_Dropdown[] trainerAnimationsDropdown;
    [SerializeField] private TMP_InputField[] trainerClothesInputField;
    [SerializeField] private Button[] trainerInitializeButtons;

    private List<List<string>> modelAnimationsNames;

    private void Start()
    {
        modelAnimationsNames = new List<List<string>>();
        for (int i = 0; i < trainerModels.Length; i++)
        {
            int index = i;
            trainerAnimationsDropdown[i].onValueChanged.AddListener(delegate { OnTrainerAnimationChanged(index); });
            trainerInitializeButtons[i].onClick.AddListener(delegate { OnTrainerClothesChanged(index); });
            trainerModels[i].onClothesInitialized += delegate { OnTrainerClothesInitialized(index); };

            modelAnimationsNames.Add(new List<string>());
        }
    }

    private void OnTrainerAnimationChanged(int index)
    {
        if (!trainerModels[index].IsInitialized)
            return;

        trainerModels[index].ActiveAnimator.Play(modelAnimationsNames[index][trainerAnimationsDropdown[index].value]);
    }

    private void OnTrainerClothesChanged(int index)
    {
        trainerModels[index].InitializeClothes(PlayerClothesInfo.Deserialize(trainerClothesInputField[index].text));

        modelAnimationsNames[index].Clear();

        foreach (var clip in trainerModels[index].ActiveAnimator.runtimeAnimatorController.animationClips)
        {
            modelAnimationsNames[index].Add(clip.name);
        }
    }

    private void OnTrainerClothesInitialized(int index)
    {
        modelAnimationsNames[index].Clear();

        foreach (var clip in trainerModels[index].ActiveAnimator.runtimeAnimatorController.animationClips)
        {
            modelAnimationsNames[index].Add(clip.name);
        }

        trainerAnimationsDropdown[index].ClearOptions();

        trainerAnimationsDropdown[index].AddOptions(modelAnimationsNames[index]);

        trainerAnimationsDropdown[index].value = 0;
    }
}
