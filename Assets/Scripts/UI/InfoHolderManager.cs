using System.Collections;
using UnityEngine;

public class InfoHolderManager : MonoBehaviour
{
    [SerializeField] private GameObject allyInfoPrefab;

    [SerializeField] private Transform allyInfoHolder;

    private void Start()
    {
        GameManager.Instance.onGameStateChanged += HandleGameStateChanged;
    }

    private void HandleGameStateChanged(GameState state)
    {
        if (state == GameState.Initialising)
        {
            StartCoroutine(InitializeUI());
        }
    }

    private IEnumerator InitializeUI()
    {
        yield return new WaitForSeconds(1f);

        bool orangeTeam = LobbyController.Instance.GetLocalPlayerTeam();
        foreach (var player in GameManager.Instance.Players)
        {
            if (player.IsLocalPlayer || player.OrangeTeam != orangeTeam)
            {
                continue;
            }
            CreateAllyInfo(player);
        }
    }

    public void CreateAllyInfo(PlayerManager player)
    {
        GameObject allyInfo = Instantiate(allyInfoPrefab, allyInfoHolder);
        allyInfo.GetComponent<AllyInfoUI>().InitializeUI(player);
    }
}
