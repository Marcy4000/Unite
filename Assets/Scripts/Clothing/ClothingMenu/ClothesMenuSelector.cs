using UnityEngine;
using UnityEngine.UI;

public class ClothesMenuSelector : MonoBehaviour
{
    [SerializeField] private Toggle[] menuToggles;

    public event System.Action<ClothingType> OnSelectedMenuChanged;

    private void Start()
    {
        foreach (var toggle in menuToggles)
        {
            toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }
    }

    private void OnToggleValueChanged(bool isOn)
    {
        if (isOn)
        {
            for (int i = 0; i < menuToggles.Length; i++)
            {
                if (menuToggles[i].isOn)
                {
                    OnSelectedMenuChanged?.Invoke((ClothingType)i);
                }
            }
        }
    }
}
