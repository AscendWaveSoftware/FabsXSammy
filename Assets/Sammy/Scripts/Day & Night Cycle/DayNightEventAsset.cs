using System;
using System.Collections.Generic;
using UnityEngine;

namespace DNE.Events
{
    public enum DNTriggerType
    {
        AtExactTime,
        BetweenTimes,
        EveryNMinutes,
        OnPhaseEnter,
        OnPhaseExit
    }

    public enum DayPhase
    {
        Night,
        Sunrise,
        Day,
        Sunset
    }

    [CreateAssetMenu(menuName = "Environment/DayNight/Event", order = 100)]
    public class DayNightEventAsset : ScriptableObject
    {
        [Header("Trigger")]
        public DNTriggerType TriggerType = DNTriggerType.AtExactTime;

        [Tooltip("Bei 'AtExactTime' & 'BetweenTimes' genutzt.")]
        [Range(0, 23)] public int HourA;
        [Range(0, 59)] public int MinuteA;

        [Tooltip("Nur bei 'BetweenTimes'.")]
        [Range(0, 23)] public int HourB = 23;
        [Range(0, 59)] public int MinuteB = 59;

        [Tooltip("Nur 'EveryNMinutes'.")]
        [Min(1)] public int PeriodMinutes = 30;

        [Tooltip("F�r Phase-Trigger")]
        public DayPhase Phase = DayPhase.Day;

        [Header("Guards")]
        [Tooltip("Nur einmal pro Kalendertag feuern (Reset um 00:00)")]
        public bool OncePerDay;
        [Tooltip("Mindestabstand in Minuten zwischen Feuern (zus�tzlich zu OncePerDay)")]
        [Min(0)] public int CooldownMinutes;

        [Range(0f, 1f)] public float RandomChance = 1f;

        [Header("Conditions (alle m�ssen true sein)")]
        public List<DNCondition> Conditions = new();

        [Header("Actions (der Reihe nach)")]
        public List<DNAction> Actions = new();

        [NonSerialized] public int lastFiredDayIndex = -1;
        [NonSerialized] public int lastFiredMinuteOfDay = -999999;

        public int StartMinutes => TimeMath.ToMinutes(HourA, MinuteA);
        public int EndMinutes => TimeMath.ToMinutes(HourB, MinuteB);
    }
}