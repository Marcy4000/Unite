using JSAM;
using System.Collections;
using System.Collections.Generic;
using Unite.Multiplayer;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RacingReadyScreenManager : NetworkBehaviour
{
    [SerializeField] private CharacterInfo initialCharacter;
    [SerializeField] private TrainerModel trainerModel;

    [SerializeField, Min(1f)] private float readinessTimeoutSeconds = 120f;
    private readonly ClientReadinessBarrier playerReady = new ClientReadinessBarrier(ClientLoadStage.Ready);
    private NetworkManager subscribedNetworkManager;
    private NetworkSceneManager subscribedSceneManager;
    private bool placeholderStarted;
    private bool transitionRequested;

    public override void OnNetworkSpawn()
    {
        placeholderStarted = false;
        transitionRequested = false;
        subscribedNetworkManager = NetworkManager;
        subscribedSceneManager = NetworkManager.SceneManager;
        subscribedSceneManager.OnLoadEventCompleted += HandleSceneLoaded;
        if (IsServer)
        {
            playerReady.Reset(NetworkManager.ConnectedClientsIds);
            subscribedNetworkManager.OnClientDisconnectCallback += OnClientDisconnect;
            StartCoroutine(WatchReadinessDeadline());
        }
    }

    private void OnDisable()
    {
        DetachNetworkEvents();
    }

    public override void OnNetworkDespawn()
    {
        StopAllCoroutines();
        DetachNetworkEvents();
    }

    private void DetachNetworkEvents()
    {
        if (subscribedSceneManager != null)
        {
            subscribedSceneManager.OnLoadEventCompleted -= HandleSceneLoaded;
            subscribedSceneManager = null;
        }
        if (subscribedNetworkManager != null)
        {
            subscribedNetworkManager.OnClientDisconnectCallback -= OnClientDisconnect;
            subscribedNetworkManager = null;
        }
    }

    private void OnClientDisconnect(ulong clientId) => playerReady.Remove(clientId);

    private IEnumerator WatchReadinessDeadline()
    {
        var deadline = Time.realtimeSinceStartupAsDouble + readinessTimeoutSeconds;
        while (IsSpawned && !transitionRequested)
        {
            if (Time.realtimeSinceStartupAsDouble >= deadline)
            {
                LobbyController.Instance?.ReportSessionFailure(SessionFailureReason.LoadingTimedOut);
                yield break;
            }
            yield return null;
        }
    }

    private void HandleSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName == "RacingReadyScreen")
        {
            if (IsServer && clientsTimedOut.Count > 0)
            {
                LobbyController.Instance?.ReportSessionFailure(SessionFailureReason.LoadingTimedOut);
                return;
            }
            if (placeholderStarted) return;
            placeholderStarted = true;
            StartCoroutine(PlaceholderStart());
            LoadingScreen.Instance.HideGameBeginScreen();
        }
    }

    private IEnumerator Start()
    {
        AudioManager.StopMusic(DefaultAudioMusic.LobbyTheme);
        AudioManager.PlayMusic(DefaultAudioMusic.ChoosePokemon, true);

        PlayerClothesInfo playerClothesInfo = PlayerClothesInfo.Deserialize(LobbyController.Instance.Player.Data["ClothingInfo"].Value);
        trainerModel.InitializeClothes(playerClothesInfo);

        yield return new WaitForSeconds(1.5f);

        while (trainerModel != null && trainerModel.IsLoading) yield return null;
        if (trainerModel == null || !trainerModel.IsInitialized) yield break;

        trainerModel.ActiveAnimator.Play(trainerModel.IsMale ? "ani_40645solo_lob_male" : "ani_50645solo_lob_female");
    }

    private IEnumerator PlaceholderStart()
    {
        LobbyController.Instance.UpdatePlayerItemsPokemonAndBattleItem(
            HeldItemDatabase.SerializeHeldItems(new byte[] { 0, 0, 0 }),
            CharactersList.Instance.GetCharacterID(initialCharacter),
            "0"
        );

        yield return new WaitForSeconds(2.5f);

        NotifyPlayerReadyRpc(NetworkManager.Singleton.LocalClientId);

        if (IsServer)
        {
            yield return StartCoroutine(WaitForAllPlayersReady());
        }
        else
        {
            yield break;
        }

        yield return new WaitForSeconds(1.5f);

        if (!IsSpawned || !IsServer || !playerReady.IsReady) yield break;
        transitionRequested = true;
        ShowLoadingScreenRpc();
        LobbyController.Instance.LoadGameMap();
    }

    private IEnumerator WaitForAllPlayersReady()
    {
        while (IsSpawned && !playerReady.IsReady)
        {
            yield return null;
        }
    }

    [Rpc(SendTo.Server)]
    private void NotifyPlayerReadyRpc(ulong clientId, RpcParams rpcParams = default)
    {
        if (!IsServer) return;
        playerReady.MarkReady(rpcParams.Receive.SenderClientId, clientId, ClientLoadStage.Ready);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowLoadingScreenRpc()
    {
        AudioManager.StopMusic(DefaultAudioMusic.ChoosePokemon);
        AudioManager.PlayMusic(DefaultAudioMusic.LoadingTheme, true);
        AudioManager.PlaySound(DefaultAudioSounds.Play_Load17051302578487348);
        LoadingScreen.Instance.ShowMatchLoadingScreen();
    }
}
