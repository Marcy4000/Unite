using System;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectIcon : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image charSprite;

    private CharacterInfo info;

    public event Action<CharacterInfo> OnCharacterSelected;

    public void Initialize(CharacterInfo info)
    {
        this.info = info;
        charSprite.sprite = info.portrait;
        button.onClick.AddListener(() =>
        {
            OnCharacterSelected?.Invoke(this.info);
        });
    }

    private void OnDisable()
    {
        OnCharacterSelected = null;
    }
}
