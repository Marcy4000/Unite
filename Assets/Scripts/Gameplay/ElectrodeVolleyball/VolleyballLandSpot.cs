using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class VolleyballLandSpot : NetworkBehaviour
{
    [SerializeField] private GameObject holder;
    [SerializeField] private GameObject circleIndicator;
    [SerializeField] private Team assignedTeam;
    [SerializeField] private float maxScale = 2f;
    [SerializeField] private float minScale = 1f;

    private NetworkVariable<bool> isHolderVisible = new NetworkVariable<bool>();
    private NetworkVariable<float> indicatorScale = new NetworkVariable<float>(1f);
    private NetworkList<ulong> networkPlayerList;

    private void Awake()
    {
        networkPlayerList = new NetworkList<ulong>();
    }

    public Team AssignedTeam => assignedTeam;
    public bool HasPlayersInside 
    {
        get 
        {
            foreach (ulong playerId in networkPlayerList)
            {
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerId, out NetworkObject netObj))
                {
                    PlayerManager player = netObj.GetComponent<PlayerManager>();
                    if (player != null && player.CurrentTeam.IsOnSameTeam(assignedTeam))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

    public List<PlayerManager> GetValidPlayers()
    {
        List<PlayerManager> validPlayers = new List<PlayerManager>();
        foreach (ulong playerId in networkPlayerList)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerId, out NetworkObject netObj))
            {
                PlayerManager player = netObj.GetComponent<PlayerManager>();
                if (player != null && player.CurrentTeam.IsOnSameTeam(assignedTeam))
                {
                    validPlayers.Add(player);
                }
            }
        }
        return validPlayers;
    }

    public override void OnNetworkSpawn()
    {
        ShowCircleIndicator(false);
        holder.SetActive(false);
        isHolderVisible.OnValueChanged += OnHolderVisibilityChanged;
        indicatorScale.OnValueChanged += OnIndicatorScaleChanged;
        base.OnNetworkSpawn();
    }

    private void OnHolderVisibilityChanged(bool previousValue, bool newValue)
    {
        holder.SetActive(newValue);
    }

    private void OnIndicatorScaleChanged(float previousValue, float newValue)
    {
        Vector3 scale = circleIndicator.transform.localScale;
        scale.x = scale.z = newValue;
        circleIndicator.transform.localScale = scale;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        if (other.CompareTag("Player"))
        {
            PlayerManager playerManager = other.GetComponent<PlayerManager>();
            if (playerManager.CurrentTeam.IsOnSameTeam(assignedTeam))
            {
                networkPlayerList.Add(playerManager.NetworkObjectId);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        if (other.CompareTag("Player"))
        {
            PlayerManager playerManager = other.GetComponent<PlayerManager>();
            networkPlayerList.Remove(playerManager.NetworkObjectId);
        }
    }

    public void ShowCircleIndicator(bool show)
    {
        if (IsServer)
        {
            isHolderVisible.Value = show;
        }
    }

    public PlayerManager GetFirstPlayerInside()
    {
        var validPlayers = GetValidPlayers();
        if (validPlayers.Count > 0)
        {
            return validPlayers[0];
        }
        return null;
    }

    public void UpdatePosition(Vector3 position)
    {
        transform.position = new Vector3(position.x, transform.position.y, position.z);
    }

    public void UpdateScale(float remainingTime, float totalTime)
    {
        float t = remainingTime / totalTime;
        indicatorScale.Value = Mathf.Lerp(minScale, maxScale, t);
    }

    public override void OnDestroy()
    {
        if (isHolderVisible != null)
            isHolderVisible.OnValueChanged -= OnHolderVisibilityChanged;
        if (indicatorScale != null)
            indicatorScale.OnValueChanged -= OnIndicatorScaleChanged;

        base.OnDestroy();
    }
}
