using UnityEngine;
using UnityEngine.UI;

public class DraftCharacterSelectIcon : MonoBehaviour
{
    [SerializeField] private Image charSprite;
    [SerializeField] private Toggle toggle;

    private CharacterInfo info;

    public CharacterInfo Info => info;
    public Toggle Toggle => toggle;

    public void Initialize(CharacterInfo info)
    {
        this.info = info;
        charSprite.sprite = info.portrait;
        toggle.isOn = false;
    }
}
