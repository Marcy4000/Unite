using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class GameStatsUI : MonoBehaviour
{
    [SerializeField] private Image pingImage;
    [SerializeField] private TMP_Text fpsText, pingText;

    [SerializeField] private Sprite[] pingSprites;

    [SerializeField] private bool updateFPS = true;
    [SerializeField] private bool updatePing = true;

    private float fpsUpdateTimer;
    private float pingUpdateTimer;

    private void Start()
    {
        pingUpdateTimer = Random.Range(0f, 3f);
    }

    private void Update()
    {
        if (updatePing)
            UpdatePing();

        if (updateFPS)
            UpdateFPS();
    }

    private void UpdatePing()
    {
        pingUpdateTimer -= Time.deltaTime;

        if (pingUpdateTimer <= 0)
        {
            try
            {
                var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport;
                ulong ping = transport.GetCurrentRtt(NetworkManager.ServerClientId);
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

                if (pingText != null)
                {
                    pingText.text = $"{ping}ms";
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error getting RTT: {ex.Message}");
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
