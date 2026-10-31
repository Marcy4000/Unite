using UnityEngine;

public partial class CameraController
{
    private object skillAimOwner;
    private Transform skillAimTarget;
    private Vector3 skillAimOffset;
    private float skillAimReturnDelay, skillAimReturnAt;
    private bool skillAimReleased;

    // A shared, local-only follow target. Manual camera controls always take
    // priority, and a stale skill cannot release a newer skill's camera lease.
    public void UpdateSkillAim(object owner, Vector3 direction, float range, float returnDelay)
    {
        if (owner == null || isPanning || !isActiveAndEnabled || virtualCamera == null || playerTransform == null
            || !CombatTargeting.Finite(direction) || !float.IsFinite(range) || range <= 0
            || !float.IsFinite(returnDelay) || returnDelay < 0) return;
        direction.y = 0;
        if (direction.sqrMagnitude < 0.001f) return;
        if (skillAimTarget == null)
        {
            var target = new GameObject("Skill Aim Camera Target") { hideFlags = HideFlags.HideAndDontSave };
            target.transform.SetParent(transform, false); skillAimTarget = target.transform;
        }
        skillAimOwner = owner;
        skillAimOffset = direction.normalized * range;
        skillAimReturnDelay = returnDelay;
        skillAimReleased = false;
        skillAimTarget.position = playerTransform.position + skillAimOffset;
        virtualCamera.Follow = skillAimTarget;
        virtualCamera.LookAt = skillAimTarget;
    }

    public void EndSkillAim(object owner, bool immediate = false)
    {
        if (!ReferenceEquals(skillAimOwner, owner)) return;
        if (immediate || skillAimReturnDelay <= 0) { ClearSkillAim(); return; }
        if (skillAimReleased) return; // Repeated cleanup cannot extend the delay.
        skillAimReleased = true;
        skillAimReturnAt = Time.time + skillAimReturnDelay;
    }

    public void ClearSkillAim()
    {
        if (skillAimTarget != null && virtualCamera != null)
        {
            if (virtualCamera.Follow == skillAimTarget) virtualCamera.Follow = playerTransform;
            if (virtualCamera.LookAt == skillAimTarget) virtualCamera.LookAt = playerTransform;
        }
        skillAimOwner = null;
        skillAimOffset = Vector3.zero;
        skillAimReleased = false;
    }

    private void TickSkillAim(float now)
    {
        if (skillAimOwner == null) return;
        if (playerTransform == null || isPanning || virtualCamera == null
            || (playerTransform.TryGetComponent<Pokemon>(out var pokemon) && pokemon.CurrentHp <= 0)
            || (skillAimReleased && now >= skillAimReturnAt))
        { ClearSkillAim(); return; }
        if (skillAimTarget != null) skillAimTarget.position = playerTransform.position + skillAimOffset;
    }
}
