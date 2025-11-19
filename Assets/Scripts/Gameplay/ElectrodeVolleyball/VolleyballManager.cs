using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using JSAM;

public enum VolleyballState
{
    WaitingToStart,
    RoundStarting,
    Playing,
    BallLanding,
    RoundEnding,
    GameOver
}

public class VolleyballManager : NetworkBehaviour
{
    public static VolleyballManager Instance { get; private set; }

    public static readonly int MAX_POINTS = 3;

    [SerializeField] private GameObject ball;
    [SerializeField] private Transform[] blueLandSpots, orangeLandSpots;
    [SerializeField] private VolleyballLandSpot blueLandSpot, orangeLandSpot;
    [SerializeField] private Transform blueSpawnPoints, orangeSpawnPoints;
    [SerializeField] private VolleyballRoundScreen roundScreen;

    private NetworkVariable<VolleyballState> currentState = new NetworkVariable<VolleyballState>();
    private NetworkVariable<float> stateTimer = new NetworkVariable<float>();

    public Transform[] BlueLandSpots => blueLandSpots;
    public Transform[] OrangeLandSpots => orangeLandSpots;
    public VolleyballLandSpot BlueLandSpot => blueLandSpot;
    public VolleyballLandSpot OrangeLandSpot => orangeLandSpot;
    public VolleyballState CurrentState => currentState.Value;

    private bool finalStretchTriggered = false;
    private bool hasPlayedFirstRoundTransition = false;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        GameManager.Instance.onGameStateChanged += OnGameStateChanged;
        if (IsServer)
        {
            currentState.Value = VolleyballState.WaitingToStart;
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        if (stateTimer.Value > 0)
        {
            stateTimer.Value -= Time.deltaTime;
            if (stateTimer.Value <= 0)
            {
                OnStateTimerComplete();
            }
        }
    }

