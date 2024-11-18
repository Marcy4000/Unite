using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class FlareonFlamethrowerArea : NetworkBehaviour
{
    [SerializeField] private Transform flameTransform;
    [SerializeField] private LayerMask playerMask;
    private Vector3 flameHitboxCenter = new Vector3(0, 0, 2.29f);
    private Vector3 flameHitboxSize = new Vector3(1.2f, 1f, 4.42f);

    private float maxAngle = 30f;

    private StatusEffect burn = new StatusEffect(StatusType.Burned, 3f, true, 0);
    private DamageInfo damageInfo;

    private bool orangeTeam;

    private List<Pokemon> hitPokemon = new List<Pokemon>();

    [Rpc(SendTo.Server)]
    public void InitializeRPC(Vector3 pos, Vector3 rot, bool orangeTeam, DamageInfo damage)
    {
        transform.position = pos;
        transform.eulerAngles = rot;

        this.orangeTeam = orangeTeam;
        damageInfo = damage;

        StartCoroutine(DoFlameArchRotation());
    }

    private IEnumerator DoFlameArchRotation()
    {
        float angle = -maxAngle; // Start from -maxAngle
        float duration = 0.4f; // Duration of rotation in seconds
        float step = (maxAngle * 2f) / duration; // Calculate step based on full angle range and duration
        float elapsedTime = 0f; // Elapsed time

        Quaternion startRotation = Quaternion.Euler(0, -maxAngle, 0); // Initial rotation
        Quaternion targetRotation = Quaternion.Euler(0, maxAngle, 0); // Target rotation

        while (elapsedTime < duration)
        {
            // Interpolating between start and target based on elapsed time
            float normalizedTime = Mathf.Clamp01(elapsedTime / duration);
            flameTransform.localRotation = Quaternion.Lerp(startRotation, targetRotation, normalizedTime);

            elapsedTime += Time.deltaTime; // Update elapsed time

            // Call CheckForCollisions at regular intervals
            if (Mathf.FloorToInt(elapsedTime * 10f) % 1 == 0)
            {
                CheckForCollisions();
            }

            yield return null;
        }

        flameTransform.localRotation = targetRotation; // Ensure final rotation is exactly the target rotation

        yield return new WaitForSeconds(0.3f);

        NetworkObject.Despawn(true);
    }

    private void CheckForCollisions()
    {
        Collider[] colliders = Physics.OverlapBox(flameTransform.TransformPoint(flameHitboxCenter), flameHitboxSize / 2, flameTransform.rotation, playerMask);

        foreach (Collider collider in colliders)
        {
            if (collider.TryGetComponent(out Pokemon pokemon))
            {
                if (hitPokemon.Contains(pokemon) || !Aim.Instance.CanPokemonBeTargeted(pokemon.gameObject, AimTarget.NonAlly, orangeTeam))
                {
                    continue;
                }

                pokemon.AddStatusEffect(burn);
                pokemon.TakeDamage(damageInfo);

                hitPokemon.Add(pokemon);
            }
        }
    }
}
