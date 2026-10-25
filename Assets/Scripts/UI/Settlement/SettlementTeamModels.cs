using System.Collections;
using Player = Unite.Multiplayer.GameSessionPlayer;
using UnityEngine;
using UnityEngine.AddressableAssets;

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

                CharacterInfo currentPokemon = CharactersList.Instance.GetCharacterFromID(NumberEncoder.FromBase64<short>(teamPlayers[i].Data["SelectedCharacter"].Value));

                foreach (Transform child in trainerPokemonPositions[i].transform)
                {
                    Addressables.ReleaseInstance(child.gameObject);
                }

                Addressables.InstantiateAsync(currentPokemon.model, trainerPokemonPositions[i].position, trainerPokemonPositions[i].rotation, trainerPokemonPositions[i]);
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
                StartCoroutine(PlayVictoryAnimationWhenReady(trainerModel));
            }
        }
    }

    public void PlayVictoryAnimation(int trainerIndex)
    {
        if (trainerIndex < 0 || trainerIndex >= trainerModels.Length)
            return;

        if (trainerModels[trainerIndex].gameObject.activeSelf)
        {
            StartCoroutine(PlayVictoryAnimationWhenReady(trainerModels[trainerIndex]));
        }
    }

    private IEnumerator PlayVictoryAnimationWhenReady(TrainerModel model)
    {
        while (model != null && model.IsLoading) yield return null;
        if (model == null || !model.IsInitialized || !model.gameObject.activeInHierarchy) yield break;
        var animations = model.IsMale ? maleVictoryAnimations : femaleVictoryAnimations;
        if (animations == null || animations.Length == 0) yield break;
        model.ActiveAnimator.Play(animations[Random.Range(0, animations.Length)]);
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
