using UnityEngine;

// Only animates authored transforms/particles. Never constructs geometry in a match.
public class FlygonMovePresentation : MonoBehaviour
{
    [SerializeField] private FlygonMoveObject effect;
    [SerializeField] private GameObject anticipation, active;
    [SerializeField] private Transform impactRing, swirl, tornadoShell;
    [SerializeField] private ParticleSystem sand;
    private Transform model;
    private Vector3 modelOrigin;
    private byte lastPhase = byte.MaxValue;
    private bool shellCaptured;
    private Vector3 shellScale;

    private void LateUpdate()
    {
        if (effect == null || !effect.IsClient || effect.PresentationActor == null) return;
        byte phase = effect.Phase;
        float age = Mathf.Max(0, effect.PhaseAge);
        if (anticipation != null) anticipation.SetActive(phase == 0);
        if (active != null) active.SetActive(phase > 0 && !(effect.Kind == FlygonCast.Unite && phase == 5));
        bool impact = phase > 0 && (effect.Kind == FlygonCast.Bite || effect.Kind == FlygonCast.Boomburst || effect.Kind == FlygonCast.Earthquake
            || (effect.Kind == FlygonCast.Dig && phase == 2) || (effect.Kind == FlygonCast.Unite && phase == 5));
        if (impactRing != null)
        {
            impactRing.gameObject.SetActive(impact && age < 0.4f);
            float radius = effect.Kind == FlygonCast.Boomburst ? 5 : effect.Kind == FlygonCast.Earthquake ? FlygonMoveRules.QuakeRadius
                : effect.Kind == FlygonCast.Unite ? FlygonMoveRules.UniteRadius : FlygonMoveRules.DigRadius;
            impactRing.localScale = new Vector3(radius, 1, radius) * Mathf.Lerp(0.15f, 1, age / 0.4f);
        }
        float power = effect.Kind == FlygonCast.Unite ? FlygonMoveRules.TornadoPower(Mathf.Clamp(phase - 1, 0, 3)) : 1;
        if (swirl != null)
        {
            swirl.localRotation = Quaternion.Euler(0, age * (phase == 0 ? 480 : 150 * power), 0);
            swirl.localScale = new Vector3(1, effect.Kind == FlygonCast.Unite ? Mathf.Lerp(0.35f, 1, power) : 1, 1);
        }
        if (tornadoShell != null)
        {
            if (!shellCaptured) { shellCaptured = true; shellScale = tornadoShell.localScale; }
            tornadoShell.gameObject.SetActive(phase < 5);
            float size = phase == 0 ? Mathf.Lerp(0.15f, 1, age / FlygonMoveRules.UniteFormation) : Mathf.Lerp(0.45f, 1, power);
            tornadoShell.localScale = shellScale * size;
        }
        if (phase != lastPhase)
        {
            lastPhase = phase;
            if (sand != null)
            {
                if (phase > 0 && phase < 5) sand.Play(true);
                else sand.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
        if (effect.Kind != FlygonCast.Dig && effect.Kind != FlygonCast.Earthquake) return;
        var body = effect.PresentationActor.Pokemon.ActiveModel;
        if (body == null) return;
        if (model != body.transform)
        {
            RestoreModel(); model = body.transform; modelOrigin = model.localPosition;
        }
        float offset = effect.Kind == FlygonCast.Earthquake ? phase == 0 ? Mathf.Sin(Mathf.Clamp01(age / FlygonMoveRules.QuakeLeap) * Mathf.PI) * 2.5f : 0
            : phase == 0 ? Mathf.Lerp(0, -2.5f, age / FlygonMoveRules.DigEnter) : phase == 1 ? -2.5f : Mathf.Lerp(-2.5f, 0, age / FlygonMoveRules.DigExit);
        model.localPosition = modelOrigin + Vector3.up * offset;
    }
    private void RestoreModel() { if (model != null) model.localPosition = modelOrigin; model = null; }
    private void OnDisable() { RestoreModel(); }
    private void OnDestroy() { RestoreModel(); }
}
