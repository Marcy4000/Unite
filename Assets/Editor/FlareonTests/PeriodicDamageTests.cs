using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class PeriodicDamageTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
    private static object Get(object target, string field) => target.GetType().GetField(field).GetValue(target);
    private static object Damage(ulong source) => Activator.CreateInstance(Type("DamageInfo"), source, 0.2f, (short)0, (short)0,
        Enum.Parse(Type("DamageType"), "Physical"), Enum.Parse(Type("DamageProprieties"), "CanCrit, IsBasicAttack"));
    private static object Status(ulong source, float duration = 3, ushort id = 0, string type = "Burned") => Type("StatusEffect").GetMethod("DamageOverTime").Invoke(null,
        new[] { Enum.Parse(Type("StatusType"), type), Damage(source), (object)duration, 1f, id, 0.01f, (short)200 });

    [Test]
    public void FinalBoundaryTickIsNotDroppedAndNothingTicksImmediately()
    {
        object clock = Activator.CreateInstance(Type("PeriodicDamageClock"), 0d, 3d, 1d);
        Assert.That(Call(clock, "TryTick", 0d), Is.False);
        Assert.That(Call(clock, "TryTick", 1d), Is.True);
        Assert.That(Call(clock, "TryTick", 1d), Is.False);
        Assert.That(Call(clock, "TryTick", 2d), Is.True);
        Assert.That(Call(clock, "TryTick", 3d), Is.True);
        Assert.That(Call(clock, "TryTick", 4d), Is.False);
    }
    [Test]
    public void ReapplicationRefreshesRatherThanAddsDurationAndCannotStarveTicks()
    {
        object clock = Activator.CreateInstance(Type("PeriodicDamageClock"), 0d, 3d, 1d);
        Call(clock, "Refresh", 0.9d, 3d, 1d);
        Assert.That(clock.GetType().GetProperty("Expires").GetValue(clock), Is.EqualTo(3.9d));
        Assert.That(Call(clock, "TryTick", 1d), Is.True);
        Call(clock, "Refresh", 1.9d, 3d, 1d);
        Assert.That(Call(clock, "TryTick", 2d), Is.True);
    }
    [Test]
    public void SlowFramesCatchUpToTheSameNumberOfTicks()
    {
        object clock = Activator.CreateInstance(Type("PeriodicDamageClock"), 0d, 3d, 0.75d); int ticks = 0;
        while ((bool)Call(clock, "TryTick", 4d)) ticks++;
        Assert.That(ticks, Is.EqualTo(4));
    }
    [Test]
    public void MaintenanceNeverShortensAnExistingStatusAndSupportsCleansedReapplication()
    {
        object clock = Activator.CreateInstance(Type("PeriodicDamageClock"), 0d, 3d, 1d);
        Call(clock, "Extend", 0d, 2d); Assert.That(clock.GetType().GetProperty("Expires").GetValue(clock), Is.EqualTo(3d));
        Call(clock, "Extend", 2.5d, 2d); Assert.That(clock.GetType().GetProperty("Expires").GetValue(clock), Is.EqualTo(4.5d));
        Call(clock, "SetRemaining", 3d, 0d); Assert.That(Call(clock, "TryTick", 4d), Is.True); // overdue boundary ticks remain deterministic
        object fresh = Activator.CreateInstance(Type("PeriodicDamageClock"), 5d, 3d, 1d);
        Assert.That(Call(fresh, "TryTick", 5d), Is.False);
    }
    [Test]
    public void SourceIdentityIsIndependentOfIdAndDurationButNotOfAttackerOrStatusType()
    {
        object first = Status(42), sameSource = Status(42, 2), otherSource = Status(43), poison = Status(42, type: "Poisoned");
        Assert.That(Call(first, "SamePeriodicSource", sameSource), Is.True);
        Assert.That(Call(first, "SamePeriodicSource", otherSource), Is.False);
        Assert.That(Call(first, "SamePeriodicSource", poison), Is.False);
        Assert.That(first.Equals(otherSource), Is.False);
        Assert.That(first.Equals(Status(42)), Is.True);
    }
    [TestCase(false, 5000, 400, 50)]
    [TestCase(true, 100000, 400, 200)]
    public void PeriodicDamagePreservesAttributionSuppressesAttackProcsAndCapsNonPlayerDamage(bool wild, int hp, int attack, int expectedFlat)
    {
        object status = Status(42), profile = Get(status, "PeriodicDamage");
        object damage = Call(profile, "TickDamage", hp, attack, 10, wild);
        Assert.That(Get(damage, "attackerId"), Is.EqualTo(42UL));
        Assert.That(Get(damage, "baseDmg"), Is.EqualTo((short)expectedFlat));
        Assert.That(Get(damage, "proprieties").ToString(), Is.EqualTo("Unused_2"));
        Assert.That(Get(damage, "ratio"), Is.EqualTo(wild ? 0f : 0.2f));
    }
    [Test]
    public void OrdinaryBurnMarkersDoNotInventAnAttackerOrVictimOwnedDamageLoop()
    {
        object marker = Activator.CreateInstance(Type("StatusEffect"), Enum.Parse(Type("StatusType"), "Burned"), 3f, true, (ushort)0);
        object profile = Get(marker, "PeriodicDamage"); Assert.That(profile.GetType().GetProperty("Enabled").GetValue(profile), Is.False);
        string player = File.ReadAllText("Assets/Scripts/Player/PlayerManager.cs");
        Assert.That(player, Does.Not.Contain("StartTickDamage").And.Not.Contain("TickDamage()"));
        string generic = File.ReadAllText("Assets/Scripts/Pokemons/Pokemon.PeriodicDamage.cs");
        Assert.That(generic, Does.Not.Contain("Cinder").And.Not.Contain("Flareon"));
    }
    [Test]
    public void BothCharactersAndTheCopiedUniteUseTheSharedStatusDamagePath()
    {
        string cinder = File.ReadAllText("Assets/Scripts/Player/MovesController.Cinderace.cs");
        Assert.That(cinder, Does.Contain("StatusEffect.DamageOverTime").And.Not.Contain("BurnTicks").And.Not.Contain("NextBurn"));
        Assert.That(File.ReadAllText("Assets/Scripts/Moves/Flareon/FlareonMoveRules.cs"), Does.Contain("StatusEffect.DamageOverTime"));
        Assert.That(File.ReadAllText("Assets/Scripts/Player/MovesController.MarshadowCopy.cs"), Does.Contain("FlareonMoveRules.Burn(target, NetworkObjectId, 3)"));
        Assert.That(Resources.Load<GameObject>("Combat/BurnStatus"), Is.Not.Null);
    }
    [TestCase("Burned")]
    [TestCase("Poisoned")]
    public void PeriodicPayloadRoundTripsThroughTheRealNgoSerializer(string kind)
    {
        object status = Status(42, type: kind);
        object allocator = Enum.Parse(Type("Unity.Collections.Allocator"), "Temp");
        object writer = Activator.CreateInstance(Type("Unity.Netcode.FastBufferWriter"), 512, allocator, 512), reader = null;
        try
        {
            var write = writer.GetType().GetMethods().Single(m => m.Name == "WriteNetworkSerializable" && m.IsGenericMethod && m.GetParameters().Length == 1);
            write.MakeGenericMethod(Type("StatusEffect")).Invoke(writer, new[] { status });
            byte[] bytes = (byte[])Call(writer, "ToArray");
            reader = Activator.CreateInstance(Type("Unity.Netcode.FastBufferReader"), bytes, allocator, -1, 0);
            var read = reader.GetType().GetMethods().Single(m => m.Name == "ReadNetworkSerializable" && m.IsGenericMethod && m.GetParameters().Length == 1
                && m.GetParameters()[0].ParameterType.GetElementType().IsGenericParameter);
            var output = new object[] { null }; read.MakeGenericMethod(Type("StatusEffect")).Invoke(reader, output);
            Assert.That(output[0].Equals(status), Is.True);
            object restoredDamage = Get(Get(output[0], "PeriodicDamage"), "Damage");
            Assert.That(Get(restoredDamage, "attackerId"), Is.EqualTo(42UL));
        }
        finally { (reader as IDisposable)?.Dispose(); (writer as IDisposable)?.Dispose(); }
    }
    [Test]
    public void InvalidPeriodicProfilesAreRejectedAndTheCapIncludesDamageMultipliers()
    {
        object profile = Get(Status(42), "PeriodicDamage");
        profile.GetType().GetField("Interval").SetValue(profile, float.NaN);
        Assert.That(profile.GetType().GetProperty("IsValid").GetValue(profile), Is.False);
        profile.GetType().GetField("Interval").SetValue(profile, -1f);
        Assert.That(profile.GetType().GetProperty("IsValid").GetValue(profile), Is.False);
        profile = Get(Status(42), "PeriodicDamage"); object damage = Get(profile, "Damage"); damage.GetType().GetField("multiplier").SetValue(damage, 3f);
        profile.GetType().GetField("Damage").SetValue(profile, damage);
        object tick = Call(profile, "TickDamage", 10000, 1000, 14, true);
        Assert.That(Get(tick, "baseDmg"), Is.EqualTo((short)200)); Assert.That(Get(tick, "multiplier"), Is.EqualTo(1f));
    }
}
