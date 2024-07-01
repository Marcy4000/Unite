using System.Collections;
using System.Collections.Generic;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen Instance { get; private set; }

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform blueTeamSpawn, orangeTeamSpawn;
    [SerializeField] private GameObject holder;
    [SerializeField] private GameObject loadingScreen;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        HideGenericLoadingScreen();
        HideMatchLoadingScreen();
    }

    public void ShowMatchLoadingScreen()
    {
        InitializeLoadingScreen();
        holder.SetActive(true);
    }

    public void HideMatchLoadingScreen()
    {
        holder.SetActive(false);
    }

    public void ShowGenericLoadingScreen()
    {
        loadingScreen.SetActive(true);
    }

    public void HideGenericLoadingScreen()
    {
        loadingScreen.SetActive(false);
    }

    private void InitializeLoadingScreen()
    {
        foreach (Transform child in blueTeamSpawn)
        {
            Destroy(child.gameObject);
        }

        foreach (Transform child in orangeTeamSpawn)
        {
            Destroy(child.gameObject);
        }

        Player[] orangeTeamPlayers = LobbyController.Instance.GetTeamPlayers(true);
        Player[] blueTeamPlayers = LobbyController.Instance.GetTeamPlayers(false);

        foreach (var player in orangeTeamPlayers)
        {
            GameObject playerObj = Instantiate(playerPrefab, orangeTeamSpawn);
            playerObj.GetComponent<LoadingScreenPlayer>().SetPlayerData(player);
        }

        foreach (var player in blueTeamPlayers)
        {
            GameObject playerObj = Instantiate(playerPrefab, blueTeamSpawn);
            playerObj.GetComponent<LoadingScreenPlayer>().SetPlayerData(player);
        }
    }
}
