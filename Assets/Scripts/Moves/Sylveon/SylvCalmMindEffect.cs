using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class SylvCalmMindEffect : PrefabMoveEffect
{
    [SerializeField] private GameObject guard;
    [SerializeField] private GameObject shield;
    private bool armed;
    private float shieldUntil;

    protected override void BeginPresentation()
    {
        guard.SetActive(Upgraded);
        shield.SetActive(false);
    }

    protected override IEnumerator Execute()
    {
        RefreshStat(Actor.Pokemon, new StatChange(30, Stat.Speed, 3, true, true, true, SylvMoveRules.CalmSpeed));
        RefreshStat(Actor.Pokemon, new StatChange(40, Stat.SpAttack, 3, true, true, true, SylvMoveRules.CalmAttack));
        RefreshStat(Actor.Pokemon, new StatChange(10, Stat.SpDefense, 3, true, true, true, SylvMoveRules.CalmDefense));
        armed = Upgraded;
        if (armed) Actor.Pokemon.BeforeDamage += Block;
        yield return new WaitForSeconds(3);
        armed = false;
        Actor.Pokemon.BeforeDamage -= Block;
        EndGuardRPC();
        while (Time.time < shieldUntil) yield return null;
    }

    private bool Block(DamageInfo damage)
    {
        if (!armed || !SylvMoveRules.CanBlock(damage.proprieties)
            || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(damage.attackerId, out var obj)
            || !obj.TryGetComponent(out Pokemon attacker) || attacker.TeamMember.IsOnSameTeam(Actor.CurrentTeam)) return false;
        armed = false;
        Actor.Pokemon.RemoveShieldWithIDRPC(SylvMoveRules.CalmShield);
        Actor.Pokemon.AddShieldRPC(new ShieldInfo(SylvMoveRules.CalmShieldAmount(Actor.Pokemon.GetSpAttack()),
            SylvMoveRules.CalmShield, 0, 3, true));
        shieldUntil = Time.time + 3;
        ShowShieldRPC();
        return true;
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void ShowShieldRPC() { guard.SetActive(false); shield.SetActive(true); }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void EndGuardRPC() { guard.SetActive(false); }
    protected override void Update()
    {
        base.Update();
        if (Actor != null) transform.position = Actor.transform.position;
    }
    protected override void Cleanup() { if (IsServer && Actor != null) Actor.Pokemon.BeforeDamage -= Block; }
}
