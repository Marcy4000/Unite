using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Services.Lobbies.Models;
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
        if (state == GameState.Playing)
        {
            StartCoroutine(InitializeUI());
        }
    }

    private IEnumerator InitializeUI()
    {
        yield return new WaitForSeconds(1f);

        bool orangeTeam = LobbyController.Instance.Player.Data["PlayerTeam"].Value == "Orange";
        foreach (var player in GameManager.Instance.Players)
        {
            if (player.IsLocalPlayer || player.OrangeTeam != orangeTeam)
            {
                continue;
            }
            CreateAllyInfo(player.Pokemon);
        }
    }

    public void CreateAllyInfo(Pokemon pokemon)
    {
        GameObject allyInfo = Instantiate(allyInfoPrefab, allyInfoHolder);
        allyInfo.GetComponent<AllyInfoUI>().InitializeUI(pokemon);
    }
}
