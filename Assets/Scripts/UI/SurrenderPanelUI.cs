using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SurrenderPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject votePrefab;
    [SerializeField] private Transform voteContainer;
    [SerializeField] private GameObject buttonsHolder;
    [SerializeField] private Image timeBarImage;

    [SerializeField] private Sprite yesImage, noImage;

    private Dictionary<string, GameObject> votesDictionary = new Dictionary<string, GameObject>();
    private Dictionary<GameObject, Image> votesImages = new Dictionary<GameObject, Image>();

    public Image TimeBarImage => timeBarImage;
    public GameObject ButtonsHolder => buttonsHolder;

    public void InitializeUI(string[] teamPlayers)
    {
        votesDictionary.Clear();
        votesImages.Clear();

        buttonsHolder.SetActive(true);

        foreach (Transform child in voteContainer)
        {
            Destroy(child.gameObject);
        }

        foreach (var player in teamPlayers)
        {
            var vote = Instantiate(votePrefab, voteContainer);
            votesDictionary.Add(player, vote);
            votesImages.Add(vote, vote.GetComponentsInChildren<Image>()[1]);
            UpdateVoteIndicator(player, 0);
        }
    }

    public void UpdateVoteIndicator(string playerId, byte vote)
    {
        if (votesDictionary.TryGetValue(playerId, out var voteIndicator))
        {
            Image voteImage = votesImages[voteIndicator];

            switch (vote)
            {
                case 0:
                    voteImage.gameObject.SetActive(false);
                    break;
                case 1:
                    voteImage.gameObject.SetActive(true);
                    voteImage.sprite = yesImage;
                    break;
                case 2:
                    voteImage.gameObject.SetActive(true);
                    voteImage.sprite = noImage;
                    break;
                default:
                    break;
            }
        }
    }
}
