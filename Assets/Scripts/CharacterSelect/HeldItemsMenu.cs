using UnityEngine;

public class HeldItemsMenu : MonoBehaviour
{
    [SerializeField] private GameObject itemPickerUI;
    [SerializeField] private CurrentItemsMenu currentItemsMenu;

    private void Start()
    {
        currentItemsMenu.OnItemsChanged += SaveChanges;

        CancelAndExit();
    }

    private void OnEnable()
    {
        currentItemsMenu.InitializeIcons(HeldItemDatabase.DeserializeHeldItems(LobbyController.Instance.Player.Data["HeldItems"].Value));
    }

    private void SaveChanges()
    {
        byte[] battleItemsIDs = CharactersList.Instance.GetHeldItemsIDs(currentItemsMenu.SelectedHeldItems);
        LobbyController.Instance.UpdatePlayerHeldItems(HeldItemDatabase.SerializeHeldItems(battleItemsIDs));
    }

    public void CancelAndExit()
    {
        gameObject.SetActive(false);
    }

    public void Open()
    {
        gameObject.SetActive(true);
    }

    public void ShowItemPickerUI(bool show)
    {
        itemPickerUI.SetActive(show);
    }
}
