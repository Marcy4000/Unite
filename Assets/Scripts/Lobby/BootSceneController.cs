using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BootSceneController : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private TMP_InputField playerNameInputField;

    private void Start()
    {
        startButton.onClick.AddListener(OnStartButtonClicked);
    }

    private void OnStartButtonClicked()
    {
        if (string.IsNullOrWhiteSpace(playerNameInputField.text))
        {
            return;
        }
        LobbyController.instance.StartGame(playerNameInputField.text);
    }
}
