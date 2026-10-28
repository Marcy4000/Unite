using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Unity.Collections;
using Unity.Netcode;
using Unite.Multiplayer;
using Unity.Services.Multiplayer;

public class PlayerLoadoutInitializationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);

    [Test]
    public void UnequippedSlotHasNoStatBoostsAndASafeDescription()
    {
        var item = AssetDatabase.LoadMainAssetAtPath("Assets/HeldItems/EmptyHeldItem.asset");
        Assert.That(item, Is.Not.Null);
        Assert.That((Array)item.GetType().GetProperty("StatBoosts").GetValue(item), Is.Empty);
        Assert.That(item.GetType().GetProperty("Description").GetValue(item), Is.EqualTo(string.Empty));
    }

    [Test]
    public void LegacyNullStatListAlsoBehavesAsAnUnequippedSlot()
    {
        var item = ScriptableObject.CreateInstance(GameType("HeldItemInfo"));
        try
        {
            item.GetType().GetField("statBoosts").SetValue(item, null);
            Assert.That((Array)item.GetType().GetProperty("StatBoosts").GetValue(item), Is.Empty);
            Assert.That(item.GetType().GetProperty("Description").GetValue(item), Is.EqualTo(string.Empty));
        }
        finally { UnityEngine.Object.DestroyImmediate(item); }
    }

    [Test]
    public void MissingBoostEntriesDoNotBreakItemDescriptions()
    {
        var item = ScriptableObject.CreateInstance(GameType("HeldItemInfo"));
        try
        {
            item.GetType().GetField("statBoosts").SetValue(item, Array.CreateInstance(GameType("HeldItemStatBoost"), 1));
            Assert.That(item.GetType().GetProperty("Description").GetValue(item), Is.EqualTo(string.Empty));
        }
        finally { UnityEngine.Object.DestroyImmediate(item); }
    }

    [Test]
    public void HumanWithThreeUnequippedSlotsCompletesHeldItemInitialization()
    {
        var root = new GameObject("human empty-loadout regression"); root.SetActive(false);
        var catalogType = GameType("CharactersList"); var lobbyType = GameType("LobbyController");
        var catalogInstance = catalogType.GetProperty("Instance"); var lobbyInstance = lobbyType.GetProperty("Instance");
        var oldCatalog = catalogInstance.GetValue(null); var oldLobby = lobbyInstance.GetValue(null);
        var item = ScriptableObject.CreateInstance(GameType("HeldItemInfo"));
        try
        {
            // Reproduce the legacy No Held Item asset, including its absent list.
            item.GetType().GetField("statBoosts").SetValue(item, null);
            var catalog = root.AddComponent(catalogType);
            var items = Array.CreateInstance(item.GetType(), 1); items.SetValue(item, 0);
            catalogType.GetField("heldItems", Private).SetValue(catalog, items); catalogInstance.SetValue(null, catalog);
            var lobby = root.AddComponent(lobbyType);
            var human = new GameSessionPlayer("human-empty-loadout");
            human.Data["HeldItems"] = new PlayerProperty(Convert.ToBase64String(new byte[] { 0, 0, 0 }));
            var view = new GameSessionView(); view.Players.Add(human);
            lobbyType.GetField("partyLobby", Private).SetValue(lobby, view); lobbyInstance.SetValue(null, lobby);
            var player = root.AddComponent(GameType("PlayerManager"));
            ((NetworkVariable<FixedString32Bytes>)player.GetType().GetField("lobbyPlayerId", Private).GetValue(player)).Value = new FixedString32Bytes(human.Id);
            var manager = root.AddComponent(GameType("HeldItemManager"));
            Assert.DoesNotThrow(() => manager.GetType().GetMethod("Initialize").Invoke(manager, new object[] { player }));
            Assert.That(manager.GetType().GetField("initialized", Private).GetValue(manager), Is.True);
            var runtimeItems = (Array)manager.GetType().GetField("heldItemBases", Private).GetValue(manager);
            Assert.That(runtimeItems.Cast<object>().Select(entry => entry.GetType().Name), Is.EqualTo(new[] { "EmptyHeldItem", "EmptyHeldItem", "EmptyHeldItem" }));
        }
        finally
        {
            catalogInstance.SetValue(null, oldCatalog); lobbyInstance.SetValue(null, oldLobby);
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void EjectReleaseAndCancelBeforeFirstPressDoNotNeedAnActor()
    {
        var item = Activator.CreateInstance(GameType("EjectButton"));
        int completed = 0;
        item.GetType().GetEvent("onBattleItemOver").AddEventHandler(item, (Action)(() => completed++));
        foreach (string method in new[] { "Cancel", "Update", "Finish", "Cancel", "Finish" })
            Assert.DoesNotThrow(() => item.GetType().GetMethod(method).Invoke(item, null), method);
        Assert.That(completed, Is.Zero, "An inactive release must not consume an item or signal a successful action.");
        Assert.That(item.GetType().GetField("IsActive").GetValue(item), Is.False);
    }

    [Test]
    public void QueuedKillWithDestroyedVictimDoesNotTouchUiOrNetwork()
    {
        var uiRoot = new GameObject("stale kill notification"); uiRoot.SetActive(false);
        var victimRoot = new GameObject("despawned victim"); victimRoot.SetActive(false);
        try
        {
            var ui = uiRoot.AddComponent(GameType("KillNotificationUI"));
            var victim = victimRoot.AddComponent(GameType("Pokemon"));
            var info = Activator.CreateInstance(GameType("KillInfo"), Activator.CreateInstance(GameType("DamageInfo")), victim, "");
            UnityEngine.Object.DestroyImmediate(victimRoot);
            Assert.DoesNotThrow(() => ui.GetType().GetMethod("ShowKill", Private).Invoke(ui, new[] { info }));
        }
        finally
        {
            if (victimRoot != null) UnityEngine.Object.DestroyImmediate(victimRoot);
            UnityEngine.Object.DestroyImmediate(uiRoot);
        }
    }
}
