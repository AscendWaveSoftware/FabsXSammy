using System;
using UnityEngine;

public sealed class ClockService : ITimeSource
{
    public int Hours { get; private set; }
    public int Minutes { get; private set; }
    public int Days { get; private set; }
    public float FractionalMinute { get; private set; }
    public float TimeOfDayMinutes => Hours * 60f + Minutes + FractionalMinute;
    public float TimeOfDay01 => Mathf.Repeat(TimeOfDayMinutes / 1440f, 1f);

    // Open Events
    public event Action MinutesElapsed;
    public event Action<int> HourChanged;
    public event Action DayChanged;

    private float acc;

    public void Tick(float _dt, float _realSecondsPerGameMinute)
    {
        if (_realSecondsPerGameMinute <= 0f)
        {
            return;
        }

        acc += Mathf.Max(0f, _dt);
        while (acc >= _realSecondsPerGameMinute)
        {
            acc -= _realSecondsPerGameMinute;
            AdvanceOneMinute();
        }

        FractionalMinute = Mathf.Clamp01(acc / _realSecondsPerGameMinute);
    }

    public void SetTime(int _hours, int _minutes)
    {
        Hours = Mathf.Clamp(_hours, 0, 23);
        Minutes = Mathf.Clamp(_minutes, 0, 59);
        acc = 0f;
        FractionalMinute = 0f;
    }

    public void AdvanceOneMinute()
    {
        Minutes++;
        if (Minutes >= 60)
        {
            Minutes = 0;
            Hours++;
            if (Hours >= 24)
            {
                Hours = 0;
                Days++;
                DayChanged?.Invoke();
            }

            HourChanged?.Invoke(Hours);
        }

        MinutesElapsed?.Invoke();
    }
}
