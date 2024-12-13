using JSAM;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RacingReadyScreenManager : NetworkBehaviour
{
    [SerializeField] private CharacterInfo initialCharacter;

    private void OnEnable()
    {
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName == "RacingReadyScreen")
        {
            StartCoroutine(PlaceholderStart());
            LoadingScreen.Instance.HideGameBeginScreen();
        }
    }

    private IEnumerator PlaceholderStart()
    {
        LobbyController.Instance.ChangePlayerCharacter(CharactersList.Instance.GetCharacterID(initialCharacter));
        yield return new WaitForSeconds(1f);
        LobbyController.Instance.ChangePlayerBattleItem("0");
        yield return new WaitForSeconds(1f);
        LobbyController.Instance.UpdatePlayerHeldItems(HeldItemDatabase.SerializeHeldItems(new byte[] { 0, 0, 0 }));
        yield return new WaitForSeconds(1f);

        if (!IsServer)
        {
            yield break;
        }

        ShowLoadingScreenRpc();
        LobbyController.Instance.LoadGameMap();
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
