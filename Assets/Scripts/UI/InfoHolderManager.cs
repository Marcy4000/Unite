using System.Collections;
using UnityEngine;

public class InfoHolderManager : MonoBehaviour
{
    [SerializeField] private GameObject infoPrefab;

    [SerializeField] private Transform infoHolder;
    [SerializeField] private bool enemyTeam;

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

        Team orangeTeam = LobbyController.Instance.GetLocalPlayerTeam();
        foreach (var player in GameManager.Instance.Players)
        {
            if (!enemyTeam)
            {
                if (player.IsLocalPlayer || !player.CurrentTeam.IsOnSameTeam(orangeTeam))
                {
                    continue;
                }
                CreateAllyInfo(player);
            }
            else
            {
                if (player.CurrentTeam.IsOnSameTeam(orangeTeam))
                {
                    continue;
                }
                CreateEnemyInfo(player);
            }
        }
    }

    public void CreateAllyInfo(PlayerManager player)
    {
        GameObject allyInfo = Instantiate(infoPrefab, infoHolder);
        allyInfo.GetComponent<AllyInfoUI>().InitializeUI(player);
    }

    public void CreateEnemyInfo(PlayerManager player)
    {
        GameObject allyInfo = Instantiate(infoPrefab, infoHolder);
        allyInfo.GetComponent<EnemyDeathInfoUI>().InitializeUI(player);
    }
}
