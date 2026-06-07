using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace DNE.Events
{
    /// <summary>
    /// L�uft einmal pro Frame, evaluiert aber nur auf Minutenwechsel bzw. Phase-Events.
    /// Klemmt sich an EnviromentDirector, nutzt dessen ClockService/PhaseService indirekt.
    /// </summary>
    [DefaultExecutionOrder(+50)]
    public class DayNightEventRunner : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Referenz auf bestehenden Director in der Szene.")]
        public EnvironmentDirector Env;

        [Header("Config")]
        public List<DayNightEventAsset> Events = new();

        // Runtime
        private int lastMinuteOfDay = -1;
        private DayPhase lastPhase;
        private int dayIndex;
        private TimeProfile profile;


        private void Awake()
        {
            if (!Env)
            {
                Env = FindAnyObjectByType<EnvironmentDirector>();
            }
        }

        private void Start()
        {
            SafeInit();
        }

        private void Update()
        {
            if (Env == null || Env.TimeSource == null)
            {
                SafeInit();
                if (Env == null || Env.TimeSource == null) return;
            }

            int minute = CurrentMinuteOfDay();
            DayPhase phase = CurrentPhase();

            // Mitternacht erkennen
            if (minute < lastMinuteOfDay)
                dayIndex++;

            bool minuteChanged = minute != lastMinuteOfDay;
            bool phaseChanged = phase != lastPhase;

            var ctx = new DNContext
            {
                MinuteOfDay = minute,
                DayIndex = dayIndex,
                CurrentDayPhase = phase,
                Time01 = Env.TimeSource.TimeOfDay01,
                FacingSun01 = 0f,
                Sender = this
            };

            if (minuteChanged)
                EvaluateMinuteTriggers(ctx);

            if (phaseChanged)
                EvaluatePhaseTriggers(ctx, _entered: phaseChanged, _exited: phaseChanged, _previous: lastPhase);

            lastMinuteOfDay = minute;
            lastPhase = phase;
        }

        private void SafeInit()
        {
            if (Env == null || Env.TimeSource == null) return;

            if (profile == null && Env != null)
                profile = Env.Profile;

            lastMinuteOfDay = CurrentMinuteOfDay();
            lastPhase = CurrentPhase();
        }

        private int CurrentMinuteOfDay()
        {
            float t01 = Env.TimeSource.TimeOfDay01;
            return TimeMath.T01ToMinutes(t01);
        }

        private DayPhase CurrentPhase()
        {
            if (profile == null || Env == null || Env.TimeSource == null)
                return lastPhase;

            int h = Mathf.FloorToInt(Env.TimeSource.Hours);

            if (InRange(h, profile.NightHour, profile.SunriseHour)) return DayPhase.Night;
            if (InRange(h, profile.SunriseHour, profile.DayHour)) return DayPhase.Sunrise;
            if (InRange(h, profile.DayHour, profile.SunsetHour)) return DayPhase.Day;
            return DayPhase.Sunset;
        }

        private static bool InRange(int _h, int _start, int _end)
        {
            if (_start <= _end) return _h >= _start && _h < _end;
            return _h >= _start || _h < _end;
        }

        private void EvaluateMinuteTriggers(in DNContext _ctx)
        {
            foreach (var e in Events)
            {
                if (e == null) continue;

                bool shouldFire = e.TriggerType switch
                {
                    DNTriggerType.AtExactTime => _ctx.MinuteOfDay == e.StartMinutes,
                    DNTriggerType.BetweenTimes => TimeMath.IsWithinWindow(_ctx.MinuteOfDay, e.StartMinutes, e.EndMinutes),
                    DNTriggerType.EveryNMinutes => (_ctx.MinuteOfDay % Mathf.Max(1, e.PeriodMinutes)) == 0,
                    _ => false
                };

                if (!shouldFire) continue;
                if (!GuardsPass(e, _ctx)) continue;
                if (!ConditionsPass(e, _ctx)) continue;

                Execute(e, _ctx);
            }
        }

        private void EvaluatePhaseTriggers(in DNContext _ctx, bool _entered, bool _exited, DayPhase _previous)
        {
            foreach (var e in Events)
            {
                if (e == null) continue;

                bool shouldFire = false;
                if (e.TriggerType == DNTriggerType.OnPhaseEnter)
                    shouldFire = _entered && _ctx.CurrentDayPhase == e.Phase;
                else if (e.TriggerType == DNTriggerType.OnPhaseExit)
                    shouldFire = _exited && _previous == e.Phase;

                if (!shouldFire) continue;
                if (!GuardsPass(e, _ctx)) continue;
                if (!ConditionsPass(e, _ctx)) continue;

                Execute(e, _ctx);
            }
        }

        private static bool ConditionsPass(DayNightEventAsset _e, in DNContext _ctx)
        {
            if (_e.Conditions == null || _e.Conditions.Count == 0) return true;
            for (int i = 0; i < _e.Conditions.Count; i++)
            {
                var c = _e.Conditions[i];
                if (!c) continue;
                if (!c.Evaluate(_ctx)) return false;
            }
            return true;
        }

        private static bool GuardsPass(DayNightEventAsset _e, in DNContext _ctx)
        {
            // Random Chance (global auf dem Event)
            if (_e.RandomChance < 1f && Random.value > Mathf.Clamp01(_e.RandomChance))
                return false;

            // Once per day
            if (_e.OncePerDay && _e.lastFiredDayIndex == _ctx.DayIndex)
                return false;

            // Cooldown
            int elapsed = (_ctx.DayIndex - _e.lastFiredDayIndex) * TimeMath.MinutesPerDay
                + (_ctx.MinuteOfDay - _e.lastFiredMinuteOfDay);
            if (_e.CooldownMinutes > 0 && elapsed < _e.CooldownMinutes)
                return false;

            return true;
        }

        private static void Execute(DayNightEventAsset _e, in DNContext _ctx)
        {
            if (_e.Actions != null)
            {
                for (int i = 0; i < _e.Actions.Count; i++)
                {
                    var a = _e.Actions[i];
                    if (!a) continue;
                    a.Execute(_ctx);
                }
            }

            _e.lastFiredDayIndex = _ctx.DayIndex;
            _e.lastFiredMinuteOfDay = _ctx.MinuteOfDay;
        }
    }
}