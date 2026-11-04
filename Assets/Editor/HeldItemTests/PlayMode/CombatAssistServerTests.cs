using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public abstract partial class HeldItemServerTests
{
    private Component[] Assistants(Component victim) => ((IEnumerable)Get(Field(victim, "resolvedKnockout"), "Assistants")).Cast<Component>().ToArray();
    private void Kind(Component pokemon, string type) => pokemon.GetType().GetProperty("Type").SetValue(pokemon, EnumValue("PokemonType", type));
    private void ExpireContributions(Component pokemon, string ledger)
    {
        object history = Field(pokemon, ledger);
        Call(history, "Clear");
        Call(history, "Record", 0UL, -100d);
    }

    [UnityTest]
    public IEnumerator DamageAssistIsUniqueAndTheKillerIsNeverAnAssistant()
    {
        var killer = Pokemon("Killer"); var helper = Pokemon("Helper"); var victim = Pokemon("Victim", "Orange");
        for (int i = 0; i < 3; i++) Call(victim, "ResolveDamage", Damage(helper, 10));
        Call(killer, "RecordSupport", helper);
        Call(victim, "ResolveDamage", Damage(killer, 5000));
        CollectionAssert.AreEqual(new[] { helper }, Assistants(victim));
        Call(victim, "ResolveDamage", Damage(killer, 5000));
        CollectionAssert.AreEqual(new[] { helper }, Assistants(victim));
        yield return null;
    }

    [UnityTest]
    public IEnumerator SupportOnlyHealingShieldAndBuffAssistUseTheSameHeldItemAward()
    {
        var killer = Pokemon("Supported killer"); var healer = Pokemon("Healer"); var shielder = Pokemon("Shielder"); var buffer = Pokemon("Buffer");
        var victim = Pokemon("Victim", "Orange");
        Equip(healer, "DriveLens"); Equip(shielder, "DriveLens"); Equip(buffer, "DriveLens");
        Health(killer, 4000);
        Call(killer, "HealDamageRPC", 100, (ulong)Get(healer, "NetworkObjectId"));
        object shield = Call(Shield(100, 59990), "WithSource", (ulong)Get(shielder, "NetworkObjectId"));
        Call(killer, "AddShieldRPC", shield);
        object buff = Activator.CreateInstance(Find("StatChange"), (short)20, EnumValue("Stat", "Attack"), 5f, true, true, true, (ushort)59991, true);
        Call(killer, "AddStatChange", Call(buff, "WithSource", (ulong)Get(buffer, "NetworkObjectId")));
        Call(victim, "ResolveDamage", Damage(killer, 5000));
        CollectionAssert.AreEquivalent(new[] { healer, shielder, buffer }, Assistants(victim));
        foreach (var helper in new[] { healer, shielder, buffer })
            Assert.That(Call(helper, "GetPreciseStatChange", EnumValue("Stat", "SpAttack")), Is.EqualTo(0.6f).Within(0.0001));
        yield return null;
    }

    [UnityTest]
    public IEnumerator IneffectiveHealingAndBlockedDamageDoNotQualify()
    {
        var killer = Pokemon("Killer"); var helper = Pokemon("Helper"); var victim = Pokemon("Victim", "Orange");
        Call(killer, "HealDamageRPC", 100, (ulong)Get(helper, "NetworkObjectId"));
        object invincible = Activator.CreateInstance(Find("StatusEffect"), EnumValue("StatusType", "Invincible"), 1f, true, (ushort)59992);
        Call(victim, "AddStatusEffect", invincible);
        Assert.That(Call(victim, "ResolveDamage", Damage(helper, 100)), Is.EqualTo(0));
        Call(victim, "RemoveStatusEffectWithID", (ushort)59992);
        Call(victim, "ResolveDamage", Damage(killer, 5000));
        Assert.That(Assistants(victim), Is.Empty);
        yield return null;
    }

    [UnityTest]
    public IEnumerator OnlyFourMostRecentSameTeamPlayersAssistAContestedObjective()
    {
        var killer = Pokemon("Killer"); var objective = Pokemon("Objective", "Neutral"); Kind(objective, "Objective");
        var helpers = Enumerable.Range(0, 6).Select(i => Pokemon("Helper " + i)).ToArray();
        foreach (var helper in helpers) Call(objective, "ResolveDamage", Damage(helper, 10));
        var opponent = Pokemon("Other team", "Orange"); Call(objective, "ResolveDamage", Damage(opponent, 10));
        var wild = Pokemon("Wild", "Neutral"); Kind(wild, "Wild"); Call(objective, "ResolveDamage", Damage(wild, 10));
        Call(objective, "ResolveDamage", Damage(killer, 5000));
        CollectionAssert.AreEqual(helpers.Reverse().Take(4).ToArray(), Assistants(objective));
        yield return null;
    }

    [UnityTest]
    public IEnumerator OrdinaryWildPokemonNeverAwardAssistsOrPlayerKoItemStacks()
    {
        var killer = Pokemon("Killer"); var helper = Pokemon("Helper"); var wild = Pokemon("Wild", "Neutral"); Kind(wild, "Wild");
        Equip(helper, "DriveLens");
        Call(wild, "ResolveDamage", Damage(helper, 10)); Call(wild, "ResolveDamage", Damage(killer, 5000));
        Assert.That(Assistants(wild), Is.Empty);
        Assert.That(Call(helper, "GetPreciseStatChange", EnumValue("Stat", "SpAttack")), Is.EqualTo(0));
        yield return null;
    }

    [UnityTest]
    public IEnumerator WildFinisherPromotesLatestPlayerAndKeepsOriginalDamageSource()
    {
        var earlier = Pokemon("Earlier"); var latest = Pokemon("Latest"); var victim = Pokemon("Victim", "Orange");
        var wild = Pokemon("Wild", "Neutral"); Kind(wild, "Wild");
        Equip(latest, "AccelBracer"); Equip(earlier, "DriveLens");
        Call(victim, "ResolveDamage", Damage(earlier, 10)); Call(victim, "ResolveDamage", Damage(latest, 10));
        Call(victim, "ResolveDamage", Damage(wild, 5000));
        object result = Field(victim, "resolvedKnockout");
        Assert.That(Get(result, "Killer"), Is.SameAs(latest));
        Assert.That(Get(result, "DamageSource"), Is.SameAs(wild));
        CollectionAssert.AreEqual(new[] { earlier }, Assistants(victim));
        Assert.That(Get(latest, "KillStreak"), Is.EqualTo(1));
        Assert.That(Call(latest, "GetPreciseStatChange", EnumValue("Stat", "Attack")), Is.EqualTo(1.2f).Within(0.0001));
        yield return null;
    }

    [UnityTest]
    public IEnumerator WildFinisherWithoutRecentContributorsRetainsWildCredit()
    {
        var helper = Pokemon("Helper"); var victim = Pokemon("Victim", "Orange"); var wild = Pokemon("Wild", "Neutral"); Kind(wild, "Wild");
        Call(victim, "ResolveDamage", Damage(helper, 10)); ExpireContributions(victim, "hostileContributions");
        Call(victim, "ResolveDamage", Damage(wild, 5000));
        Assert.That(Get(Field(victim, "resolvedKnockout"), "Killer"), Is.SameAs(wild));
        Assert.That(Assistants(victim), Is.Empty);
        yield return null;
    }

    [UnityTest]
    public IEnumerator HindranceWithoutDamageCanAssist()
    {
        var killer = Pokemon("Killer"); var helper = Pokemon("Helper"); var victim = Pokemon("Victim", "Orange");
        object stun = Activator.CreateInstance(Find("StatusEffect"), EnumValue("StatusType", "Incapacitated"), 1f, true, (ushort)59993);
        Call(victim, "AddStatusEffect", Call(stun, "WithSource", (ulong)Get(helper, "NetworkObjectId")));
        Call(victim, "ResolveDamage", Damage(killer, 5000));
        CollectionAssert.AreEqual(new[] { helper }, Assistants(victim));
        yield return null;
    }

    [UnityTest]
    public IEnumerator RespawnClearsThePreviousVictimsContributions()
    {
        var killer = Pokemon("Killer"); var helper = Pokemon("Helper"); var victim = Pokemon("Victim", "Orange");
        Call(victim, "ResolveDamage", Damage(helper, 10)); Call(victim, "RestoreHealthOnRespawnRPC");
        Call(victim, "ResolveDamage", Damage(killer, 5000));
        Assert.That(Assistants(victim), Is.Empty);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ActiveSupportOutlivesItsApplicationWindowButRemovedSupportDoesNotRefresh()
    {
        var killer = Pokemon("Killer"); var buffer = Pokemon("Buffer"); var victim = Pokemon("Victim", "Orange");
        object buff = Activator.CreateInstance(Find("StatChange"), (short)20, EnumValue("Stat", "Attack"), 0f, false, true, true, (ushort)59994, true);
        Call(killer, "AddStatChange", Call(buff, "WithSource", (ulong)Get(buffer, "NetworkObjectId")));
        ExpireContributions(killer, "supportContributions");
        Call(victim, "ResolveDamage", Damage(killer, 5000));
        CollectionAssert.AreEqual(new[] { buffer }, Assistants(victim));
        Call(killer, "RemoveAllStatChangeWithIDRPC", (ushort)59994);
        ExpireContributions(killer, "supportContributions");
        var nextVictim = Pokemon("Next victim", "Orange"); Call(nextVictim, "ResolveDamage", Damage(killer, 5000));
        Assert.That(Assistants(nextVictim), Is.Empty);
        yield return null;
    }

    [UnityTest]
    public IEnumerator DespawnedPlayersAreExcludedButDeadContributorsKeepTheirParticipation()
    {
        var killer = Pokemon("Killer"); var helper = Pokemon("Dead helper"); var disconnected = Pokemon("Disconnected helper");
        var victim = Pokemon("Victim", "Orange");
        Call(victim, "ResolveDamage", Damage(helper, 10)); Call(victim, "ResolveDamage", Damage(disconnected, 10));
        Call(helper, "ResolveDamage", Damage(victim, 5000));
        Call(disconnected.GetComponent(Find("Unity.Netcode.NetworkObject")), "Despawn", true);
        Call(victim, "ResolveDamage", Damage(killer, 5000));
        CollectionAssert.AreEqual(new[] { helper }, Assistants(victim));
        yield return null;
    }

    [UnityTest]
    public IEnumerator AssistAwardsCannotBeRepeatedByPublishingTheSameKoAgain()
    {
        var killer = Pokemon("Killer"); var helper = Pokemon("Helper"); var victim = Pokemon("Victim", "Orange");
        Equip(helper, "DriveLens");
        Call(victim, "ResolveDamage", Damage(helper, 10)); Call(victim, "ResolveDamage", Damage(killer, 5000));
        Call(victim, "PublishKnockout", Field(victim, "resolvedKnockout"));
        Assert.That(Call(helper, "GetPreciseStatChange", EnumValue("Stat", "SpAttack")), Is.EqualTo(0.6f).Within(0.0001));
        yield return null;
    }
}
