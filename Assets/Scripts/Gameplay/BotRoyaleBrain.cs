using UnityEngine;

public class BotRoyaleBrain : BotModeBrain
{
    private BotArenaContext context;
    private WildPokemonSpawner objective;
    private float committedUntil;
    private readonly System.Collections.Generic.Dictionary<int, float> blocked = new();
    protected override void Start()
    {
        base.Start(); context = BotArenaContext.Instance;
        if (enabled && context == null) { Debug.LogError("Royale bots need an authored BotArenaContext.", this); enabled = false; }
    }
    protected override void Decide()
    {
        anchored = false;
        var enemy = Enemy(12);
        float health = player.Pokemon.CurrentHp / (float)Mathf.Max(1, player.Pokemon.GetMaxHp());
        if (enemy != null && health < .18f)
        { Task = "Disengage"; target = null; destination = transform.position + (transform.position - enemy.transform.position).normalized * 8; committedUntil = 0; return; }
        if (enemy != null)
        {
            Task = "Fight"; target = enemy.Pokemon;
            destination = Distance(transform.position, enemy.transform.position) > (player.MovesController.BasicAttack?.range ?? 2) * .8f ? enemy.transform.position : transform.position;
            return;
        }
        if (objective != null && objective.HasLivingPokemon && objective.WildPokemon != null && Time.time < committedUntil)
        { AimObjective(); return; }
        target = null; objective = null;
        if (Time.time < committedUntil && Distance(transform.position, destination) > 2) return;
        if (health > .3f && random.NextDouble() < .65)
        {
            float best = float.PositiveInfinity;
            foreach (var entry in context.objectives)
            {
                if (entry == null || !entry.HasLivingPokemon || entry.WildPokemon == null || entry.WildPokemon.Pokemon.CurrentHp <= 0 ||
                    (blocked.TryGetValue(entry.GetInstanceID(), out float until) && Time.time < until)) continue;
                float cost = Distance(transform.position, entry.transform.position) + (float)random.NextDouble() * 10;
                if (cost < best) { objective = entry; best = cost; }
            }
        }
        committedUntil = Time.time + 8 + random.Next(6);
        if (objective != null) { AimObjective(); return; }
        Task = "Wander";
        destination = context.patrolPoints.Length > 0 ? context.patrolPoints[random.Next(context.patrolPoints.Length)] : transform.position;
    }
    private void AimObjective()
    {
        Task = "Secure objective"; target = objective.WildPokemon.Pokemon;
        destination = Distance(transform.position, target.transform.position) > (player.MovesController.BasicAttack?.range ?? 2) * .8f ? target.transform.position : transform.position;
    }
    protected override void OnUnreachable()
    {
        if (objective != null) blocked[objective.GetInstanceID()] = Time.time + 10;
        objective = null; target = null; committedUntil = 0;
    }
}
