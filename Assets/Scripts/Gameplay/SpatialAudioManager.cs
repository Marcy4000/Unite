using System.Collections.Generic;
using JSAM;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Defines who can hear a spatial sound.
/// </summary>
public enum AudioAudibility : byte
{
    /// <summary>Only the triggering player can hear the sound.</summary>
    Self,
    /// <summary>Only players on the same team as the source can hear the sound.</summary>
    Team,
    /// <summary>Everyone can hear the sound (still respects vision rules).</summary>
    Everyone
}

/// <summary>
/// Manages spatial audio playback with vision-based and team-based filtering.
/// Sounds are only played if the source is visible to the local player's team.
/// </summary>
public class SpatialAudioManager : NetworkBehaviour
{
    public static SpatialAudioManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    #region Server-Side Methods (Call from Server)

    /// <summary>
    /// Plays a spatial sound from a Vision source. Only clients who can see the Vision will hear the sound.
    /// Must be called from the server.
    /// </summary>
    /// <param name="sound">The sound to play.</param>
    /// <param name="sourceVision">The Vision component of the sound source (used for visibility check).</param>
    /// <param name="audibility">Who can potentially hear this sound.</param>
    /// <param name="triggeringPlayer">The player who triggered the sound (required for Self/Team audibility).</param>
    public void PlaySpatialSound<T>(T sound, Vision sourceVision, AudioAudibility audibility, PlayerManager triggeringPlayer = null) where T : System.Enum
    {
        if (!IsServer) return;

        Vector3 position = sourceVision != null ? sourceVision.transform.position : Vector3.zero;
        Team sourceTeam = sourceVision != null ? sourceVision.CurrentTeam : Team.Neutral;

        PlaySpatialSoundInternal(sound, position, sourceTeam, audibility, triggeringPlayer, sourceVision);
    }

    /// <summary>
    /// Plays a spatial sound at a position. Uses a team for visibility filtering.
    /// Must be called from the server.
    /// </summary>
    /// <param name="sound">The sound to play.</param>
    /// <param name="position">World position of the sound.</param>
    /// <param name="sourceTeam">Team of the sound source (for visibility checks).</param>
    /// <param name="audibility">Who can potentially hear this sound.</param>
    /// <param name="triggeringPlayer">The player who triggered the sound (required for Self/Team audibility).</param>
    public void PlaySpatialSound<T>(T sound, Vector3 position, Team sourceTeam, AudioAudibility audibility, PlayerManager triggeringPlayer = null) where T : System.Enum
    {
        if (!IsServer) return;

        PlaySpatialSoundInternal(sound, position, sourceTeam, audibility, triggeringPlayer, null);
    }

    /// <summary>
    /// Plays a spatial sound to a single specific player (ignores vision).
    /// Useful for feedback sounds that only one player should hear.
    /// Must be called from the server.
    /// </summary>
    /// <param name="sound">The sound to play.</param>
    /// <param name="position">World position of the sound.</param>
    /// <param name="targetPlayer">The player who should hear the sound.</param>
    public void PlaySoundToPlayer<T>(T sound, Vector3 position, PlayerManager targetPlayer) where T : System.Enum
    {
        if (!IsServer || targetPlayer == null) return;

        int soundIndex = System.Convert.ToInt32(sound);
        PlaySoundClientRpc(soundIndex, position, RpcTarget.Single(targetPlayer.OwnerClientId, RpcTargetUse.Temp));
    }

    private void PlaySpatialSoundInternal<T>(T sound, Vector3 position, Team sourceTeam, AudioAudibility audibility, PlayerManager triggeringPlayer, Vision sourceVision) where T : System.Enum
    {
        int soundIndex = System.Convert.ToInt32(sound);
        List<ulong> targetClients = new List<ulong>();

        foreach (var player in GameManager.Instance.Players)
        {
            if (!ShouldPlayerHearSound(player, audibility, triggeringPlayer, sourceTeam, sourceVision, position))
                continue;

            targetClients.Add(player.OwnerClientId);
        }

        if (targetClients.Count == 0) return;

        if (targetClients.Count == 1)
        {
            PlaySoundClientRpc(soundIndex, position, RpcTarget.Single(targetClients[0], RpcTargetUse.Temp));
        }
        else
        {
            PlaySoundClientRpc(soundIndex, position, RpcTarget.Group(targetClients.ToArray(), RpcTargetUse.Temp));
        }
    }

