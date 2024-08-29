using System.Collections;
using System.Collections.Generic;
using UI.ThreeDimensional;
using UnityEngine;
using UnityEngine.UI;

public class ClothesSelector : MonoBehaviour
{
    [SerializeField] private GameObject selectionPrefab;

    [SerializeField] private Transform selectionHolder;
    [SerializeField] private ToggleGroup selectionToggleGroup;
    [SerializeField] private UIObject3D trainerModelUI;
    [SerializeField] private ClothesMenuSelector clothesMenuSelector;

    [SerializeField] private Toggle[] genderToggles;

    private List<Toggle> menuToggles = new List<Toggle>();

    private TrainerModel trainerModel;

    private ClothingType currentMenu;

    private void Start()
    {
        foreach (var toggle in genderToggles)
        {
            toggle.onValueChanged.AddListener((value) =>
            {
                if (!value) return;
                StartCoroutine(ChangeGender());
            });
        }

        clothesMenuSelector.OnSelectedMenuChanged += (type) =>
        {
            currentMenu = type;
            InitializeMenuItems(GetPlayerClothesInfo().IsMale);
        };

        InitializeMenuItems(GetPlayerClothesInfo().IsMale);
        genderToggles[GetPlayerClothesInfo().IsMale ? 0 : 1].isOn = true;
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
        trainerModel = trainerModelUI.TargetGameObject.GetComponent<TrainerModel>();
        trainerModel.InitializeClothes(GetPlayerClothesInfo());
    }

    private void InitializeMenuItems(bool isMale)
    {
        foreach (var toggle in menuToggles)
        {
            toggle.onValueChanged.RemoveAllListeners();
        }

        foreach (Transform child in selectionHolder)
        {
            Destroy(child.gameObject);
        }

        menuToggles.Clear();

        foreach (var item in ClothesList.Instance.GetAvailableClothesOfType(currentMenu, isMale))
        {
            var selectionItem = Instantiate(selectionPrefab, selectionHolder).GetComponent<ClothingSelectionItem>();
            selectionItem.SetItem(item);
            selectionItem.ItemToggle.group = selectionToggleGroup;
            menuToggles.Add(selectionItem.ItemToggle);

            if (ClothesList.Instance.GetClothingIndex(currentMenu, item, isMale) == GetPlayerClothesInfo().GetClothingIndex(currentMenu))
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

        PlayerClothesInfo playerClothesInfo = GetPlayerClothesInfo();

        foreach (var toggle in menuToggles)
        {
            if (toggle.isOn)
            {
                ClothingItem item = toggle.GetComponent<ClothingSelectionItem>().Item;
                playerClothesInfo.SetClothingItem(currentMenu, ClothesList.Instance.GetClothingIndex(currentMenu, item, playerClothesInfo.IsMale));
            }
        }

        trainerModel.InitializeClothes(playerClothesInfo);
        LobbyController.Instance.UpdatePlayerClothes(playerClothesInfo);
    }

    private PlayerClothesInfo GetPlayerClothesInfo()
    {
        return PlayerClothesInfo.Deserialize(LobbyController.Instance.Player.Data["ClothingInfo"].Value);
    }

    private IEnumerator ChangeGender()
    {
        yield return null;

        PlayerClothesInfo playerClothesInfo = GetPlayerClothesInfo();

        playerClothesInfo.IsMale = genderToggles[0].isOn;

        trainerModel.InitializeClothes(playerClothesInfo);
        LobbyController.Instance.UpdatePlayerClothes(playerClothesInfo);

        InitializeMenuItems(playerClothesInfo.IsMale);
    }

    public void SaveClothingChanges()
    {
        PlayerClothesInfo playerClothesInfo = GetPlayerClothesInfo();
        PlayerPrefs.SetString("ClothingInfo", playerClothesInfo.Serialize());
    }
}
