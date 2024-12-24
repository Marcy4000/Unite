using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RedBlueBuffAura : MonoBehaviour
{
    [SerializeField] private GameObject redBuffModel;
    [SerializeField] private GameObject blueBuffModel;
    [SerializeField] private GameObject redBlueBuffModel;
    [SerializeField] private GameObject auraHolder;

    [Space]
    [SerializeField] private Pokemon targetPokemon;

    private bool hasRedBuff;
    private bool hasBlueBuff;

    private ushort redBuffID;
    private ushort blueBuffID;

    public GameObject AuraHolder => auraHolder;

    private StatChange blueBuffCDR = new StatChange(10, Stat.Cdr, 0, false, true, true, 31);
    private StatChange redBuffSlow = new StatChange(30, Stat.Speed, 2, true, false, true, 32);

    public void Awake()
    {
        redBuffModel.SetActive(false);
        blueBuffModel.SetActive(false);
        redBlueBuffModel.SetActive(false);

        targetPokemon.OnStatusChange += OnStatusChange;
        targetPokemon.OnDeath += OnDeath;
        if (targetPokemon.IsServer)
        {
            targetPokemon.OnDamageDealt += OnDamageDealt;
        }

        auraHolder.transform.SetParent(null);
        auraHolder.transform.rotation = Quaternion.identity;
    }

    private void OnStatusChange(StatusEffect status, bool added)
    {
        if (status.Type == StatusType.RedBuff)
        {
            redBuffID = status.ID;
            hasRedBuff = added;
        }
        else if (status.Type == StatusType.BlueBuff)
        {
            blueBuffID = status.ID;
            hasBlueBuff = added;

            if (targetPokemon.IsServer && status.ID == 1)
            {
                if (added)
                {
                    targetPokemon.AddStatChange(blueBuffCDR);
                }
                else
                {
                    targetPokemon.RemoveStatChangeWithIDRPC(blueBuffCDR.ID);
                }
            }
        }
        UpdateBuffAura();
    }

    private void Update()
    {
        auraHolder.transform.position = transform.position;
    }

    private void UpdateBuffAura()
    {
        redBuffModel.SetActive(hasRedBuff && !hasBlueBuff);
        blueBuffModel.SetActive(hasBlueBuff && !hasRedBuff);
        redBlueBuffModel.SetActive(hasRedBuff && hasBlueBuff);
    }

    private void OnDamageDealt(ulong targetID, DamageInfo damageInfo)
    {
        Pokemon target = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetID].GetComponent<Pokemon>();

        if (hasRedBuff)
        {
            foreach (var statChange in target.StatChanges)
            {
                if (statChange.ID == redBuffSlow.ID)
                {
                    return;
                }
            }
            target.AddStatChange(redBuffSlow);
        }
    }

    private void OnDeath(DamageInfo damage)
    {
        if (hasBlueBuff)
        {
            targetPokemon.RemoveStatChangeWithIDRPC(blueBuffCDR.ID);
            hasBlueBuff = false;
        }
        if (hasRedBuff)
        {
            hasRedBuff = false;
        }
        UpdateBuffAura();
    }

    private void OnDestroy()
    {
        Destroy(auraHolder);
    }
}
