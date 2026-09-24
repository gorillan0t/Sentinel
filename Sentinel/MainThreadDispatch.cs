using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sentinel;

public class MainThreadDispatch : MonoBehaviour
{
    private static readonly Queue<Action> queue0 = new();

    private void Update()
    {
        while (true)
        {
            Action action;
            lock (queue0)
            {
                if (queue0.Count == 0)
                {
                    break;
                }

                action = queue0.Dequeue();
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
        lock (queue0)
        {
            queue0.Enqueue(a);
        }
    }
}