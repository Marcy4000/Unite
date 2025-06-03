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
        Unity.Services.Lobbies.Models.Player player = LobbyController.Instance.Lobby.Players.Find(p => p.Id == racePlayerResult.PlayerID);

        trainerNameText.text = player.Data["PlayerName"].Value;
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
        if (trainerModel.gameObject.activeSelf)
        {
            int animIndex = Random.Range(0, trainerModel.PlayerClothesInfo.IsMale ? maleVictoryAnimations.Length : femaleVictoryAnimations.Length);
            trainerModel.ActiveAnimator.Play(trainerModel.PlayerClothesInfo.IsMale ? maleVictoryAnimations[animIndex] : femaleVictoryAnimations[animIndex]);
        }
    }

    private void OnDestroy()
    {
        foreach (Transform child in psyduckPosition)
        {
            Addressables.ReleaseInstance(child.gameObject);
        }
    }
}
