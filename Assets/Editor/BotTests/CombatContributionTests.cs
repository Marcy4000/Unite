using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class CombatContributionTests
{
    private Type LedgerType => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CombatContributionLedger")).First(t => t != null);
    private object ledger;
    private object Call(string method, params object[] args) => LedgerType.GetMethod(method).Invoke(ledger, args);
    private ulong[] Recent(double now) => ((IEnumerable)Call("Recent", now)).Cast<object>()
        .Select(entry => (ulong)entry.GetType().GetField("PlayerId").GetValue(entry)).ToArray();

    [SetUp] public void SetUp() { ledger = Activator.CreateInstance(LedgerType); }

    [Test]
    public void WindowIsInclusiveAndRefreshesDeduplicate()
    {
        Call("Record", 1UL, 0d); Call("Record", 2UL, 1d); Call("Record", 2UL, 2d);
        CollectionAssert.AreEqual(new[] { 2UL, 1UL }, Recent(10));
        CollectionAssert.AreEqual(new[] { 2UL }, Recent(10.001));
        Assert.That(Recent(12.001), Is.Empty);
    }

    [Test]
    public void SameFrameContributionsUseTheirActualOrderAndClearResetsHistory()
    {
        Call("Record", 9UL, 5d); Call("Record", 1UL, 5d);
        CollectionAssert.AreEqual(new[] { 1UL, 9UL }, Recent(5));
        Call("Clear"); Assert.That(Recent(5), Is.Empty);
    }

    [Test]
    public void ANewClockCannotReuseOldMatchHistory()
    {
        Call("Record", 1UL, 100d);
        Assert.That(Recent(0), Is.Empty);
    }

    [Test]
    public void ServerAssistTotalsOverrideLegacyOwnerDataAndReachAvatarStats()
    {
        Type TypeNamed(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var root = new GameObject("Assist stat projection"); root.SetActive(false);
        try
        {
            var coordinator = root.AddComponent(TypeNamed("PlayerNetworkManager"));
            var avatar = root.AddComponent(TypeNamed("PlayerManager"));
            coordinator.GetType().GetField("playerManager", flags).SetValue(coordinator, avatar);
            object legacy = coordinator.GetType().GetField("playerStats", flags).GetValue(coordinator);
            object stats = Activator.CreateInstance(TypeNamed("PlayerStats"), "player", 3, 2, 999, 40, 500, 600, 700);
            legacy.GetType().GetProperty("Value").SetValue(legacy, stats);
            object authoritative = coordinator.GetType().GetField("serverAssists", flags).GetValue(coordinator);
            authoritative.GetType().GetProperty("Value").SetValue(authoritative, (ushort)7);
            coordinator.GetType().GetMethod("HandleAssistCountChanged", flags).Invoke(coordinator, new object[] { (ushort)0, (ushort)7 });
            object projected = coordinator.GetType().GetProperty("PlayerStats").GetValue(coordinator);
            object avatarStats = avatar.GetType().GetProperty("PlayerStats").GetValue(avatar);
            Assert.That(projected.GetType().GetField("assists").GetValue(projected), Is.EqualTo((ushort)7));
            Assert.That(projected.GetType().GetField("kills").GetValue(projected), Is.EqualTo((ushort)3));
            Assert.That(projected.GetType().GetField("healingDone").GetValue(projected), Is.EqualTo(700u));
            Assert.That(avatarStats.GetType().GetField("assists").GetValue(avatarStats), Is.EqualTo((ushort)7));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
