public class JoltPassive : PassiveBase
{
    private JolteonCharacterRuntime Character => playerManager?.MovesController?.Combat?.Character as JolteonCharacterRuntime;
    public bool IsPassiveReady => Character?.PassiveReady == true;
    public float PassiveCharge => Character?.PassiveAmount ?? 0f;
    public override void Start(PlayerManager controller) { base.Start(controller); }
    public override void Update() { }
    public bool TryConsumeCharge() => Character?.ConsumePassiveCharge() == true;
    public void ResetPassiveCharge() => Character?.ReducePassiveCharge(100f);
    public void ReducePassiveCharge(float amount) => Character?.ReducePassiveCharge(amount);
    public void UniteFillCharge(float duration) => Character?.BeginUniteOvercharge(duration);
    public override void Dispose() => playerManager = null;
}
