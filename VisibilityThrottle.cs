internal sealed class VisibilityThrottle
{
    private readonly bool enabled;

    private float nextUpdateAt;

    private bool wasVisible;

    internal VisibilityThrottle(bool arg1) => enabled = arg1;

    internal bool ShouldUpdate(float arg1)
    {
        if (wasVisible && enabled)
        {
            return false;
        }

        return arg1 >= nextUpdateAt;
    }

    internal void MarkVisible(float arg1, bool arg2 = true)
    {
        wasVisible   = arg2;
        nextUpdateAt = arg1 + 1f / 15f;
    }
}