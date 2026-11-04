using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public abstract partial class HeldItemServerTests
{
    private double BushDeadline(Component pokemon) => (double)Get(Field(pokemon, "bushCombatRevealEndsAt"), "Value");
    private bool BushRevealed(Component pokemon) => (bool)Get(pokemon, "IsRevealedByBushCombat");
    private void BushDeadline(Component pokemon, double deadline)
    {
        object variable = Field(pokemon, "bushCombatRevealEndsAt");
        variable.GetType().GetProperty("Value").SetValue(variable, deadline);
    }
    private double ServerNow => (double)Get(Get(server, "ServerTime"), "Time");
    private Component BushVision(Component pokemon)
    {
        var body = pokemon.GetComponent<BoxCollider>();
        if (body == null) body = pokemon.gameObject.AddComponent<BoxCollider>();
        var vision = pokemon.GetComponent(Find("Vision"));
        if (vision == null) vision = pokemon.gameObject.AddComponent(Find("Vision"));
        return vision;
    }
    private BoxCollider VisionGrass(Vector3 position)
    {
        var root = Node("shared grass root"); var leaf = Node("grass collider"); leaf.transform.SetParent(root.transform);
        leaf.transform.position = position;
        var grass = leaf.AddComponent<BoxCollider>(); grass.size = Vector3.one * 4; grass.isTrigger = true;
        leaf.AddComponent(Find("BushCollider")); return grass;
    }

    [UnityTest]
    public IEnumerator BushCombatRevealsBothPlayersForFiveSecondsAndRefreshesOnContact()
    {
        var source = Pokemon("bush attacker"); var target = Pokemon("bush victim", "Orange");
        double before = ServerNow;
        Call(target, "ResolveDamage", Damage(source, 100));
        Assert.That(BushRevealed(source), Is.True); Assert.That(BushRevealed(target), Is.True);
        Assert.That(BushDeadline(source), Is.InRange(before + 5, ServerNow + 5));
        Assert.That(BushDeadline(target), Is.EqualTo(BushDeadline(source)));
        double first = BushDeadline(source);
        yield return new WaitForSeconds(0.15f);
        Call(source, "ResolveDamage", Damage(target, 10));
        Assert.That(BushDeadline(source), Is.GreaterThan(first));
        Assert.That(BushDeadline(source), Is.EqualTo(BushDeadline(target)));
        yield return new WaitForSeconds(5.1f);
        Assert.That(BushRevealed(source), Is.False); Assert.That(BushRevealed(target), Is.False);
    }

    [UnityTest]
    public IEnumerator WildCombatMissesBuffsHealingFriendlyAndBlockedDamageDoNotReveal()
    {
        var source = Pokemon("hidden farmer"); var wild = Pokemon("wild", "Orange");
        wild.GetType().GetProperty("Type").SetValue(wild, EnumValue("PokemonType", "Wild"));
        Call(wild, "ResolveDamage", Damage(source, 100)); Call(source, "ResolveDamage", Damage(wild, 100));
        Call(source, "NotifyOffensiveAction"); Call(source, "HealDamageRPC", 50);
        var ally = Pokemon("ally"); Call(ally, "ResolveDamage", Damage(source, 100));
        var target = Pokemon("untouched opponent", "Orange");
        Call(target, "ResolveDamage", Damage(source, 0));
        object invincible = Activator.CreateInstance(Find("StatusEffect"), EnumValue("StatusType", "Invincible"), 1f, true, (ushort)59990);
        Call(target, "AddStatusEffect", invincible); Call(target, "ResolveDamage", Damage(source, 100));
        foreach (var pokemon in new[] { source, wild, ally, target }) Assert.That(BushRevealed(pokemon), Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ShieldAndSecondaryPvpDamageCountButFarmingCannotExtendTheDeadline()
    {
        var source = Pokemon("shield attacker"); var target = Pokemon("shield victim", "Orange");
        Call(target, "AddShieldRPC", Shield(1000, 59991));
        object secondary = Damage(source, 100, false);
        secondary.GetType().GetField("suppressOnHitEffects").SetValue(secondary, true);
        Call(target, "ResolveDamage", secondary);
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(5000));
        Assert.That(BushRevealed(source), Is.True); Assert.That(BushRevealed(target), Is.True);
        double deadline = BushDeadline(source);
        yield return new WaitForSeconds(0.1f);
        var wild = Pokemon("farm target", "Orange");
        wild.GetType().GetProperty("Type").SetValue(wild, EnumValue("PokemonType", "Wild"));
        Call(wild, "ResolveDamage", Damage(source, 100));
        Assert.That(BushDeadline(source), Is.EqualTo(deadline));
    }

    [UnityTest]
    public IEnumerator EnteringGrassDuringPvpDoesNotHideUntilExpiryAndStillNeedsNormalVision()
    {
        var source = Pokemon("visible attacker"); var target = Pokemon("victim", "Orange");
        var vision = BushVision(source);
        source.transform.position = new Vector3(1000, 1000, 1000); target.transform.position = source.transform.position + Vector3.right * 2;
        var observer = Node("enemy observer"); observer.transform.position = target.transform.position;
        var controller = observer.AddComponent(Find("VisionController"));
        var grass = VisionGrass(source.transform.position);
        Call(source, "NotifyOffensiveAction");
        Physics.SyncTransforms(); Call(vision, "RefreshBushOverlaps");
        Assert.That(Get(vision, "IsInBush"), Is.True);
        Assert.That(Call(controller, "CanSee", vision), Is.False);
        Call(target, "ResolveDamage", Damage(source, 10));
        Assert.That(Call(controller, "CanSee", vision), Is.True);
        Call(vision, "ClearTransientState"); Call(vision, "RefreshBushOverlaps");
        Assert.That(Call(controller, "CanSee", vision), Is.True, "Re-entering grass cannot reset combat.");
        observer.transform.position += Vector3.right * 100;
        Assert.That(Call(controller, "CanSee", vision), Is.False, "Combat reveal must not grant map-wide sight.");
        observer.transform.position = target.transform.position;
        controller.GetType().GetProperty("IsBlinded").SetValue(controller, true);
        Assert.That(Call(controller, "CanSee", vision), Is.False);
        controller.GetType().GetProperty("IsBlinded").SetValue(controller, false);
        vision.GetType().GetProperty("IsVisiblyEligible").SetValue(vision, false);
        Assert.That(Call(controller, "CanSee", vision), Is.False, "Combat cannot override ability invisibility.");
        vision.GetType().GetProperty("IsVisiblyEligible").SetValue(vision, true);
        BushDeadline(source, ServerNow - 1);
        Assert.That(Call(controller, "CanSee", vision), Is.False);
        source.transform.position += Vector3.forward * 20; Physics.SyncTransforms(); Call(vision, "RefreshBushOverlaps");
        Assert.That(Get(vision, "IsInBush"), Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator RespawnAndTeamChangesClearCombatRevealWithoutTouchingOtherPlayers()
    {
        var source = Pokemon("respawning player"); var target = Pokemon("opponent", "Orange");
        Call(target, "ResolveDamage", Damage(source, 100));
        Health(source, 0); Assert.That(BushRevealed(source), Is.False);
        Call(source, "RestoreHealthOnRespawnRPC"); Assert.That(BushRevealed(source), Is.False);
        Assert.That(BushRevealed(target), Is.True);
        Call(target, "ResolveDamage", Damage(source, 100)); Assert.That(BushRevealed(source), Is.True);
        Call(source, "UpdateTeamRPC", EnumValue("Team", "Orange"));
        Assert.That(BushRevealed(source), Is.False); Assert.That(BushRevealed(target), Is.True);
        yield return null;
    }

    [UnityTest]
    public IEnumerator LateJoiningClientReceivesCombatDeadlineAndItsExpiry()
    {
        var source = Pokemon("replicated combat source"); var target = Pokemon("replicated combat target", "Orange");
        Call(target, "ResolveDamage", Damage(source, 10));
        var root = Node("bush vision remote client");
        Type managerType = Find("Unity.Netcode.NetworkManager"); client = root.AddComponent(managerType);
        var transport = root.AddComponent(Find("Unity.Netcode.Transports.UTP.UnityTransport"));
        transport.GetType().GetMethods().First(method => method.Name == "SetConnectionData" && method.GetParameters().Length == 3 && method.GetParameters()[0].ParameterType == typeof(string))
            .Invoke(transport, new object[] { "127.0.0.1", port, "127.0.0.1" });
        object config = Activator.CreateInstance(Find("Unity.Netcode.NetworkConfig"));
        managerType.GetField("NetworkConfig").SetValue(client, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport);
        config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
        Call(client, "AddNetworkPrefab", prefab); Assert.That(Call(client, "StartClient"), Is.True);
        var spawned = (IDictionary)Get(client, "SpawnManager").GetType().GetField("SpawnedObjects").GetValue(Get(client, "SpawnManager"));
        object sourceId = Get(source, "NetworkObjectId");
        float until = Time.realtimeSinceStartup + 8;
        while (!spawned.Contains(sourceId) && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(spawned.Contains(sourceId), Is.True);
        var remoteSource = ((Component)spawned[sourceId]).GetComponent(Find("Pokemon"));
        Assert.That(BushDeadline(remoteSource), Is.EqualTo(BushDeadline(source)));
        Assert.That(BushRevealed(remoteSource), Is.True);
        BushDeadline(source, ServerNow - 1);
        until = Time.realtimeSinceStartup + 4;
        while (BushRevealed(remoteSource) && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(BushRevealed(remoteSource), Is.False);
        Call(target, "ResolveDamage", Damage(source, 10));
        until = Time.realtimeSinceStartup + 4;
        while (!BushRevealed(remoteSource) && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(BushRevealed(remoteSource), Is.True);
        Assert.That(BushDeadline(remoteSource), Is.EqualTo(BushDeadline(source)));
    }
}
