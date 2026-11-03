using UnityEngine;
using UnityEngine.UI;

public sealed class OpenWorldHomeButton : MonoBehaviour
{
    [SerializeField] private Button button;

    public void EnterCity() => LobbyController.Instance?.EnterOpenWorldLobby();

    private void Update()
    {
        if (button != null)
            button.gameObject.SetActive(LobbyController.Instance != null &&
                LobbyController.Instance.Lobby == null);
    }
}
