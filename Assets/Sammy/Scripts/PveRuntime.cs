using System;
using UnityEngine;

/// <summary>
/// Pause switch and clock for the PVE arena.
///
/// The tower defence side has to keep running while the player studies the lane,
/// so the arena cannot simply be frozen with Time.timeScale. Everything in the
/// arena therefore checks <see cref="IsPaused"/> before doing work and reads
/// <see cref="Time"/> instead of UnityEngine.Time.time, so no cooldown, stun,
/// windup or poison can quietly expire while nobody is watching.
/// </summary>
public static class PveRuntime
{
    private static float s_pausedTotal;
    private static float s_pauseStartedAt;

    public static bool IsPaused { get; private set; }

    /// <summary>Raised when the arena is paused (true) or resumed (false).</summary>
    public static event Action<bool> PauseChanged;

    /// <summary>
    /// Arena replacement for UnityEngine.Time.time. Stands still while paused, so
    /// a deadline set before the pause is still the same distance away after it.
    /// </summary>
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

    /// <summary>
    /// Statics outlive a play mode exit when domain reloading is switched off.
    /// Without this reset the accumulated offset would survive while
    /// UnityEngine.Time.time restarts at zero, dragging the arena clock into the
    /// past and firing every pending deadline at once.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        s_pausedTotal = 0f;
        s_pauseStartedAt = 0f;
        IsPaused = false;
        PauseChanged = null;
    }
}
