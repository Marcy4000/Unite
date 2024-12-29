using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MovesLevelButton : MonoBehaviour
{
    [SerializeField] private Image portrait;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Toggle toggle;

    private int movesIndex;

    public event System.Action<int> OnSelected;
    public Toggle Toggle => toggle;

    public void Initialize(Sprite portrait, int level, int movesIndex, ToggleGroup toggleGroup)
    {
        this.portrait.sprite = portrait;
        levelText.text = $"{level+1}";
        this.movesIndex = movesIndex;
        toggle.group = toggleGroup;

        toggle.onValueChanged.AddListener(OnToggleValueChanged);
    }

    private void OnToggleValueChanged(bool value)
    {
        if (value)
        {
            OnSelected?.Invoke(movesIndex);
        }
    }
}
