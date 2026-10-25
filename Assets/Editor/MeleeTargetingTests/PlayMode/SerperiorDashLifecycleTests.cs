using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Real frames/CharacterController movement, but no lobby, owner input or network session.
public class SerperiorDashLifecycleTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private ScriptableObject stats;
    private Component pokemon, manager, movement, combat;
    private object attack;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
    private void Health(int value)
    {
        // Stage the NetworkVariable value without sending RPCs in this deliberately offline fixture.
        var variable = pokemon.GetType().GetField("currentHp", Private).GetValue(pokemon);
        variable.GetType().GetField("m_InternalValue", Private).SetValue(variable, value);
    }
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        root = new GameObject("Serperior isolated dash"); root.AddComponent(GameType("Unity.Netcode.NetworkObject"));
        var controller = root.AddComponent<CharacterController>(); controller.center = Vector3.up; controller.height = 2; controller.radius = 0.5f;
        pokemon = root.AddComponent(GameType("Pokemon")); ((Behaviour)pokemon).enabled = false;
        movement = root.AddComponent(GameType("PlayerMovement")); ((Behaviour)movement).enabled = false; Set(movement, "characterController", controller);
        ((Behaviour)root.AddComponent(GameType("AnimationManager"))).enabled = false;
        combat = root.AddComponent(GameType("MovesController")); ((Behaviour)combat).enabled = false;
        manager = root.AddComponent(GameType("PlayerManager")); ((Behaviour)manager).enabled = false;
        Set(combat, "pokemon", pokemon); Set(combat, "playerManager", manager);
        stats = ScriptableObject.CreateInstance(GameType("PokemonBase")); Set(stats, "maxHp", Enumerable.Repeat(5000, 15).ToArray()); Set(pokemon, "baseStats", stats);
        Health(5000);
        var statuses = (Array)combat.GetType().GetField("moveStatuses", Private).GetValue(combat);
        for (int i = 0; i < statuses.Length; i++) statuses.SetValue(Activator.CreateInstance(GameType("BattleActionStatus"), 0f), i);
        var moves = (Array)combat.GetType().GetField("moves", Private).GetValue(combat);
        for (int i = 0; i < moves.Length; i++) moves.SetValue(Activator.CreateInstance(GameType("LockedMove")), i);
        Set(combat, "uniteMove", Activator.CreateInstance(GameType("LockedMove")));
        attack = Activator.CreateInstance(GameType("SerperiorBasicAtk")); Call(combat, "LearnBasicAttack", attack);
        yield return null;
    }
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (attack != null) Call(attack, "Dispose");
        if (root != null) UnityEngine.Object.Destroy(root);
        if (stats != null) UnityEngine.Object.Destroy(stats);
        yield return null;
    }
    private void Dash(float distance = 2) => attack.GetType().GetMethod("StartDash", Private).Invoke(attack, new object[] { Vector3.forward, distance });
    private void Die()
    {
        Health(0);
        pokemon.GetType().GetMethod("CurrentHpChanged", Private).Invoke(pokemon, new object[] { 5000, 0 });
    }
    [UnityTest]
    public IEnumerator DashDistanceIsCappedEvenIfAnOldTargetWasFarAway()
    {
        Dash(80); yield return new WaitForSeconds(0.45f);
        float range = (float)attack.GetType().GetField("range").GetValue(attack);
        Assert.That(root.transform.position.z, Is.InRange(0.1f, range + 0.01f));
        Assert.That(Get(attack, "IsDashing"), Is.False);
    }
    [UnityTest]
    public IEnumerator DeathCancelsMovementBeforeTheDeathTeleportAndNeverUnlocksTheDeadPlayer()
    {
        Dash(); yield return null; Die();
        Vector3 death = new Vector3(0, -50, -50); root.transform.position = death;
        yield return new WaitForSeconds(0.45f);
        Assert.That(root.transform.position, Is.EqualTo(death)); Assert.That(Get(attack, "IsDashing"), Is.False);
        Assert.That(Get(movement, "CanMove"), Is.False);
    }
    [UnityTest]
    public IEnumerator ImmediateRespawnCannotResumeTheOldDash()
    {
        Dash(); yield return null; Die(); Health(5000);
        Vector3 respawn = new Vector3(-20, 0, 0); root.transform.position = respawn;
        movement.GetType().GetProperty("CanMove").SetValue(movement, true);
        yield return new WaitForSeconds(0.45f);
        Assert.That(root.transform.position, Is.EqualTo(respawn));
        Assert.That(attack.GetType().GetField("nextAttackDashes").GetValue(attack), Is.False);
    }
    [UnityTest]
    public IEnumerator RepeatedResetReleasesOnlyTheDashesOwnRestrictions()
    {
        object busy = Enum.Parse(GameType("ActionStatusType"), "Busy");
        var status = ((Array)combat.GetType().GetField("moveStatuses", Private).GetValue(combat)).GetValue(0);
        var external = (IDisposable)Call(status, "AcquireStatus", busy); Call(movement, "AddMovementRestriction");
        Dash(); Call(combat, "ResetAllMoves"); Call(combat, "ResetAllMoves");
        Assert.That(Get(movement, "CanMove"), Is.False); Assert.That(Call(status, "HasStatus", busy), Is.True);
        external.Dispose(); Call(movement, "RemoveMovementRestriction");
        Assert.That(Get(movement, "CanMove"), Is.True); Assert.That(Call(status, "HasStatus", busy), Is.False);
        yield return null;
    }
    [UnityTest]
    public IEnumerator ReplacingTheBasicAttackCancelsItsDashAndUnsubscribesDeathAndEvolution()
    {
        Dash(); yield return null;
        Call(combat, "LearnBasicAttack", Activator.CreateInstance(GameType("EmptyBasicAtk")));
        Vector3 stopped = root.transform.position; yield return new WaitForSeconds(0.45f);
        Assert.That(root.transform.position, Is.EqualTo(stopped)); Assert.That(Get(attack, "IsDashing"), Is.False);
        foreach (string name in new[] { "OnDeath", "OnEvolution" })
        {
            var callbacks = (Delegate)pokemon.GetType().GetField(name, Private).GetValue(pokemon);
            Assert.That(callbacks == null || callbacks.GetInvocationList().All(d => d.Target != attack), Is.True, name);
        }
    }
}
