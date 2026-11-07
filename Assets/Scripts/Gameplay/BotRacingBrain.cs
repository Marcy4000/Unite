using UnityEngine;

public class BotRacingBrain : BotModeBrain
{
    private RaceLapCounter counter;
    private BotRaceRoute route;
    private int previousCheckpoint = -1, previousLap = -1, guide;
    private float nextItem;
    private float routeOffset;
    private Vector3 lastProgress;
    private float progressAt;
    private Vector3 recoveryDestination;
    private float recoveryUntil;
    protected override bool UsesNavigation => false;
    protected override bool CanAct => counter != null && !counter.RaceFinished;
    protected override void Start()
    {
        base.Start(); route = BotRaceRoute.Instance;
        if (!enabled) return;
        routeOffset = ((float)random.NextDouble() - .5f) * .6f;
        if (route == null || route.segments.Length == 0) { Debug.LogError("Race bots need an authored BotRaceRoute.", this); enabled = false; }
    }
    protected override void Decide()
    {
        target = null; anchored = false;
        if (counter == null || counter.RaceFinished || route == null) return;
        Task = "Race";
        int next = (counter.CheckpointCount + 1) % route.segments.Length;
        var segment = route.Next(next);
        if (segment == null) { player.SetBotMovement(Vector2.zero); return; }
        if (previousCheckpoint != counter.CheckpointCount || previousLap != counter.LapCount)
        { guide = 0; previousCheckpoint = counter.CheckpointCount; previousLap = counter.LapCount; }
        while (guide < segment.guides.Length)
        {
            var previous = guide > 0 ? segment.guides[guide - 1] :
                route.Next(counter.CheckpointCount).checkpoint.transform.position;
            if (!HasReachedGuide(transform.position, previous, segment.guides[guide])) break;
            guide++;
        }
        bool stalled = false;
        if (Time.time >= progressAt)
        {
            stalled = Distance(lastProgress, transform.position) < .5f &&
                player.PassiveController.Passive is PsyduckRacePassive racer && racer.CanMove && !player.PlayerMovement.IsKnockedUp;
            if (stalled) guide = Mathf.Max(0, guide - 1);
            lastProgress = transform.position; progressAt = Time.time + 3;
        }
        Vector3 center = segment.checkpoint.transform.position;
        Vector3 approach = segment.guides.Length > 0 ? center - segment.guides[segment.guides.Length - 1] : center - transform.position;
        approach.y = 0;
        destination = guide < segment.guides.Length ? segment.guides[guide] : center;
        if (guide >= segment.guides.Length && Distance(transform.position, center) < 3) destination = center + approach.normalized * 4;
        Vector3 legStart = guide > 0 ? segment.guides[guide - 1] : route.Next(counter.CheckpointCount).checkpoint.transform.position;
        Vector3 side = Vector3.Cross(Vector3.up, destination - legStart).normalized;
        if (Distance(transform.position, center) > 4) destination += side * routeOffset;
        // The track's rotating physical obstacles are not represented by its
        // baked NavMesh. Steer around a snag instead of pushing forever or
        // advancing checkpoints artificially. Bias the sidestep inbounds.
        if (stalled)
        {
            recoveryDestination = RecoveryTarget(transform.position, destination);
            recoveryUntil = Time.time + 1.5f;
        }
        if (Time.time < recoveryUntil) destination = recoveryDestination;
        if (Time.time >= nextItem)
        {
            nextItem = Time.time + 1.5f + (float)random.NextDouble() * 3;
            int slot = random.Next(2);
            if (slot == 1 || Vector3.Dot(transform.forward, (destination - transform.position).normalized) > .85f) UseMove(slot);
        }
    }
    internal static Vector3 RecoveryTarget(Vector3 position, Vector3 goal)
    {
        Vector3 forward = goal - position; forward.y = 0; forward.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        if (Vector3.Dot(side, -position) < 0) side = -side;
        return position + side * 6 + forward * 2;
    }
    private void LateUpdate()
    {
        if (player != null && counter == null && RaceManager.Instance != null)
            RaceManager.Instance.PlayerLapCounters.TryGetValue(player.NetworkObjectId, out counter);
    }
    internal static bool HasReachedGuide(Vector3 position, Vector3 previous, Vector3 goal)
    {
        Vector3 leg = goal - previous; leg.y = 0;
        Vector3 offset = position - goal; offset.y = 0;
        if (offset.sqrMagnitude < 1.5f * 1.5f) return true;
        // Accept a small overshoot along the authored leg. Never skip a guide
        // from across the track or advance a checkpoint without its trigger.
        return leg.sqrMagnitude > .01f && Vector3.Dot(offset, leg) >= 0 &&
            offset.magnitude < 4 && Vector3.Cross(offset, leg.normalized).magnitude < 2;
    }

    internal static Vector2 SteeringInput(Vector3 forward, Vector3 direction)
    {
        direction.y = 0; forward.y = 0;
        if (direction.sqrMagnitude < .01f) return Vector2.zero;
        float alignment = Mathf.Clamp01(Vector3.Dot(forward.normalized, direction.normalized));
        // Slow before reaching the target as well as while turning. Otherwise
        // momentum carries the bot past a guide and it orbits the missed point.
        float throttle = Mathf.Min(Mathf.Lerp(.15f, 1, alignment * alignment),
            Mathf.Lerp(.15f, 1, Mathf.Clamp01(direction.magnitude / 6)));
        return new Vector2(direction.x, direction.z).normalized * throttle;
    }

    protected override void Drive()
    {
        player.SetBotMovement(SteeringInput(transform.forward, destination - transform.position));
    }
}
