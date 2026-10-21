using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class MoveUseTests
{
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static object Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method).Invoke(target, args);

    private static T Property<T>(object target, string name) => (T)target.GetType().GetProperty(name).GetValue(target);

    private static object State(int capacity = 2, float duration = 10f)
    {
        object state = Activator.CreateInstance(GameType("MoveUseState"));
        Call(state, "Tick", 0f, capacity, duration);
        return state;
    }

    [Test]
    public void CannotSpendAnUninitializedState()
    {
        var state = Activator.CreateInstance(GameType("MoveUseState"));
        Assert.That(Call(state, "TrySpend", 0f, 1f), Is.False);
    }

    [Test]
    public void ReservesDoNotBypassTheDelayBetweenCasts()
    {
        object state = State();
        Assert.That(Call(state, "TrySpend", 0f, 2.5f), Is.True);
        Assert.That(Call(state, "TrySpend", 1f, 2.5f), Is.False);
        Assert.That(Property<int>(state, "Count"), Is.EqualTo(1));
        Assert.That(Call(state, "GetUseCooldown", 1f), Is.EqualTo(1.5f));
        Assert.That(Call(state, "TrySpend", 2.5f, 2.5f), Is.True);
        Assert.That(Call(state, "GetUseCooldown", 3f), Is.EqualTo(7f));
    }

    [Test]
    public void EmptyReservesRecoverSequentiallyNotInParallel()
    {
        object state = State();
        Call(state, "TrySpend", 0f, 1f);
        Call(state, "TrySpend", 1f, 1f);
        Call(state, "Tick", 10f, 2, 10f);
        Assert.That(Property<int>(state, "Count"), Is.EqualTo(1));
        Assert.That(Call(state, "GetRechargeRemaining", 10f), Is.EqualTo(10f));
        Call(state, "Tick", 20f, 2, 10f);
        Assert.That(Property<int>(state, "Count"), Is.EqualTo(2));
        Assert.That(Call(state, "GetRechargeRemaining", 20f), Is.EqualTo(0f));
    }

    [Test]
    public void SpendingAnotherReserveDoesNotRestartRecharge()
    {
        object state = State();
        Call(state, "TrySpend", 0f, 1f);
        Call(state, "TrySpend", 8f, 1f);
        Assert.That(Property<float>(state, "NextRecovery"), Is.EqualTo(10f));
    }

    [Test]
    public void CatchingUpAfterDeathCannotBankExtraCharges()
    {
        object state = State();
        Call(state, "TrySpend", 0f, 0f);
        Call(state, "TrySpend", 1f, 0f);
        Call(state, "Tick", 1000f, 2, 10f);
        Assert.That(Property<int>(state, "Count"), Is.EqualTo(2));
        Call(state, "TrySpend", 1000f, 0f);
        Assert.That(Property<float>(state, "NextRecovery"), Is.EqualTo(1010f));
    }

    [Test]
    public void CdrChangesPreserveRechargeProgressAndNotTheCastInterval()
    {
        object state = State();
        Call(state, "TrySpend", 0f, 8f);
        Call(state, "Tick", 5f, 2, 5f);
        Assert.That(Call(state, "GetRechargeRemaining", 5f), Is.EqualTo(2.5f));
        Assert.That(Call(state, "GetUseCooldown", 5f), Is.EqualTo(3f));
        Call(state, "Tick", 7.5f, 2, 5f);
        Assert.That(Property<int>(state, "Count"), Is.EqualTo(2));
        Assert.That(Call(state, "TrySpend", 7.5f, 8f), Is.False);
    }

    [Test]
    public void UpgradeGrantsOnlyTheAdditionalCapacity()
    {
        object state = State();
        Call(state, "TrySpend", 0f, 1f);
        Call(state, "TrySpend", 1f, 1f);
        Call(state, "Tick", 2f, 3, 10f);
        Call(state, "Tick", 2f, 3, 10f);
        Assert.That(Property<int>(state, "Count"), Is.EqualTo(1));
        Assert.That(Property<float>(state, "NextRecovery"), Is.EqualTo(10f));
    }

    [Test]
    public void LoweringCapacityClampsCountWithoutAnOverflow()
    {
        object state = State(3);
        Call(state, "Tick", 1f, 1, 10f);
        Assert.That(Property<int>(state, "Capacity"), Is.EqualTo(1));
        Assert.That(Property<int>(state, "Count"), Is.EqualTo(1));
        Assert.That(Call(state, "GetRechargeRemaining", 1f), Is.EqualTo(0f));
    }

    [Test]
    public void RechargeReductionCanRestoreUsesButCannotSkipCastInterval()
    {
        object state = State();
        Call(state, "TrySpend", 0f, 2.5f);
        Call(state, "TrySpend", 2.5f, 2.5f);
        Call(state, "ReduceRecharge", 100f, 3f);
        Assert.That(Property<int>(state, "Count"), Is.EqualTo(2));
        Assert.That(Call(state, "GetUseCooldown", 3f), Is.EqualTo(2f));
        Assert.That(Call(state, "TrySpend", 3f, 2.5f), Is.False);
        Assert.That(Call(state, "TrySpend", 5f, 2.5f), Is.True);
        Assert.That(Property<float>(state, "NextRecovery"), Is.EqualTo(15f));
    }

    [Test]
    public void InvalidCapacityAndZeroDurationHaveSafeMinimums()
    {
        object state = State(0, 0f);
        Assert.That(Property<int>(state, "Capacity"), Is.EqualTo(1));
        Assert.That(Property<float>(state, "RechargeDuration"), Is.GreaterThan(0));
        Call(state, "TrySpend", 0f, 0f);
        Call(state, "Tick", 10f, 0, 0f);
        Assert.That(Property<int>(state, "Count"), Is.EqualTo(1));
    }

    [Test]
    public void ExistingNormalMovesOptOutByDefault()
    {
        var move = Activator.CreateInstance(GameType("CinderEmber"));
        float originalCooldown = (float)move.GetType().GetField("Cooldown").GetValue(move);
        Call(move, "UpdateStoredUses", 0f, 30f);
        Assert.That(Property<bool>(move, "IsStoredMove"), Is.False);
        Assert.That(move.GetType().GetField("Cooldown").GetValue(move), Is.EqualTo(originalCooldown));
    }

    [TestCase("DuraDragonTail", 2, 2.5f)]
    [TestCase("DuraStealthRock", 2, 1f)]
    public void DuraludonUsesTheSharedBaseWithoutItsOwnResourceSubclass(string name, int capacity, float interval)
    {
        var move = Activator.CreateInstance(GameType(name));
        Assert.That(move.GetType().BaseType, Is.EqualTo(GameType("MoveBase")));
        Assert.That(Property<bool>(move, "IsStoredMove"), Is.True);
        Assert.That(Property<int>(move, "MaxStoredUses"), Is.EqualTo(capacity));
        Assert.That(Property<float>(move, "StoredUseInterval"), Is.EqualTo(interval));
    }

    [TestCase("Assets/Prefabs/MoveUI.prefab")]
    [TestCase("Assets/Prefabs/MoveUIMobile.prefab")]
    public void PrefabsHaveWiredReserveUiWithoutItsDuplicatedIcon(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var ui = new SerializedObject(prefab.GetComponent(GameType("MoveUI")));
        foreach (string field in new[] { "storedUsesRoot", "storedCdLine", "storedCdBg", "storedCdText", "storedUsesText" })
            Assert.That(ui.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        var root = (GameObject)ui.FindProperty("storedUsesRoot").objectReferenceValue;
        Assert.That(root.activeSelf, Is.False);
        Assert.That(root.transform.Find("Mask/MoveIcon").gameObject.activeSelf, Is.False);
    }

    // Call the shared implementation without executing the character's aim/VFX
    // override. This tests MoveBase's completion contract without a live match.
    private static void CallBase(object move, string method)
    {
        Type baseType = GameType("MoveBase");
        var wrapper = new DynamicMethod("TestBase" + method, typeof(void), new[] { typeof(object) }, typeof(MoveUseTests).Module, true);
        ILGenerator il = wrapper.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Castclass, baseType);
        il.Emit(OpCodes.Call, baseType.GetMethod(method));
        il.Emit(OpCodes.Ret);
        ((Action<object>)wrapper.CreateDelegate(typeof(Action<object>)))(move);
    }

    [Test]
    public void SharedFinishSpendsOnlySuccessfulCastsAndNeverRewritesCooldown()
    {
        var move = Activator.CreateInstance(GameType("DuraDragonTail"));
        Call(move, "UpdateStoredUses", Time.time, 0f);
        CallBase(move, "Finish");
        Assert.That(Property<int>(move, "StoredUses"), Is.EqualTo(2));
        move.GetType().GetField("wasMoveSuccessful").SetValue(move, true);
        CallBase(move, "Finish");
        Assert.That(Property<int>(move, "StoredUses"), Is.EqualTo(1));
        Assert.That(move.GetType().GetField("Cooldown").GetValue(move), Is.EqualTo(14f));
        Assert.That(move.GetType().GetField("wasMoveSuccessful").GetValue(move), Is.False);
        CallBase(move, "Finish");
        CallBase(move, "Cancel");
        Assert.That(Property<int>(move, "StoredUses"), Is.EqualTo(1));
    }

    [TestCase("Assets/Prefabs/MoveUI.prefab")]
    [TestCase("Assets/Prefabs/MoveUIMobile.prefab")]
    public void UiSwitchesBetweenNormalStoredRechargingAndUpgradedMoves(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Component component = root.GetComponent(GameType("MoveUI"));
            var serialized = new SerializedObject(component);
            var reserve = (GameObject)serialized.FindProperty("storedUsesRoot").objectReferenceValue;
            var reserveTimer = (Component)serialized.FindProperty("storedCdText").objectReferenceValue;
            var reserveCount = (Component)serialized.FindProperty("storedUsesText").objectReferenceValue;
            var mainLine = (GameObject)serialized.FindProperty("cdLine").objectReferenceValue;
            var rechargeLine = (GameObject)serialized.FindProperty("storedCdLine").objectReferenceValue;
            var move = Activator.CreateInstance(GameType("DuraStealthRock"));
            Call(move, "UpdateStoredUses", Time.time, 0f);
            Call(component, "BindMove", move);
            Assert.That(reserve.activeSelf, Is.True);
            Assert.That(reserveTimer.gameObject.activeSelf, Is.False);
            Assert.That(mainLine.activeSelf, Is.False);
            Assert.That(Property<string>(reserveCount, "text"), Is.EqualTo("2"));

            object state = GameType("MoveBase").GetField("useState", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(move);
            Call(state, "TrySpend", Time.time, 1f);
            Call(component, "BindMove", move);
            Assert.That(rechargeLine.activeSelf, Is.True);
            Assert.That(reserveTimer.gameObject.activeSelf, Is.True);
            Assert.That(mainLine.activeSelf, Is.True);
            Assert.That(Property<string>(reserveCount, "text"), Is.EqualTo("1"));

            move.GetType().GetField("IsUpgraded").SetValue(move, true);
            Call(move, "UpdateStoredUses", Time.time, 0f);
            Call(component, "BindMove", move);
            Assert.That(Property<string>(reserveCount, "text"), Is.EqualTo("2"));
            Assert.That(rechargeLine.activeSelf, Is.True);

            Call(component, "BindMove", Activator.CreateInstance(GameType("CinderEmber")));
            Assert.That(reserve.activeSelf, Is.False);
            Assert.That(mainLine.activeSelf, Is.False);

            // A normal timer that expired while its HUD was hidden must not
            // reappear as a stale overlay when the button is enabled again.
            mainLine.SetActive(true);
            GameType("MoveUI").GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(component, null);
            Assert.That(mainLine.activeSelf, Is.False);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [TestCase("Assets/Prefabs/MoveUI.prefab")]
    [TestCase("Assets/Prefabs/MoveUIMobile.prefab")]
    public void EmptyReservesDisplayZeroAndBothRechargeAndAvailabilityTimers(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Component component = root.GetComponent(GameType("MoveUI"));
            var serialized = new SerializedObject(component);
            var move = Activator.CreateInstance(GameType("DuraDragonTail"));
            Call(move, "UpdateStoredUses", Time.time, 0f);
            object state = GameType("MoveBase").GetField("useState", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(move);
            Call(state, "TrySpend", Time.time, 0f);
            Call(state, "TrySpend", Time.time, 0f);
            Call(component, "BindMove", move);
            Assert.That(((GameObject)serialized.FindProperty("storedUsesRoot").objectReferenceValue).activeSelf, Is.True);
            Assert.That(((GameObject)serialized.FindProperty("cdLine").objectReferenceValue).activeSelf, Is.True);
            Assert.That(((GameObject)serialized.FindProperty("storedCdLine").objectReferenceValue).activeSelf, Is.True);
            Assert.That(Property<string>(serialized.FindProperty("storedUsesText").objectReferenceValue, "text"), Is.EqualTo("0"));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
