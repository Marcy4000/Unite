using UnityEngine;

public class JoltBasicAtk : ChargedBasicAttackBase
{
    private byte charge;
    private JolteonCharacterRuntime Character => playerManager?.MovesController?.Combat?.Character as JolteonCharacterRuntime;
    protected override int AttackCharge => Character?.AttackCharge ?? charge;
    public override bool ServerResolved => true;
    public override void Initialize(PlayerManager manager) { range = 2.5f; base.Initialize(manager); }
    public override void InitializeServer(PlayerManager manager) { base.InitializeServer(manager); range = 2.5f; }
    public override void Perform(bool wildPriority)
    {
        GameObject selected = ActorAim.AimInCircle(range, wildPriority ? PokemonType.Wild : PokemonType.Player);
        if (selected != null && selected.TryGetComponent(out Pokemon target))
            playerManager.MovesController.RequestServerBasicAttack(target.NetworkObjectId);
        else if (!playerManager.PlayerMovement.IsMoving)
            playerManager.MovesController.RequestServerBasicAttack(ulong.MaxValue);
    }
    public override void PerformOnServer(Pokemon target) => Character?.PerformBasicAttack(target);
    public override void ApplyServerState(int value, float empoweredUntil) => charge = (byte)Mathf.Clamp(value, 0, 2);
    public override void ResetAttack()
    {
        if (playerManager != null && playerManager.IsServer) Character?.ResetBasicCharge();
        charge = 0;
        base.ResetAttack();
    }
}
