public abstract class PassiveBase
{
    public PlayerManager playerManager;
    public bool IsActive = false;

    public virtual void Start(PlayerManager controller)
    {
        playerManager = controller;
    }

    public abstract void Update();
}

public static class PassiveDatabase
{
    public static PassiveBase GetPassive(AvailablePassives passive)
    {
        switch (passive)
        {
            case AvailablePassives.EmptyPassive:
                return new EmptyPassive();
            case AvailablePassives.CinderPassive:
                return new CinderPassive();
            case AvailablePassives.GlaceonPassive:
                return new GlaceonPassive();
            case AvailablePassives.SylveonPassive:
                return new SylvPassive();
            case AvailablePassives.JolteonPassive:
                return new JoltPassive();
            case AvailablePassives.FlygonPassive:
                return new FlygonPassive();
            case AvailablePassives.MeowsticMPassive:
                return new MeowsticPassiveM();
            case AvailablePassives.MarshadowPassive:
                return new MarshadwPassive();
            case AvailablePassives.CresseliaPassive:
                return new CresseliaPassive();
            case AvailablePassives.EmboarPassive:
                return new EmboarPassive();
            case AvailablePassives.FlareonPassive:
                return new FlareonPassive();
            case AvailablePassives.PsyduckRacePassive:
                return new PsyduckRacePassive();
            case AvailablePassives.SerperiorPassive:
                return new SerperiorPassive();
            default:
                return new EmptyPassive();
        }
    }
}

public enum AvailablePassives
{
    EmptyPassive,
    CinderPassive,
    GlaceonPassive,
    SylveonPassive,
    VaporeonPassive,
    JolteonPassive,
    FlygonPassive,
    MeowsticMPassive,
    MarshadowPassive,
    CresseliaPassive,
    EmboarPassive,
    FlareonPassive,
    PsyduckRacePassive,
    SerperiorPassive,
}