using System;
using System.Collections.Generic;

namespace Unite.Combat
{
    public sealed class ActionBuffer<T>
    {
        private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
        private readonly struct Entry
        {
            public readonly T Action;
            public readonly int Revision;
            public readonly double Expires;
            public Entry(T action, int revision, double expires) { Action = action; Revision = revision; Expires = expires; }
        }

        public int Count => entries.Count;

        public void Put(int slot, int revision, T action, double now, double duration)
        {
            if (duration <= 0 || double.IsNaN(duration) || double.IsInfinity(duration)) return;
            entries[slot] = new Entry(action, revision, now + duration);
        }

        public bool TryTake(int slot, int revision, double now, out T action)
        {
            action = default;
            if (!entries.TryGetValue(slot, out Entry entry)) return false;
            entries.Remove(slot);
            if (entry.Revision != revision || now >= entry.Expires) return false;
            action = entry.Action;
            return true;
        }

        public void Clear(int slot) => entries.Remove(slot);
        public void Clear() => entries.Clear();

        public void Clear(Action<T> removed)
        {
            var previous = new List<Entry>(entries.Values);
            entries.Clear();
            foreach (Entry entry in previous) removed?.Invoke(entry.Action);
        }

        public void Clear(int slot, Action<T> removed)
        {
            if (!entries.TryGetValue(slot, out Entry entry)) return;
            entries.Remove(slot);
            removed?.Invoke(entry.Action);
        }

        public void Expire(double now, Action<T> expired)
        {
            foreach (var pair in new List<KeyValuePair<int, Entry>>(entries))
            {
                if (now < pair.Value.Expires) continue;
                entries.Remove(pair.Key);
                expired?.Invoke(pair.Value.Action);
            }
        }
    }
}
