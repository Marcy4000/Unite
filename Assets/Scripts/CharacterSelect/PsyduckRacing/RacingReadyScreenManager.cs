using JSAM;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RacingReadyScreenManager : NetworkBehaviour
{
    [SerializeField] private CharacterInfo initialCharacter;
    [SerializeField] private TrainerModel trainerModel;

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

    private IEnumerator Start()
    {
        AudioManager.StopMusic(DefaultAudioMusic.LobbyTheme);
        AudioManager.PlayMusic(DefaultAudioMusic.ChoosePokemon, true);

        PlayerClothesInfo playerClothesInfo = PlayerClothesInfo.Deserialize(LobbyController.Instance.Player.Data["ClothingInfo"].Value);
        trainerModel.InitializeClothes(playerClothesInfo);

        yield return new WaitForSeconds(1.5f);

        trainerModel.ActiveAnimator.Play(trainerModel.IsMale ? "ani_obpos5_40040_lob_male" : "ani_createend_50040_lob_female");
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

        yield return new WaitForSeconds(2f);

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
