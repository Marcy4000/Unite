using System.Collections;
using System.Collections.Generic;
using TMPro; // Add this using statement
using UnityEngine;
using UnityEngine.UI;

public class PhotoMakerSaveSlotUI : MonoBehaviour
{
    [SerializeField] private Button loadButton;
    [SerializeField] private Button deleteButton;

    [SerializeField] private TextMeshProUGUI saveSlotNameText; // Changed from Text to TextMeshProUGUI
    [SerializeField] private Image saveSlotImage;

    private int saveSlotIndex;

    public int SaveSlotIndex
    {
        get { return saveSlotIndex; }
    }
    public event System.Action<int> OnLoadButtonClick;
    public event System.Action<int> OnDeleteButtonClick;

    public void Initialize(string saveSlotName, Sprite saveSlotImageSprite, int saveSlotIndex)
    {
        saveSlotNameText.text = saveSlotName;
        saveSlotImage.sprite = saveSlotImageSprite;

        if (saveSlotImage.sprite == null)
        {
            saveSlotImage.color = Color.gray;
        }
        else
        {
            saveSlotImage.color = Color.white;
        }

        this.saveSlotIndex = saveSlotIndex;

        loadButton.onClick.RemoveAllListeners();
        deleteButton.onClick.RemoveAllListeners();

        loadButton.onClick.AddListener(() => OnLoadButtonClick?.Invoke(saveSlotIndex));
        deleteButton.onClick.AddListener(() => OnDeleteButtonClick?.Invoke(saveSlotIndex));
    }
}
