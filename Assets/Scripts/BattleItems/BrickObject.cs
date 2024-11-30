using DG.Tweening;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class BrickObject : NetworkBehaviour
{
    [SerializeField] private Transform modelHolder;
    [SerializeField] private float rotationDuration = 2f;
    [SerializeField] private float delayBetweenRotations = 0.5f;

    private StatusEffect stun = new StatusEffect(StatusType.Incapacitated, 1.5f, true, 0);
    private DamageInfo damage;

    private Team teamToIgnore;
    private Vector3 landPosition;

    private void Start()
    {
        StartRotatingBrick();
    }

    private void StartRotatingBrick()
    {
        if (modelHolder == null)
        {
            Debug.LogError("ModelHolder is not assigned!");
            return;
        }

        RotateRandomly();
    }

    private void RotateRandomly()
    {
        // Generate a random rotation
        Vector3 randomRotation = new Vector3(
            Random.Range(0f, 360f),
            Random.Range(0f, 360f),
            Random.Range(0f, 360f)
        );

        // Rotate the modelHolder to the random rotation over a specified duration
        modelHolder.DORotate(randomRotation, rotationDuration, RotateMode.FastBeyond360)
                   .SetEase(Ease.InOutSine) // Smooth easing for a natural feel
                   .OnComplete(() =>
                   {
                       // Add a delay and then rotate again
                       DOVirtual.DelayedCall(delayBetweenRotations, RotateRandomly);
                   });
    }

    [Rpc(SendTo.Server)]
    public void InitializeRPC(Vector3 startPosition, Vector3 landPosition, Team teamToIgnore, DamageInfo damage)
    {
        this.teamToIgnore = teamToIgnore;
        this.landPosition = landPosition;
        this.damage = damage;
        transform.position = startPosition;

        transform.DOJump(landPosition, 2f, 1, 1f)
                 .OnComplete(() =>
                 {
                     StartCoroutine(DoDamage());
                 });
    }

    private IEnumerator DoDamage()
    {
        GameObject[] enemiesHit = Aim.Instance.AimInCircleAtPosition(landPosition, 2f, AimTarget.NonAlly, teamToIgnore);

        foreach (GameObject enemy in enemiesHit)
        {
            if (enemy.TryGetComponent(out Pokemon pokemon))
            {
                pokemon.TakeDamage(damage);
                pokemon.AddStatusEffect(stun);
            }
        }

        yield return new WaitForSeconds(0.1f);

        NetworkObject.Despawn(true);
    }
}
