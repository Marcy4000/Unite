using Unity.Netcode;
using UnityEngine;

public class PsyduckRacingShieldObject : NetworkBehaviour
{
    private PlayerManager assignedPlayer;

    private float timer = 12f;

    private StatusEffect unstoppableEffect = new StatusEffect(StatusType.HindranceResistance, 0, false, 0);

    public void Initialize(ulong assigndPlayerID)
    {
        assignedPlayer = NetworkManager.Singleton.SpawnManager.SpawnedObjects[assigndPlayerID].gameObject.GetComponent<PlayerManager>();

        unstoppableEffect.ID = (ushort)Random.Range(70, ushort.MaxValue-1);

        if (assignedPlayer == null)
        {
            Debug.LogError("Failed to find player manager for player ID: " + assigndPlayerID);
            DespawnRPC();
            return;
        }

        transform.position = assignedPlayer.transform.position;

        assignedPlayer.Pokemon.OnStatusChange += OnStatusChange;
        assignedPlayer.Pokemon.OnStatChange += OnStatChange;

        assignedPlayer.Pokemon.AddStatusEffect(unstoppableEffect);
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

    private void OnStatChange(NetworkListEvent<StatChange> changeEvent)
    {
        if (changeEvent.Type != NetworkListEvent<StatChange>.EventType.Add)
            return;

        if (assignedPlayer.Pokemon.StatChanges[changeEvent.Index].AffectedStat == Stat.Speed)
        {
            assignedPlayer.Pokemon.RemoveStatChangeRPC(assignedPlayer.Pokemon.StatChanges[changeEvent.Index]);
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
        if (assignedPlayer != null)
            assignedPlayer.Pokemon.RemoveStatusEffectWithID(unstoppableEffect.ID);

        NetworkObject.Despawn(true);
    }

    public override void OnDestroy()
    {
        if (assignedPlayer != null)
        {
            assignedPlayer.Pokemon.OnStatusChange -= OnStatusChange;
            assignedPlayer.Pokemon.OnStatChange -= OnStatChange;
        }
        base.OnDestroy();
    }
}
