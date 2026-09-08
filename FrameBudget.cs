using System;

internal sealed class FrameBudget
{
    private readonly int capacity;

    private readonly Func<double> clock;

    private readonly double windowSeconds;

    private int consumed;

    private int windowId = -1;

    private double windowStart;

    internal FrameBudget(int arg1, Func<double> arg2 = null, double arg3 = 0.0)
    {
        capacity      = arg1;
        clock         = arg2;
        windowSeconds = arg3;
    }

    internal bool TryConsume(int arg1, int arg2 = 1)
    {
        if (windowId != arg1)
        {
            windowId    = arg1;
            consumed    = 0;
            windowStart = clock != null ? clock() : 0.0;
        }

        if (arg2 >= 1 && arg2 <= capacity - consumed)
        {
            if (consumed > 0 && clock != null && clock() - windowStart >= windowSeconds)
            {
                return false;
            }

            consumed += arg2;

            return true;
        }

        return false;
    }
}