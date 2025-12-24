using UnityEngine;

public static class TeleportData
{
    public static bool HasPending;
    public static Vector3 PendingPos;

    public static void Set(Vector3 pos)
    {
        HasPending = true;
        PendingPos = pos;
    }

    public static bool TryConsume(out Vector3 pos)
    {
        if (HasPending)
        {
            HasPending = false;
            pos = PendingPos;
            return true;
        }
        pos = default;
        return false;
    }

    public static void Clear()
    {
        HasPending = false;
        PendingPos = default;
    }
}
