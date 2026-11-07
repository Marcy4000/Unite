using System;
using JSAM;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class OpenWorldStageJukebox : NetworkBehaviour
{
    [Serializable]
    public struct Track
    {
        [InspectorName("Display Name")]
        public string title;
        [InspectorName("Music Track")]
        public MusicFileObject music;

        public string DisplayName => !string.IsNullOrWhiteSpace(title) ? title : music != null ? music.name : "Missing music";
        public bool IsPlayable => music != null && music.Files.Count == 1 && music.Files[0] != null;
    }

    [Serializable]
    public sealed class Snapshot
    {
        public int track;
        public double elapsed;
        public float cooldown;
    }

    public struct Playback : INetworkSerializable, IEquatable<Playback>
    {
        public int Track;
        public double StartedAt;
        public double NextChangeAt;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Track);
            serializer.SerializeValue(ref StartedAt);
            serializer.SerializeValue(ref NextChangeAt);
        }

        public bool Equals(Playback other) => Track == other.Track && StartedAt == other.StartedAt && NextChangeAt == other.NextChangeAt;
    }

    public const float SwitchCooldown = 5f;
    [Tooltip("The stage music volume. Box and sphere colliders are supported directly; other colliders use their closest-point volume.")]
    [SerializeField] private Collider stageArea;
    [SerializeField] private OpenWorldNpc console;
    [SerializeField, Min(0f)] private float transitionSeconds = 1f;
    [Header("Playlist")]
    [Tooltip("The only tracks players can request. Their order also controls the menu order. Assign single-clip JSAM Music File assets.")]
    [SerializeField] private Track[] tracks = Array.Empty<Track>();
    [SerializeField] private int defaultTrack = -1;
    private readonly NetworkVariable<Playback> playback = new NetworkVariable<Playback>();
    private bool hearingStage;
    private bool refreshAudio;
    private MusicChannelHelper stageHelper;
    private MusicChannelHelper fadingHelper;
    private Snapshot pendingSnapshot;

    public static OpenWorldStageJukebox Instance { get; private set; }
    public Track[] Tracks => tracks;
    public int DefaultTrack
    {
        get
        {
            if (IsValidTrack(defaultTrack)) return defaultTrack;
            for (int index = 0; index < tracks.Length; index++)
                if (IsValidTrack(index)) return index;
            return -1;
        }
    }
    public int SelectedTrack => playback.Value.Track;
    public float CooldownRemaining => !IsSpawned ? 0f : (float)Math.Max(0d, playback.Value.NextChangeAt - NetworkManager.ServerTime.Time);
    public bool CanUseConsole(OpenWorldTrainerAvatar avatar) => IsSpawned && avatar != null && avatar.IsSpawned &&
        Contains(avatar.Position) && console != null && console.isActiveAndEnabled && console.IsWithinInteractionDistance(avatar.Position);

    private void OnEnable() => Instance = this;

    public bool Contains(Vector3 position)
    {
        if (stageArea == null || !stageArea.enabled) return false;
        if (stageArea is BoxCollider box)
        {
            Vector3 local = box.transform.InverseTransformPoint(position) - box.center;
            Vector3 half = box.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }
        if (stageArea is SphereCollider sphere)
        {
            Vector3 scale = sphere.transform.lossyScale;
            float radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            return (position - sphere.transform.TransformPoint(sphere.center)).sqrMagnitude <= radius * radius;
        }
        return (stageArea.ClosestPoint(position) - position).sqrMagnitude < 0.000001f;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            playback.Value = new Playback { Track = DefaultTrack, StartedAt = NetworkManager.ServerTime.Time };
            if (pendingSnapshot != null) RestoreSnapshot(pendingSnapshot);
        }
        playback.OnValueChanged += OnPlaybackChanged;
        refreshAudio = true;
    }

    public override void OnNetworkDespawn()
    {
        playback.OnValueChanged -= OnPlaybackChanged;
        RestoreCityMusic();
    }

    private void OnPlaybackChanged(Playback previous, Playback current) => refreshAudio = true;

    public Snapshot CaptureSnapshot() => !IsSpawned ? pendingSnapshot : new Snapshot
    {
        track = SelectedTrack,
        elapsed = Math.Max(0d, NetworkManager.ServerTime.Time - playback.Value.StartedAt),
        cooldown = CooldownRemaining
    };

    public void RestoreSnapshot(Snapshot snapshot)
    {
        if (snapshot == null || !IsValidTrack(snapshot.track) || double.IsNaN(snapshot.elapsed) ||
            double.IsInfinity(snapshot.elapsed) || snapshot.elapsed < 0d || float.IsNaN(snapshot.cooldown) ||
            snapshot.cooldown < 0f || snapshot.cooldown > SwitchCooldown) return;
        pendingSnapshot = snapshot;
        if (!IsSpawned || !IsServer) return;
        double now = NetworkManager.ServerTime.Time;
        playback.Value = new Playback { Track = snapshot.track, StartedAt = now - snapshot.elapsed, NextChangeAt = now + snapshot.cooldown };
        pendingSnapshot = null;
    }

    public bool TrySelectTrack(int index)
    {
        if (!IsClient || !CanUseConsole(OpenWorldTrainerAvatar.LocalAvatar) || !IsValidTrack(index) ||
            index == SelectedTrack || CooldownRemaining > 0f) return false;
        SelectTrackRpc(index);
        return true;
    }

    public bool IsValidTrack(int index) => index >= 0 && index < tracks.Length && tracks[index].IsPlayable;

    public void OpenMenu()
    {
        if (CanUseConsole(OpenWorldTrainerAvatar.LocalAvatar)) OpenWorldStageMenu.Instance?.Open(this);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SelectTrackRpc(int index, RpcParams rpcParams = default)
    {
        double now = NetworkManager.ServerTime.Time;
        if (!IsValidTrack(index) || index == SelectedTrack || now < playback.Value.NextChangeAt) return;
        OpenWorldTrainerAvatar sender = null;
        foreach (var spawned in NetworkManager.SpawnManager.SpawnedObjectsList)
        {
            if (spawned.OwnerClientId != rpcParams.Receive.SenderClientId) continue;
            if (spawned.TryGetComponent(out OpenWorldTrainerAvatar avatar)) { sender = avatar; break; }
        }
        if (!CanUseConsole(sender) || sender.IsSeated) return;
        playback.Value = new Playback { Track = index, StartedAt = now, NextChangeAt = now + SwitchCooldown };
    }

    private void Update()
    {
        if (IsSpawned && IsServer && !IsValidTrack(SelectedTrack) && SelectedTrack != DefaultTrack)
            playback.Value = new Playback { Track = DefaultTrack, StartedAt = NetworkManager.ServerTime.Time };
        bool inside = IsSpawned && IsClient && OpenWorldTrainerAvatar.LocalAvatar != null &&
            Contains(OpenWorldTrainerAvatar.LocalAvatar.Position) && IsValidTrack(SelectedTrack);
        if (!inside) { RestoreCityMusic(); return; }
        if ((!hearingStage || refreshAudio || stageHelper == null || stageHelper.AudioFile != tracks[SelectedTrack].music) &&
            FindFirstObjectByType<AudioManager>() != null)
        {
            stageHelper = TransitionTo(tracks[SelectedTrack].music);
            if (stageHelper == null) return;
            stageHelper.AudioSource.pitch = tracks[SelectedTrack].music.startingPitch;
            if (tracks[SelectedTrack].music.loopMode == LoopMode.NoLooping) stageHelper.AudioSource.loop = true;
            hearingStage = true;
            refreshAudio = false;
        }
        if (stageHelper == null || stageHelper.AudioSource.clip == null) return;
        var music = tracks[SelectedTrack].music;
        float expected = PlaybackTime((NetworkManager.ServerTime.Time - playback.Value.StartedAt) * music.startingPitch,
            stageHelper.AudioSource.clip.length, music.loopStart, music.loopEnd, (int)music.loopMode);
        if (Mathf.Abs(stageHelper.AudioSource.time - expected) > 0.2f) stageHelper.AudioSource.time = expected;
    }

    public static float PlaybackTime(double elapsed, float length, float loopStart, float loopEnd, int loopMode)
    {
        if (length <= 0f) return 0f;
        elapsed = Math.Max(0d, elapsed);
        if (loopMode >= (int)LoopMode.LoopWithLoopPoints && loopEnd > loopStart && loopEnd <= length)
        {
            if (loopMode == (int)LoopMode.ClampedLoopPoints) elapsed += loopStart;
            return (float)(elapsed < loopEnd ? elapsed : loopStart + (elapsed - loopEnd) % (loopEnd - loopStart));
        }
        return (float)(elapsed % length);
    }

    private MusicChannelHelper TransitionTo(MusicFileObject music)
    {
        FadeCurrentMusic();
        return AudioManager.FadeMusicIn(music, transitionSeconds, true);
    }

    private void FadeCurrentMusic()
    {
        if (fadingHelper != null) fadingHelper.Stop();
        if (AudioManager.MainMusicHelper != null && AudioManager.MainMusicHelper.AudioSource.isPlaying)
            fadingHelper = AudioManager.FadeMainMusicOut(transitionSeconds);
    }

    private void RestoreCityMusic()
    {
        if (!hearingStage) return;
        hearingStage = false;
        stageHelper = null;
        if (FindFirstObjectByType<AudioManager>() != null)
        {
            FadeCurrentMusic();
            AudioManager.FadeMusicIn(DefaultAudioMusic.MainLobbyCityTheme, transitionSeconds, true);
        }
    }

    private void OnDisable()
    {
        RestoreCityMusic();
        if (Instance == this) Instance = null;
    }
}
