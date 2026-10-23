using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class RayquazaBuffTests
{
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Rule(string method, params object[] args) => GameType("RayquazaBuff").GetMethod(method).Invoke(null, args);
    private static object Field(object target, string name) => target.GetType().GetField(name).GetValue(target);

    [TestCase(true, 30)]
    [TestCase(false, 15)]
    public void CaptureEnergyIsThirtyForTheSecurerAndFifteenForTeammates(bool secured, int expected)
    {
        Assert.That(Rule("EnergyReward", secured), Is.EqualTo((ushort)expected));
    }

    [TestCase(5000, 4500)]
    [TestCase(10000, 6000)]
    [TestCase(7777, 5333)]
    public void ShieldUsesMaximumHpAndHasNoExpiry(int hp, int expected)
    {
        object shield = Rule("CreateShield", hp);
        Assert.That(Field(shield, "Amount"), Is.EqualTo(expected));
        Assert.That(Field(shield, "Priority"), Is.EqualTo(ushort.MaxValue));
        Assert.That(Field(shield, "IsTimed"), Is.False);
    }

    [TestCase(true, "Physical", true)]
    [TestCase(true, "Special", true)]
    [TestCase(true, "True", false)]
    [TestCase(false, "Physical", false)]
    [TestCase(false, "True", false)]
    public void TrueDamageAndShieldBreakingHitsDoNotProtectScoring(bool shield, string kind, bool expected)
    {
        Assert.That(Rule("ProtectsScoring", shield, Enum.Parse(GameType("DamageType"), kind)), Is.EqualTo(expected));
    }

    [Test]
    public void RayquazaScoringIsFourTimesFasterAndKeepsGoalGetterStacking()
    {
        var boosts = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(GameType("ScoreBoost")));
        var method = GameType("ScoringSystem").GetMethod("CalculateTrueScoreTime");
        float normal = (float)method.Invoke(null, new object[] { 0, boosts, 50 });
        boosts.Add(Activator.CreateInstance(GameType("ScoreBoost"), (ushort)65000, Enum.Parse(GameType("ScoreSpeedFactor"), "Rayquaza"), 0f, false));
        Assert.That(method.Invoke(null, new object[] { 0, boosts, 50 }), Is.EqualTo(normal / 4).Within(0.0001f));
        boosts.Add(Activator.CreateInstance(GameType("ScoreBoost"), (ushort)0, Enum.Parse(GameType("ScoreSpeedFactor"), "GoalGetter"), 6f, true));
        Assert.That(method.Invoke(null, new object[] { 0, boosts, 50 }), Is.EqualTo(normal / 5).Within(0.0001f));
    }

    [Test]
    public void RayquazaAndZapdosAssetsUseDifferentHandlersWithoutRenumberingOtherObjectives()
    {
        var rayquaza = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Pokemons/WildPokemons/WildPokemonInfo/TheiaSkyRuins/Rayquaza.asset");
        Assert.That(rayquaza, Is.Not.Null);
        Assert.That(rayquaza.GetType().GetProperty("ObjectiveType").GetValue(rayquaza).ToString(), Is.EqualTo("Rayquaza"));
        Assert.That(rayquaza.GetType().GetProperty("KillNotification").GetValue(rayquaza).ToString(), Does.Not.Contain("defenseless"));
        Assert.That(Convert.ToInt32(Enum.Parse(GameType("ObjectiveType"), "Zapdos")), Is.Zero);
        Assert.That(Convert.ToInt32(Enum.Parse(GameType("ObjectiveType"), "Regirock")), Is.EqualTo(5));
        // The separate Zapdos500Points fun-mode asset deliberately uses Rotom.
        var zapdos = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Pokemons/WildPokemons/WildPokemonInfo/Zapdos.asset");
        Assert.That(zapdos, Is.Not.Null);
        Assert.That(zapdos.GetType().GetProperty("ObjectiveType").GetValue(zapdos).ToString(), Is.EqualTo("Zapdos"));
    }

    [Test]
    public void AuthoredShieldVisualIsIncludedInBuildsWithSupportedMaterialsAndNoColliders()
    {
        var prefab = Resources.Load<GameObject>("Effects/RayquazaShield");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<Collider>(), Is.Empty);
        var lines = prefab.GetComponentsInChildren<LineRenderer>();
        Assert.That(lines.Length, Is.GreaterThanOrEqualTo(3));
        foreach (var line in lines)
        {
            Assert.That(line.sharedMaterial, Is.Not.Null);
            Assert.That(line.sharedMaterial.shader.isSupported, Is.True);
            Assert.That(line.positionCount, Is.GreaterThan(20));
        }
    }
}
