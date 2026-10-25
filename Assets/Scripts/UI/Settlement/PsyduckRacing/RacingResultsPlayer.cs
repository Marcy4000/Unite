using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class RacingResultsPlayer : MonoBehaviour
{
    [SerializeField] private TrainerModel trainerModel;
    [SerializeField] private TMPro.TMP_Text trainerNameText;
    [SerializeField] private Transform psyduckPosition;

    [SerializeField] private string[] maleVictoryAnimations;
    [SerializeField] private string[] femaleVictoryAnimations;

    public TrainerModel TrainerModel => trainerModel;

    public void Initialize(RacePlayerResult racePlayerResult, bool spawnPsyduck = true)
    {
        Unite.Multiplayer.GameSessionPlayer player = LobbyController.Instance.GetPlayerByID(racePlayerResult.PlayerID);

        trainerNameText.text = player.Data["PlayerName"].Value;
        
        // Set local player's name to yellow
        if (racePlayerResult.PlayerID == LobbyController.Instance.Player.Id)
        {
            trainerNameText.color = Color.yellow;
        }
        
        trainerModel.InitializeClothes(PlayerClothesInfo.Deserialize(player.Data["ClothingInfo"].Value));

        CharacterInfo currentPokemon = CharactersList.Instance.GetCharacterFromID(NumberEncoder.FromBase64<short>(player.Data["SelectedCharacter"].Value));

        if (!spawnPsyduck || currentPokemon == null)
        {
            return;
        }

        foreach (Transform child in psyduckPosition)
        {
            Addressables.ReleaseInstance(child.gameObject);
        }

        Addressables.InstantiateAsync(currentPokemon.model, psyduckPosition.position, psyduckPosition.rotation, psyduckPosition);
    }

    public void PlayVictoryAnimation()
    {
        StartCoroutine(PlayVictoryAnimationWhenReady());
    }

    private IEnumerator PlayVictoryAnimationWhenReady()
    {
        while (trainerModel != null && trainerModel.IsLoading) yield return null;
        if (trainerModel == null || !trainerModel.IsInitialized || !trainerModel.gameObject.activeInHierarchy) yield break;
        var animations = trainerModel.IsMale ? maleVictoryAnimations : femaleVictoryAnimations;
        if (animations == null || animations.Length == 0) yield break;
        trainerModel.ActiveAnimator.Play(animations[Random.Range(0, animations.Length)]);
    }

    private void OnDestroy()
    {
        foreach (Transform child in psyduckPosition)
        {
            Addressables.ReleaseInstance(child.gameObject);
        }
    }
}
