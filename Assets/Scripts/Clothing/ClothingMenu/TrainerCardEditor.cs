using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TrainerCardEditor : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown animationDropdown;
    [SerializeField] private Slider xPosSlider, yPosSlider, rotationSlider, scaleSlider;

    [SerializeField] private TrainerCardUI trainerCardUI;
    [SerializeField] private ClothesMenuSelector clothesMenuSelector;

    [SerializeField] private GameObject normalMenu;
    [SerializeField] private TrainerCardEditorMenu backgroundMenu, frameMenu;

    private void Start()
    {
        clothesMenuSelector.OnSelectedMenuChanged += OnSelectedMenuChanged;

        backgroundMenu.OnItemSelected += UpdateBackground;
        frameMenu.OnItemSelected += UpdateFrame;
    }

    private void OnEnable()
    {
        SetActiveMenu(0);

        PlayerClothesInfo playerClothesInfo = GetPlayerClothesInfo();

        float xPos = (playerClothesInfo.TrainerCardInfo.TrainerOffestX / (float)short.MaxValue);
        float yPos = (playerClothesInfo.TrainerCardInfo.TrainerOffestY / (float)short.MaxValue);
        xPosSlider.value = xPos;
        yPosSlider.value = yPos;
        rotationSlider.value = playerClothesInfo.TrainerCardInfo.RotationOffset;
        scaleSlider.value = playerClothesInfo.TrainerCardInfo.TrainerScale;
        animationDropdown.value = playerClothesInfo.TrainerCardInfo.TrainerAnimation;

        xPosSlider.onValueChanged.AddListener(UpdateXPos);
        yPosSlider.onValueChanged.AddListener(UpdateYPos);
        rotationSlider.onValueChanged.AddListener(UpdateRotation);
        scaleSlider.onValueChanged.AddListener(UpdateScale);
        animationDropdown.onValueChanged.AddListener(UpdateAnimation);

        trainerCardUI.Initialize(playerClothesInfo);
    }

    private void OnSelectedMenuChanged(ClothingType clothingType)
    {
        SetActiveMenu((int)clothingType);
    }

    private void UpdateXPos(float value)
    {
        PlayerClothesInfo playerClothesInfo = GetPlayerClothesInfo();

        playerClothesInfo.TrainerCardInfo.TrainerOffestX = (short)(value * short.MaxValue);

        LobbyController.Instance.UpdatePlayerClothes(playerClothesInfo);
        trainerCardUI.UpdateCardPlayer(playerClothesInfo.TrainerCardInfo);
    }

    private void UpdateYPos(float value)
    {
        PlayerClothesInfo playerClothesInfo = GetPlayerClothesInfo();

        playerClothesInfo.TrainerCardInfo.TrainerOffestY = (short)(value * short.MaxValue);

        LobbyController.Instance.UpdatePlayerClothes(playerClothesInfo);
        trainerCardUI.UpdateCardPlayer(playerClothesInfo.TrainerCardInfo);
    }

    private void UpdateRotation(float value)
    {
        PlayerClothesInfo playerClothesInfo = GetPlayerClothesInfo();

        playerClothesInfo.TrainerCardInfo.RotationOffset = (sbyte)value;

        LobbyController.Instance.UpdatePlayerClothes(playerClothesInfo);
        trainerCardUI.UpdateCardPlayer(playerClothesInfo.TrainerCardInfo);
    }

    private void UpdateScale(float value)
    {
        PlayerClothesInfo playerClothesInfo = GetPlayerClothesInfo();

        playerClothesInfo.TrainerCardInfo.TrainerScale = (byte)value;

        LobbyController.Instance.UpdatePlayerClothes(playerClothesInfo);
        trainerCardUI.UpdateCardPlayer(playerClothesInfo.TrainerCardInfo);
    }

    private void UpdateAnimation(int value)
    {
        PlayerClothesInfo playerClothesInfo = GetPlayerClothesInfo();

        playerClothesInfo.TrainerCardInfo.TrainerAnimation = (byte)value;

        LobbyController.Instance.UpdatePlayerClothes(playerClothesInfo);
        trainerCardUI.UpdateCardPlayer(playerClothesInfo.TrainerCardInfo);
    }

    private void UpdateBackground(int index)
    {
        PlayerClothesInfo playerClothesInfo = GetPlayerClothesInfo();
        playerClothesInfo.TrainerCardInfo.BackgroundIndex = (byte)index;
        LobbyController.Instance.UpdatePlayerClothes(playerClothesInfo);
        trainerCardUI.UpdateCardFrame(playerClothesInfo.TrainerCardInfo);
    }

    private void UpdateFrame(int index)
    {
        PlayerClothesInfo playerClothesInfo = GetPlayerClothesInfo();
        playerClothesInfo.TrainerCardInfo.FrameIndex = (byte)index;
        LobbyController.Instance.UpdatePlayerClothes(playerClothesInfo);
        trainerCardUI.UpdateCardFrame(playerClothesInfo.TrainerCardInfo);
    }

    public void SaveClothingChanges()
    {
        PlayerClothesInfo playerClothesInfo = GetPlayerClothesInfo();
        PlayerPrefs.SetString("ClothingInfo", playerClothesInfo.Serialize());
    }

    private PlayerClothesInfo GetPlayerClothesInfo()
    {
        return PlayerClothesInfo.Deserialize(LobbyController.Instance.Player.Data["ClothingInfo"].Value);
    }

    private void SetActiveMenu(int index)
    {
        normalMenu.SetActive(index == 0);
        backgroundMenu.gameObject.SetActive(index == 1);
        frameMenu.gameObject.SetActive(index == 2);
    }
}
