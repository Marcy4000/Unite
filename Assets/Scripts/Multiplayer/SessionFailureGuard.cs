namespace Unite.Multiplayer
{
    public enum SessionFailureReason
    {
        None,
        ConnectionLost,
        TransportFailure,
        HostLost,
        RemovedFromSession,
        SessionDeleted,
        ContentLoadFailed,
        LoadingTimedOut
    }

    /// <summary>
    /// Coalesces failure callbacks without losing failures during a session operation.
    /// Each activation has a generation: callbacks from a previous session are harmless.
    /// This is failure arbitration, not gameplay host migration.
    /// </summary>
    public sealed class SessionFailureGuard
    {
        private bool active;
        private bool handling;
        public int Generation { get; private set; }
        public SessionFailureReason PendingReason { get; private set; }

        public int Activate()
        {
            Generation++;
            active = true;
            handling = false;
            PendingReason = SessionFailureReason.None;
            return Generation;
        }

        public bool Report(int generation, SessionFailureReason reason)
        {
            if (!active || handling || generation != Generation ||
                reason == SessionFailureReason.None || PendingReason != SessionFailureReason.None)
                return false;

            PendingReason = reason;
            return true;
        }

        public bool TryBeginHandling(out SessionFailureReason reason)
        {
            reason = PendingReason;
            if (!active || handling || reason == SessionFailureReason.None) return false;
            handling = true;
            return true;
        }

        public void BeginIntentionalLeave(int generation)
        {
            if (generation != Generation) return;
            active = false;
            PendingReason = SessionFailureReason.None;
        }
    }
}
