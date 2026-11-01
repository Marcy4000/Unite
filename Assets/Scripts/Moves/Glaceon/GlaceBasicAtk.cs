using UnityEngine;

public class GlaceBasicAtk : ChargedBasicAttackBase
{
    private byte charge, basicAtkType;
    private GlaceonCharacterRuntime Character => playerManager?.MovesController.Combat?.Character as GlaceonCharacterRuntime;
    protected override int AttackCharge => charge;
    protected override bool ShowAttackCounter => basicAtkType == 0;
    public override bool ServerResolved => true;
    public byte Charge
    {
        get => playerManager != null && playerManager.IsServer ? Character?.Charge ?? charge : charge;
        set { if (value == 2 && playerManager != null && playerManager.IsServer) Character?.FillCharge(); }
    }

    public override void Initialize(PlayerManager manager) { range = 7; base.Initialize(manager); }
    public override void InitializeServer(PlayerManager manager) { base.InitializeServer(manager); range = 7; }
    public override void Perform(bool wildPriority)
    {
        range = basicAtkType == 2 ? 8 : 7;
        var target = ActorAim.AimInCircle(range, wildPriority ? PokemonType.Wild : PokemonType.Player);
        if (target != null && target.TryGetComponent(out Pokemon pokemon))
            playerManager.MovesController.RequestServerBasicAttack(pokemon.NetworkObjectId);
    }
    public override void PerformOnServer(Pokemon target) => Character?.PerformBasicAttack(target);
    public override void ApplyServerState(int value, float empoweredUntil) => charge = (byte)Mathf.Clamp(value, 0, 2);
    public void ApplySnapshot(byte value, byte type)
    {
        charge = (byte)Mathf.Clamp(value, 0, 2);
        basicAtkType = (byte)Mathf.Clamp(type, 0, 2);
        range = basicAtkType == 2 ? 8 : 7;
    }
    public void ChangeBasicAtkType(byte type) { if (playerManager != null && playerManager.IsServer) Character?.SetMode(type); }
    public void FillChargeAmount() { if (playerManager != null && playerManager.IsServer) Character?.FillCharge(); }
    public override void ResetAttack()
    {
        if (playerManager != null && playerManager.IsServer) Character?.ResetBasicCharge();
        charge = 0;
        base.ResetAttack();
    }
}
