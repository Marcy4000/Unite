using JSAM;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RacingResultsManager : MonoBehaviour
{
    [SerializeField] private RacingResultsPlayer[] racingResultsPlayers;
    [SerializeField] private TMP_Text placingText;

    [SerializeField] private Button returnLobbyButton;
    [SerializeField] private Button returnMainMenu;

    [SerializeField] private Animator cameraAnimator;

    private IEnumerator Start()
    {
        returnMainMenu.onClick.AddListener(() =>
        {
            AudioManager.StopMusic(DefaultAudioMusic.GameEnd);
            LobbyController.Instance.ReturnToLobby(true);
        });

        returnLobbyButton.onClick.AddListener(() =>
        {
            AudioManager.StopMusic(DefaultAudioMusic.GameEnd);
            LobbyController.Instance.ReturnToHomeWithoutLeavingLobby();
        });

        RaceGameResults raceGameResults = LobbyController.Instance.RaceGameResults;

        for (int i = 0; i < 6; i++)
        {
            if (i < raceGameResults.PlayerResults.Length)
            {
                racingResultsPlayers[i].gameObject.SetActive(true);
                racingResultsPlayers[i].Initialize(raceGameResults.PlayerResults[i]);
            }
            else
            {
                racingResultsPlayers[i].gameObject.SetActive(false);
            }
        }

        RacePlayerResult localPlayerResult = raceGameResults.PlayerResults.Where(p => p.PlayerID == LobbyController.Instance.Player.Id).First();
        placingText.text = $"{localPlayerResult.Position}{GetOrdinalSuffix(localPlayerResult.Position)} Place";

        yield return new WaitForSeconds(1.5f);

        LoadingScreen.Instance.HideGenericLoadingScreen();

        StartCoroutine(DoResultAnimation());
    }

    private string GetOrdinalSuffix(int number)
    {
        if (number <= 0)
        {
            return string.Empty;
        }
        switch (number % 100)
        {
            case 11:
            case 12:
            case 13:
                return "th";
        }
        switch (number % 10)
        {
            case 1:
                return "st";
            case 2:
                return "nd";
            case 3:
                return "rd";
            default:
                return "th";
        }
    }

    private IEnumerator DoResultAnimation()
    {
        cameraAnimator.Play("VictoryCamera");

        yield return new WaitForSeconds(0.5f);
        racingResultsPlayers[0].PlayVictoryAnimation();

        yield return new WaitForSeconds(1.22f);
        racingResultsPlayers[1].PlayVictoryAnimation();

        yield return new WaitForSeconds(1.5f);
        racingResultsPlayers[2].PlayVictoryAnimation();

        yield return new WaitForSeconds(0.4f);

        RacePlayerResult localPlayerResult = LobbyController.Instance.RaceGameResults.PlayerResults.Where(p => p.PlayerID == LobbyController.Instance.Player.Id).First();
        bool gameWon = localPlayerResult.Position <= 3;
        
        // Process ranked system if this was a matchmaking game
        ProcessRankedResults(gameWon);
        
        StartCoroutine(PlayResultSound(gameWon));
    }
    
    private void ProcessRankedResults(bool won)
    {
        if (RankedManager.Instance == null)
        {
            Debug.LogWarning("RankedManager not found, skipping rank processing");
            return;
        }
        
        // Check if this was a matchmaking game by looking for matchmaking lobby indicator
        bool wasMatchmakingGame = false;
        if (LobbyController.Instance.Lobby != null && 
            LobbyController.Instance.Lobby.Data != null && 
            LobbyController.Instance.Lobby.Data.ContainsKey("IsMatchmakingLobby"))
        {
            wasMatchmakingGame = LobbyController.Instance.Lobby.Data["IsMatchmakingLobby"].Value == "true";
        }
        
        // Also check the LobbyController's internal matchmaking flag
        if (!wasMatchmakingGame && LobbyController.Instance.CurrentLobbyType == LobbyController.LobbyType.Standards)
        {
            wasMatchmakingGame = true;
        }
        
        Debug.Log($"Processing racing game result: Won={won}, WasMatchmaking={wasMatchmakingGame}");
        RankedManager.Instance.ProcessMatchResult(won, wasMatchmakingGame);
    }

    private IEnumerator PlayResultSound(bool gameWon)
    {
        if (gameWon)
        {
            AudioManager.PlaySound(DefaultAudioSounds.JingleVictory);
            AudioManager.PlaySound(DefaultAudioSounds.AnnouncerYouWin);
            AudioManager.PlaySound(DefaultAudioSounds.Play_JieSuan_ShengLi);
        }
        else
        {
            AudioManager.PlaySound(DefaultAudioSounds.JingleLose);
            AudioManager.PlaySound(DefaultAudioSounds.AnnouncerYouLose);
            AudioManager.PlaySound(DefaultAudioSounds.Play_JieSuan_ShiBai);
        }

        DefaultAudioSounds jingle = gameWon ? DefaultAudioSounds.JingleVictory : DefaultAudioSounds.JingleLose;

        AudioManager.TryGetPlayingSound(jingle, out SoundChannelHelper helper);
        while (helper.AudioSource.isPlaying)
        {
            yield return null;
        }

        AudioManager.PlayMusic(DefaultAudioMusic.GameEnd, true);
    }
}
