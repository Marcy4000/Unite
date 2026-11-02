// Authoritative interception is bound on the server through EspeonRuntime,
// including when the license is owned by a remote client or a bot.
public class EspeonPassive : PassiveBase
{
    public override void Update() { }
}
