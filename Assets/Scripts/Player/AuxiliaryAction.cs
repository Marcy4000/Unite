using System;
using UnityEngine;

/// <summary>A local binding to an authoritative action, reusable by passives and other features.</summary>
public sealed class AuxiliaryAction : IDisposable
{
    public string Label { get; }
    public Sprite Icon { get; }
    private readonly Action activate;
    private readonly Func<bool> available;
    private readonly Func<float> remaining;
    public float Cooldown { get; }
    public bool IsDisposed { get; private set; }
    public float Remaining => IsDisposed ? 0 : Mathf.Max(0, remaining());
    public bool Available => !IsDisposed && available() && Remaining <= 0;

    public AuxiliaryAction(string label, Sprite icon, float cooldown, Action activate, Func<bool> available, Func<float> remaining)
    {
        Label = label; Icon = icon; Cooldown = cooldown;
        this.activate = activate; this.available = available; this.remaining = remaining;
    }
    public void TryActivate() { if (Available) activate(); }
    public void Dispose() { IsDisposed = true; }
}