    private bool ShouldPlayerHearSound(PlayerManager player, AudioAudibility audibility, PlayerManager triggeringPlayer, Team sourceTeam, Vision sourceVision, Vector3 position)
    {
        Team playerTeam = player.CurrentTeam.Team;

        // Check audibility filter first
        switch (audibility)
        {
            case AudioAudibility.Self:
                if (triggeringPlayer == null || player.OwnerClientId != triggeringPlayer.OwnerClientId)
                    return false;
                break;

            case AudioAudibility.Team:
                if (triggeringPlayer == null || playerTeam != triggeringPlayer.CurrentTeam.Team)
                    return false;
                break;

            case AudioAudibility.Everyone:
                // No audibility filter, proceed to vision check
                break;
        }

        // For sounds from same team, always audible (no vision check needed)
        if (sourceTeam == playerTeam)
            return true;

        // For enemy sounds, check if the source is visible to this player
        // If we have a Vision component, check if it's rendered (visible to enemies)
        if (sourceVision != null)
        {
            return IsVisionVisibleToTeam(sourceVision, playerTeam);
        }

        // If no Vision component, use position-based check with VisionControllers
        return IsPositionVisibleToTeam(position, playerTeam);
    }

    private bool IsVisionVisibleToTeam(Vision vision, Team viewerTeam)
    {
        // Same team always sees
        if (vision.CurrentTeam == viewerTeam)
            return true;

        // Check if not visibly eligible
        if (!vision.IsVisiblyEligible)
            return false;

        // Check if temporarily revealed
        if (vision.TemporarilyRevealed)
            return true;

        // Check if any VisionController from viewerTeam can see this Vision
        foreach (var player in GameManager.Instance.Players)
        {
            if (player.CurrentTeam.Team != viewerTeam)
                continue;

            var controller = player.VisionController;
            if (controller == null || !controller.IsEnabled || controller.IsBlinded)
                continue;

            if (controller.ContainsVision(vision) &&
                (!vision.IsInBush || vision.IsVisibleInBush.Contains(controller.gameObject.GetInstanceID())))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPositionVisibleToTeam(Vector3 position, Team viewerTeam)
    {
        // Simple distance-based check using VisionControllers
        foreach (var player in GameManager.Instance.Players)
        {
            if (player.CurrentTeam.Team != viewerTeam)
                continue;

            var controller = player.VisionController;
            if (controller == null || !controller.IsEnabled || controller.IsBlinded)
                continue;

            // Use a simple distance check (default vision range is ~10 units)
            float distance = Vector3.Distance(controller.transform.position, position);
            if (distance <= 10f) // Default vision range
                return true;
        }

        return false;
    }

    #endregion

    #region Client-Side Methods (Call Locally)

    /// <summary>
    /// Plays a sound locally with vision check. Use this for client-predicted sounds.
    /// The sound will only play if the position is visible to the local player.
    /// </summary>
    /// <param name="sound">The sound to play.</param>
    /// <param name="position">World position of the sound.</param>
    /// <param name="sourceVision">Optional: Vision component for accurate visibility check.</param>
    /// <returns>True if the sound was played, false if not visible.</returns>
    public bool PlayLocalSoundWithVisionCheck<T>(T sound, Vector3 position, Vision sourceVision = null) where T : System.Enum
    {
        Team localTeam = VisionManager.Instance.LocalPlayerTeam;

        bool isVisible;
        if (sourceVision != null)
        {
            isVisible = sourceVision.IsRendered || sourceVision.CurrentTeam == localTeam;
        }
        else
        {
            isVisible = IsPositionVisibleToLocalPlayer(position);
        }

        if (!isVisible) return false;

        AudioManager.PlaySound(sound, position);
        return true;
    }

    /// <summary>
    /// Plays a sound locally without any visibility check.
    /// Use this for UI sounds or sounds that should always play.
    /// </summary>
    public void PlayLocalSound<T>(T sound, Vector3 position) where T : System.Enum
    {
        AudioManager.PlaySound(sound, position);
    }

    /// <summary>
    /// Plays a sound locally attached to a transform (follows the transform).
    /// </summary>
    public void PlayLocalSound<T>(T sound, Transform source) where T : System.Enum
    {
        AudioManager.PlaySound(sound, source);
    }

    private bool IsPositionVisibleToLocalPlayer(Vector3 position)
    {
        // Find local player's VisionController
        foreach (var player in GameManager.Instance.Players)
        {
            if (!player.IsLocalPlayer) continue;

            var controller = player.VisionController;
            if (controller == null) return true; // Fallback: play sound if no controller

            float distance = Vector3.Distance(controller.transform.position, position);
            return distance <= 10f; // Default vision range
        }

        return true; // Fallback: play sound
    }

    #endregion

    #region RPCs

    [Rpc(SendTo.SpecifiedInParams)]
    private void PlaySoundClientRpc(int soundIndex, Vector3 position, RpcParams rpcParams = default)
    {
        // Convert back to enum and play
        DefaultAudioSounds sound = (DefaultAudioSounds)soundIndex;
        AudioManager.PlaySound(sound, position);
    }

    #endregion
}
