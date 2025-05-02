using System.Collections;
using System.Collections.Generic;
using TMPro;
using UI.ThreeDimensional;
using UnityEngine;
using UnityEngine.UI;

public class ClothesSelector : MonoBehaviour
{
    [SerializeField] private GameObject selectionPrefab;

    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private ToggleGroup selectionToggleGroup;
    [SerializeField] private UIObject3D trainerModelUI;
    [SerializeField] private ClothesMenuSelector clothesMenuSelector;

    [SerializeField] private Toggle[] genderToggles;

    [SerializeField] private GameObject colorPickersHolder;
    [SerializeField] private FlexibleColorPicker hairColor, eyeColor;
    [SerializeField] private TMP_Dropdown skinColor;

    private List<Toggle> menuToggles = new List<Toggle>();

    private TrainerModel trainerModel;

    private ClothingType currentMenu;

    private bool isShowingColorPickers;

    private PlayerClothesInfo localPlayerClothesInfo;

    public PlayerClothesInfo LocalPlayerClothesInfo => localPlayerClothesInfo;

    private void Start()
    {
        PlayerClothesInfo playerClothesInfo = GetPlayerClothesInfo();
        localPlayerClothesInfo = playerClothesInfo;

        genderToggles[playerClothesInfo.IsMale ? 0 : 1].isOn = true;

        foreach (var toggle in genderToggles)
        {
            toggle.onValueChanged.AddListener((value) =>
            {
                if (!value) return;
                StartCoroutine(ChangeGender());
            });
        }

        hairColor.onColorChange.AddListener(ChangeHairColor);
        eyeColor.onColorChange.AddListener(ChangeEyeColor);

        hairColor.SetColorNoAlpha(playerClothesInfo.HairColor);
        eyeColor.SetColorNoAlpha(playerClothesInfo.EyeColor);

        skinColor.value = playerClothesInfo.SkinColor;

        skinColor.onValueChanged.AddListener(ChangeSkinColor);

        clothesMenuSelector.OnSelectedMenuChanged += (type) =>
        {
            currentMenu = type;
            InitializeMenuItems(localPlayerClothesInfo.IsMale);
        };

        isShowingColorPickers = false;
        colorPickersHolder.SetActive(isShowingColorPickers);

        InitializeMenuItems(playerClothesInfo.IsMale);
    }

    public void SetNewClothesInfo(PlayerClothesInfo newClothesInfo)
    {
        localPlayerClothesInfo = newClothesInfo;

        genderToggles[localPlayerClothesInfo.IsMale ? 0 : 1].isOn = true;
        genderToggles[localPlayerClothesInfo.IsMale ? 1 : 0].isOn = false;

        hairColor.SetColorNoAlpha(localPlayerClothesInfo.HairColor);
        eyeColor.SetColorNoAlpha(localPlayerClothesInfo.EyeColor);

        skinColor.value = localPlayerClothesInfo.SkinColor;

        if (trainerModel != null)
            trainerModel.InitializeClothes(localPlayerClothesInfo);

        InitializeMenuItems(localPlayerClothesInfo.IsMale);
    }

    private void OnEnable()
    {
        StartCoroutine(SetTrainerModel());
    }

    private void OnDisable()
    {
        trainerModel = null;
    }

    private IEnumerator SetTrainerModel()
    {
        while (trainerModelUI.TargetGameObject == null)
        {
            yield return null;
        }

        yield return null;

        trainerModel = trainerModelUI.TargetGameObject.GetComponent<TrainerModel>();
        trainerModel.InitializeClothes(localPlayerClothesInfo);
    }

    private void InitializeMenuItems(bool isMale)
    {
        foreach (var toggle in menuToggles)
        {
            toggle.onValueChanged.RemoveAllListeners();
        }

        foreach (Transform child in scrollRect.content)
        {
            Destroy(child.gameObject);
        }

        menuToggles.Clear();
        scrollRect.verticalNormalizedPosition = 1;

        foreach (var item in ClothesList.Instance.GetAvailableClothesOfType(currentMenu, isMale))
        {
            var selectionItem = Instantiate(selectionPrefab, scrollRect.content).GetComponent<ClothingSelectionItem>();
            selectionItem.SetItem(item);
            selectionItem.ItemToggle.group = selectionToggleGroup;
            menuToggles.Add(selectionItem.ItemToggle);

            if (ClothesList.Instance.GetClothingIndex(currentMenu, item, isMale) == localPlayerClothesInfo.GetClothingIndex(currentMenu))
            {
                selectionItem.ItemToggle.isOn = true;
            }
            else
            {
                selectionItem.ItemToggle.isOn = false;
            }
        }

        foreach (var toggle in menuToggles)
        {
            toggle.onValueChanged.AddListener((value) =>
            {
                if (!value) return;
                StartCoroutine(OnAnyToggleChanged());
            });
        }
    }

    private IEnumerator OnAnyToggleChanged()
    {
        yield return null;

        foreach (var toggle in menuToggles)
        {
            if (toggle.isOn)
            {
                ClothingItem item = toggle.GetComponent<ClothingSelectionItem>().Item;
                localPlayerClothesInfo.SetClothingItem(currentMenu, ClothesList.Instance.GetClothingIndex(currentMenu, item, localPlayerClothesInfo.IsMale));
            }
        }

        trainerModel.InitializeClothes(localPlayerClothesInfo);
    }

    private PlayerClothesInfo GetPlayerClothesInfo()
    {
        return PlayerClothesInfo.Deserialize(LobbyController.Instance.Player.Data["ClothingInfo"].Value);
    }

    private IEnumerator ChangeGender()
    {
        yield return null;

        localPlayerClothesInfo.IsMale = genderToggles[0].isOn;

        StartCoroutine(SetTrainerModel());

        InitializeMenuItems(localPlayerClothesInfo.IsMale);
    }

    public void SaveClothingChanges()
    {
        PlayerPrefs.SetString("ClothingInfo", localPlayerClothesInfo.Serialize());

        LobbyController.Instance.UpdatePlayerClothes(localPlayerClothesInfo);
    }

    private void ChangeHairColor(Color color)
    {
        localPlayerClothesInfo.HairColor = color;

        if (trainerModel != null)
            trainerModel.UpdateMaterialColors(localPlayerClothesInfo);
    }

    private void ChangeEyeColor(Color color)
    {
        localPlayerClothesInfo.EyeColor = color;

        if (trainerModel != null)
            trainerModel.UpdateMaterialColors(localPlayerClothesInfo);
    }

    private void ChangeSkinColor(int index)
    {
        localPlayerClothesInfo.SkinColor = (byte)index;

        if (trainerModel != null)
            trainerModel.UpdateMaterialColors(localPlayerClothesInfo);
    }

    public void ToggleColorPickers()
    {
        isShowingColorPickers = !isShowingColorPickers;
        colorPickersHolder.SetActive(isShowingColorPickers);
    }

    public void CloseColorPickers()
    {
        isShowingColorPickers = false;
        colorPickersHolder.SetActive(isShowingColorPickers);
    }
}
