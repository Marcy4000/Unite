using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class JolteonRulesTests
{
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object State(string name) => Activator.CreateInstance(GameType(name));
    private static object Call(object state, string name, params object[] args) => state.GetType().GetMethod(name).Invoke(state, args);
    private static T Value<T>(object state, string name) => (T)state.GetType().GetProperty(name).GetValue(state);

    [Test]
    public void MeterClampsBothEnds()
    {
        object charge = State("JoltChargeState");
        Call(charge, "Add", 150f); Assert.That(Value<float>(charge, "Amount"), Is.EqualTo(100f));
        Call(charge, "Add", -200f); Assert.That(Value<float>(charge, "Amount"), Is.Zero);
    }

    [Test]
    public void OnlyFullMeterCanEmpowerAndItIsSpentOnce()
    {
        object charge = State("JoltChargeState");
        Call(charge, "Add", 99f); Assert.That(Call(charge, "Consume", 1f), Is.False);
        Call(charge, "Add", 1f); Assert.That(Call(charge, "Consume", 1f), Is.True);
        Assert.That(Value<float>(charge, "Amount"), Is.Zero);
        Assert.That(Call(charge, "Consume", 1f), Is.False);
    }

    [Test]
    public void OverchargeProtectsSpendingButExpiresAtItsDeadline()
    {
        object charge = State("JoltChargeState");
        Call(charge, "Overcharge", 10f, 5f);
        Assert.That(Call(charge, "Consume", 14f), Is.True);
        Call(charge, "Reduce", 15f, 14f);
        Assert.That(Value<float>(charge, "Amount"), Is.EqualTo(100f));
        Assert.That(Call(charge, "Consume", 15f), Is.True);
        Assert.That(Value<float>(charge, "Amount"), Is.Zero);
    }

    [Test]
    public void DeathClearsMeterEvenDuringOvercharge()
    {
        object charge = State("JoltChargeState");
        Call(charge, "Overcharge", 10f, 5f); Call(charge, "Clear");
        Assert.That(Value<bool>(charge, "Ready"), Is.False);
        Assert.That(Value<float>(charge, "OverchargeUntil"), Is.Zero);
    }

    [TestCase(0, false)]
    [TestCase(2, false)]
    [TestCase(16, false)]
    [TestCase(64, false)]
    [TestCase(128, true)]
    [TestCase(4, true)]
    [TestCase(130, false)]
    [TestCase(68, false)]
    public void OnlyDirectMoveDamageTriggersWebSpeed(int flags, bool expected)
    {
        object value = Enum.ToObject(GameType("DamageProprieties"), flags);
        Assert.That(GameType("JoltMoveRules").GetMethod("TriggersWebSpeed").Invoke(null, new[] { value }), Is.EqualTo(expected));
    }

    [Test]
    public void WebMarkStatusIdsDoNotOverlapDuraludonsReservedRanges()
    {
        Type rules = GameType("JoltMoveRules");
        Assert.That((ushort)rules.GetField("WebMarkIdMin").GetRawConstantValue(), Is.GreaterThanOrEqualTo(40000));
        Assert.That((ushort)rules.GetField("WebMarkIdMax").GetRawConstantValue(), Is.LessThan(ushort.MaxValue));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void RecastRetainsOriginalEmpowerment(bool empowered)
    {
        object combo = State("JoltRecastState");
        Call(combo, "Begin", empowered); Call(combo, "Open", 1f, 4f);
        Assert.That(Call(combo, "TryRecast", 2f), Is.True);
        Assert.That(Value<bool>(combo, "Empowered"), Is.EqualTo(empowered));
        Assert.That(Value<bool>(combo, "IsSecondPass"), Is.True);
        Assert.That(Value<bool>(combo, "HasWindow"), Is.False);
        Assert.That(Call(combo, "Expired", 100f), Is.False, "A committed second pass has no expiring recast timer.");
    }

    [Test]
    public void ASecondPassCannotOpenAThirdPassEvenDuringUnite()
    {
        object combo = State("JoltRecastState");
        Call(combo, "Begin", true); Call(combo, "Open", 0f, 4f);
        Assert.That(Call(combo, "TryRecast", 1f), Is.True);
        Call(combo, "Open", 2f, 4f);
        Assert.That(Call(combo, "TryRecast", 3f), Is.False);
    }

    [Test]
    public void RecastExpiresAtDeadlineAndResetErasesIt()
    {
        object combo = State("JoltRecastState");
        Call(combo, "Begin", true); Call(combo, "Open", 0f, 4f);
        Assert.That(Call(combo, "Expired", 4f), Is.True);
        Assert.That(Call(combo, "TryRecast", 4f), Is.False);
        Call(combo, "Clear");
        Assert.That(Value<bool>(combo, "Empowered"), Is.False);
        Assert.That(Value<bool>(combo, "HasWindow"), Is.False);
    }

    [Test]
    public void BeginningANewSequenceClearsThePreviousRecastState()
    {
        object combo = State("JoltRecastState");
        Call(combo, "Begin", true);
        Call(combo, "Open", 1f, 4f);
        Assert.That(Call(combo, "TryRecast", 2f), Is.True);
        Call(combo, "Begin", false);
        Assert.That(Value<bool>(combo, "Empowered"), Is.False);
        Assert.That(Value<bool>(combo, "IsSecondPass"), Is.False);
        Assert.That(Value<bool>(combo, "HasWindow"), Is.False);
        Assert.That(Value<float>(combo, "Until"), Is.Zero);
    }

    [TestCase("Charge", false, false, 5f)]
    [TestCase("Charge", true, false, 6f)]
    [TestCase("Electroweb", false, false, 0.75f)]
    [TestCase("WebMark", false, false, 4f)]
    [TestCase("WebMark", true, true, 5f)]
    [TestCase("LightningBlitz", false, false, 3.5f)]
    [TestCase("Overcharge", false, false, 5f)]
    [TestCase("Ready", false, false, 65f)]
    public void EffectLifetimeFollowsTheExistingTuning(string effect, bool empowered, bool upgraded, float expected)
    {
        object effectValue = Enum.Parse(GameType("JoltEffect"), effect);
        object result = GameType("JoltMoveRules").GetMethod("EffectDuration")
            .Invoke(null, new[] { effectValue, (object)empowered, upgraded });
        Assert.That((float)result, Is.EqualTo(expected).Within(0.0001f));
    }

    [TestCase("JoltThunderFang", 7f)]
    [TestCase("JoltDischarge", 11f)]
    [TestCase("JoltCharge", 9f)]
    [TestCase("JoltElectroweb", 10f)]
    public void ExistingFactoriesAndCooldownsRemainCompatible(string name, float cooldown)
    {
        object move = State(name);
        Assert.That((float)move.GetType().GetField("Cooldown").GetValue(move), Is.EqualTo(cooldown));
        Assert.That(Value<bool>(move, "IsStoredMove"), Is.False);
        Type available = GameType("AvailableMoves");
        string enumName = name == "JoltElectroweb" ? "JoltElectroWeb" : name;
        object factory = GameType("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { Enum.Parse(available, enumName) });
        Assert.That(factory.GetType(), Is.EqualTo(move.GetType()));
    }

    [TestCase(1f, 0f, -1f, 0f)]
    [TestCase(0f, 1f, 0f, -1f)]
    public void WallReflectionsReverseTheNormalComponentWithoutLosingSpeed(float x, float z, float nx, float nz)
    {
        Vector3 reflected = (Vector3)GameType("JoltMoveRules").GetMethod("Reflect").Invoke(null,
            new object[] { new Vector3(x, 0, z), new Vector3(nx, 0, nz) });
        Assert.That(reflected.magnitude, Is.EqualTo(1f).Within(0.001f));
        Assert.That(Vector3.Dot(reflected, new Vector3(x, 0, z)), Is.EqualTo(-1f).Within(0.001f));
        Assert.That(reflected.y, Is.Zero);
    }

    [TestCase(false, 4f)]
    [TestCase(true, 5f)]
    public void WebUpgradeExtendsRealMarkLifetime(bool upgraded, float duration)
    {
        object effect = Enum.Parse(GameType("JoltEffect"), "WebMark");
        Assert.That(GameType("JoltMoveRules").GetMethod("EffectDuration").Invoke(null,
            new[] { effect, (object)false, upgraded }), Is.EqualTo(duration));
    }

}
