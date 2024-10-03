using UnityEngine;
using UnityEngine.UI;

public class DraftCharacterSelectIcon : MonoBehaviour
{
    [SerializeField] private Image charSprite;
    [SerializeField] private Toggle toggle;
    [SerializeField] private GameObject disabledSprite, bannedSprite;

    private CharacterInfo info;

    public CharacterInfo Info => info;
    public Toggle Toggle => toggle;

    public void Initialize(CharacterInfo info)
    {
        this.info = info;
        charSprite.sprite = info.portrait;
        toggle.isOn = false;
        disabledSprite.SetActive(false);
        bannedSprite.SetActive(false);
    }

    public void SetEnabled(bool enabled, bool banned)
    {
        toggle.interactable = enabled;
        disabledSprite.SetActive(!enabled);
        bannedSprite.SetActive(banned);
    }
}
