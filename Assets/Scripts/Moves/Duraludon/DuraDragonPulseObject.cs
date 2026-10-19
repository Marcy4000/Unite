using System.Collections;
using UnityEngine;

public class DuraDragonPulseObject : DuraMoveObject
{
    [SerializeField] private SphereCollider pulseArea;
    [SerializeField] private Transform pulseWave;
    [SerializeField] private float effectDuration = 0.35f;

    protected override void Configure()
    {
        pulseArea.radius = DuraMoveRules.PulseRadius(Power);
    }

    protected override void BeginPresentation()
    {
        StartCoroutine(ExpandWave());
    }

    private IEnumerator ExpandWave()
    {
        float started = Time.time;
        while (Time.time - started < effectDuration)
        {
            float radius = pulseArea.radius * Mathf.Clamp01((Time.time - started) / effectDuration);
            pulseWave.localScale = new Vector3(radius, 1, radius);
            yield return null;
        }
    }

    protected override IEnumerator Execute()
    {
        bool knockedOut = false;
        int playersKnockedOut = 0;
        foreach (Pokemon target in TargetsIn(pulseArea))
        {
            int marks = Combat != null ? Combat.ConsumePulseMarks(target) : 0;
            target.TakeDamageRPC(DuraMoveRules.PulseDamage(Actor.NetworkObjectId, Power, marks, target.GetMaxHp() - target.CurrentHp));
            if (target.CurrentHp <= 0)
            {
                knockedOut = true;
                if (target.Type == PokemonType.Player) playersKnockedOut++;
            }
        }
        if (knockedOut && Combat != null) Combat.GrantPulseKnockoutBonus(Upgraded, playersKnockedOut);
        yield return new WaitForSeconds(effectDuration);
    }
}
