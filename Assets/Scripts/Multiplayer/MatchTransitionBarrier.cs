using System.Collections.Generic;

namespace Unite.Multiplayer
{
    public sealed class MatchTransitionBarrier
    {
        private readonly HashSet<ulong> participants;
        private readonly HashSet<ulong> closed = new HashSet<ulong>();
        private readonly HashSet<ulong> ready = new HashSet<ulong>();

        public int Generation { get; }
        public string SceneName { get; }
        public bool LoadRequested { get; private set; }
        public bool SceneLoaded { get; private set; }
        public bool OpenScheduled { get; private set; }
        public bool CanLoad => !LoadRequested && closed.IsSupersetOf(participants);
        public bool CanOpen => LoadRequested && SceneLoaded && !OpenScheduled && ready.IsSupersetOf(participants);

        public MatchTransitionBarrier(int generation, string sceneName, IEnumerable<ulong> clientIds)
        {
            Generation = generation;
            SceneName = sceneName;
            participants = new HashSet<ulong>(clientIds);
        }

        public void MarkClosed(int generation, ulong clientId)
        {
            if (generation == Generation && !LoadRequested && participants.Contains(clientId))
                closed.Add(clientId);
        }

        public void MarkReady(int generation, ulong clientId)
        {
            if (generation == Generation && LoadRequested && participants.Contains(clientId))
                ready.Add(clientId);
        }

        public void RemoveClient(ulong clientId)
        {
            participants.Remove(clientId);
            closed.Remove(clientId);
            ready.Remove(clientId);
        }

        public bool StartLoading()
        {
            if (!CanLoad) return false;
            LoadRequested = true;
            return true;
        }

        public void MarkSceneLoaded(string sceneName)
        {
            if (LoadRequested && SceneName == sceneName) SceneLoaded = true;
        }

        public bool ScheduleOpen()
        {
            if (!CanOpen) return false;
            OpenScheduled = true;
            return true;
        }
    }
}
