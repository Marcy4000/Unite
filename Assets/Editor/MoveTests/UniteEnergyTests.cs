using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class UniteEnergyTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private Component controller;
    private Component pokemon;
    private ScriptableObject definition;
    private ScriptableObject unite;

    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static object EnumValue(string type, string value) => Enum.Parse(GameType(type), value);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
    private static object Call(object target, string method, params object[] arguments) => target.GetType()
        .GetMethod(method, Private | BindingFlags.Public).Invoke(target, arguments);

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Unlearned Unite energy");
        root.AddComponent(GameType("Unity.Netcode.NetworkObject"));
        pokemon = root.AddComponent(GameType("Pokemon"));
        Call(pokemon, "Awake");
        controller = root.AddComponent(GameType("MovesController"));
        Set(controller, "playerManager", root.AddComponent(GameType("PlayerManager")));
        definition = ScriptableObject.CreateInstance(GameType("PokemonBase"));
        unite = ScriptableObject.CreateInstance(GameType("MoveAsset"));
        unite.GetType().GetField("move").SetValue(unite, EnumValue("AvailableMoves", "GlaceonUnite"));
        unite.GetType().GetField("moveType").SetValue(unite, EnumValue("MoveType", "UniteMove"));
        unite.GetType().GetField("uniteEnergyCost").SetValue(unite, 80000);
        object learn = Activator.CreateInstance(GameType("LearnableMove"));
        Array choices = Array.CreateInstance(GameType("MoveAsset"), 1);
        choices.SetValue(unite, 0);
        learn.GetType().GetField("moves").SetValue(learn, choices);
        learn.GetType().GetField("level").SetValue(learn, 8);
        Array learnset = Array.CreateInstance(GameType("LearnableMove"), 1);
        learnset.SetValue(learn, 0);
        Set(definition, "learnableMoves", learnset);
        Set(pokemon, "baseStats", definition);
        Set(controller, "pokemon", pokemon);
        Set(controller, "uniteMoveMaxCharge", 1000000);
        GameType("Unity.Netcode.NetworkBehaviour").GetProperty("IsServer").SetValue(controller, true);
        GameType("Unity.Netcode.NetworkBehaviour").GetProperty("IsOwner").SetValue(controller, true);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(root);
        UnityEngine.Object.DestroyImmediate(definition);
        UnityEngine.Object.DestroyImmediate(unite);
    }

    [TestCase(0, false)]
    [TestCase(8, false)]
    [TestCase(8, true)]
    public void ServerCapDoesNotDependOnLearningOrLevel(int level, bool learned)
    {
        object currentLevel = Get(pokemon, "currentLevel");
        currentLevel.GetType().GetProperty("Value").SetValue(currentLevel, level);
        if (learned) ((Array)Get(controller, "prefabLearnedMoves")).SetValue(EnumValue("AvailableMoves", "GlaceonUnite"), 2);
        Assert.That(Call(controller, "LegacyItemUniteCost"), Is.EqualTo(80000));
    }

    [TestCase(900)]
    [TestCase(5000)]
    [TestCase(12000)]
    public void ServerEnergyAccumulatesBeforeLearningAndCapsAtActualUniteCost(int amount)
    {
        Call(controller, "GrantLegacyItemEnergy", amount);
        Assert.That(Get(controller, "legacyItemEnergy"), Is.EqualTo((float)amount));
        Call(controller, "GrantLegacyItemEnergy", 100000);
        Assert.That(Get(controller, "legacyItemEnergy"), Is.EqualTo(80000f));
        ((Array)Get(controller, "prefabLearnedMoves")).SetValue(EnumValue("AvailableMoves", "GlaceonUnite"), 2);
        Call(controller, "GrantLegacyItemEnergy", 900);
        Assert.That(Get(controller, "legacyItemEnergy"), Is.EqualTo(80000f));
    }

    [Test]
    public void LegacyOwnerEnergyAlreadyAccumulatesBeforeLearning()
    {
        Call(controller, "IncrementUniteCharge", 900);
        Call(controller, "IncrementUniteCharge", 5000);
        Call(controller, "IncrementUniteCharge", 12000);
        Assert.That(Get(controller, "uniteMoveCharge"), Is.EqualTo(17900));
    }

    [Test]
    public void GlaceonMirrorsServerEnergyWhileUniteIsStillLocked()
    {
        Set(controller, "uniteMove", Activator.CreateInstance(GameType("LockedMove")));
        object energy = Get(controller, "glaceUniteEnergy");
        energy.GetType().GetProperty("Value").SetValue(energy, 17900);
        Call(controller, "UpdateGlaceonPresentation");
        Assert.That(Get(controller, "uniteMoveCharge"), Is.EqualTo(17900));
    }
}
