using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Server-owned tasks with bounded commitment. The regular player controllers
// still own movement, combat, cooldowns, recall and scoring.
public partial class BotBrain : MonoBehaviour
{
    public enum BotState { Waiting, Travel, Farm, Fight, Score, Retreat, Recover, Objective, Dead, Ended, Gank, Rotate, Recall }
    public BotState State { get; private set; }
    public string AssignedLane { get; private set; }
    private static readonly HashSet<BotBrain> activeBots = new();
    private readonly Dictionary<int, float> unreachableUntil = new();
    private readonly HashSet<WildPokemonSpawner> clearedJungle = new();
    private NavMeshPath path;
    private PlayerManager player;
    private BotMapContext map;
    private BotRosterConfig settings;
    private System.Random random;
    private Pokemon target;
    private WildPokemonSpawner camp;
    private GoalZone recoveryGoal;
    private Vector3 destination, pathDestination, home, stuckPosition;
    private Vector3[] corners = new Vector3[32];
    private int corner, cornerCount;
    private float nextDecision, nextPath, nextMove, nextStuckCheck, taskUntil, nextRotation, nextReposition;
    private bool recovering, firstGank, finalWasAlive, attendFinal;
    private BotMapContext.Camp finalCamp;

    private void Start()
    {
        player = GetComponent<PlayerManager>();
        if (!player.IsBot || !player.IsServer) { enabled = false; return; }
        path = new NavMeshPath();
        map = BotMapContext.Instance;
        settings = BotRosterConfig.Instance;
        random = new System.Random(System.Environment.TickCount ^ GetInstanceID());
        AssignedLane = LobbyController.Instance.BotRoster.GetBotLane(player.LobbyPlayer.Id);
        home = SpawnpointManager.Instance.GetSpawnpoint(player.CurrentTeam.Team,
            NumberEncoder.FromBase64<short>(player.LobbyPlayer.Data["PlayerPos"].Value)).position;
        if (map == null) { Debug.LogError("Bots require an authored BotMapContext in this map.", this); enabled = false; return; }
        foreach (var entry in map.camps) if (entry.kind == BotMapContext.CampKind.FinalObjective) finalCamp = entry;
        activeBots.Add(this);
        player.OnRespawn += ResetAfterRespawn;
        nextRotation = Time.time + 55 + random.Next(20);
        ResetAfterRespawn();
    }

    private void OnDestroy()
    {
        activeBots.Remove(this);
        if (player != null) player.OnRespawn -= ResetAfterRespawn;
    }

    private void ResetAfterRespawn()
    {
        target = null; camp = null; recoveryGoal = null;
        pendingMove = null;
        cornerCount = 0; unreachableUntil.Clear();
        nextDecision = nextPath = nextMove = taskUntil = 0;
        recovering = false;
        stuckPosition = transform.position; nextStuckCheck = Time.time + 3;
        player.SetBotMovement(Vector2.zero);
    }

    private void Update()
    {
        if (player == null || !player.IsSpawned || GameManager.Instance == null) return;
        player.TickBotLifecycle();
        UpdateFinalDecision(); // Observe spawn edges even while dead.
        if (GameManager.Instance.GameState != GameState.Playing || player.PlayerState == PlayerState.Dead)
        {
            State = player.PlayerState == PlayerState.Dead ? BotState.Dead :
                GameManager.Instance.GameState == GameState.Ended ? BotState.Ended : BotState.Waiting;
            player.SetBotMovement(Vector2.zero); target = null; camp = null; return;
        }
        if (Time.time >= nextDecision) { nextDecision = Time.time + .25f; Decide(); }
        TickMoveRelease();
        if (player.BotIsRecalling || State == BotState.Recall) { player.SetBotMovement(Vector2.zero); player.TickBotScoring(false); return; }
        bool combat = target != null && target.CurrentHp > 0;
        player.Aim.SetBotAim(combat ? target : null, combat ? target.transform.position : destination);
        player.TickBotScoring(State == BotState.Score);
        if (combat) Fight();
        else if (player.IsScoring) player.SetBotMovement(Vector2.zero);
        else Steer();
        CheckStuck();
    }

