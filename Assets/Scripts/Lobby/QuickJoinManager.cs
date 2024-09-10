using JSAM;
using System.Collections;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Lobbies;
using UnityEngine;
using UnityEngine.UI;

public class QuickJoinManager : MonoBehaviour
{
    [SerializeField] MatchmakingBarUI matchmakingBarUI;
    [SerializeField] private Button startSearchButton;
    [SerializeField] private Button createLobbyButton;

    [SerializeField] private TMP_InputField lobbyCodeInput;

    private float timeElapsed = 0f;
    private bool isSearching = false;

    private Coroutine searchCoroutine;

    private void Start()
    {
        startSearchButton.onClick.AddListener(StartSearch);
        matchmakingBarUI.SetCancelButtonAction(CancelSearch);

        matchmakingBarUI.SetBarVisibility(false);
    }

    private void StartSearch()
    {
        if (searchCoroutine != null)
        {
            return;
        }

        AudioManager.PlaySound(DefaultAudioSounds.Home_ui_start_04);

        timeElapsed = 0f;

        matchmakingBarUI.SetBarVisibility(true);
        matchmakingBarUI.SetEstimatedTime("Estimated time: N/A");

        startSearchButton.interactable = false;
        createLobbyButton.interactable = false;
        lobbyCodeInput.interactable = false;

        isSearching = true;

        searchCoroutine = StartCoroutine(SearchForMatch());
    }

    private void CancelSearch()
    {
        if (searchCoroutine != null)
        {
            StopCoroutine(searchCoroutine);
            searchCoroutine = null;
        }

        AudioManager.PlaySound(DefaultAudioSounds.Home_ui_back_01);

        startSearchButton.interactable = true;
        createLobbyButton.interactable = true;
        lobbyCodeInput.interactable = true;

        isSearching = false;

        matchmakingBarUI.SetBarVisibility(false);
        timeElapsed = 0f;
    }

    private void Update()
    {
        if (isSearching)
        {
            timeElapsed += Time.deltaTime;

            int minutes = Mathf.FloorToInt(timeElapsed / 60);
            int seconds = Mathf.FloorToInt(timeElapsed % 60);

            string formattedTime = string.Format("{0:00}:{1:00}", minutes, seconds);

            matchmakingBarUI.SetElapsedTime(formattedTime);
        }
    }

    private IEnumerator SearchForMatch()
    {
        while (isSearching)
        {
            Task<bool> searchTask = LobbyController.Instance.QuickJoin();

            yield return new WaitUntil(() => searchTask.IsCompleted);

            if (searchTask.Result)
            {
                isSearching = false;
                matchmakingBarUI.SetBarVisibility(false);
                timeElapsed = 0f;
                startSearchButton.interactable = true;
                createLobbyButton.interactable = true;
                lobbyCodeInput.interactable = true;
                searchCoroutine = null;
                yield break;
            }

            yield return new WaitForSeconds(10.1f);
        }
    }
}
