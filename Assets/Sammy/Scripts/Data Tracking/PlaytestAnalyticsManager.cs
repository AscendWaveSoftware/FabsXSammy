using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class PlaytestAnalyticsManager : MonoBehaviour
{
    public static PlaytestAnalyticsManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private bool _startRunAutomatically = true;
    [SerializeField] private bool _saveOnApplicationQuit = true;

    private PlaytestRunData _currentRun;
    private float _runStartTime;
    private bool _runActive;
    private bool _hasSaved;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (_startRunAutomatically)
        {
            StartRun();
        }
    }

    public void StartRun()
    {
        _runStartTime = Time.time;
        _runActive = true;
        _hasSaved = false;

        _currentRun = new PlaytestRunData
        {
            RunId = Guid.NewGuid().ToString(),
            GameVersion = Application.version,
            StartDateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Won = false,
            Deaths = 0,
            Events = new List<PlaytestEventData>()
        };

        AddEvent("RunStarted");

        Debug.Log("[PlaytestAnalytics] Run started.");
    }

    public void RegisterDeath()
    {
        if (!_runActive)
            return;

        _currentRun.Deaths++;
        AddEvent("PlayerDeath");

        Debug.Log("[PlaytestAnalytics] Death registered.");
    }

    public void RegisterVictory()
    {
        if (!_runActive)
            return;

        _currentRun.Won = true;
        _currentRun.TimeToVictorySeconds = GetCurrentRunTime();

        AddEvent("Victory");

        Debug.Log("[PlaytestAnalytics] Victory registered.");
    }

    public void RegisterCustomEvent(string eventName)
    {
        if (!_runActive)
            return;

        AddEvent(eventName);
    }

    public void EndRun()
    {
        if (!_runActive || _hasSaved)
            return;

        _runActive = false;
        _hasSaved = true;

        _currentRun.EndDateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _currentRun.TotalPlaytimeSeconds = GetCurrentRunTime();

        AddEvent("RunEnded");

        SaveRunToJson();

        Debug.Log("[PlaytestAnalytics] Run ended and saved.");
    }

    private void AddEvent(string eventName)
    {
        if (_currentRun == null)
            return;

        _currentRun.Events.Add(new PlaytestEventData
        {
            EventName = eventName,
            TimeSinceRunStartSeconds = GetCurrentRunTime(),
            DateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        });
    }

    private float GetCurrentRunTime()
    {
        return Time.time - _runStartTime;
    }

    private void SaveRunToJson()
    {
        string folderPath = Path.Combine(Application.persistentDataPath, "PlaytestRuns");

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string fileName = $"run_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
        string filePath = Path.Combine(folderPath, fileName);

        string json = JsonUtility.ToJson(_currentRun, true);

        File.WriteAllText(filePath, json);

        Debug.Log($"[PlaytestAnalytics] Saved run to: {filePath}");
    }

    private void OnApplicationQuit()
    {
        if (_saveOnApplicationQuit && _runActive && !_hasSaved)
        {
            EndRun();
        }
    }
}