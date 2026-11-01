using System;

namespace Unite.Combat
{
    public sealed class AbilityResources
    {
        private readonly AbilityDefinition definition;
        private int charges;
        private int reservations;
        private int deferredCasts;
        private double readyAt;
        private double rechargeAt;
        private double rechargeDuration;
        private double intervalUntil;

        public AbilityResources(AbilityDefinition definition)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            charges = definition.Capacity;
            rechargeDuration = Math.Max(0.01, definition.Cooldown);
        }

        public AbilityAvailability Read(double now)
        {
            Recover(now);
            double remaining = definition.StoredUses
                ? Math.Max(intervalUntil - now, charges == 0 ? rechargeAt - now : 0)
                : readyAt - now;
            return new AbilityAvailability(definition.StoredUses ? charges : 1, remaining, reservations > 0, deferredCasts > 0);
        }

        internal bool TryReserve(double now, bool freeUse, out ResourceReservation reservation)
        {
            reservation = null;
            if (!freeUse)
            {
                AbilityAvailability availability = Read(now);
                if (availability.Remaining > 0 || deferredCasts > 0 || reservations > 0
                    || (definition.StoredUses && charges <= reservations)) return false;
                reservations++;
            }
            reservation = new ResourceReservation(this, freeUse);
            return true;
        }

        private void Recover(double now)
        {
            if (!definition.StoredUses || charges >= definition.Capacity) return;
            while (charges < definition.Capacity && now >= rechargeAt)
            {
                charges++;
                rechargeAt += rechargeDuration;
            }
            if (charges == definition.Capacity) rechargeAt = 0;
        }

        public void SetRechargeDuration(double duration, double now)
        {
            Recover(now);
            duration = Math.Max(0.01, duration);
            if (charges < definition.Capacity) rechargeAt = now + Math.Max(0, rechargeAt - now) / rechargeDuration * duration;
            rechargeDuration = duration;
        }

        public void ReduceCooldown(double amount, double now)
        {
            if (amount <= 0 || double.IsNaN(amount) || double.IsInfinity(amount)) return;
            if (definition.StoredUses)
            {
                if (charges < definition.Capacity) rechargeAt -= amount;
                Recover(now);
            }
            else readyAt = Math.Max(now, readyAt - amount);
        }

        public void ReduceCooldownByFraction(double fraction, double now)
        {
            fraction = Math.Max(0, Math.Min(1, fraction));
            if (fraction == 1 && !definition.StoredUses)
            {
                readyAt = now;
                return;
            }
            ReduceCooldown(Read(now).Remaining * fraction, now);
        }

        internal sealed class ResourceReservation : IDisposable
        {
            private AbilityResources owner;
            private readonly bool freeUse;
            private bool committed;
            private bool completed;

            internal ResourceReservation(AbilityResources owner, bool freeUse)
            {
                this.owner = owner;
                this.freeUse = freeUse;
            }

            internal void Commit(double now, double cooldown)
            {
                if (owner == null || committed) return;
                committed = true;
                if (freeUse) return;
                owner.reservations--;
                if (owner.definition.StoredUses)
                {
                    owner.SetRechargeDuration(cooldown, now);
                    if (owner.charges == owner.definition.Capacity) owner.rechargeAt = now + owner.rechargeDuration;
                    owner.charges--;
                    owner.intervalUntil = now + owner.definition.UseInterval;
                }
                else if (owner.definition.CooldownOnEnd) owner.deferredCasts++;
                else owner.readyAt = now + cooldown;
            }

            internal void Complete(double now, double cooldown)
            {
                if (owner == null || completed) return;
                completed = true;
                if (committed && !freeUse && !owner.definition.StoredUses && owner.definition.CooldownOnEnd)
                {
                    owner.deferredCasts = Math.Max(0, owner.deferredCasts - 1);
                    owner.readyAt = now + cooldown;
                }
                Dispose();
            }

            public void Dispose()
            {
                if (owner == null) return;
                if (!committed && !freeUse) owner.reservations--;
                owner = null;
            }
        }
    }
}
