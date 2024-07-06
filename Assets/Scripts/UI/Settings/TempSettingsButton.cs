using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TempSettingsButton : MonoBehaviour
{
    [SerializeField] private Button settingsButton;

    private void Start()
    {
        settingsButton.onClick.AddListener(SettingsManager.Instance.OpenSettings);
    }
}
