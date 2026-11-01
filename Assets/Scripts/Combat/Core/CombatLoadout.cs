using System;

namespace Unite.Combat
{
    public sealed class AbilityInstance
    {
        public AbilityDefinition Definition { get; }
        public int Slot { get; }
        public int Revision { get; }
        public bool Upgraded { get; internal set; }
        public AbilityResources Resources { get; }

        internal AbilityInstance(AbilityDefinition definition, int slot, int revision, bool upgraded)
        {
            Definition = definition;
            Slot = slot;
            Revision = revision;
            Upgraded = upgraded;
            Resources = new AbilityResources(definition);
        }
    }

    public sealed class CombatLoadout
    {
        private readonly AbilityInstance[] slots;
        private readonly int[] revisions;
        public int SlotCount => slots.Length;
        public event Action<AbilityInstance> Removing;

        public CombatLoadout(int slotCount = 3)
        {
            if (slotCount < 1) throw new ArgumentOutOfRangeException(nameof(slotCount));
            slots = new AbilityInstance[slotCount];
            revisions = new int[slotCount];
        }

        public AbilityInstance Get(int slot) => slot >= 0 && slot < slots.Length ? slots[slot] : null;

        public AbilityInstance Install(int slot, AbilityDefinition definition, bool upgraded)
        {
            if (slot < 0 || slot >= slots.Length) throw new ArgumentOutOfRangeException(nameof(slot));
            AbilityInstance previous = slots[slot];
            if (previous != null && definition != null && previous.Definition.Id == definition.Id)
            {
                previous.Upgraded |= upgraded;
                return previous;
            }
            slots[slot] = null;
            revisions[slot]++;
            if (previous != null) Removing?.Invoke(previous);
            if (definition == null) return null;
            var replacement = new AbilityInstance(definition, slot, revisions[slot], upgraded);
            if (previous != null) replacement.Resources.InheritCooldown(previous.Resources);
            return slots[slot] = replacement;
        }
    }
}
