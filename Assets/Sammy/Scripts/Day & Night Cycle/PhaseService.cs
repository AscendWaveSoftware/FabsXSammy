using UnityEngine;

public enum DayPhase
{
    Night,
    Sunrise,
    Day,
    Sunset,
    Max
}

public sealed class PhaseService
{
    private readonly int sunrise, day, sunset, night;
    private DayPhase current;
    private bool initialized;

    public DayPhase Current => current;

    public event System.Action<DayPhase, DayPhase> PhaseChanged;

    public PhaseService(int _sunrise, int _day, int _sunset, int _night)
    {
        this.sunrise = _sunrise;
        this.day = _day;
        this.sunset = _sunset;
        this.night = _night;
    }

    public void Initialize(int _hour)
    {
        current = Evaluate(_hour);
        initialized = true;
    }

    public DayPhase Evaluate(int _hour)
    {
        DayPhase p = (_hour < sunrise) ? DayPhase.Night :
                     (_hour < day) ? DayPhase.Sunrise :
                     (_hour < sunset) ? DayPhase.Day :
                     (_hour < night) ? DayPhase.Sunset : DayPhase.Night;
        return p;
    }

    public void Update(int _hour)
    {
        var next = Evaluate(_hour);
        if (!initialized)
        {
            current = next;
            initialized = true;
            return;
        }

        if (next != current)
        {
            var prev = current;
            current = next;
            PhaseChanged?.Invoke(prev, next);
        }
    }

    public PhaseTransitionState EvaluateTransition(float _minuteOfDay, float _transitionGameMinutes)
    {
        const float minutesPerHour = 60f;
        const float minutesPerDay = 24f * minutesPerHour;

        float sunriseMinute = sunrise * minutesPerHour;
        float dayMinute = day * minutesPerHour;
        float sunsetMinute = sunset * minutesPerHour;
        float nightMinute = night * minutesPerHour;
        float minute = Mathf.Repeat(_minuteOfDay, minutesPerDay);

        DayPhase from;
        DayPhase to;
        float phaseStart;
        float nextBoundary;

        if (minute < sunriseMinute)
        {
            from = DayPhase.Night;
            to = DayPhase.Sunrise;
            phaseStart = nightMinute - minutesPerDay;
            nextBoundary = sunriseMinute;
        }
        else if (minute < dayMinute)
        {
            from = DayPhase.Sunrise;
            to = DayPhase.Day;
            phaseStart = sunriseMinute;
            nextBoundary = dayMinute;
        }
        else if (minute < sunsetMinute)
        {
            from = DayPhase.Day;
            to = DayPhase.Sunset;
            phaseStart = dayMinute;
            nextBoundary = sunsetMinute;
        }
        else if (minute < nightMinute)
        {
            from = DayPhase.Sunset;
            to = DayPhase.Night;
            phaseStart = sunsetMinute;
            nextBoundary = nightMinute;
        }
        else
        {
            from = DayPhase.Night;
            to = DayPhase.Sunrise;
            phaseStart = nightMinute;
            nextBoundary = sunriseMinute + minutesPerDay;
        }

        float phaseDuration = Mathf.Max(1f, nextBoundary - phaseStart);
        float transitionDuration = Mathf.Clamp(_transitionGameMinutes, 0.01f, phaseDuration);
        float linearBlend = Mathf.InverseLerp(nextBoundary - transitionDuration, nextBoundary, minute);
        float easedBlend = linearBlend * linearBlend * (3f - 2f * linearBlend);
        return new PhaseTransitionState(from, to, easedBlend);
    }
}

public readonly struct PhaseTransitionState
{
    public readonly DayPhase From;
    public readonly DayPhase To;
    public readonly float Blend01;

    public PhaseTransitionState(DayPhase _from, DayPhase _to, float _blend01)
    {
        From = _from;
        To = _to;
        Blend01 = Mathf.Clamp01(_blend01);
    }
}
