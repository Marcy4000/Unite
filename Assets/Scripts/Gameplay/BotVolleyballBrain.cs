using UnityEngine;

public class BotVolleyballBrain : BotModeBrain
{
    public bool IsPrimaryDefender { get; private set; }
    public bool IsBackupDefender { get; private set; }
    protected override bool CanAct => VolleyballManager.Instance != null &&
        (VolleyballManager.Instance.CurrentState == VolleyballState.Playing || VolleyballManager.Instance.CanBotsApproach);
    protected override void Decide()
    {
        var game = VolleyballManager.Instance;
        Team team = player.CurrentTeam.Team;
        var ball = game.Ball;
        var zone = team == Team.Blue ? game.BlueLandSpot : game.OrangeLandSpot;
        Vector3 anchor = ball.IncomingTeam == team ? ball.LandingPosition : game.DefensiveAnchor(team);
        PlayerManager primary = null, backup = null, server = null;
        foreach (var other in GameManager.Instance.Players)
        {
            if (other == null || !other.IsBot || other.CurrentTeam.Team != team) continue;
            if (primary == null || string.CompareOrdinal(other.LobbyPlayer.Id, primary.LobbyPlayer.Id) < 0) primary = other;
        }
        bool needsBackup = primary != null && Unite.Multiplayer.BotModeRules.NeedsBackup(
            primary.PlayerState == PlayerState.Alive && primary.Pokemon.CurrentHp > 0, primary.PlayerMovement.CanMove,
            ball.IncomingTeam == team, Distance(primary.transform.position, anchor), zone.CoverageRadius, ball.RemainingFlightTime);
        foreach (var other in GameManager.Instance.Players)
        {
            if (other == null || !other.IsBot || other == primary || other.CurrentTeam.Team != team || other.PlayerState != PlayerState.Alive || other.Pokemon.CurrentHp <= 0) continue;
            if (server == null || string.CompareOrdinal(other.LobbyPlayer.Id, server.LobbyPlayer.Id) < 0) server = other;
            if (!other.PlayerMovement.CanMove && Distance(other.transform.position, anchor) > zone.CoverageRadius) continue;
            if (backup == null || Distance(other.transform.position, anchor) < Distance(backup.transform.position, anchor) - .1f ||
                (Mathf.Abs(Distance(other.transform.position, anchor) - Distance(backup.transform.position, anchor)) <= .1f && string.CompareOrdinal(other.LobbyPlayer.Id, backup.LobbyPlayer.Id) < 0)) backup = other;
        }
        IsPrimaryDefender = primary == player; IsBackupDefender = needsBackup && backup == player;
        anchored = IsPrimaryDefender || IsBackupDefender;
        // Use the opening hallways immediately; do not fight before the rally.
        var enemy = game.CanBotsApproach ? null : Enemy(anchored ? (player.MovesController.BasicAttack?.range ?? 2) : 16);
        if (anchored && enemy != null && player.BotLoadout != null && player.BotLoadout.pokemon.Range == PokemonRange.Melee &&
            Distance(enemy.transform.position, anchor) > zone.CoverageRadius) enemy = null;
        target = enemy != null ? enemy.Pokemon : null;
        if (anchored)
        {
            Task = IsPrimaryDefender ? "Defend landing" : "Backup landing";
            destination = anchor; // Combat never replaces this with an enemy position.
            return;
        }
        if (!ball.InFlight && server == player) { Task = "Serve"; destination = ball.transform.position; return; }
        Task = "Pressure enemy court";
        Vector3 enemyZone = team == Team.Blue ? game.OrangeLandSpot.transform.position : game.BlueLandSpot.transform.position;
        if (enemy != null) destination = enemy.transform.position;
        else destination = enemyZone + new Vector3(team == Team.Blue ? -1 : 1, 0, player.GetInstanceID() % 2 == 0 ? 2 : -2);
        if (target != null && Distance(transform.position, target.transform.position) < Mathf.Max(1, (player.MovesController.BasicAttack?.range ?? 2) * .8f)) destination = transform.position;
    }
}
