using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DraftCharacterSelector : MonoBehaviour
{
    [SerializeField] private Transform holder;
    [SerializeField] private GameObject iconPrefab;
    [SerializeField] private ToggleGroup toggleGroup;
    [SerializeField] private TMP_Dropdown categoryDropdown;

    private List<DraftCharacterSelectIcon> icons = new List<DraftCharacterSelectIcon>();

    public event System.Action<CharacterInfo> OnCharacterSelected;
    public event System.Action OnSelectedToggleChanged;
    public CharacterInfo HoveredCharacter { get; private set; }

    public void InitializeUI()
    {
        icons.Clear();

        foreach (var character in CharactersList.Instance.Characters)
        {
            if (character.Hidden)
            {
                continue;
            }

            GameObject characterIcon = Instantiate(iconPrefab, holder);
            DraftCharacterSelectIcon draftCharacterIcon = characterIcon.GetComponent<DraftCharacterSelectIcon>();
            draftCharacterIcon.Initialize(character);
            draftCharacterIcon.Toggle.group = toggleGroup;
            draftCharacterIcon.Toggle.onValueChanged.AddListener((value) =>
            {
                if (value)
                {
                    HoveredCharacter = draftCharacterIcon.Info;
                    OnSelectedToggleChanged?.Invoke();
                }
            });
            icons.Add(draftCharacterIcon);
        }

        categoryDropdown.onValueChanged.AddListener(OnCategoryChange);
    }

    private void OnCategoryChange(int value)
    {
        foreach (var icon in icons)
        {
            bool shouldShow = value == 0 || (int)icon.Info.pClass == value-1;

            icon.gameObject.SetActive(shouldShow);
        }
    }

    public void UpdateUnavailablePokemons()
    {
        foreach (var icon in icons)
        {
            icon.SetEnabled(IsCharacterAvailable(icon.Info), DraftSelectController.Instance.BannedCharacters.Contains(CharactersList.Instance.GetCharacterID(icon.Info)));
            icon.Toggle.isOn = false;
        }
    }

    private bool IsCharacterAvailable(CharacterInfo info)
    {
        short id = CharactersList.Instance.GetCharacterID(info);

        if (DraftSelectController.Instance.BannedCharacters.Contains(id))
        {
            return false;
        }

        string idBase64 = NumberEncoder.ToBase64(id);

        foreach (var player in LobbyController.Instance.Lobby.Players)
        {
            if (player.Data["SelectedCharacter"].Value == idBase64)
            {
                return false;
            }
        }

        return true;
    }

    public void OnConfirmButtonPressed()
    {
        if (icons.Any(icon => icon.Toggle.isOn))
        {
            OnCharacterSelected?.Invoke(icons.FirstOrDefault(icon => icon.Toggle.isOn).Info);
        }
    }

    public int GetSelectedCharacterID()
    {
        Debug.Log(icons.FirstOrDefault(icon => icon.Toggle.isOn).Info);
        Debug.Log(CharactersList.Instance.Characters.ToList().IndexOf(icons.FirstOrDefault(icon => icon.Toggle.isOn).Info));
        return CharactersList.Instance.Characters.ToList().IndexOf(icons.FirstOrDefault(icon => icon.Toggle.isOn).Info);
    }
}
