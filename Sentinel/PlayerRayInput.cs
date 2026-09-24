namespace Sentinel;

public sealed class PlayerRayInput
{
    private bool wasReleased;

    public bool Aiming { get; private set; }

    public PlayerRayAction Step(bool permitted, bool held, bool selectPressed, bool selectOnRelease)
    {
        if (!permitted)
        {
            Aiming      = false;
            wasReleased = false;

            return PlayerRayAction.None;
        }

        if (held)
        {
            if (wasReleased)
            {
                Aiming = true;
                if (!(!selectOnRelease && selectPressed))
                {
                    return PlayerRayAction.Aim;
                }

                Aiming      = false;
                wasReleased = false;

                return PlayerRayAction.Select;
            }

            return PlayerRayAction.None;
        }

        bool num = Aiming && selectOnRelease;
        wasReleased = true;
        Aiming      = false;
        if (!num)
        {
            return PlayerRayAction.None;
        }

        return PlayerRayAction.Select;
    }
}