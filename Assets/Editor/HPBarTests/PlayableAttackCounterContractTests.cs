using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public class PlayableAttackCounterContractTests
{
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);

    [TestCase("cinderace")]
    [TestCase("glaceon")]
    [TestCase("sylveon")]
    [TestCase("vaporeon")]
    [TestCase("jolteon")]
    [TestCase("flygon")]
    [TestCase("meowstic")]
    [TestCase("marshadow")]
    [TestCase("emboar")]
    [TestCase("flareon")]
    [TestCase("duraludon")]
    public void DiscreteAttackFactoriesUseSharedPersonalCounter(string pokemon)
    {
        object attack = GameType("BasicAttacksDatabase").GetMethod("GetBasicAttack").Invoke(null, new object[] { pokemon });
        Assert.That(GameType("ChargedBasicAttackBase").IsInstanceOfType(attack), Is.True, pokemon);
    }

    [TestCase("cresselia")]
    [TestCase("serperior")]
    [TestCase("wildpokemon")]
    [TestCase("")]
    public void TimedAndNonPlayableAttacksDoNotAcquireDiscreteCounters(string pokemon)
    {
        object attack = GameType("BasicAttacksDatabase").GetMethod("GetBasicAttack").Invoke(null, new object[] { pokemon });
        Assert.That(GameType("ChargedBasicAttackBase").IsInstanceOfType(attack), Is.False);
    }
}
