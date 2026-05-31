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
    DayPhase current;

    public event System.Action<DayPhase, DayPhase> PhaseChanged;

    public PhaseService(int _sunrise, int _day, int _sunset, int _night)
    {
        this.sunrise = _sunrise;
        this.day = _day;
        this.sunset = _sunset;
        this.night = _night;
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
        if (next != current)
        {
            var prev = current;
            current = next;
            PhaseChanged?.Invoke(prev, next);
        }
    }
}