    private void TransitionTo(VolleyballState newState, float timer = 0)
    {
        if (!IsServer) return;
        currentState.Value = newState;
        stateTimer.Value = timer;

        switch (newState)
        {
            case VolleyballState.RoundStarting:
                if (!finalStretchTriggered)
                {
                    int bluePointsToWin = MAX_POINTS - GameManager.Instance.BlueTeamScore;
                    int orangePointsToWin = MAX_POINTS - GameManager.Instance.OrangeTeamScore;
                    if (bluePointsToWin == 1 || orangePointsToWin == 1)
                    {
                        finalStretchTriggered = true;
                        EnterFinalStretchRPC();
                    }
                }
                if (hasPlayedFirstRoundTransition)
                {
                    PlayRoundTransitionAnimationRPC(finalStretchTriggered);
                }
                else
                {
                    hasPlayedFirstRoundTransition = true;
                }
                RespawnPlayersRPC();
                ball.GetComponent<VolleyballElectrode>().ResetBall();
                SetPlayersCanMoveRPC(false);
                break;
            case VolleyballState.Playing:
                ball.GetComponent<VolleyballElectrode>().EnableInteractions();
                SetPlayersCanMoveRPC(true);
                break;
            case VolleyballState.RoundEnding:
                HidePlayersRPC();
                break;
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void EnterFinalStretchRPC()
    {
        AudioManager.StopAllMusic();
        AudioManager.PlayMusic(GameManager.Instance.CurrentMap.finalStretchMusic, true);
        AudioManager.PlaySound(DefaultAudioSounds.Final_Stretch_Begin);
        AudioManager.PlaySound(DefaultAudioSounds.AnnouncerFinalStretch);

        if (!IsServer) return;

        foreach (var player in GameManager.Instance.Players)
        {
            if (player == null) continue;

            player.Pokemon.AddStatChange(new StatChange(15, Stat.Speed, 0, false, false, true, 0, false));
        }
    }

    private void OnStateTimerComplete()
    {
        switch (currentState.Value)
        {
            case VolleyballState.RoundStarting:
                TransitionTo(VolleyballState.Playing);
                break;
            case VolleyballState.BallLanding:
                TransitionTo(VolleyballState.RoundStarting, 3f);
                break;
            case VolleyballState.RoundEnding:
                if (GameManager.Instance.BlueTeamScore >= MAX_POINTS || GameManager.Instance.OrangeTeamScore >= MAX_POINTS)
                {
                    TransitionTo(VolleyballState.GameOver);
                }
                else
                {
                    TransitionTo(VolleyballState.RoundStarting, 3f);
                }
                break;
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void HidePlayersRPC()
    {
        foreach (var player in GameManager.Instance.Players)
        {
            if (player == null || !player.IsLocalPlayer) continue;

            CameraController.Instance.ForcePan(true);
            player.PlayerMovement.CanMove = false;
            player.UpdatePosAndRotRPC(new Vector3(0, -50, -50), Quaternion.identity);

            break;
        }
    }

    private Transform GetSpawnPoint(PlayerManager player)
    {
        short pos = NumberEncoder.FromBase64<short>(player.LobbyPlayer.Data["PlayerPos"].Value);
        Transform spawnpoint = SpawnpointManager.Instance.GetSpawnpoint(player.CurrentTeam.Team, pos);
        return spawnpoint;
    }

    [Rpc(SendTo.ClientsAndHost)]

    private void RespawnPlayersRPC()
    {
        foreach (var player in GameManager.Instance.Players)
        {
            if (player == null || !player.IsLocalPlayer) continue;

            player.Respawn();
            Transform spawnpoint = GetSpawnPoint(player);
            player.UpdatePosAndRotRPC(spawnpoint.position, spawnpoint.rotation);

            break;
        }
    }

    private void OnGameStateChanged(GameState state)
    {
        if (!IsServer) return;

        switch (state)
        {
            case GameState.Starting:
                foreach (var player in GameManager.Instance.Players)
                {
                    player.Pokemon.GainExperienceRPC(100000);
                }
                break;
            case GameState.Playing:
                foreach (var player in GameManager.Instance.Players)
                {
                    UpdateLocalPlayerRPC(player.NetworkObjectId, RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp));
                }
                UpdateSpawnpointsPositionsRPC();
                TransitionTo(VolleyballState.RoundStarting, 3f);
                break;
            case GameState.Ended:
                break;
            default:
                break;
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void UpdateSpawnpointsPositionsRPC()
    {
        StartCoroutine(UpdateSpawnpointsPositionsDelayed());
    }

    private IEnumerator UpdateSpawnpointsPositionsDelayed()
    {
        yield return new WaitForSeconds(4f);

        blueSpawnPoints.position = new Vector3(-17.38f, 0.2f, 0);
        orangeSpawnPoints.position = new Vector3(17.38f, 0.2f, 0);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void UpdateLocalPlayerRPC(ulong playerID, RpcParams rpcParams = default)
    {
        PlayerManager player = NetworkManager.Singleton.SpawnManager.SpawnedObjects[playerID].GetComponent<PlayerManager>();

        StartCoroutine(UpdateLocalPlayerDelayed(player));
    }

    private IEnumerator UpdateLocalPlayerDelayed(PlayerManager player)
    {
        yield return null;

        player.MovesController.BattleItemStatus.AddStatus(ActionStatusType.Disabled);
        player.ScoreStatus.AddStatus(ActionStatusType.Busy);
    }

    public void ScorePoint(ulong scorerID)
    {
        if (!IsServer) return;

        GameManager.Instance.GoalScoredRpc(new ScoreInfo(
            1,
            scorerID
        ));
        TransitionTo(VolleyballState.RoundEnding, 2f);

        if (GameManager.Instance.BlueTeamScore >= MAX_POINTS || GameManager.Instance.OrangeTeamScore >= MAX_POINTS)
        {
            EndGame();
        }
    }

    private void EndGame()
    {
        if (!IsServer) return;

        GameManager.Instance.EndGame();
    }

    public int GetScore(Team team)
    {
        return team == Team.Blue ? GameManager.Instance.BlueTeamScore : GameManager.Instance.OrangeTeamScore;
    }

    public void UpdateLandSpotScale(Team team, float remainingTime, float totalTime)
    {
        if (!IsServer) return;

        if (team == Team.Blue)
        {
            blueLandSpot.UpdateScale(remainingTime, totalTime);
        }
        else
        {
            orangeLandSpot.UpdateScale(remainingTime, totalTime);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayRoundTransitionAnimationRPC(bool isFinalStretch)
    {
        roundScreen.StartAnimation(isFinalStretch);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SetPlayersCanMoveRPC(bool canMove)
    {
        foreach (var player in GameManager.Instance.Players)
        {
            if (player == null || !player.IsLocalPlayer) continue;
            player.PlayerMovement.CanMove = canMove;
            break;
        }
    }
}
