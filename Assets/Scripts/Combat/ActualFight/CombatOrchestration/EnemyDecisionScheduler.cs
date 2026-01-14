using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Owns the enemy "thinking delay" coroutine. Keeps coroutine fields out of the controller.
/// </summary>
public sealed class EnemyDecisionScheduler
{
    private readonly MonoBehaviour _runner;
    private Coroutine _routine;

    public EnemyDecisionScheduler(MonoBehaviour runner)
    {
        _runner = runner;
    }

    public void Schedule(float delaySeconds, Action action)
    {
        Cancel();

        if (_runner == null)
            return;

        _routine = _runner.StartCoroutine(Run(delaySeconds, action));
    }

    public void Cancel()
    {
        if (_runner != null && _routine != null)
            _runner.StopCoroutine(_routine);

        _routine = null;
    }

    private static IEnumerator Run(float delaySeconds, Action action)
    {
        if (delaySeconds > 0f)
            yield return new WaitForSeconds(delaySeconds);

        action?.Invoke();
    }
}
