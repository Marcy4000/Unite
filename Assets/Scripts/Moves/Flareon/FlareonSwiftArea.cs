using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class FlareonSwiftArea : NetworkBehaviour
{
    [SerializeField] private GameObject swiftStar;
    [SerializeField] private Transform starsHolder;

    private DamageInfo tickDamage;
    private bool orangeTeam;
    private bool initialized;

    private float tickCooldown = 0.4f;

    private List<GameObject> stars = new List<GameObject>();

    [Rpc(SendTo.Server)]
    public void InitializeRPC(DamageInfo tickDamage, bool orangeTeam)
    {
        this.tickDamage = tickDamage;
        this.orangeTeam = orangeTeam;

        InizializeStarsRPC(4, 1.7f);

        initialized = true;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void InizializeStarsRPC(int amount, float radius)
    {
        foreach (GameObject star in stars)
        {
            Destroy(star);
        }

        stars.Clear();
        starsHolder.DOKill();

        for (int i = 0; i < amount; i++)
        {
            float angle = i * (2 * Mathf.PI / amount);
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;

            Vector3 starPosition = transform.position + new Vector3(x, 0f, y);
            stars.Add(Instantiate(swiftStar, starPosition, Quaternion.identity, starsHolder));
        }

        starsHolder.DOLocalRotate(new Vector3(0f, 360f, 0f), 0.8f, RotateMode.FastBeyond360).SetLoops(-1, LoopType.Incremental).SetEase(Ease.Linear);
    }

    [Rpc(SendTo.Server)]
    public void DespawnRPC()
    {
        NetworkObject.Despawn(true);
    }

    private void Update()
    {
        if (!initialized || !IsServer)
        {
            return;
        }

        if (tickCooldown > 0f)
        {
            tickCooldown -= Time.deltaTime;
        }
        else
        {
            tickCooldown = 0.4f;

            GameObject[] enemies = Aim.Instance.AimInCircleAtPosition(transform.position, 2f, AimTarget.NonAlly, orangeTeam);

            foreach (GameObject enemy in enemies)
            {
                if (enemy.TryGetComponent(out Pokemon pokemon))
                {
                    pokemon.TakeDamage(tickDamage);
                }
            }
        }
    }
}
