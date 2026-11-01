using System;
using System.Collections;
using UnityEngine;

internal sealed class CombatRoutine : IDisposable
{
    private MonoBehaviour actor;
    private Coroutine coroutine;
    private IEnumerator routine;

    public CombatRoutine(MonoBehaviour actor, IEnumerator routine)
    {
        this.actor = actor;
        this.routine = routine;
        coroutine = actor.StartCoroutine(routine);
    }

    public void Dispose()
    {
        if (actor != null && coroutine != null) actor.StopCoroutine(coroutine);
        (routine as IDisposable)?.Dispose();
        routine = null;
        actor = null;
        coroutine = null;
    }
}
