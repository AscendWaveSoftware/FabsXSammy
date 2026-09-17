using System;
using UnityEngine;

public static class PveRuntime
{
    private static float s_pausedTotal;
    private static float s_pauseStartedAt;

    public static bool IsPaused { get; private set; }

    public static event Action<bool> PauseChanged;

    public static float Time
    {
        get
        {
            float ongoingPause = IsPaused ? UnityEngine.Time.time - s_pauseStartedAt : 0f;
            return UnityEngine.Time.time - s_pausedTotal - ongoingPause;
        }
    }

    public static void SetPaused(bool _paused)
    {
        if (_paused == IsPaused)
            return;

        if (_paused)
            s_pauseStartedAt = UnityEngine.Time.time;
        else
            s_pausedTotal += UnityEngine.Time.time - s_pauseStartedAt;

        IsPaused = _paused;
        PauseChanged?.Invoke(_paused);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        s_pausedTotal = 0f;
        s_pauseStartedAt = 0f;
        IsPaused = false;
        PauseChanged = null;
    }
}
