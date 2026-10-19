using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class DuraludonUniteObject : DuraMoveObject
{
    [SerializeField] private SphereCollider sweepArea, ringArea;
    [SerializeField] private Transform beamPivot;
    [SerializeField] private GameObject beam, burningRing, explosion;
    [SerializeField] private float sweepDuration = 2f;
    [SerializeField] private float ringHalfWidth = 0.6f;
    [SerializeField] private float tickInterval = 0.5f;
    private bool actionsLocked;

    protected override void BeginPresentation()
    {
        beam.SetActive(true);
        burningRing.SetActive(false);
        explosion.SetActive(false);
        SetActionLock(true);
        StartCoroutine(AnimateSweep());
    }

    private IEnumerator AnimateSweep()
    {
        float started = Time.time;
        while (Time.time - started < sweepDuration)
        {
            beamPivot.localRotation = Quaternion.Euler(0, (Time.time - started) / sweepDuration * 360f, 0);
            yield return null;
        }
    }

    private void SetActionLock(bool locked)
    {
        if (Actor == null || !Actor.IsOwner || !Actor.IsSpawned || actionsLocked == locked) return;
        actionsLocked = locked;
        if (locked)
        {
            Actor.PlayerMovement.AddMovementRestriction();
            Actor.MovesController.BasicAttackStatus.AddStatus(ActionStatusType.Busy);
            Actor.ScoreStatus.AddStatus(ActionStatusType.Busy);
            for (int i = 0; i < 3; i++) Actor.MovesController.AddMoveStatus(i, ActionStatusType.Busy);
            Actor.AnimationManager.PlayAnimation("ani_spellu_bat_0884");
        }
        else
        {
            Actor.PlayerMovement.RemoveMovementRestriction();
            if (Actor.Pokemon.CurrentHp <= 0) Actor.PlayerMovement.CanMove = false;
            Actor.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Busy);
            Actor.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
            for (int i = 0; i < 3; i++) Actor.MovesController.RemoveMoveStatus(i, ActionStatusType.Busy);
        }
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void BeginRingRPC()
    {
        if (!IsClient) return;
        SetActionLock(false);
        beam.SetActive(false);
        burningRing.SetActive(true);
        explosion.SetActive(true);
        StartCoroutine(AnimateExplosion());
    }

    private IEnumerator AnimateExplosion()
    {
        float started = Time.time;
        while (Time.time - started < 0.35f)
        {
            explosion.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, sweepArea.radius, (Time.time - started) / 0.35f);
            yield return null;
        }
        explosion.SetActive(false);
    }

    protected override IEnumerator Execute()
    {
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, sweepDuration, true, 8844));
        var hit = new HashSet<ulong>();
        int stacks = 0;
        float started = Time.time;
        float previousAngle = -1;
        while (previousAngle < 360)
        {
            float angle = Mathf.Clamp01((Time.time - started) / sweepDuration) * 360f;
            foreach (Pokemon target in TargetsIn(sweepArea))
            {
                float targetAngle = Mathf.Repeat(Vector3.SignedAngle(Direction, target.transform.position - Origin, Vector3.up), 360f);
                if (targetAngle <= previousAngle || targetAngle > angle || !hit.Add(target.NetworkObjectId)) continue;
                target.TakeDamageRPC(new DamageInfo(Actor.NetworkObjectId, 1.63f, 6, 350, DamageType.Physical, DamageProprieties.IsUniteMove));
                if (stacks < 5)
                {
                    stacks++;
                    Actor.Pokemon.RemoveStatChangeWithIDRPC(8842);
                    Actor.Pokemon.AddStatChange(new StatChange((short)(stacks * 8), Stat.Attack, 8f, true, true, true, 8842));
                    int shield = Mathf.RoundToInt(Actor.Pokemon.GetAttack() * 0.68f + 68);
                    AddRefreshingShield(8842, shield, shield * 5, 8f);
                }
            }
            previousAngle = angle;
            if (angle >= 360) break;
            yield return null;
        }
        foreach (Pokemon target in TargetsIn(sweepArea))
        {
            target.TakeDamageRPC(new DamageInfo(Actor.NetworkObjectId, 1.63f, 6, 350, DamageType.Physical, DamageProprieties.IsUniteMove));
            target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.5f, true, 0));
            target.ApplyKnockbackRPC((target.transform.position - Origin).normalized, 10f);
        }
        Actor.Pokemon.AddStatChange(new StatChange(30, Stat.Speed, 6f, true, true, true, 0));
        Actor.Pokemon.AddStatChange(new StatChange(35, Stat.AtkSpeed, 6f, true, true, true, 0));
        Actor.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.RoundToInt(Actor.Pokemon.GetMaxHp() * 0.2f), 8845, 0, 6f, true));
        BeginRingRPC();
        for (int tick = 0; tick < 20; tick++)
        {
            foreach (Pokemon target in TargetsIn(ringArea))
            {
                Vector3 offset = target.transform.position - Origin;
                offset.y = 0;
                if (offset.magnitude < sweepArea.radius - ringHalfWidth) continue;
                target.TakeDamageRPC(new DamageInfo(Actor.NetworkObjectId, 0.35f, 0, 63, DamageType.Physical, DamageProprieties.IsUniteMove));
                Slow(target, 50, 1f, 8846);
            }
            yield return new WaitForSeconds(tickInterval);
        }
    }

    protected override void EndPresentation() { SetActionLock(false); }
}
