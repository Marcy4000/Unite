using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class DraftCharacterSelector : MonoBehaviour
{
    [SerializeField] private Transform holder;
    [SerializeField] private GameObject iconPrefab;
    [SerializeField] private ToggleGroup toggleGroup;

    private List<DraftCharacterSelectIcon> icons = new List<DraftCharacterSelectIcon>();

    public event System.Action<CharacterInfo> OnCharacterSelected;
    public event System.Action OnSelectedToggleChanged;
    public CharacterInfo HoveredCharacter { get; private set; }

    public void InitializeUI()
    {
        icons.Clear();

        foreach (var character in CharactersList.Instance.Characters)
        {
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
    }

    public void UpdateUnavailablePokemons()
    {
        foreach (var icon in icons)
        {
            icon.Toggle.interactable = IsCharacterAvailable(icon.Info);
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
