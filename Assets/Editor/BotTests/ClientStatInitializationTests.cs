using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class ClientStatInitializationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private Component pokemon;
    private Component movement;
    private ScriptableObject stats;

    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Client stat initialization regression");
        root.SetActive(false);
        pokemon = root.AddComponent(GameType("Pokemon"));
        pokemon.GetType().GetMethod("Awake", Private).Invoke(pokemon, null);
        movement = root.AddComponent(GameType("PlayerMovement"));
        movement.GetType().GetField("pokemon", Private).SetValue(movement, pokemon);
        var refresh = movement.GetType().GetMethod("UpdateSpeed", Private, null, Type.EmptyTypes, null);
        pokemon.GetType().GetEvent("OnPokemonInitialized").AddEventHandler(pokemon,
            Delegate.CreateDelegate(typeof(Action), movement, refresh));
        var statEvent = pokemon.GetType().GetEvent("OnStatChange");
        statEvent.AddEventHandler(pokemon, Delegate.CreateDelegate(statEvent.EventHandlerType, movement,
            movement.GetType().GetMethod("UpdateSpeed", Private, null,
                new[] { typeof(NetworkListEvent<>).MakeGenericType(GameType("StatChange")) }, null)));
        stats = ScriptableObject.CreateInstance(GameType("PokemonBase"));
        stats.GetType().GetField("speed", Private).SetValue(stats, Enumerable.Repeat(4100, 15).ToArray());
        stats.GetType().GetField("evolutions", Private).SetValue(stats, Array.CreateInstance(GameType("PokemonEvolution"), 0));
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(root);
        UnityEngine.Object.DestroyImmediate(stats);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void StatDeltaBatchCompletesWhetherStatsLoadBeforeOrAfterReplication(bool loadBeforeReplication)
    {
        if (loadBeforeReplication) InitializePokemon();
        typeof(ClientStatInitializationTests).GetMethod(nameof(ReadStatDeltas), Private)
            .MakeGenericMethod(GameType("StatChange")).Invoke(this, null);
        if (!loadBeforeReplication)
        {
            Assert.That(MoveSpeed(), Is.EqualTo(4.1f));
            InitializePokemon();
        }
        Assert.That(MoveSpeed(), Is.EqualTo(4.9f).Within(0.0001f));
    }

    private void ReadStatDeltas<T>() where T : unmanaged, INetworkSerializable, IEquatable<T>
    {
        var manager = root.AddComponent<NetworkManager>();
        var changes = (NetworkList<T>)pokemon.GetType().GetProperty("StatChanges").GetValue(pokemon);
        typeof(NetworkVariableBase).GetField("m_NetworkManager", Private).SetValue(changes, manager);
        object boxedChange = default(T);
        typeof(T).GetField("AffectedStat").SetValue(boxedChange, Enum.Parse(GameType("Stat"), "Speed"));
        typeof(T).GetField("Amount").SetValue(boxedChange, (short)100);
        typeof(T).GetField("IsBuff").SetValue(boxedChange, true);
        var change = (T)boxedChange;
        using (var writer = new FastBufferWriter(2048, Allocator.Temp))
        {
            writer.WriteValueSafe((ushort)9);
            for (int index = 0; index < 9; index++)
            {
                writer.WriteValueSafe(NetworkListEvent<T>.EventType.Add);
                writer.WriteNetworkSerializable(change);
            }
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
                Assert.DoesNotThrow(() => changes.ReadDelta(reader, false));
        }
        Assert.That(changes.Count, Is.EqualTo(9));
        using (var writer = new FastBufferWriter(128, Allocator.Temp))
        {
            writer.WriteValueSafe((ushort)1);
            writer.WriteValueSafe(NetworkListEvent<T>.EventType.RemoveAt);
            BytePacker.WriteValueBitPacked(writer, 8);
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
                Assert.DoesNotThrow(() => changes.ReadDelta(reader, false));
        }
        Assert.That(changes.Count, Is.EqualTo(8));
    }

    private void InitializePokemon() => pokemon.GetType().GetMethod("SetNewPokemon").Invoke(pokemon, new object[] { stats });
    private float MoveSpeed() => (float)movement.GetType().GetField("moveSpeed", Private).GetValue(movement);
}