    private PlayerManager ClosestEnemy(float radius)
    {
        PlayerManager result = null;
        float closest = radius;
        foreach (var other in GameManager.Instance.Players)
        {
            if (other == null || other == player || other.Pokemon == null || other.Pokemon.CurrentHp <= 0 || other.CurrentTeam.Team == player.CurrentTeam.Team) continue;
            float distance = FlatDistance(transform.position, other.transform.position);
            if (distance >= closest || !player.Aim.CanPokemonBeTargeted(other.gameObject, AimTarget.Enemy, player.CurrentTeam, false)) continue;
            closest = distance; result = other;
        }
        return result;
    }

    private void Decide()
    {
        float health = player.Pokemon.CurrentHp / (float)Mathf.Max(1, player.Pokemon.GetMaxHp());
        var enemy = ClosestEnemy(settings != null ? settings.safeRecallEnemyDistance : 13);
        TryBattleItem(health, enemy);
        if (health < (settings != null ? settings.recoverBelow : .3f)) recovering = true;
        if (recovering && health >= (settings != null ? settings.resumeAbove : .58f)) { recovering = false; recoveryGoal = null; }
        if (recovering && FlatDistance(transform.position, home) < 5)
        {
            player.CancelBotRecall(); State = BotState.Recover; target = null; camp = null;
            destination = home; return;
        }
        if (health < (settings != null ? settings.recallBelow : .14f))
        {
            target = null; camp = null;
            if (enemy == null)
            {
                State = BotState.Recall; player.SetBotMovement(Vector2.zero); player.TryBotRecall(); return;
            }
            player.CancelBotRecall(); State = BotState.Retreat;
            destination = BotMapContext.Sample(transform.position + (transform.position - enemy.transform.position).normalized * 8, home);
            return;
        }
        player.CancelBotRecall();
        if (recovering)
        {
            recoveryGoal = FindHealingGoal();
            if (recoveryGoal != null)
            {
                State = FlatDistance(transform.position, recoveryGoal.transform.position) < RecoveryRadius() ? BotState.Recover : BotState.Retreat;
                destination = recoveryGoal.transform.position;
                target = State == BotState.Recover ? FindRecoveryTarget(enemy) : null;
                camp = null; return;
            }
            // No functioning goal: retreat into safety, then channel normally.
            target = null; camp = null;
            if (enemy == null) { State = BotState.Recall; player.SetBotMovement(Vector2.zero); player.TryBotRecall(); }
            else { State = BotState.Retreat; destination = home; }
            return;
        }
        if (finalWasAlive && attendFinal)
        {
            camp = finalCamp.spawner;
            destination = camp.transform.position;
            State = BotState.Objective;
            target = enemy != null && FlatDistance(enemy.transform.position, destination) < 14 ? enemy.Pokemon :
                camp.WildPokemon != null ? camp.WildPokemon.Pokemon : null;
            return;
        }
        if (enemy != null && FlatDistance(enemy.transform.position, transform.position) < 10)
        {
            target = enemy.Pokemon; destination = target.transform.position; State = BotState.Fight; return;
        }
        target = null;
        if ((State == BotState.Gank || State == BotState.Rotate) && Time.time < taskUntil && FlatDistance(destination, transform.position) > 2) return;
        if (AssignedLane == "mid" && !firstGank && FirstJungleCleared())
        {
            firstGank = true; camp = null; State = BotState.Gank;
            destination = map.LaneFront(player.CurrentTeam.Team, ChooseGankLane(), home);
            taskUntil = Time.time + 12; return;
        }
        GoalZone scoring = FindScoringGoal();
        if (scoring != null && player.CurrentEnergy >= 10 && (player.IsEnergyFull() || FlatDistance(scoring.transform.position, transform.position) < 12))
        {
            State = BotState.Score; camp = null; destination = scoring.transform.position; return;
        }
        bool rotate = Time.time >= nextRotation && GameManager.Instance.GameTime > 50;
        if (!rotate && camp != null && camp.HasLivingPokemon && camp.WildPokemon != null &&
            camp.WildPokemon.Pokemon.CurrentHp > 0 && Time.time < taskUntil && !IsTemporarilyUnreachable(camp.GetInstanceID()))
        {
            target = camp.WildPokemon.Pokemon; destination = camp.transform.position;
            State = camp.IsObjective ? BotState.Objective : BotState.Farm; return;
        }
        camp = ChooseCamp(rotate);
        if (camp != null)
        {
            destination = camp.transform.position;
            target = camp.WildPokemon != null && camp.WildPokemon.Pokemon.CurrentHp > 0 ? camp.WildPokemon.Pokemon : null;
            State = camp.IsObjective ? BotState.Objective : BotState.Farm;
            taskUntil = Time.time + (settings != null ? settings.taskCommitmentSeconds : 8);
            if (rotate) nextRotation = Time.time + 35 + random.Next(25);
            return;
        }
        camp = null;
        State = rotate || AssignedLane == "mid" ? BotState.Rotate : BotState.Travel;
        Vector3 anchor = State == BotState.Rotate ? new Vector3(player.CurrentTeam.Team == Team.Blue ? -9 : 9, 0, random.Next(-12, 13)) :
            map.LaneFront(player.CurrentTeam.Team, AssignedLane, home);
        // Small, useful patrols around the current front, not fixed invalid
        // interpolations under a destroyed goal or waiting at an empty spawner.
        destination = BotMapContext.Sample(anchor + new Vector3(random.Next(-3, 4), 0, random.Next(-3, 4)), home);
        taskUntil = Time.time + 4;
        if (rotate) nextRotation = Time.time + 35 + random.Next(25);
    }

