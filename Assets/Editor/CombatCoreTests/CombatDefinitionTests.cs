using System;
using NUnit.Framework;

namespace Unite.Combat.Tests
{
    public sealed class CombatDefinitionTests
    {
        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidCooldownIsRejected(double cooldown)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityDefinition(1, "Move", cooldown));
            Assert.That(exception.ParamName, Is.EqualTo("cooldown"));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NonpositiveCapacityIsRejected(int capacity)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityDefinition(1, "Move", 0, capacity: capacity));
            Assert.That(exception.ParamName, Is.EqualTo("capacity"));
        }

        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidUseIntervalIsRejected(double interval)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityDefinition(1, "Move", 0, useInterval: interval));
            Assert.That(exception.ParamName, Is.EqualTo("useInterval"));
        }

        [Test]
        public void ValidDefinitionPreservesConfigurationAndNormalizesNullName()
        {
            var definition = new AbilityDefinition(7, null, 0, true, 3, true, 0);
            Assert.That(definition.Id, Is.EqualTo(7));
            Assert.That(definition.Name, Is.Empty);
            Assert.That(definition.Cooldown, Is.Zero);
            Assert.That(definition.CooldownOnEnd, Is.True);
            Assert.That(definition.Capacity, Is.EqualTo(3));
            Assert.That(definition.StoredUses, Is.True);
            Assert.That(definition.UseInterval, Is.Zero);
        }

        [Test]
        public void ConstructorsRejectMissingDependencies()
        {
            Assert.Throws<ArgumentNullException>(() => new AbilityResources(null));
            Assert.Throws<ArgumentNullException>(() => new CastCoordinator(null, () => 0));
            Assert.Throws<ArgumentNullException>(() => new CastCoordinator(new CombatLoadout(), null));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void LoadoutRejectsNonpositiveSlotCount(int slotCount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatLoadout(slotCount));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void OutOfRangeSlotsCannotBeInstalled(int slot)
        {
            var loadout = new CombatLoadout();
            Assert.That(loadout.Get(slot), Is.Null);
            Assert.Throws<ArgumentOutOfRangeException>(() => loadout.Install(slot, new AbilityDefinition(1, "Move", 0), false));
        }
    }
}
