using System;
using System.Collections.Generic;
using UnityEngine;

public class ThreadDispatcher
{
    private static readonly Lazy<ThreadDispatcher> Ins =
        new Lazy<ThreadDispatcher>(() => new ThreadDispatcher());

    public static ThreadDispatcher Instance => Ins.Value;

    private Queue<Action> _actions = new Queue<Action>();

    public static void AddThread(Action action)
    {
        lock (Instance._actions)
        {
            Instance._actions.Enqueue(action);
        }
    }

    public void Update()
    {
        lock (_actions)
        {
            while (_actions.Count > 0)
            {
                Action action = _actions.Dequeue();
                action.Invoke();
            }
        }
    }
}
