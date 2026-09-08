using System;
using System.Runtime.CompilerServices;
using System.Threading;

internal static class DispatchHelper
{

    internal static bool TryDispatch(Func<Action, bool> arg1, Action arg2, int arg3, Action arg4)
    {
        for (int i = 0; i < arg3; i++)
        {
            if (!arg1(arg2))
            {
                if (i + 1 < arg3)
                {
                    arg4();
                }

                continue;
            }

            return true;
        }

        return false;
    }

    internal static bool TryDispatchAndWait(Func<Action, bool> arg1, Action arg2, int arg3, Action arg4, int arg5)
    {
        ManualResetEventSlim waitHandle = new(false);
        try
        {
            bool completed = false;
            int  timeoutMs = 0;
            Action arg6 = delegate
                          {
                              if (Interlocked.CompareExchange(ref timeoutMs, 1, 0) != 0)
                              {
                                  return;
                              }

                              try
                              {
                                  arg2();
                                  completed = true;
                              }
                              catch { }
                              finally
                              {
                                  waitHandle.Set();
                              }
                          };

            if (TryDispatch(arg1, arg6, arg3, arg4))
            {
                if (waitHandle.Wait(arg5))
                {
                    return completed;
                }

                if (Interlocked.CompareExchange(ref timeoutMs, 2, 0) == 0)
                {
                    return false;
                }

                waitHandle.Wait();

                return completed;
            }

            return false;
        }
        finally
        {
            if (waitHandle != null)
            {
                ((IDisposable)waitHandle).Dispose();
            }
        }
    }

    [CompilerGenerated]
    private sealed class DispatchWaitState
    {

        public Action action;

        public bool completed;
        public int  timeoutMs;

        public ManualResetEventSlim waitHandle;

        internal void RunOnce()
        {
            if (Interlocked.CompareExchange(ref timeoutMs, 1, 0) != 0)
            {
                return;
            }

            try
            {
                action();
                completed = true;
            }
            catch { }
            finally
            {
                waitHandle.Set();
            }
        }
    }
}