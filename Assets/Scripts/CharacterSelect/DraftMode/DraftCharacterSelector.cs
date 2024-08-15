using System.Collections;
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

    public void InitializeUI()
    {
        foreach (var character in CharactersList.Instance.Characters)
        {
            GameObject characterIcon = Instantiate(iconPrefab, holder);
            DraftCharacterSelectIcon draftCharacterIcon = characterIcon.GetComponent<DraftCharacterSelectIcon>();
            draftCharacterIcon.Initialize(character);
            draftCharacterIcon.Toggle.group = toggleGroup;
        }
    }

    public void OnConfirmButtonPressed()
    {
        if (icons.Any(icon => icon.Toggle.isOn))
        {
            OnCharacterSelected?.Invoke(icons.FirstOrDefault(icon => icon.Toggle.isOn).Info);
        }
    }
}
