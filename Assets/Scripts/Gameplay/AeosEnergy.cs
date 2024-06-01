using System.Collections;
using System.Collections.Generic;
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
        SnapToGround();
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

    void SnapToGround()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position+(Vector3.up*3f), Vector3.down, out hit, 10f, groundLayer))
        {
            transform.position = new Vector3(transform.position.x, hit.point.y+(model.transform.localScale.x/2f), transform.position.z);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerManager playerManager = other.GetComponent<PlayerManager>();
            if (playerManager.IsEnergyFull())
            {
                return;
            }

            short energy = (short)(IsBigEnergy ? 5 : 1);
            playerManager.GainEnergy(energy);
            if (IsServer)
            {
                gameObject.GetComponent<NetworkObject>().Despawn(true);
            }
        }
    }
}
