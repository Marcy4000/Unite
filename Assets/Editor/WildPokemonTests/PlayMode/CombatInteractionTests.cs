using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Real server + two clients. Gameplay lives in Assembly-CSharp, hence reflection.
public abstract class CombatInteractionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<Component> clients = new List<Component>();
    private readonly List<Action> unsubscribe = new List<Action>();
    private Component server;
    private GameObject prefab;
    private ScriptableObject stats;
    protected abstract bool Host { get; }

    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object E(string type, string value) => Enum.Parse(T(type), value);
    private static object Get(object obj, string name) => obj.GetType().GetProperty(name).GetValue(obj);
    private static object Field(object obj, string name) => obj.GetType().GetField(name, Private).GetValue(obj);
    private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
    private static object Call(object obj, string name, params object[] args) => obj.GetType()
        .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        .First(m => m.Name == name && m.GetParameters().Length == args.Length
            && m.GetParameters().Select((p, i) => p.ParameterType.IsInstanceOfType(args[i])).All(match => match)).Invoke(obj, args);
    private static void Hp(Component pokemon, int value) => Field(pokemon, "currentHp").GetType().GetProperty("Value").SetValue(Field(pokemon, "currentHp"), value);
    private static int Count(object collection) => (int)Get(collection, "Count");
    private static object Stat(ushort id, string kind = "Speed", short amount = 10, float duration = 10, bool buff = true)
        => Activator.CreateInstance(T("StatChange"), amount, E("Stat", kind), duration, true, buff, false, id, true);
    private static object Status(ushort id, string kind = "Scriptable", float duration = 10)
        => Activator.CreateInstance(T("StatusEffect"), E("StatusType", kind), duration, true, id);
    private static object Shield(ushort id, int amount = 100, float duration = 10, ushort priority = 0, ushort cap = 0)
        => Activator.CreateInstance(T("ShieldInfo"), amount, id, priority, duration, true, cap);
    private static object Damage(Component source, short amount, string kind = "Physical")
        => Activator.CreateInstance(T("DamageInfo"), (ulong)Get(source, "NetworkObjectId"), 0f, (short)0, amount, E("DamageType", kind), E("DamageProprieties", "None"));

    private void Observe(object target, string name, Action<object[]> callback)
    {
        var ev = target.GetType().GetEvent(name);
        var parameters = ev.EventHandlerType.GetMethod("Invoke").GetParameters().Select(p => Expression.Parameter(p.ParameterType)).ToArray();
        var body = Expression.Invoke(Expression.Constant(callback), Expression.NewArrayInit(typeof(object), parameters.Select(p => Expression.Convert(p, typeof(object)))));
        var handler = Expression.Lambda(ev.EventHandlerType, body, parameters).Compile();
        ev.AddEventHandler(target, handler);
        unsubscribe.Add(() => ev.RemoveEventHandler(target, handler));
    }

    private GameObject Node(string name)
    {
        var node = new GameObject(name); objects.Add(node); return node;
    }

    private Component Manager(string name, ushort port)
    {
        var root = Node(name);
        var manager = root.AddComponent(T("Unity.Netcode.NetworkManager"));
        var transport = root.AddComponent(T("Unity.Netcode.Transports.UTP.UnityTransport"));
        Call(transport, "SetConnectionData", "127.0.0.1", port, "127.0.0.1");
        var config = Activator.CreateInstance(T("Unity.Netcode.NetworkConfig"));
        manager.GetType().GetField("NetworkConfig").SetValue(manager, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport);
        config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
        Call(manager, "AddNetworkPrefab", prefab);
        return manager;
    }

    private static IEnumerator Until(Func<bool> condition, string message)
    {
        float deadline = Time.realtimeSinceStartup + 5;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, message);
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        if (T("Unity.Netcode.NetworkManager").GetProperty("Singleton").GetValue(null) != null)
            Assert.Ignore("An external session is active.");
