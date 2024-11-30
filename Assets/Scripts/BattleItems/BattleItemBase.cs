using System;

public abstract class BattleItemBase
{
    public string Name;
    public float Cooldown;
    public PlayerManager playerManager;
    public bool IsActive = false;

    public bool wasUseSuccessful = false;

    public event Action onBattleItemOver;

    public virtual void Start(PlayerManager controller)
    {
        playerManager = controller;
        IsActive = true;
        wasUseSuccessful = false;
    }

    public abstract void Update();

    public virtual void Finish()
    {
        onBattleItemOver?.Invoke();
        IsActive = false;
    }

    public virtual void Cancel()
    {
        IsActive = false;
    }
}

public enum AvailableBattleItems { None, Eject, XSpeed, XAttack, FullHeal, GoalGetter, Potion, SlowSmoke, ShedinjaDoll, Brick }

public static class BattleItemDatabase
{
    public static BattleItemBase GetBattleItem(AvailableBattleItems battleItem)
    {
        switch (battleItem)
        {
            case AvailableBattleItems.None:
                return new EmptyBattleItem();
            case AvailableBattleItems.Eject:
                return new EjectButton();
            case AvailableBattleItems.XSpeed:
                return new XSpeed();
            case AvailableBattleItems.XAttack:
                return new XAttack();
            case AvailableBattleItems.FullHeal:
                return new FullHeal();
            case AvailableBattleItems.GoalGetter:
                return new GoalGetter();
            case AvailableBattleItems.Potion:
                return new Potion();
            case AvailableBattleItems.SlowSmoke:
                //return new SlowSmoke();
            case AvailableBattleItems.ShedinjaDoll:
            //return new ShedinjaDoll();
            case AvailableBattleItems.Brick:
                return new BrickLol();
            default:
                return new EmptyBattleItem();
        }
    }
}