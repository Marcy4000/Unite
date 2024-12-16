using Unity.Netcode;
using UnityEngine;

public class PsyduckRacingShieldObject : NetworkBehaviour
{
    private PlayerManager assignedPlayer;

    private float timer = 12f;

    public void Initialize(ulong assigndPlayerID)
    {
        assignedPlayer = NetworkManager.Singleton.SpawnManager.SpawnedObjects[assigndPlayerID].gameObject.GetComponent<PlayerManager>();

        if (assignedPlayer == null)
        {
            Debug.LogError("Failed to find player manager for player ID: " + assigndPlayerID);
            DespawnRPC();
            return;
        }

        transform.position = assignedPlayer.transform.position;

        assignedPlayer.Pokemon.OnStatusChange += OnStatusChange;
    }

    private void OnStatusChange(StatusEffect status, bool added)
    {
        if (!added)
            return;

        if (status.Type == StatusType.Incapacitated || (status.Type == StatusType.Scriptable && status.ID == 20))
        {
            assignedPlayer.Pokemon.RemoveStatusEffectRPC(status);
            DespawnRPC();
        }
    }

    private void Update()
    {
        if (assignedPlayer != null)
        {
            transform.position = assignedPlayer.transform.position;

            timer -= Time.deltaTime;
            if (timer <= 0)
            {
                timer = 12f;
                DespawnRPC();
            }
        }
    }

    [Rpc(SendTo.Server)]
    private void DespawnRPC()
    {
        NetworkObject.Despawn(true);
    }

    public override void OnDestroy()
    {
        if (assignedPlayer != null)
        {
            assignedPlayer.Pokemon.OnStatusChange -= OnStatusChange;
        }
        base.OnDestroy();
    }
}
