using System;
using System.Threading;
using System.Threading.Tasks;

namespace Unite.Multiplayer
{
    public static class SessionTaskTimeout
    {
        /// <summary>
        /// Bounds waiting for SDK operations that have no cancellation overload.
        /// This does NOT cancel the SDK operation; its owner must retain the handle
        /// and must not start a replacement until it actually finishes.
        /// </summary>
        public static async Task WaitAsync(Task operation, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
            using var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var delay = Task.Delay(timeout, delayCancellation.Token);
            try
            {
                // This helper never touches Unity objects. Do not require a
                // Unity main-thread continuation merely to finish a timeout.
                if (await Task.WhenAny(operation, delay).ConfigureAwait(false) != operation)
                {
                    // A timed-out operation can still fail later. Observe it even
                    // when its original caller will never await it again.
                    _ = ObserveCompletionAsync(operation);
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException("The session operation did not finish before its deadline.");
                }
                await operation.ConfigureAwait(false);
            }
            finally
            {
                delayCancellation.Cancel();
            }
        }

        private static async Task ObserveCompletionAsync(Task operation)
        {
            try { await operation.ConfigureAwait(false); }
            catch (Exception) { /* The owning lifecycle reports the failed cleanup. */ }
        }
    }
}
