using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class RacingCurrentZone : MonoBehaviour
{
    public float currentStrength = 5f; // Speed boost strength
    public float resistanceStrength = 3f; // Slowdown when moving against the current
    public float rotationSpeed = 90f; // Degrees per second for the current's rotation
    public bool clockwise = true; // Direction of the current

    private SphereCollider sphereCollider;

    private void Awake()
    {
        sphereCollider = GetComponent<SphereCollider>();
        sphereCollider.isTrigger = true; // Ensure the collider acts as a trigger
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.TryGetComponent(out PassiveController passiveController))
        {
            if (!passiveController.IsOwner)
            {
                return;
            }

            PsyduckRacePassive passiveBase = passiveController.Passive as PsyduckRacePassive;

            Vector3 toPlayer = (other.transform.position - transform.position).normalized;

            // Determine current's force direction (tangential to the sphere)
            Vector3 currentDirection = Vector3.Cross(toPlayer, Vector3.up);
            if (!clockwise)
                currentDirection = -currentDirection;

            // Get the player's velocity relative to the current
            Vector3 playerVelocity = passiveBase.CurrentVelocity.normalized;
            float alignment = Vector3.Dot(playerVelocity, currentDirection);

            // Adjust speed based on alignment
            if (alignment > 0)
            {
                // Boost speed when aligned with the current
                passiveBase.SetSpeedModifier(1.5f); // Increase max speed
                passiveBase.CurrentVelocity += currentDirection * (currentStrength * Time.deltaTime);
            }
            else
            {
                // Reduce speed when moving against the current
                passiveBase.SetSpeedModifier(1f); // Reset max speed
                passiveBase.CurrentVelocity = Vector3.Lerp(
                    passiveBase.CurrentVelocity,
                    Vector3.zero,
                    resistanceStrength * Time.deltaTime
                );
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out PassiveController passiveController))
        {
            if (!passiveController.IsOwner)
            {
                return;
            }

            PsyduckRacePassive passiveBase = passiveController.Passive as PsyduckRacePassive;
            passiveBase.SetSpeedModifier(1f);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, GetComponent<SphereCollider>().radius);
    }
}
