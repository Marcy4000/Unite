using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class GameStatsUI : MonoBehaviour
{
    [SerializeField] private Image pingImage;
    [SerializeField] private TMP_Text fpsText;

    [SerializeField] private Sprite[] pingSprites;

    private float fpsUpdateTimer;
    private float pingUpdateTimer;

    private void Start()
    {
        pingUpdateTimer = Random.Range(0f, 3f);
    }

    private void Update()
    {
        UpdatePing();
        UpdateFPS();
    }

    private void UpdatePing()
    {
        pingUpdateTimer -= Time.deltaTime;

        if (pingUpdateTimer <= 0)
        {
            ulong ping = NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.Singleton.NetworkConfig.NetworkTransport.ServerClientId);
            if (ping < 110)
            {
                pingImage.sprite = pingSprites[0];
            }
            else if (ping < 210)
            {
                pingImage.sprite = pingSprites[1];
            }
            else
            {
                pingImage.sprite = pingSprites[2];
            }

            pingUpdateTimer = 3f;
        }
    }

    private void UpdateFPS()
    {
        fpsUpdateTimer -= Time.deltaTime;

        if (fpsUpdateTimer > 0) return;

        fpsText.text = $"FPS: {Mathf.FloorToInt(1f / Time.unscaledDeltaTime)}";
        fpsUpdateTimer = 2f;
    }
}
