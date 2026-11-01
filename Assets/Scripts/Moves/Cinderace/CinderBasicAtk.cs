using UnityEngine;

public class CinderBasicAtk : ChargedBasicAttackBase
{
    private byte charge;
    private CinderaceCharacterRuntime Character => playerManager?.MovesController.Combat?.Character as CinderaceCharacterRuntime;
    protected override int AttackCharge => charge;
    public override bool ServerResolved => true;
    public byte Charge
    {
        get => playerManager != null && playerManager.IsServer ? Character?.Charge ?? charge : charge;
        set { if (value == 2 && playerManager != null && playerManager.IsServer) Character?.FillCharge(); }
    }

    public override void Initialize(PlayerManager manager)
    {
        range = 7f;
        if (manager?.MovesController?.Combat?.Character is CinderaceCharacterRuntime runtime)
            charge = runtime.Charge;
        base.Initialize(manager);
    }

    public override void InitializeServer(PlayerManager manager)
    {
        base.InitializeServer(manager);
        range = 7f;
    }

    public override void Perform(bool wildPriority)
    {
        var target = ActorAim.AimInCircle(range, wildPriority ? PokemonType.Wild : PokemonType.Player);
        if (target != null && target.TryGetComponent(out Pokemon pokemon))
            playerManager.MovesController.RequestServerBasicAttack(pokemon.NetworkObjectId);
    }

    public override void PerformOnServer(Pokemon target) => Character?.PerformBasicAttack(target);
    public override void ApplyServerState(int value, float empoweredUntil) => charge = (byte)Mathf.Clamp(value, 0, 2);
    public void ApplySnapshot(byte value) => charge = (byte)Mathf.Clamp(value, 0, 2);
    public void FillChargeAmount() { if (playerManager != null && playerManager.IsServer) Character?.FillCharge(); }

    public override void ResetAttack()
    {
        if (playerManager != null && playerManager.IsServer) Character?.ResetBasicCharge();
        charge = 0;
        base.ResetAttack();
    }
}
