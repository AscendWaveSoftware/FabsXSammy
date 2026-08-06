using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
[CreateAssetMenu(fileName = "Time Profile", menuName = "Enviroment/Time Profile", order = 0)]
public class TimeProfile : ScriptableObject
{
    [Header("Skybox Textures")]
    public Texture2D SkyboxNight;
    public Texture2D SkyboxSunrise;
    public Texture2D SkyboxDay;
    public Texture2D SkyboxSunset;

    [Header("Light Gradients")]
    [FormerlySerializedAs("graddientNightToSunrise")]
    public Gradient GradientNightToSunrise;
    [FormerlySerializedAs("graddientSunriseToDay")]
    public Gradient GradientSunriseToDay;
    [FormerlySerializedAs("graddientDayToSunset")]
    public Gradient GradientDayToSunset;
    [FormerlySerializedAs("graddientSunsetToNight")]
    public Gradient GradientSunsetToNight;

    [Header("Time")]
    [Tooltip("Echte Sekunden pro In-Game-Minute.")]
    [Min(0.01f)] public float RealSecondsPerGameMinute = 1f;

    [Header("Start Time")]
    [Range(0, 23)] public int StartHour = 6;
    [Range(0, 59)] public int StartMinute = 0;

    [Header("Phase Switch (Stunden)")]
    public int SunriseHour = 6;
    public int DayHour = 8;
    public int SunsetHour = 18;
    public int NightHour = 22;

    [Header("Blend Duration")]
    [Tooltip("Sekunden f�r Skybox- und Light-Lerp.")]
    [Min(0.01f)] public float TransitionSeconds = 10f;

    [Header("Post-Processing (Time-based)")]
    [Tooltip("0..1 ueber den Tag (0 = 00:00, 0.5 = 12:00, 1 = 24:00)")]
    public Gradient PostColorFilterOverTime;
    public AnimationCurve PostExposureEVOverTime;
    public AnimationCurve BloomIntensityOverTime;
    public AnimationCurve VignetteIntensityOverTime;
    public Gradient VignetteColorOverTime;
    public bool UseACES = true;

    [Header("Bloom Facing Settings")]
    [Tooltip("Dot-Wert, ab dem der Facing-Bloom anf�ngt.")]
    [Range(-1f, 1f)] public float FacingBloomDotMin = 0.75f;
    [Tooltip("Dot-Wert, bei dem Facing-Bloom maximal ist.")]
    [Range(-1f, 1f)] public float FacingBloomDotMax = 0.98f;
    [Tooltip("Zus�tzlicher Bloom-Intensit�tsbonus beim maximalem Facing.")]
    [Min(0f)] public float FacingBloomBonus = 0.6f;

    private void OnValidate()
    {
        SunriseHour = Mathf.Clamp(SunriseHour, 0, 20);
        DayHour = Mathf.Clamp(DayHour, SunriseHour + 1, 21);
        SunsetHour = Mathf.Clamp(SunsetHour, DayHour + 1, 22);
        NightHour = Mathf.Clamp(NightHour, SunsetHour + 1, 23);
    }
}
