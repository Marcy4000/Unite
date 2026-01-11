using Unity.Netcode;
using UnityEngine;

public class DuraBoostedObj : NetworkBehaviour
{
    [SerializeField] private GameObject beamObject, beamObjectCannon;
    [SerializeField] private BoxCollider beamCollider, beamColliderCannon;

    private bool initialized = false;
    private bool isCasting = false;

    private PlayerManager duraludon;
    private DamageInfo damageInfo;

    [Rpc(SendTo.Owner)]
    public void InitializeRPC(DamageInfo dmgInfo, ulong playerID)
    {
        damageInfo = dmgInfo;
        duraludon = NetworkManager.Singleton.SpawnManager.SpawnedObjects[playerID].gameObject.GetComponent<PlayerManager>();
        beamObject.SetActive(false);
        beamObjectCannon.SetActive(false);

        initialized = true;
    }

    void Update()
    {
        if (!initialized || !IsOwner || isCasting)
        {
            return;
        }

        transform.position = duraludon.transform.position;
        transform.rotation = duraludon.transform.rotation;
    }

    [Rpc(SendTo.Everyone)]
    public void CastRPC(bool cannonMode)
    {
        if (!initialized)
        {
            return;
        }

        StartCoroutine(CastRoutine(cannonMode));
    }

    private System.Collections.IEnumerator CastRoutine(bool cannonMode)
    {
        if (cannonMode)
        {
            beamColliderCannon.enabled = true;
            beamObjectCannon.SetActive(true);
        }
        else
        {
            beamCollider.enabled = true;
            beamObject.SetActive(true);
        }
        yield return new WaitForSeconds(0.05f);
        
        isCasting = true;
        if (IsOwner)
        {
            BoxCollider currentCollider = cannonMode ? beamColliderCannon : beamCollider;
            foreach (var hit in Physics.OverlapBox(currentCollider.bounds.center, currentCollider.bounds.extents, currentCollider.transform.rotation))
            {
                if (hit.TryGetComponent(out Pokemon pokemon))
                {
                    if (Aim.Instance.CanPokemonBeTargeted(pokemon.gameObject, AimTarget.NonAlly, duraludon.CurrentTeam.Team, true))
                    {
                        pokemon.TakeDamageRPC(damageInfo);
                        if (cannonMode)
                            pokemon.AddStatChange(new StatChange(60, Stat.Speed, 1f, true, false, true, 0));

                        short damageAmount = (short)Mathf.RoundToInt(pokemon.GetMaxHp() * 0.015f);

                        if (pokemon.Type == PokemonType.Wild || pokemon.Type == PokemonType.Objective)
                        {
                            damageAmount = (short)Mathf.Min(damageAmount, 500);
                        }

                        pokemon.TakeDamageRPC(new DamageInfo(duraludon.NetworkObjectId, 0f, 0, damageAmount, DamageType.True, DamageProprieties.None));
                    }
                }
            }
        }

        yield return new WaitForSeconds(0.25f);

        if (cannonMode)
        {
            beamColliderCannon.enabled = false;
            beamObjectCannon.SetActive(false);
        }
        else
        {
            beamCollider.enabled = false;
            beamObject.SetActive(false);
        }
        isCasting = false;
    }
}
