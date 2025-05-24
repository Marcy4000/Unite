using System.Collections;
using UnityEngine;
using Unity.Netcode;
using DG.Tweening;

public class VolleyballElectrode : NetworkBehaviour
{
    [SerializeField] private float initialFlightTime = 6f;
    [SerializeField] private float timeDecrement = 0.3f;
    [SerializeField] private float minFlightTime = 3f;
    [SerializeField] private AnimationManager animationManager;

    private NetworkVariable<float> currentFlightTime = new NetworkVariable<float>();
    private NetworkVariable<float> remainingFlightTime = new NetworkVariable<float>();
    private NetworkVariable<bool> isMoving = new NetworkVariable<bool>();
    private NetworkVariable<ulong> lastHitPlayerId = new NetworkVariable<ulong>();
    private NetworkVariable<TeamMember> lastHitTeam = new NetworkVariable<TeamMember>(new TeamMember(Team.Neutral));
    private NetworkVariable<bool> isMovementComplete = new NetworkVariable<bool>(true);
    private NetworkVariable<bool> interactionsEnabled = new NetworkVariable<bool>();

    private Tween currentMoveTween;
    private Vector3 startPosition;
    private VolleyballLandSpot currentLandingZone;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        animationManager.AssignAnimator(GetComponentInChildren<Animator>());
        if (IsServer)
        {
            startPosition = transform.position;
            currentFlightTime.Value = initialFlightTime;
        }
    }

    public void OnPlayerHit(PlayerManager player)
    {
        if (!IsServer || !interactionsEnabled.Value) return;

        lastHitTeam.Value = player.CurrentTeam;
        lastHitPlayerId.Value = player.NetworkObjectId;
        // Invertiamo la direzione: Blue team ora colpisce verso Orange (1) e viceversa
        float direction = player.CurrentTeam.Team == Team.Blue ? 1f : -1f;

        // Cancella il movimento precedente se esiste
        if (currentMoveTween != null)
        {
            currentMoveTween.Kill();
        }
        isMoving.Value = true;

        // Seleziona il punto di atterraggio opposto
        Transform[] landSpots = direction < 0 ?
            VolleyballManager.Instance.BlueLandSpots :
            VolleyballManager.Instance.OrangeLandSpots;

        int randomSpot = Random.Range(0, landSpots.Length);
        Vector3 targetPosition = landSpots[randomSpot].position;

        // Mostra l'indicatore nel punto di atterraggio
        if (direction < 0)
        {
            VolleyballManager.Instance.BlueLandSpot.ShowCircleIndicator(true);
            VolleyballManager.Instance.BlueLandSpot.UpdatePosition(targetPosition);
            VolleyballManager.Instance.OrangeLandSpot.ShowCircleIndicator(false);
        }
        else
        {
            VolleyballManager.Instance.OrangeLandSpot.ShowCircleIndicator(true);
            VolleyballManager.Instance.OrangeLandSpot.UpdatePosition(targetPosition);
            VolleyballManager.Instance.BlueLandSpot.ShowCircleIndicator(false);
        }

        animationManager.SetBool("Walking", true);

        // Muovi la palla verso il punto di atterraggio
        MoveToPositionServerRpc(targetPosition);
    }

    [ServerRpc]
    private void MoveToPositionServerRpc(Vector3 targetPosition)
    {
        MoveToPositionClientRpc(targetPosition);
    }

    [ClientRpc]
    private void MoveToPositionClientRpc(Vector3 targetPosition)
    {
        if (currentMoveTween != null)
        {
            currentMoveTween.Kill();
        }

        if (IsServer)
        {
            currentFlightTime.Value = Mathf.Max(currentFlightTime.Value - timeDecrement, minFlightTime);
            remainingFlightTime.Value = currentFlightTime.Value;
            isMovementComplete.Value = false;
        }

        Vector3 midPoint = (transform.position + targetPosition) / 2f + Vector3.up * 3f;

        currentMoveTween = transform.DOPath(
            new Vector3[] { transform.position, midPoint, targetPosition },
            currentFlightTime.Value,
            PathType.CatmullRom
        ).SetEase(Ease.Linear)
        .OnUpdate(() =>
        {
            if (IsServer)
            {
                remainingFlightTime.Value = currentFlightTime.Value * (1 - currentMoveTween.ElapsedPercentage());
                Team flyingTowards = lastHitTeam.Value.Team == Team.Blue ? Team.Orange : Team.Blue;
                VolleyballManager.Instance.UpdateLandSpotScale(flyingTowards, remainingFlightTime.Value, currentFlightTime.Value);
            }
        })
        .OnComplete(() =>
        {
            if (IsServer)
            {
                isMovementComplete.Value = true;
                remainingFlightTime.Value = 0;
                if (currentLandingZone != null)
                {
                    HandleLandingZone(currentLandingZone);
                }
            }
        });
    }

    private void HandleLandingZone(VolleyballLandSpot landSpot)
    {
        if (!IsServer) return;

        // Check if there are valid players of the correct team in the landing zone
        bool validLanding = landSpot.HasPlayersInside && landSpot.AssignedTeam != lastHitTeam.Value.Team;

        if (!validLanding)
        {
            // Point for the opposite team
            VolleyballManager.Instance.ScorePoint(lastHitPlayerId.Value);
            ResetBall();
        }
        else
        {
            // The ball lands in a zone with players of the same team
            OnBallLanded(landSpot);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        PlayerManager player = other.GetComponent<PlayerManager>();
        if (player != null && !isMoving.Value)
        {
            OnPlayerHit(player);
            return;
        }

        if (other.CompareTag("LandingZone"))
        {
            currentLandingZone = other.GetComponent<VolleyballLandSpot>();
        }
    }

    private void OnBallLanded(VolleyballLandSpot landSpot)
    {
        if (!IsServer) return;

        // Se non ci sono giocatori nella zona di atterraggio
        if (!landSpot.HasPlayersInside)
        {
            // Determina quale squadra ottiene il punto (la squadra opposta all'ultima che ha colpito)
            VolleyballManager.Instance.ScorePoint(lastHitPlayerId.Value);
            ResetBall();
            return;
        }

        // Heal all ally players on the land spot
        var validPlayers = landSpot.GetValidPlayers();
        foreach (var player in validPlayers)
        {
            if (player.Pokemon.IsHPFull())
            {
                continue; // Skip if the player's HP is already full
            }
            player.Pokemon.HealDamageRPC(Mathf.RoundToInt(player.Pokemon.GetMaxHp() / 2.5f));
        }

        // Il resto del codice per quando ci sono giocatori rimane invariato
        PlayerManager firstPlayer = landSpot.GetFirstPlayerInside();
        if (firstPlayer != null)
        {
            lastHitTeam.Value = firstPlayer.CurrentTeam;
            lastHitPlayerId.Value = firstPlayer.NetworkObjectId;
        }

        // Trova un punto di atterraggio casuale nel lato opposto
        Transform[] targetSpots = landSpot.AssignedTeam == Team.Blue ?
            VolleyballManager.Instance.OrangeLandSpots :
            VolleyballManager.Instance.BlueLandSpots;

        int randomSpot = Random.Range(0, targetSpots.Length);
        Vector3 nextTarget = targetSpots[randomSpot].position;

        // Aggiorna gli indicatori
        if (landSpot.AssignedTeam == Team.Blue)
        {
            VolleyballManager.Instance.BlueLandSpot.ShowCircleIndicator(false);
            VolleyballManager.Instance.OrangeLandSpot.ShowCircleIndicator(true);
            VolleyballManager.Instance.OrangeLandSpot.UpdatePosition(nextTarget);
        }
        else
        {
            VolleyballManager.Instance.BlueLandSpot.ShowCircleIndicator(true);
            VolleyballManager.Instance.BlueLandSpot.UpdatePosition(nextTarget);
            VolleyballManager.Instance.OrangeLandSpot.ShowCircleIndicator(false);
        }

        MoveToPositionServerRpc(nextTarget);
    }

    public void EnableInteractions()
    {
        if (!IsServer) return;
        interactionsEnabled.Value = true;
    }

    public void ResetBall()
    {
        if (!IsServer) return;

        if (currentMoveTween != null)
        {
            currentMoveTween.Kill();
        }
        transform.position = startPosition;
        currentFlightTime.Value = initialFlightTime;
        isMoving.Value = false;
        isMovementComplete.Value = true;
        lastHitPlayerId.Value = 0;
        lastHitTeam.Value = new TeamMember(Team.Neutral);
        currentLandingZone = null;
        interactionsEnabled.Value = false;

        VolleyballManager.Instance.BlueLandSpot.ShowCircleIndicator(false);
        VolleyballManager.Instance.OrangeLandSpot.ShowCircleIndicator(false);

        animationManager.SetBool("Walking", false);
    }
}
