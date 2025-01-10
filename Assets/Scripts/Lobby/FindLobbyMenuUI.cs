using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FindLobbyMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject lobbyItemPrefab;
    [SerializeField] private Transform lobbyListContent;
    [SerializeField] private GameObject noLobbiesText;

    private Coroutine refreshCoroutine;

    private void OnEnable()
    {
        foreach (Transform child in lobbyListContent)
        {
            Destroy(child.gameObject);
        }

        refreshCoroutine = StartCoroutine(RefreshLobbyList());
    }

    private void OnDisable()
    {
        if (refreshCoroutine != null)
        {
            StopCoroutine(refreshCoroutine);
        }
    }

    private IEnumerator RefreshLobbyList()
    {
        while (true)
        {
            var lobbies = LobbyController.Instance.GetOpenLobbies();

            yield return new WaitUntil(() => lobbies.IsCompleted);

            foreach (Transform child in lobbyListContent)
            {
                Destroy(child.gameObject);
            }

            foreach (var lobby in lobbies.Result.Results)
            {
                var lobbyItem = Instantiate(lobbyItemPrefab, lobbyListContent).GetComponent<LobbyItemUI>();
                lobbyItem.Initialize(lobby);
            }

            noLobbiesText.SetActive(lobbies.Result.Results.Count == 0);

            yield return new WaitForSeconds(5f);
        }
    }
}
