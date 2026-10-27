/// <summary>One short, latest-press-wins buffer. Releases cannot create or replace a press.</summary>
public sealed class BufferedMoveInput
{
    public int Slot { get; private set; } = -1;
    public bool Released { get; private set; }
    public float ExpiresAt { get; private set; }
    public bool Pending => Slot >= 0;

    public bool Queue(int slot, bool released, float now, float duration)
    {
        if (released)
        {
            if (Slot != slot || !IsValid(now)) return false;
            Released = true;
            return true;
        }
        if (slot < 0 || duration <= 0) return false;
        Slot = slot; Released = false; ExpiresAt = now + duration;
        return true;
    }

    public bool IsValid(float now) => Pending && now <= ExpiresAt;
    public void Clear() { Slot = -1; Released = false; ExpiresAt = 0; }
}
