using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TrainerCardEditorMenu : MonoBehaviour
{
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private Transform content;
    [SerializeField] private ToggleGroup toggleGroup;
    [SerializeField] private bool isBackground;

    [SerializeField] private TrainerCardItem[] items;

    private List<Toggle> toggles = new List<Toggle>();

    public event System.Action<int> OnItemSelected;

    private void Start()
    {
        TrainerCardInfo info = PlayerClothesInfo.Deserialize(LobbyController.Instance.Player.Data["ClothingInfo"].Value).TrainerCardInfo;

        int i = 0;
        foreach (TrainerCardItem item in items)
        {
            GameObject itemObject = Instantiate(itemPrefab, content);
            TrainerCardItemUI itemUI = itemObject.GetComponent<TrainerCardItemUI>();
            itemUI.Initialize(item);
            itemUI.Toggle.group = toggleGroup;
            toggles.Add(itemUI.Toggle);

            itemUI.Toggle.onValueChanged.AddListener((value) =>
            {
                if (value)
                {
                    OnItemSelected?.Invoke(System.Array.IndexOf(items, item));
                }
            });

            if (i == (isBackground ? info.BackgroundIndex : info.FrameIndex))
            {
                itemUI.Toggle.isOn = true;
            }
            i++;
        }
    }
}
