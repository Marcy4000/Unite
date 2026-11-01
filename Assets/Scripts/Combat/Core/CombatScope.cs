using System;
using System.Collections.Generic;

namespace Unite.Combat
{
    public sealed class CombatScope : IDisposable
    {
        private readonly List<IDisposable> resources = new List<IDisposable>();
        private readonly List<Exception> cleanupErrors = new List<Exception>();
        public bool IsDisposed { get; private set; }
        public IReadOnlyList<Exception> CleanupErrors => cleanupErrors;

        public T Own<T>(T resource) where T : IDisposable
        {
            if (resource == null) return resource;
            if (IsDisposed) resource.Dispose();
            else resources.Add(resource);
            return resource;
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            for (int index = resources.Count - 1; index >= 0; index--)
            {
                try { resources[index].Dispose(); }
                catch (Exception exception) { cleanupErrors.Add(exception); }
            }
            resources.Clear();
        }
    }
}
