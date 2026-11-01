using UnityEngine;

public partial class MovesController
{
    public FlareonCharacterRuntime FlareonRuntime => Combat?.Character as FlareonCharacterRuntime;
    public int FlareonAttackCharge => FlareonRuntime?.AttackCharge ?? 0;
    public int FlareonAttackStacks => FlareonRuntime?.Stacks ?? 0;
    public float FlareonStackRemaining => FlareonRuntime != null ? Mathf.Max(0, (float)(FlareonRuntime.StacksExpire - FlareonRuntime.PresentationNow)) : 0;
    public Pokemon FlareonRecastTarget => FlareonRuntime?.RecastTarget;
    public bool FlareonRecastAvailable => FlareonRuntime?.RecastAvailable == true;

}
