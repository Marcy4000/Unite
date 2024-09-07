using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TextCore.Text;

public class SettlementTeamModels : MonoBehaviour
{
    [SerializeField] private TrainerModel[] trainerModels;
    [SerializeField] private Transform[] trainerPokemonPositions;

    [SerializeField] private string[] maleVictoryAnimations;
    [SerializeField] private string[] femaleVictoryAnimations;

    public void Initialize(Player[] teamPlayers)
    {
        for (int i = 0; i < trainerModels.Length; i++)
        {
            if (i < teamPlayers.Length)
            {
                trainerModels[i].gameObject.SetActive(true);
                PlayerClothesInfo playerClothesInfo = PlayerClothesInfo.Deserialize(teamPlayers[i].Data["ClothingInfo"].Value);
                trainerModels[i].InitializeClothes(playerClothesInfo);

                CharacterInfo currentPokemon = CharactersList.Instance.GetCharacterFromString(teamPlayers[i].Data["SelectedCharacter"].Value);

                foreach (Transform child in trainerPokemonPositions[i].transform)
                {
                    Addressables.ReleaseInstance(child.gameObject);
                }

                Addressables.InstantiateAsync(currentPokemon.model, trainerPokemonPositions[i].position, trainerPokemonPositions[i].rotation);
            }
            else
            {
                trainerModels[i].gameObject.SetActive(false);
            }
        }
    }

    public void PlayVictoryAnimations()
    {
        foreach (var trainerModel in trainerModels)
        {
            if (trainerModel.gameObject.activeSelf)
            {
                int animIndex = Random.Range(0, trainerModel.PlayerClothesInfo.IsMale ? maleVictoryAnimations.Length : femaleVictoryAnimations.Length);
                trainerModel.ActiveAnimator.Play(trainerModel.PlayerClothesInfo.IsMale ? maleVictoryAnimations[animIndex] : femaleVictoryAnimations[animIndex]);
            }
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < trainerPokemonPositions.Length; i++)
        {
            foreach (Transform child in trainerPokemonPositions[i])
            {
                Addressables.ReleaseInstance(child.gameObject);
            }
        }
    }
}