    private void UpdateFinalDecision()
    {
        bool alive = finalCamp != null && finalCamp.spawner != null && finalCamp.spawner.HasLivingPokemon;
        if (alive && !finalWasAlive) attendFinal = random.NextDouble() < (settings != null ? settings.finalObjectiveAttendance : .97f);
        finalWasAlive = alive;
    }

    private GoalZone FindHealingGoal()
    {
        GoalZone result = null; float best = float.PositiveInfinity;
        foreach (var goal in map.goals)
            if (goal != null && goal.gameObject.activeInHierarchy && goal.ProvidesDefense && goal.Team == player.CurrentTeam.Team)
            {
                float distance = FlatDistance(transform.position, goal.transform.position);
                if (distance < best) { best = distance; result = goal; }
            }
        return result;
    }
    private GoalZone FindScoringGoal()
    {
        GoalZone result = null; float best = float.PositiveInfinity;
        foreach (var goal in map.goals)
            if (goal != null && goal.gameObject.activeInHierarchy && goal.IsActive && goal.GoalStatus != GoalStatus.Destroyed && goal.Team != player.CurrentTeam.Team && !IsTemporarilyUnreachable(goal.GetInstanceID()))
            {
                float distance = FlatDistance(transform.position, goal.transform.position);
                if (distance < best) { best = distance; result = goal; }
            }
        return result;
    }
    private float RecoveryRadius()
    {
        var collider = recoveryGoal != null ? recoveryGoal.GetComponent<Collider>() : null;
        return collider != null ? Mathf.Max(.5f, Mathf.Min(collider.bounds.extents.x, collider.bounds.extents.z) * .7f) : 2;
    }
    private Pokemon FindRecoveryTarget(PlayerManager enemy)
    {
        float range = player.MovesController.BasicAttack?.range ?? 2;
        if (enemy != null && FlatDistance(transform.position, enemy.transform.position) < range + 1) return enemy.Pokemon;
        Pokemon result = null; float best = range;
        foreach (var entry in map.camps)
        {
            var wild = entry.spawner != null ? entry.spawner.WildPokemon : null;
            if (wild == null || wild.Pokemon.CurrentHp <= 0 || !player.Aim.CanPokemonBeTargeted(wild.gameObject, AimTarget.NonAlly, player.CurrentTeam, false)) continue;
            float distance = FlatDistance(transform.position, wild.transform.position);
            if (distance < best) { best = distance; result = wild.Pokemon; }
        }
        return result;
    }
    private bool FirstJungleCleared()
    {
        bool any = false, complete = true;
        foreach (var entry in map.camps)
        {
            if (entry.kind != BotMapContext.CampKind.Jungle || entry.team != player.CurrentTeam.Team || entry.spawner == null) continue;
            any = true;
            if (entry.spawner.HasSpawnedOnce && !entry.spawner.HasLivingPokemon) clearedJungle.Add(entry.spawner);
            if (!clearedJungle.Contains(entry.spawner)) complete = false;
        }
        return (any && complete) || GameManager.Instance.GameTime > 85;
    }
    private string ChooseGankLane()
    {
        float top = (float)random.NextDouble(), bottom = (float)random.NextDouble();
        foreach (var ally in GameManager.Instance.Players)
        {
            if (ally == null || ally == player || ally.CurrentTeam.Team != player.CurrentTeam.Team || ally.Pokemon.CurrentHp <= 0) continue;
            float need = 1 - ally.Pokemon.CurrentHp / (float)Mathf.Max(1, ally.Pokemon.GetMaxHp());
            if (!ally.IsBot) need += .5f;
            if (ally.transform.position.z > 8) top += need; else if (ally.transform.position.z < -8) bottom += need;
        }
        return top >= bottom ? "top" : "bottom";
    }
    private WildPokemonSpawner ChooseCamp(bool rotate)
    {
        WildPokemonSpawner result = null; float best = float.PositiveInfinity;
        foreach (var entry in map.camps)
        {
            var spawner = entry.spawner;
            if (spawner == null || !spawner.HasLivingPokemon || spawner.WildPokemon == null || spawner.WildPokemon.Pokemon.CurrentHp <= 0 || IsTemporarilyUnreachable(spawner.GetInstanceID())) continue;
            if (entry.kind == BotMapContext.CampKind.FinalObjective) continue;
            if (entry.team != Team.Neutral && entry.team != player.CurrentTeam.Team) continue;
            bool ownLane = entry.lane == AssignedLane;
            if (entry.kind == BotMapContext.CampKind.Opening && (!ownLane || entry.team != player.CurrentTeam.Team)) continue;
            if (entry.kind == BotMapContext.CampKind.Jungle && AssignedLane != "mid") continue;
            if (entry.kind == BotMapContext.CampKind.Lane && !ownLane && !rotate) continue;
            if (entry.kind == BotMapContext.CampKind.Center && GameManager.Instance.GameTime < 45) continue;
            if (entry.kind == BotMapContext.CampKind.Objective && GameManager.Instance.GameTime < 170) continue;
            bool reserved = false;
            foreach (var bot in activeBots)
                if (bot != this && bot.player.CurrentTeam.Team == player.CurrentTeam.Team && bot.camp == spawner) { reserved = true; break; }
            if (reserved && !spawner.IsObjective) continue;
            float cost = FlatDistance(transform.position, spawner.transform.position);
            if (entry.kind == BotMapContext.CampKind.Opening) cost -= 100;
            if (rotate && entry.kind == BotMapContext.CampKind.Center) cost -= 60;
            if (AssignedLane == "mid" && !firstGank && entry.kind != BotMapContext.CampKind.Jungle) cost += 100;
            if (cost < best) { best = cost; result = spawner; }
        }
        return result;
    }

