using System;
using UnityEngine;

public sealed class ClockService : ITimeSource
{
    public int Hours { get; private set; }
    public int Minutes { get; private set; }
    public int Days { get; private set; }
    public float TimeOfDay01 => (Hours * 60f + Minutes) / 1440f;

    // Open Events
    public event Action MinutesElapsed;
    public event Action<int> HourChanged;
    public event Action DayChanged;

    private float acc;
    private float time01;

    public void Tick(float _dt, float _realSecondsPerGameMinute)
    {
        if (_realSecondsPerGameMinute <= 0f)
        {
            return;
        }

        acc += _dt;
        while (acc >= _realSecondsPerGameMinute)
        {
            acc -= _realSecondsPerGameMinute;
            AdvanceOneMinute();
        }
    }

    public void SetTime(int _hours, int _minutes)
    {
        Hours = Mathf.Clamp(_hours, 0, 23);
        Minutes = Mathf.Clamp(_minutes, 0, 59);
    }

    public void AdvanceOneMinute()
    {
        Minutes++;
        MinutesElapsed?.Invoke();
        if (Minutes >= 60)
        {
            Minutes = 0;
            Hours++;
            HourChanged?.Invoke(Hours);
            if (Hours >= 24)
            {
                Hours = 0;
                Days++;
                DayChanged?.Invoke();
            }
        }
    }
}