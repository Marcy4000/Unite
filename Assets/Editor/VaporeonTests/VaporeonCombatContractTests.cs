using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class VaporeonCombatContractTests
{
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).FirstOrDefault(type => type != null);

    [Test]
    public void RuntimeAndExecutionOwnTheCharacterImplementation()
    {
        Type runtime = GameType("VaporeonCharacterRuntime");
        Type execution = GameType("VaporeonExecution");
        Assert.That(runtime, Is.Not.Null);
        Assert.That(execution, Is.Not.Null);
        Assert.That(runtime.BaseType, Is.EqualTo(GameType("CombatCharacterRuntime")));
        Assert.That(execution.BaseType, Is.EqualTo(GameType("CombatExecution")));
        foreach (string move in new[] { "Swift", "HelpingHand", "AquaRing", "RainDance", "Whirlpool", "Dive", "Unite" })
        {
            MethodInfo method = execution.GetMethod("Execute" + move, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, move);
            Assert.That(method.ReturnType, Is.EqualTo(typeof(IEnumerator)), move);
            Assert.That(method.GetParameters(), Is.Empty, move);
        }
    }

    [TestCase("VaporSwift")]
    [TestCase("VaporHelpingHand")]
    [TestCase("VaporAquaRing")]
    [TestCase("VaporRainDance")]
    [TestCase("VaporWhirlpool")]
    [TestCase("VaporDive")]
    [TestCase("VaporeonUnite")]
    public void MoveBridgeUsesCombatActor(string name)
    {
        Type move = GameType(name);
        Assert.That(move, Is.Not.Null);
        Assert.That(GameType("CombatMove").IsAssignableFrom(move), Is.True, name);
    }

    [Test]
    public void BasicAttackIsResolvedByTheServer()
    {
        object attack = Activator.CreateInstance(GameType("VaporBasicAtk"));
        Assert.That(attack.GetType().GetProperty("ServerResolved").GetValue(attack), Is.True);
    }
}
