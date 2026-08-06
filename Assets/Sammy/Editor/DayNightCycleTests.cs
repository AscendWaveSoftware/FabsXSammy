using NUnit.Framework;

public class DayNightCycleTests
{
    [Test]
    public void Clock_InterpolatesBetweenWholeMinutes()
    {
        var clock = new ClockService();
        clock.SetTime(12, 0);

        clock.Tick(0.25f, 0.5f);
        Assert.That(clock.TimeOfDayMinutes, Is.EqualTo(720.5f).Within(0.0001f));

        clock.Tick(0.25f, 0.5f);
        Assert.That(clock.Hours, Is.EqualTo(12));
        Assert.That(clock.Minutes, Is.EqualTo(1));
        Assert.That(clock.FractionalMinute, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void Clock_ReportsMidnightAsHourZero()
    {
        var clock = new ClockService();
        clock.SetTime(23, 59);
        int reportedHour = -1;
        clock.HourChanged += hour => reportedHour = hour;

        clock.AdvanceOneMinute();

        Assert.That(reportedHour, Is.EqualTo(0));
        Assert.That(clock.Hours, Is.EqualTo(0));
        Assert.That(clock.Days, Is.EqualTo(1));
    }

    [Test]
    public void PhaseInitialization_DoesNotEmitAFalseTransition()
    {
        var phases = new PhaseService(6, 8, 18, 22);
        int transitionCount = 0;
        phases.PhaseChanged += (_, _) => transitionCount++;

        phases.Initialize(12);
        phases.Update(13);

        Assert.That(phases.Current, Is.EqualTo(DayPhase.Day));
        Assert.That(transitionCount, Is.Zero);
    }

    [TestCase(5f * 60f + 40f, DayPhase.Night, DayPhase.Sunrise, 0f)]
    [TestCase(5f * 60f + 50f, DayPhase.Night, DayPhase.Sunrise, 0.5f)]
    [TestCase(6f * 60f, DayPhase.Sunrise, DayPhase.Day, 0f)]
    [TestCase(17f * 60f + 40f, DayPhase.Day, DayPhase.Sunset, 0f)]
    [TestCase(17f * 60f + 50f, DayPhase.Day, DayPhase.Sunset, 0.5f)]
    [TestCase(18f * 60f, DayPhase.Sunset, DayPhase.Night, 0f)]
    [TestCase(21f * 60f + 50f, DayPhase.Sunset, DayPhase.Night, 0.5f)]
    [TestCase(22f * 60f, DayPhase.Night, DayPhase.Sunrise, 0f)]
    public void TransitionTimeline_UsesTheCorrectAdjacentPhases(
        float minuteOfDay,
        DayPhase expectedFrom,
        DayPhase expectedTo,
        float expectedBlend)
    {
        var phases = new PhaseService(6, 8, 18, 22);

        PhaseTransitionState state = phases.EvaluateTransition(minuteOfDay, 20f);

        Assert.That(state.From, Is.EqualTo(expectedFrom));
        Assert.That(state.To, Is.EqualTo(expectedTo));
        Assert.That(state.Blend01, Is.EqualTo(expectedBlend).Within(0.0001f));
    }
}
