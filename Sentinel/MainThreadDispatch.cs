using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sentinel.Sentinel;

public class MainThreadDispatch : MonoBehaviour
{
    private static readonly Queue<Action> queue = new();

    private void Update()
    {
        while (true)
        {
            Action action;
            lock (queue)
            {
                if (queue.Count == 0)
                {
                    break;
                }

                action = queue.Dequeue();
            }

            try
            {
                action();
            }
            catch { }
        }
    }

    public static void Enqueue(Action a)
    {
        lock (queue)
        {
            queue.Enqueue(a);
        }
    }
}