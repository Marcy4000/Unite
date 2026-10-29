using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class WildPokemonSpawnerRespawnTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase("TimedRespawn", true, false)]
    [TestCase("SpecificTimesRespawn", true, false)]
    [TestCase("NoRespawn", true, false)]
    [TestCase("TimedRespawn", true, true)]
    [TestCase("SpecificTimesRespawn", true, true)]
    [TestCase("TimedRespawn", false, false)]
    [TestCase("SpecificTimesRespawn", false, false)]
    public void DespawnWithoutLivingPokemonCancelsPendingSpawnsOnlyWhenRequestedOnServer(
        string respawnMode, bool isServer, bool canRespawn)
    {
        var root = new GameObject("Spawner respawn regression");
        root.SetActive(false);
        try
        {
            var spawnerType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("WildPokemonSpawner")).First(type => type != null);
            var spawner = root.AddComponent(spawnerType);
            var networkType = spawnerType.BaseType;
            networkType.GetProperty("IsServer").SetValue(spawner, isServer);
            var respawnType = spawnerType.GetNestedType("RespawnType");
            spawnerType.GetField("respawnType", Private).SetValue(spawner, Enum.Parse(respawnType, respawnMode));
            spawnerType.GetField("firstSpawnTime", Private).SetValue(spawner, 10f);
            spawnerType.GetField("timer", Private).SetValue(spawner, 5f);
            var schedule = new List<float> { 20f, 30f };
            spawnerType.GetField("specificRespawnTimes", Private).SetValue(spawner, schedule);

            spawnerType.GetMethod("DespawnPokemon").Invoke(spawner, new object[] { canRespawn });

            bool cancelled = isServer && !canRespawn;
            Assert.That(spawnerType.GetProperty("PokemonRespawnType").GetValue(spawner).ToString(),
                Is.EqualTo(cancelled ? "NoRespawn" : respawnMode));
            Assert.That(spawnerType.GetField("timer", Private).GetValue(spawner), Is.EqualTo(cancelled ? 0f : 5f));
            Assert.That(spawnerType.GetField("firstSpawnTime", Private).GetValue(spawner),
                Is.EqualTo(cancelled ? Mathf.Infinity : 10f));
            Assert.That(schedule.Count, Is.EqualTo(cancelled ? 0 : 2));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
