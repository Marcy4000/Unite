using UnityEngine;
using UnityEngine.AI;

// Shared actuator only. Special modes choose their own tasks, never lane tasks,
// goals, recall, farm rotations or final-objective attendance.
public abstract class BotModeBrain : MonoBehaviour
{
    protected PlayerManager player;
    protected System.Random random;
    protected Vector3 destination;
    protected Pokemon target;
    protected bool anchored;
    public string Task { get; protected set; } = "Waiting";
    private NavMeshPath path;
    private Vector3[] corners = new Vector3[32];
    private int corner, count;
    private Vector3 planned, lastPosition;
    private float nextPath, nextDecision, nextCast, releaseAt, stuckAt;
    private MoveBase pending;
    private int pendingSlot;
    protected virtual bool CanAct => true;
    protected virtual bool UsesNavigation => true;

    protected virtual void Start()
    {
        player = GetComponent<PlayerManager>();
        if (player == null || !player.IsBot || !player.IsServer) { enabled = false; return; }
        random = new System.Random(player.LobbyPlayer.Id.GetHashCode());
        path = new NavMeshPath(); destination = lastPosition = transform.position;
        player.OnRespawn += ResetTravel;
    }
    protected virtual void OnDestroy() { if (player != null) player.OnRespawn -= ResetTravel; }
    protected void ResetTravel() { count = 0; nextPath = nextDecision = 0; pending = null; lastPosition = transform.position; }
    private void Update()
    {
        if (player == null || !player.IsSpawned || GameManager.Instance == null) return;
        player.TickBotLifecycle();
        if (GameManager.Instance.GameState != GameState.Playing || player.PlayerState != PlayerState.Alive || !CanAct)
        {
            player.SetBotMovement(Vector2.zero); target = null; count = 0;
            if (pending != null && pending.IsActive) pending.Cancel(); pending = null;
            return;
        }
        if (Time.time >= nextDecision) { nextDecision = Time.time + .2f; Decide(); }
        player.Aim.SetBotAim(target, target != null ? target.transform.position : destination);
        ReleaseMove();
        Drive();
        if (target != null && target.CurrentHp > 0) Combat();
    }
    protected abstract void Decide();
    protected virtual void Drive()
    {
        if (!UsesNavigation) { SetDirection(destination - transform.position); return; }
        if (Distance(destination, transform.position) < .4f) { player.SetBotMovement(Vector2.zero); return; }
        if (Time.time >= nextPath || Distance(planned, destination) > 1.5f)
        {
            nextPath = Time.time + .5f; planned = destination; count = 0;
            if (NavMesh.SamplePosition(transform.position, out var start, 4, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(destination, out var end, 4, NavMesh.AllAreas) &&
                NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
            {
                count = path.GetCornersNonAlloc(corners);
                while (count == corners.Length) { corners = new Vector3[corners.Length * 2]; count = path.GetCornersNonAlloc(corners); }
                corner = count > 1 ? 1 : 0;
            }
        }
        if (count == 0) { player.SetBotMovement(Vector2.zero); OnUnreachable(); return; }
        while (corner < count - 1 && Distance(corners[corner], transform.position) < .6f) corner++;
        SetDirection(corners[corner] - transform.position);
        if (Time.time >= stuckAt)
        {
            if (Distance(lastPosition, transform.position) < .25f && player.PlayerMovement.CanMove) { nextDecision = nextPath = 0; OnUnreachable(); }
            lastPosition = transform.position; stuckAt = Time.time + 3;
        }
    }
    protected void SetDirection(Vector3 direction) => player.SetBotMovement(new Vector2(direction.x, direction.z).normalized);
    protected virtual void OnUnreachable() { }
    protected PlayerManager Enemy(float radius)
    {
        PlayerManager result = null; float best = radius;
        foreach (var other in GameManager.Instance.Players)
        {
            if (other == null || other == player || other.PlayerState != PlayerState.Alive || other.Pokemon.CurrentHp <= 0 || other.CurrentTeam.Team == player.CurrentTeam.Team) continue;
            float d = Distance(transform.position, other.transform.position);
            if (d < best && player.Aim.CanPokemonBeTargeted(other.gameObject, AimTarget.Enemy, player.CurrentTeam, false)) { best = d; result = other; }
        }
        return result;
    }
    private void Combat()
    {
        float distance = Distance(target.transform.position, transform.position);
        var moves = player.MovesController;
        if (distance <= (moves.BasicAttack?.range ?? 2)) moves.TryPerformingBasicAttack(target.Type == PokemonType.Wild || target.Type == PokemonType.Objective);
        float health = player.Pokemon.CurrentHp / (float)Mathf.Max(1, player.Pokemon.GetMaxHp());
        bool item = moves.BattleItem is Potion ? health < .6f : moves.BattleItem is XAttack ? distance < 6 :
            moves.BattleItem is XSpeed ? !anchored && distance > 4 : moves.BattleItem is EjectButton ? !anchored && health < .2f && target.Type == PokemonType.Player : false;
        if (item && moves.BattleItemStatus.HasStatus(ActionStatusType.None))
        {
            if (moves.BattleItem is EjectButton) player.Aim.SetBotAim(null, transform.position + (transform.position - target.transform.position).normalized * 5);
            moves.TryUsingBattleItem(); moves.BattleItem.Update(); moves.TryFinishingBattleItem();
        }
        // Strict coverage cannot safely cast arbitrary legacy lunges or Unites.
        if (anchored || pending != null || distance > 7 || Time.time < nextCast) return;
        nextCast = Time.time + .8f;
        int slot = random.Next(target.Type == PokemonType.Player ? 3 : 2);
        UseMove(slot);
    }
    protected void UseMove(int slot)
    {
        if (pending != null) return;
        var moves = player.MovesController;
        if (slot == 2) moves.TryUsingUniteMove(); else moves.TryUsingMove(slot);
        var move = moves.GetMove(slot == 2 ? MoveType.UniteMove : slot == 0 ? MoveType.MoveA : MoveType.MoveB);
        if (move == null) return;
        if (!move.IsActive) { if (slot == 2) moves.TryFinishingUniteMove(); else moves.TryFinishingMove(slot); return; }
        pending = move; pendingSlot = slot;
        releaseAt = Time.time + (move is GlaceIcicleSpear ? 10 : move is DuraDragonPulse ? 1.2f : .15f);
    }
    private void ReleaseMove()
    {
        if (pending == null) return;
        var current = player.MovesController.GetMove(pendingSlot == 2 ? MoveType.UniteMove : pendingSlot == 0 ? MoveType.MoveA : MoveType.MoveB);
        if (current != pending) { if (pending.IsActive) pending.Cancel(); pending = null; return; }
        if (!pending.IsActive) { pending = null; return; }
        if (anchored) { pending.Cancel(); pending = null; return; }
        if (Time.time < releaseAt) return;
        if (pendingSlot == 2) player.MovesController.TryFinishingUniteMove(); else player.MovesController.TryFinishingMove(pendingSlot);
        pending = null;
    }
    protected static float Distance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }
}
