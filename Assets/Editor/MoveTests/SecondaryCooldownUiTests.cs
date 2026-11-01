using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class SecondaryCooldownUiTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object Call(object target, string method, params object[] arguments)
        => target.GetType().GetMethod(method, Members).Invoke(target, arguments);
    private static void Set(object target, string field, object value)
        => target.GetType().GetField(field, Members).SetValue(target, value);
    private static GameObject Visual(Component ui, string field)
    {
        UnityEngine.Object value = new SerializedObject(ui).FindProperty(field).objectReferenceValue;
        return value is GameObject gameObject ? gameObject : ((Component)value).gameObject;
    }
    private static float Fill(Component ui)
    {
        UnityEngine.Object image = new SerializedObject(ui).FindProperty("secondaryCd").objectReferenceValue;
        return (float)image.GetType().GetProperty("fillAmount").GetValue(image);
    }
    private static void AssertMainCooldown(Component ui, bool visible)
    {
        foreach (string field in new[] { "cdLine", "cdBg", "cdText" })
            Assert.That(Visual(ui, field).activeSelf, Is.EqualTo(visible), field);
    }

    [TestCase("Assets/Prefabs/MoveUI.prefab", "JoltThunderFang")]
    [TestCase("Assets/Prefabs/MoveUIMobile.prefab", "JoltThunderFang")]
    [TestCase("Assets/Prefabs/MoveUI.prefab", "JoltDischarge")]
    [TestCase("Assets/Prefabs/MoveUIMobile.prefab", "JoltDischarge")]
    [TestCase("Assets/Prefabs/MoveUI.prefab", "FlareonTakedown")]
    [TestCase("Assets/Prefabs/MoveUIMobile.prefab", "FlareonTakedown")]
    [TestCase("Assets/Prefabs/MoveUI.prefab", "ZoroarkNightSlash")]
    [TestCase("Assets/Prefabs/MoveUIMobile.prefab", "ZoroarkNightSlash")]
    public void RecastTimerOverridesMainCooldownAndCountsDown(string path, string moveName)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Component ui = root.GetComponent(GameType("MoveUI"));
            object move = Activator.CreateInstance(GameType(moveName));
            Call(ui, "BindMove", move);
            Call(ui, "StartCooldown", 10f, 10f);
            AssertMainCooldown(ui, true);
            Call(ui, "ShowSecondaryCooldown", 5f);
            AssertMainCooldown(ui, false);
            Assert.That(Visual(ui, "secondaryCd").activeSelf, Is.True);
            Assert.That(Fill(ui), Is.EqualTo(1f).Within(0.001f));

            Set(ui, "secondaryCooldownEndsAt", Time.time + 2.5f);
            Call(ui, "LateUpdate");
            Assert.That(Fill(ui), Is.EqualTo(0.5f).Within(0.001f));

            Call(ui, "StartCooldown", 6f, 10f);
            Call(ui, "BindMove", move);
            Call(ui, "OnEnable");
            AssertMainCooldown(ui, false);
            Assert.That(Fill(ui), Is.EqualTo(0.5f).Within(0.001f));

            Call(ui, "ShowSecondaryCooldown", 0f);
            Assert.That(Visual(ui, "secondaryCd").activeSelf, Is.False);
            AssertMainCooldown(ui, true);
            Assert.That(Quaternion.Angle(Visual(ui, "cdLine").transform.localRotation,
                Quaternion.Euler(0, 0, -144f)), Is.LessThan(0.01f));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [TestCase("Assets/Prefabs/MoveUI.prefab", true)]
    [TestCase("Assets/Prefabs/MoveUIMobile.prefab", true)]
    [TestCase("Assets/Prefabs/MoveUI.prefab", false)]
    [TestCase("Assets/Prefabs/MoveUIMobile.prefab", false)]
    public void RecastExpiryRestoresOnlyUnfinishedMainCooldowns(string path, bool mainStillCoolingDown)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Component ui = root.GetComponent(GameType("MoveUI"));
            Call(ui, "StartCooldown", 10f, 10f);
            Call(ui, "ShowSecondaryCooldown", 5f);
            root.SetActive(false);
            Set(ui, "secondaryCooldownEndsAt", Time.time - 1f);
            Set(ui, "cooldownEndsAt", Time.time + (mainStillCoolingDown ? 3f : -1f));
            root.SetActive(true);
            Call(ui, "OnEnable");
            Assert.That(Visual(ui, "secondaryCd").activeSelf, Is.False);
            Assert.That(Fill(ui), Is.Zero);
            AssertMainCooldown(ui, mainStillCoolingDown);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [TestCase("Assets/Prefabs/MoveUI.prefab")]
    [TestCase("Assets/Prefabs/MoveUIMobile.prefab")]
    public void RecastWithDeferredMainCooldownStaysReadyUntilTheWindowEnds(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Component ui = root.GetComponent(GameType("MoveUI"));
            Call(ui, "BindMove", Activator.CreateInstance(GameType("ZoroarkNightSlash")));
            Call(ui, "ShowSecondaryCooldown", 5f);
            Call(ui, "StartCooldown", 0f, 7f);
            AssertMainCooldown(ui, false);
            Assert.That(Visual(ui, "secondaryCd").activeSelf, Is.True);
            Call(ui, "ShowSecondaryCooldown", 0f);
            AssertMainCooldown(ui, false);
            Call(ui, "StartCooldown", 7f, 7f);
            AssertMainCooldown(ui, true);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [TestCase("Assets/Prefabs/MoveUI.prefab")]
    [TestCase("Assets/Prefabs/MoveUIMobile.prefab")]
    public void RebindingAnotherMoveClearsBothTimers(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Component ui = root.GetComponent(GameType("MoveUI"));
            Call(ui, "BindMove", Activator.CreateInstance(GameType("JoltThunderFang")));
            Call(ui, "StartCooldown", 7f, 7f);
            Call(ui, "ShowSecondaryCooldown", 3f);
            Call(ui, "BindMove", Activator.CreateInstance(GameType("JoltDischarge")));
            AssertMainCooldown(ui, false);
            Assert.That(Visual(ui, "secondaryCd").activeSelf, Is.False);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [TestCase("Assets/Prefabs/MoveUI.prefab")]
    [TestCase("Assets/Prefabs/MoveUIMobile.prefab")]
    public void RecastHidesStoredUseAvailabilityButNotReserveRecovery(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Component ui = root.GetComponent(GameType("MoveUI"));
            object move = Activator.CreateInstance(GameType("DuraDragonTail"));
            Call(move, "UpdateStoredUses", Time.time, 0f);
            object state = GameType("MoveBase").GetField("useState", Members).GetValue(move);
            Call(state, "TrySpend", Time.time, 2f);
            Call(ui, "BindMove", move);
            AssertMainCooldown(ui, true);
            Call(ui, "ShowSecondaryCooldown", 1f);
            Call(ui, "LateUpdate");
            AssertMainCooldown(ui, false);
            Assert.That(Visual(ui, "storedCdLine").activeSelf, Is.True);
            Call(ui, "ShowSecondaryCooldown", 0f);
            AssertMainCooldown(ui, true);
            Assert.That(Visual(ui, "storedCdLine").activeSelf, Is.True);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
