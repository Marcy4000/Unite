using System;
using System.Collections.Generic;

namespace Unite.Combat
{
    public sealed class CastSession
    {
        public long CastId { get; }
        public CastRequest Request { get; }
        public AbilityInstance Ability { get; }
        public CastPhase Phase { get; internal set; }
        public CastOutcome Outcome { get; internal set; }
        public bool Accepted { get; internal set; }
        public double Cooldown { get; }
        public bool FreeUse { get; }
        public CombatScope Scope { get; } = new CombatScope();
        internal AbilityResources.ResourceReservation Reservation { get; }

        internal CastSession(long castId, CastRequest request, AbilityInstance ability, double cooldown,
            bool freeUse, AbilityResources.ResourceReservation reservation)
        {
            CastId = castId;
            Request = request;
            Ability = ability;
            Cooldown = cooldown;
            FreeUse = freeUse;
            Reservation = reservation;
            Phase = CastPhase.Preparing;
        }
    }

    public sealed class CastCoordinator : IDisposable
    {
        private readonly CombatLoadout loadout;
        private readonly Dictionary<long, CastSession> requests = new Dictionary<long, CastSession>();
        private readonly Dictionary<long, CastSession> active = new Dictionary<long, CastSession>();
        private readonly Queue<long> history = new Queue<long>();
        private readonly HashSet<long> admitted = new HashSet<long>();
        private readonly Func<double> clock;
        private long nextCastId;
        private long highestRequest;
        private bool disposed;
        public event Action<CastSession> Changed;
        public int ActiveCount => active.Count;

        public CastCoordinator(CombatLoadout loadout, Func<double> clock)
        {
            this.loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            loadout.Removing += RemoveAbility;
        }

        public CastSession FindRequest(long requestId) => requests.TryGetValue(requestId, out CastSession session) ? session : null;
        public CastSession FindCast(long castId) => active.TryGetValue(castId, out CastSession session) ? session : null;

        public bool TryAdmit(long requestId)
        {
            if (disposed || requestId <= 0 || requestId <= highestRequest) return false;
            highestRequest = requestId;
            return admitted.Add(requestId);
        }

        public void DiscardAdmission(long requestId) => admitted.Remove(requestId);

        public bool TryPrepare(CastRequest request, double cooldown, bool freeUse, out CastSession session, out CastRejection rejection)
        {
            session = FindRequest(request.RequestId);
            rejection = CastRejection.None;
            if (session != null) return true;
            if (disposed || request.RequestId <= 0 || double.IsNaN(cooldown) || double.IsInfinity(cooldown) || cooldown < 0)
                rejection = CastRejection.InvalidRequest;
            else if (!admitted.Remove(request.RequestId) && !TryAdmit(request.RequestId)) rejection = CastRejection.StaleRequest;
            AbilityInstance ability = loadout.Get(request.Slot);
            if (rejection == CastRejection.None && (ability == null || ability.Definition.Id != request.AbilityId)) rejection = CastRejection.UnknownAbility;
            if (rejection == CastRejection.None && ability.Revision != request.Revision) rejection = CastRejection.StaleRevision;
            if (rejection != CastRejection.None)
            {
                admitted.Remove(request.RequestId);
                return false;
            }
            admitted.Remove(request.RequestId);
            if (!ability.Resources.TryReserve(clock(), freeUse, out AbilityResources.ResourceReservation reservation))
            {
                rejection = CastRejection.Unavailable;
                return false;
            }
            session = new CastSession(++nextCastId, request, ability, cooldown, freeUse, reservation);
            requests.Add(request.RequestId, session);
            active.Add(session.CastId, session);
            Changed?.Invoke(session);
            return true;
        }

        public bool TryCommit(CastSession session)
        {
            if (session == null || session.Phase != CastPhase.Preparing || !active.ContainsKey(session.CastId)) return false;
            if (!ReferenceEquals(loadout.Get(session.Request.Slot), session.Ability))
            {
                End(session, CastOutcome.Replaced);
                return false;
            }
            session.Reservation.Commit(clock(), session.Cooldown);
            session.Accepted = true;
            session.Phase = CastPhase.Executing;
            Changed?.Invoke(session);
            return true;
        }

        public void BeginRecovery(CastSession session)
        {
            if (session == null || session.Phase != CastPhase.Executing) return;
            session.Phase = CastPhase.Recovery;
            Changed?.Invoke(session);
        }

        public void End(CastSession session, CastOutcome outcome)
        {
            if (session == null || session.Phase == CastPhase.Ended) return;
            session.Phase = CastPhase.Ended;
            session.Outcome = outcome;
            active.Remove(session.CastId);
            session.Reservation.Complete(clock(), session.Cooldown);
            session.Scope.Dispose();
            history.Enqueue(session.Request.RequestId);
            while (history.Count > 256) requests.Remove(history.Dequeue());
            Changed?.Invoke(session);
        }

        public void InterruptAll(CastOutcome outcome)
        {
            foreach (CastSession session in new List<CastSession>(active.Values)) End(session, outcome);
        }

        private void RemoveAbility(AbilityInstance ability)
        {
            foreach (CastSession session in new List<CastSession>(active.Values))
                if (ReferenceEquals(session.Ability, ability)) End(session, CastOutcome.Replaced);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            loadout.Removing -= RemoveAbility;
            InterruptAll(CastOutcome.ActorDespawned);
            Changed = null;
            requests.Clear();
            admitted.Clear();
            history.Clear();
        }
    }
}
