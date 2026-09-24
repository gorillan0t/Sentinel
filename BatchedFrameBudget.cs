using System;

internal sealed class BatchedFrameBudget
{

    private readonly FrameBudget callbackBudget = new(1);
    private readonly FrameBudget frameBudget;

    internal BatchedFrameBudget(FrameBudget arg1) => frameBudget = arg1;

    internal bool RunBatch(int arg1, int arg2, Action<int> arg3)
    {
        if (callbackBudget.TryConsume(arg1) && frameBudget.TryConsume(arg1, arg2))
        {
            for (int i = 0; i < arg2; i++)
            {
                arg3(i);
            }

            return true;
        }

        return false;
    }
}