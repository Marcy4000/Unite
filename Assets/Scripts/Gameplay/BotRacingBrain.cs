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
        while (guide < segment.guides.Length && Distance(transform.position, segment.guides[guide]) < 1) guide++;
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
        Vector3 side = Vector3.Cross(Vector3.up, destination - transform.position).normalized;
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
    protected override void Drive()
    {
        Vector3 direction = destination - transform.position; direction.y = 0;
        float alignment = Vector3.Dot(transform.forward, direction.normalized);
        // Brake through tight bends without changing the human racing physics.
        player.SetBotMovement(new Vector2(direction.x, direction.z).normalized * Mathf.Lerp(.25f, 1, Mathf.Clamp01(alignment)));
    }
}
