using UnityEngine;
//*************************************
// Only Debug
//*************************************
public class ClockInspectorView : MonoBehaviour
{
    [SerializeField] EnvironmentDirector director;

    [Header("Current Time (read-only)")]
    public int Hours;
    public int Minutes;
    public int Seconds;
    public int Days;
    [TextArea(1, 1)]
    public string Formatted;

    private void Update()
    {
        if (director == null) return;
        var time = director.TimeSource;

        Hours = time.Hours;
        Minutes = time.Minutes;
        Days = time.Days;
        Formatted = $"{Hours:00}:{Minutes:00} (Days {Days})";
    }
}
