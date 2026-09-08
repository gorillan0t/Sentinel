namespace Sentinel;

public struct PlayerRaySelection
{
    private readonly float maxDistance;

    private float targetDistance;

    private int targetHitIndex;

    public float BlockerDistance { get; private set; }

    public PlayerRaySelection(float arg1)
    {
        maxDistance    = targetDistance = BlockerDistance = arg1;
        targetHitIndex = -1;
    }

    public void Consider(int index, float distance, bool target, bool solid)
    {
        if (!float.IsNaN(distance) && !(distance < 0f) && distance <= maxDistance)
        {
            if (solid && distance < BlockerDistance)
            {
                BlockerDistance = distance;
            }

            if (target && (targetHitIndex < 0 || distance < targetDistance))
            {
                targetDistance = distance;
                targetHitIndex = index;
            }
        }
    }

    public int Finish(bool saturated)
    {
        if (!saturated && targetHitIndex >= 0 && !(targetDistance > BlockerDistance))
        {
            return targetHitIndex;
        }

        return -1;
    }
}