    private void Fight()
    {
        if (target == null || target.CurrentHp <= 0 || !target.IsSpawned) { nextDecision = 0; return; }
        float distance = FlatDistance(target.transform.position, transform.position);
        float range = player.MovesController.BasicAttack?.range ?? 2.5f;
        if (State == BotState.Recover)
        {
            Vector3 delta = target.transform.position - recoveryGoal.transform.position; delta.y = 0;
            destination = recoveryGoal.transform.position + Vector3.ClampMagnitude(delta, RecoveryRadius());
        }
        else destination = target.transform.position;
        if (distance > Mathf.Max(.8f, range * .85f)) Steer();
        else if (Time.time >= nextReposition)
        {
            nextReposition = Time.time + 1.5f + (float)random.NextDouble();
            Vector3 side = Vector3.Cross(Vector3.up, target.transform.position - transform.position).normalized;
            destination = transform.position + side * (random.Next(2) == 0 ? -1 : 1);
            if (State == BotState.Recover) destination = recoveryGoal.transform.position + Vector3.ClampMagnitude(destination - recoveryGoal.transform.position, RecoveryRadius());
            destination = BotMapContext.Sample(destination, transform.position);
            Steer();
        }
        else player.SetBotMovement(Vector2.zero);
        if (distance <= range) player.MovesController.TryPerformingBasicAttack(target.Type == PokemonType.Wild);
        // Legacy dashes/channelled moves cannot guarantee a landing within the
        // healing circle. Keep recovery combat to ordinary attacks, not lunges.
        if (State == BotState.Recover) return;
        if (distance > 7 || Time.time < nextMove) return;
        nextMove = Time.time + .7f;
        int slot = random.Next(2);
        if (pendingMove != null) return;
        if (target.Type == PokemonType.Player && random.Next(3) == 0) BeginBotMove(2);
        else BeginBotMove(slot);
    }
    private void Steer()
    {
        if (FlatDistance(destination, transform.position) < .25f) { player.SetBotMovement(Vector2.zero); return; }
        if (Time.time >= nextPath || FlatDistance(pathDestination, destination) > 2)
        {
            nextPath = Time.time + .75f; pathDestination = destination;
            if (!NavMesh.SamplePosition(transform.position, out var start, 5, NavMesh.AllAreas) || !NavMesh.SamplePosition(destination, out var end, 5, NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                cornerCount = 0;
                if (camp != null) unreachableUntil[camp.GetInstanceID()] = Time.time + 8;
                else if (player.GoalZone != null) unreachableUntil[player.GoalZone.GetInstanceID()] = Time.time + 8;
                taskUntil = 0; nextDecision = 0;
            }
            else
            {
                cornerCount = path.GetCornersNonAlloc(corners);
                while (cornerCount == corners.Length) { corners = new Vector3[corners.Length * 2]; cornerCount = path.GetCornersNonAlloc(corners); }
                corner = cornerCount > 1 ? 1 : 0;
            }
        }
        if (cornerCount == 0) { player.SetBotMovement(Vector2.zero); return; }
        while (corner < cornerCount - 1 && FlatDistance(corners[corner], transform.position) < .5f) corner++;
        Vector3 direction = corners[corner] - transform.position;
        player.SetBotMovement(new Vector2(direction.x, direction.z).normalized);
    }
    private void CheckStuck()
    {
        if (Time.time < nextStuckCheck) return;
        nextStuckCheck = Time.time + 3;
        if (player.PlayerMovement.CanMove && player.MovementInput.sqrMagnitude > .1f && FlatDistance(stuckPosition, transform.position) < .3f)
        {
            if (camp != null) unreachableUntil[camp.GetInstanceID()] = Time.time + 8;
            camp = null; target = null; cornerCount = 0; taskUntil = 0; nextDecision = nextPath = 0;
        }
        stuckPosition = transform.position;
    }
    private bool IsTemporarilyUnreachable(int id) => unreachableUntil.TryGetValue(id, out var until) && Time.time < until;
    private static float FlatDistance(Vector3 first, Vector3 second) { first.y = second.y = 0; return Vector3.Distance(first, second); }
}
