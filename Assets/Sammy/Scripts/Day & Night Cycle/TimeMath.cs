using UnityEngine;

namespace DNE.Events
{
    public static class TimeMath
    {
        public const int MinutesPerDay = 24 * 60;

        public static int ToMinutes(int _hour, int _minute) => Mathf.Clamp(_hour, 0, 23) * 60 + Mathf.Clamp(_minute, 0, 59);

        public static int T01ToMinutes(float _t01)
        {
            float clamped = Mathf.Repeat(_t01, 1f);
            int minutes = Mathf.FloorToInt(clamped * MinutesPerDay) % MinutesPerDay;
            return minutes;
        }

        public static bool IsWithinWindow(int _current, int _start, int _end)
        {
            if (_start <= _end) return _current >= _start && _current <= _end;
            return _current >= _start || _current <= _end;
        }
    }
}