#if UNITY_EDITOR
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/FlareonTests/Fixtures/PeriodicPokemon.prefab");
        Assert.That(prefab, Is.Not.Null);
#else
        Assert.Ignore("The isolated fixture is Editor-only.");
#endif
        stats = ScriptableObject.CreateInstance(T("PokemonBase"));
        Set(stats, "maxHp", Enumerable.Repeat(5000, 22).ToArray());
        Set(stats, "attack", Enumerable.Repeat(100, 22).ToArray());
        Set(stats, "spAttack", Enumerable.Repeat(100, 22).ToArray());
        Set(stats, "evolutions", Array.CreateInstance(T("PokemonEvolution"), 0));
        server = Manager("Combat server", 0);
        Assert.That(Call(server, Host ? "StartHost" : "StartServer"), Is.True);
        var transport = server.GetComponent(T("Unity.Netcode.Transports.UTP.UnityTransport"));
        ushort port = (ushort)Get(Call(transport, "GetLocalEndpoint"), "Port");
        for (int i = 0; i < 2; i++)
        {
            var client = Manager("Combat client " + i, port); clients.Add(client);
            Assert.That(Call(client, "StartClient"), Is.True);
            yield return Until(() => (bool)Get(client, "IsConnectedClient"), "Client did not connect.");
        }
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (var action in unsubscribe) action(); unsubscribe.Clear();
        foreach (var client in clients) if (client != null) Call(client, "Shutdown", false);
        yield return null;
        if (server != null)
        {
            foreach (var node in objects.ToArray())
            {
                if (node == null) continue;
                var net = node.GetComponent(T("Unity.Netcode.NetworkObject"));
                if (net != null && (bool)Get(net, "IsSpawned")) Call(net, "Despawn", true);
            }
            Call(server, "Shutdown", false);
        }
        yield return null;
        foreach (var client in clients) if (client != null) UnityEngine.Object.Destroy(client.gameObject);
        yield return null;
        foreach (var node in objects) if (node != null) UnityEngine.Object.Destroy(node);
        if (stats != null) UnityEngine.Object.Destroy(stats);
        clients.Clear(); objects.Clear();
        yield return null;
    }

    private Component Spawn(ulong owner = 0)
    {
        var node = UnityEngine.Object.Instantiate(prefab); objects.Add(node);
        var pokemon = node.GetComponent(T("Pokemon"));
        Call(node.GetComponent(T("Unity.Netcode.NetworkObject")), "SpawnWithOwnership", owner, true);
        Call(pokemon, "SetNewPokemon", stats);
        return pokemon;
    }

    private Component Remote(Component pokemon, Component client)
    {
        var spawned = (IDictionary)Get(client, "SpawnManager").GetType().GetField("SpawnedObjects").GetValue(Get(client, "SpawnManager"));
        object id = Get(pokemon, "NetworkObjectId");
        return spawned.Contains(id) ? ((Component)spawned[id]).GetComponent(T("Pokemon")) : null;
    }

    private IEnumerator Replicate(params Component[] pokemons)
    {
        foreach (var pokemon in pokemons)
            foreach (var client in clients)
            {
                yield return Until(() => Remote(pokemon, client) != null, "Pokemon did not replicate.");
                Call(Remote(pokemon, client), "SetNewPokemon", stats);
            }
    }

    private void AssertTimers(Component pokemon)
    {
        Assert.That(Count(Field(pokemon, "statTimers")), Is.EqualTo(Count(Get(pokemon, "StatChanges"))));
        Assert.That(Count(Field(pokemon, "statusTimers")), Is.EqualTo(Count(Get(pokemon, "StatusEffects"))));
        Assert.That(Count(Field(pokemon, "shieldTimers")), Is.EqualTo(Count(Get(pokemon, "Shields"))));
    }

    [UnityTest]
    public IEnumerator StatCallbacksFireOnceAndCanImmediatelyReplaceTheStat()
    {
        var pokemon = Spawn(); yield return Replicate(pokemon);
        int adds = 0, removes = 0;
        Observe(pokemon, "OnStatChange", args =>
        {
            AssertTimers(pokemon);
            var type = args[0].GetType().GetField("Type").GetValue(args[0]).ToString();
            var value = args[0].GetType().GetField("Value").GetValue(args[0]);
            if (type == "Add")
            {
                adds++;
                if ((ushort)value.GetType().GetField("ID").GetValue(value) == 10)
                {
                    Call(pokemon, "RemoveStatChangeRPC", value);
                    Call(pokemon, "AddStatChange", Stat(11));
                }
            }
            if (type == "RemoveAt") removes++;
        });
        Call(pokemon, "AddStatChange", Stat(10));
        Assert.That(adds, Is.EqualTo(2));
        Assert.That(removes, Is.EqualTo(1));
        AssertTimers(pokemon);
        Assert.That(Count(Get(pokemon, "StatChanges")), Is.EqualTo(1));
        foreach (var client in clients)
            yield return Until(() => (bool)Call(Remote(pokemon, client), "HasStatChangeWithID", (ushort)11), "Replacement stat did not replicate.");
    }

    [UnityTest]
    public IEnumerator ReentrantCleansesKeepListsAndTimersConsistent()
    {
        var pokemon = Spawn(); yield return Replicate(pokemon);
        for (ushort i = 10; i < 13; i++) { Call(pokemon, "AddStatChange", Stat(i)); Call(pokemon, "AddStatusEffect", Status(i)); }
        Observe(pokemon, "OnStatChange", _ =>
        {
            AssertTimers(pokemon);
            Call(pokemon, "RemoveAllStatChangesRPC");
        });
        Observe(pokemon, "OnStatusChange", args =>
        {
            AssertTimers(pokemon);
            if (!(bool)args[1])
            {
                Assert.That(Call(pokemon, "HasStatusEffect", args[0]), Is.False, "Removal notification ran before removal.");
                Call(pokemon, "ClearStatusEffectsRPC");
            }
        });
        Call(pokemon, "RemoveAllStatChangesRPC");
        Call(pokemon, "ClearStatusEffectsRPC");
        AssertTimers(pokemon);
        Assert.That(Count(Get(pokemon, "StatChanges")), Is.Zero);
        Assert.That(Count(Get(pokemon, "StatusEffects")), Is.Zero);
    }

    [UnityTest]
    public IEnumerator PendingHealsCannotReviveButTheOwnerCanExplicitlyRespawn()
    {
        var source = Spawn();
        var target = Spawn((ulong)Get(clients[0], "LocalClientId"));
        yield return Replicate(source, target);
        var remote = Remote(target, clients[0]);
        Call(remote, "TakeDamageRPC", Damage(source, 6000, "True"));
        Call(remote, "HealDamageRPC", 1000);
        Call(remote, "HealDamageRPC", Damage(source, 1000));
        yield return Until(() => (int)Get(target, "CurrentHp") == 0, "Lethal damage did not arrive.");
        yield return new WaitForSeconds(0.15f);
        Assert.That(Get(target, "CurrentHp"), Is.Zero);
        var rpc = Activator.CreateInstance(T("Unity.Netcode.RpcParams"));
        Call(Remote(target, clients[1]), "RestoreHealthOnRespawnRPC", rpc);
        yield return new WaitForSeconds(0.15f);
        Assert.That(Get(target, "CurrentHp"), Is.Zero, "A different client must not respawn this player.");
        Call(remote, "RestoreHealthOnRespawnRPC", rpc);
        yield return Until(() => (int)Get(target, "CurrentHp") == 5000, "Explicit owner respawn did not restore HP.");
        foreach (var client in clients)
            yield return Until(() => (int)Get(Remote(target, client), "CurrentHp") == 5000, "Respawn HP did not replicate.");
    }

    [UnityTest]
    public IEnumerator DamagePreservesEverySurvivingShieldTimerAndMetadata()
    {
        var source = Spawn(); var target = Spawn(); yield return Replicate(source, target);
        Call(target, "AddShieldRPC", Shield(10, priority: 2, cap: 300));
        Call(target, "AddShieldRPC", Shield(11, priority: 1));
        var timers = (IList)Field(target, "shieldTimers"); timers[0] = 0.25f; timers[1] = 0.4f;
        Call(target, "TakeDamageRPC", Damage(source, 30));
        Assert.That(timers[0], Is.EqualTo(0.25f));
        Assert.That(timers[1], Is.EqualTo(0.4f), "Even a shield that absorbed nothing must keep its lifetime.");
        var shield = Call(target, "GetShieldWithID", (ushort)10);
        Assert.That(shield.GetType().GetField("Amount").GetValue(shield), Is.EqualTo(70));
        Assert.That(shield.GetType().GetField("MaxStacks").GetValue(shield), Is.EqualTo(300));
        yield return new WaitForSeconds(0.5f);
        Assert.That(Get(target, "ShieldHp"), Is.Zero, "Damage must not refresh shield expiry.");
        AssertTimers(target);
    }

    [UnityTest]
    public IEnumerator RapidShieldChangesNotifyTheFinalStateWithoutWaitingForOldCounts()
    {
        var pokemon = Spawn(); yield return Replicate(pokemon);
        yield return null; yield return null;
        int notifications = 0;
        Observe(pokemon, "OnShieldChange", _ => { notifications++; AssertTimers(pokemon); });
        Call(pokemon, "AddShieldRPC", Shield(10)); Call(pokemon, "AddShieldRPC", Shield(11));
        Call(pokemon, "RemoveShieldWithIDRPC", (ushort)10); Call(pokemon, "RemoveShieldWithIDRPC", (ushort)11);
        Call(pokemon, "AddShieldRPC", Shield(12));
        yield return null; yield return null;
        Assert.That(notifications, Is.EqualTo(1));
        Assert.That(Field(pokemon, "shieldChangePending"), Is.False);
        foreach (var client in clients)
            yield return Until(() => (int)Get(Remote(pokemon, client), "ShieldHp") == 100
                && !(bool)Field(Remote(pokemon, client), "shieldChangePending"), "Client shield notifications did not settle.");
        yield return new WaitForSeconds(0.1f);
        Assert.That(notifications, Is.EqualTo(1), "Obsolete notifications must not remain pending.");
    }

    [UnityTest]
    public IEnumerator StatusCallbacksRunExactlyOnceOnServerAndBothClients()
    {
        var pokemon = Spawn(); yield return Replicate(pokemon);
        int[] added = new int[3], removed = new int[3];
        var peers = new[] { pokemon, Remote(pokemon, clients[0]), Remote(pokemon, clients[1]) };
        for (int i = 0; i < peers.Length; i++)
        {
            int peer = i;
            Observe(peers[i], "OnStatusChange", args => { if ((bool)args[1]) added[peer]++; else removed[peer]++; });
        }
        Call(pokemon, "AddStatusEffect", Status(10, "Incapacitated"));
        yield return Until(() => added.All(n => n == 1), "Every peer, including a dedicated server, must receive status additions.");
        Call(pokemon, "RemoveStatusEffectWithID", (ushort)10);
        yield return Until(() => removed.All(n => n == 1), "Every peer must receive status removals exactly once.");
        Assert.That(added, Is.EqualTo(new[] { 1, 1, 1 }));
        Assert.That(removed, Is.EqualTo(new[] { 1, 1, 1 }));
    }

    [UnityTest]
    public IEnumerator InactiveShieldRecipientsNotifyOnceWhenTheyResume()
    {
        var pokemon = Spawn(); yield return Replicate(pokemon);
        yield return null; yield return null;
        int notifications = 0;
        Observe(pokemon, "OnShieldChange", _ => notifications++);
        pokemon.gameObject.SetActive(false);
        Call(pokemon, "AddShieldRPC", Shield(10));
        Call(pokemon, "AddShieldRPC", Shield(11));
        Call(pokemon, "RemoveShieldWithIDRPC", (ushort)10);
        yield return null; yield return null;
        Assert.That(notifications, Is.Zero);
        pokemon.gameObject.SetActive(true);
        yield return null; yield return null;
        Assert.That(notifications, Is.EqualTo(1));
        Assert.That(Get(pokemon, "ShieldHp"), Is.EqualTo(100));
        Assert.That(Field(pokemon, "shieldChangePending"), Is.False);
    }

    [UnityTest]
    public IEnumerator MaximumHpExpiryClampsHealthAndIncreasingMaximumDoesNotHeal()
    {
        var pokemon = Spawn(); yield return Replicate(pokemon);
        Call(pokemon, "AddStatChange", Stat(10, "MaxHp", 1000, 0.15f));
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(5000));
        Call(pokemon, "HealDamageRPC", 1000);
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(6000));
        yield return Until(() => !(bool)Call(pokemon, "HasStatChangeWithID", (ushort)10), "Max HP buff did not expire.");
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(5000));
        Call(pokemon, "HealDamageRPC", 50);
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(5000));
        Call(pokemon, "AddStatChange", Stat(11, "MaxHp", 1000, buff: false));
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(4000));
        Call(pokemon, "RemoveStatChangeWithIDRPC", (ushort)11);
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(4000), "Removing a max HP debuff must not grant healing.");
        foreach (var client in clients)
            yield return Until(() => (int)Get(Remote(pokemon, client), "CurrentHp") == 4000, "Clamped health did not replicate.");
    }

    private Component PlayerControlsView(Component pokemon)
    {
        var node = Node("UI-free status recipient"); node.SetActive(false);
        var movement = node.AddComponent(T("PlayerMovement"));
        movement.GetType().GetProperty("CanMove").SetValue(movement, true);
        var animation = node.AddComponent(T("AnimationManager"));
        var moves = node.AddComponent(T("MovesController"));
        var statuses = Array.CreateInstance(T("BattleActionStatus"), 2);
        var learned = Array.CreateInstance(T("MoveBase"), 2);
        for (int i = 0; i < 2; i++)
        {
            statuses.SetValue(Activator.CreateInstance(T("BattleActionStatus"), 0f), i);
            learned.SetValue(Activator.CreateInstance(T("LockedMove")), i);
        }
        Set(moves, "moveStatuses", statuses); Set(moves, "moves", learned);
        Set(moves, "uniteMove", Activator.CreateInstance(T("LockedMove")));
        var player = node.AddComponent(T("PlayerManager"));
        Set(player, "pokemon", pokemon); Set(player, "movesController", moves);
        Set(player, "playerMovement", movement); Set(player, "animationManager", animation);
        Call(player, "InitializeStatusActions");
        Observe(pokemon, "OnStatusChange", args => Call(player, "OnPokemonStatusChange", args));
        return player;
    }

    [UnityTest]
    public IEnumerator OverlappingStunsKeepTheirLocksUntilTheLastSourceEnds()
    {
        var pokemon = Spawn(); yield return Replicate(pokemon);
        var views = new[] { PlayerControlsView(pokemon), PlayerControlsView(Remote(pokemon, clients[0])), PlayerControlsView(Remote(pokemon, clients[1])) };
        object stunned = E("ActionStatusType", "Stunned");
        foreach (var player in views) Call(Get(Get(player, "MovesController"), "BasicAttackStatus"), "AddStatus", stunned);
        Call(pokemon, "AddStatusEffect", Status(10, "Asleep", 0.1f));
        Call(pokemon, "AddStatusEffect", Status(11, "Frozen", 0.3f));
        Call(pokemon, "AddStatusEffect", Status(12, "Asleep", 5));
        yield return Until(() => views.All(v => (bool)Call(Get(v, "ScoreStatus"), "HasStatus", stunned)), "Stun locks were not acquired.");
        yield return new WaitForSeconds(0.4f);
        foreach (var player in views)
        {
            Assert.That(Call(Get(player, "ScoreStatus"), "HasStatus", stunned), Is.True);
            Assert.That(Get(Get(player, "PlayerMovement"), "CanMove"), Is.False);
        }
        Call(pokemon, "RemoveStatusEffectWithID", (ushort)12);
        yield return Until(() => views.All(v => !(bool)Call(Get(v, "ScoreStatus"), "HasStatus", stunned)), "Final stun did not release its locks.");
        foreach (var player in views)
        {
            Assert.That(Get(Get(player, "PlayerMovement"), "CanMove"), Is.True);
            Assert.That(Call(Get(Get(player, "MovesController"), "BasicAttackStatus"), "HasStatus", stunned), Is.True, "An unrelated action lock must survive.");
        }
    }

    [UnityTest]
    public IEnumerator FocusBandCannotReenterOrContinueHealingAcrossDeathAndRespawn()
    {
        var source = Spawn(); var pokemon = Spawn(); yield return Replicate(source, pokemon);
        // Only the item lifecycle is needed; do not initialize lobby/UI/controls.
        var player = pokemon.gameObject.AddComponent(T("PlayerManager")); ((Behaviour)player).enabled = false;
        var item = Activator.CreateInstance(T("FocusBand"));
        Call(item, "Initialize", player);
        Hp(pokemon, 600);
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(1200), "The immediate heal must not recursively start another recovery.");
        Call(pokemon, "TakeDamageRPC", Damage(source, 2000, "True"));
        Assert.That(Get(pokemon, "CurrentHp"), Is.Zero);
        Call(pokemon, "RestoreHealthOnRespawnRPC", Activator.CreateInstance(T("Unity.Netcode.RpcParams")));
        Call(pokemon, "TakeDamageRPC", Damage(source, 1000, "True"));
        yield return new WaitForSeconds(1.1f);
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(4000), "The old recovery must not resume in a new life.");
    }

    [UnityTest]
    public IEnumerator TwoClientsCanBurstDamageHealingAndEffectsWithoutLostUpdates()
    {
        var source = Spawn(); var target = Spawn(); yield return Replicate(source, target);
        Hp(target, 3000);
        int deaths = 0; Observe(target, "OnDeath", _ => deaths++);
        foreach (var client in clients)
        {
            var remote = Remote(target, client);
            for (ushort i = 0; i < 10; i++)
            {
                ushort id = (ushort)(100 + 10 * clients.IndexOf(client) + i);
                Call(remote, "TakeDamageRPC", Damage(source, 20, "True"));
                Call(remote, "HealDamageRPC", 5);
                Call(remote, "AddStatChange", Stat(id, duration: 0.15f));
                Call(remote, "AddStatusEffect", Status(id, duration: 0.15f));
                Call(remote, "AddShieldRPC", Shield(id, duration: 0.15f));
            }
        }
        yield return Until(() => (int)Get(target, "CurrentHp") == 2700, "Concurrent client requests lost or duplicated HP updates.");
        yield return new WaitForSeconds(0.4f);
        Assert.That(deaths, Is.Zero); AssertTimers(target);
        Assert.That(Count(Get(target, "StatChanges")), Is.Zero);
        Assert.That(Count(Get(target, "StatusEffects")), Is.Zero);
        Assert.That(Get(target, "ShieldHp"), Is.Zero);
        foreach (var client in clients)
            yield return Until(() => (int)Get(Remote(target, client), "CurrentHp") == 2700
                && Count(Get(Remote(target, client), "StatusEffects")) == 0 && (int)Get(Remote(target, client), "ShieldHp") == 0,
                "The final combat state diverged between peers.");
    }
}

public class CombatInteractionHostTests : CombatInteractionTests { protected override bool Host => true; }
public class CombatInteractionDedicatedServerTests : CombatInteractionTests { protected override bool Host => false; }
