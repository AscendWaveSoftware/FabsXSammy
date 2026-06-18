using System;
using System.Collections.Generic;

[Serializable]
public class PlaytestRunData
{
    public string RunId;
    public string GameVersion;

    public string StartDateTime;
    public string EndDateTime;

    public float TotalPlaytimeSeconds;

    public bool Won;
    public float TimeToVictorySeconds;

    public int Deaths;

    public List<PlaytestEventData> Events;
}

[Serializable]
public class PlaytestEventData
{
    public string EventName;
    public float TimeSinceRunStartSeconds;
    public string DateTime;
}