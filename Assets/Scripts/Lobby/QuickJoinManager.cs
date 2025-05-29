using JSAM;
using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuickJoinManager : MonoBehaviour
{
    [SerializeField] MatchmakingBarUI matchmakingBarUI;
    [SerializeField] private Button startSearchButton;
    [SerializeField] private Button createLobbyButton;
    [SerializeField] private Button lobbyFinderButton;

    [SerializeField] private TMP_InputField lobbyCodeInput;

    private float timeElapsed = 0f;
    private bool isSearching = false;

    private Coroutine searchCoroutine;

    private void Start()
    {
        startSearchButton.onClick.AddListener(StartSearch);
        // Non impostare qui la callback, verrà impostata ogni volta che parte una ricerca
        matchmakingBarUI.SetBarVisibility(false);
    }

    private void StartSearch()
    {
        if (searchCoroutine != null)
        {
            return;
        }

        AudioManager.PlaySound(DefaultAudioSounds.Home_ui_start_04);

        isSearching = true;
        timeElapsed = 0f;

        // Imposta la callback di annullamento ogni volta che parte una ricerca
        matchmakingBarUI.SetCancelButtonAction(CancelSearch);
        matchmakingBarUI.StartSearching();

        startSearchButton.interactable = false;
        createLobbyButton.interactable = false;
        lobbyCodeInput.interactable = false;
        lobbyFinderButton.interactable = false;

        searchCoroutine = StartCoroutine(SearchForMatch());
    }

    private void CancelSearch()
    {
        if (searchCoroutine != null)
        {
            StopCoroutine(searchCoroutine);
            searchCoroutine = null;
        }

        startSearchButton.interactable = true;
        createLobbyButton.interactable = true;
        lobbyCodeInput.interactable = true;
        lobbyFinderButton.interactable = true;

        isSearching = false;
        timeElapsed = 0f;
        matchmakingBarUI.StopSearching();
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
            var searchTask = LobbyController.Instance.QuickJoin();

            yield return new WaitUntil(() => searchTask.IsCompleted);

            if (searchTask.Result)
            {
                isSearching = false;
                matchmakingBarUI.StopSearching();
                startSearchButton.interactable = true;
                createLobbyButton.interactable = true;
                lobbyCodeInput.interactable = true;
                lobbyFinderButton.interactable = true;
                searchCoroutine = null;
                yield break;
            }

            yield return new WaitForSeconds(10.1f);
        }
        // Se la ricerca viene annullata, assicurati che la barra venga nascosta
        matchmakingBarUI.StopSearching();
    }
}
