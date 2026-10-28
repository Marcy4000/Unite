using UnityEngine;
using Unity.Netcode;

public class AeosEnergy : NetworkBehaviour
{
    [SerializeField] private NetworkVariable<bool> isBigEnergy = new NetworkVariable<bool>();
    [SerializeField] LayerMask groundLayer;
    private bool localBigEnergy;
    private GameObject model;

    public bool IsBigEnergy { get => isBigEnergy.Value; set => isBigEnergy.Value = value; }
    public bool LocalBigEnergy { get => localBigEnergy; set => localBigEnergy = value; }

    public override void OnNetworkSpawn()
    {
        model = transform.GetChild(0).gameObject;
        isBigEnergy.OnValueChanged += OnValueChange;
        if (IsServer)
        {
            IsBigEnergy = localBigEnergy;
        }
        OnValueChange(false, isBigEnergy.Value);
        if (IsServer) SnapToGround();
    }

    private void OnValueChange(bool prev, bool curr)
    {
        if (curr)
        {
            model.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
        }
        else
        {
            model.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        }
    }

    public bool SnapToGround()
    {
        if (!GroundPlacement.TryGetGround(transform.position, groundLayer, out var point)) return false;
        float diameter = (IsSpawned ? IsBigEnergy : localBigEnergy) ? 0.4f : 0.3f;
        transform.position = point + Vector3.up * (diameter * 0.5f);
        return true;
    }

    public override void OnNetworkDespawn()
    {
        isBigEnergy.OnValueChanged -= OnValueChange;
        base.OnNetworkDespawn();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (IsServer)
            {
                PlayerManager playerManager = other.GetComponent<PlayerManager>();
                if (playerManager == null || playerManager.Pokemon == null || playerManager.Pokemon.CurrentHp <= 0 || playerManager.IsEnergyFull())
                {
                    return;
                }

                ushort energy = (ushort)(IsBigEnergy ? 5 : 1);

                playerManager.GainEnergyRPC(energy);
                gameObject.GetComponent<NetworkObject>().Despawn(true);
            }
        }
    }
}
