using DNE.Events;
using UnityEngine;
using UnityEngine.Rendering;

public class EnvironmentDirector : MonoBehaviour
{
    public ClockService TimeSource => clock;

    [SerializeField] private TimeProfile profile;
    public TimeProfile Profile => profile;
    [SerializeField] private Light sun;
    [SerializeField] private Material skyboxMat;
    [SerializeField] private Volume postVolume;
    [SerializeField] private Transform cameraTransform;

    private ClockService clock;
    private PhaseService phase;
    private SkyboxBlender sky;
    private LightDirector lightDir;
    private TransitionRunner transition = new();
    private PostDirector post;

    private System.Action<int> _onHourChanged;

    private void Awake()
    {
        clock = new ClockService();
        clock.SetTime(profile.StartHour, profile.StartMinute);
        phase = new PhaseService(profile.SunriseHour, profile.DayHour, profile.SunsetHour, profile.NightHour);
        sky = new SkyboxBlender(skyboxMat);
        lightDir = new LightDirector(sun);
        if (sun)
            RenderSettings.sun = sun;
        post = new PostDirector(postVolume);

        _onHourChanged = OnHourChanged;
        clock.HourChanged += _onHourChanged;
        phase.PhaseChanged += OnPhaseChanged;

        var p0 = phase.Evaluate(clock.Hours);
        ApplyPhaseImmediate(p0);

        ApplyPost();
    }

    private void Update()
    {
        if (!profile) return;

        clock.Tick(Time.deltaTime, profile.RealSecondsPerGameMinute);

        sun.transform.rotation = SunRotation.FromTime01(clock.TimeOfDay01);

        transition.Tick(Time.deltaTime);

        ApplyPost();
    }

    private void OnDestroy()
    {
        if (clock != null && _onHourChanged != null)
        {
            clock.HourChanged -= _onHourChanged;
        }
        if (phase != null)
        {
            phase.PhaseChanged -= OnPhaseChanged;
        }
    }

    private void OnHourChanged(int _h) => phase.Update(_h);

    private void OnPhaseChanged(DayPhase _prev, DayPhase _next)
    {
        switch (_next)
        {
            case DayPhase.Sunrise:
                sky.SetPair(profile.SkyboxNight, profile.SkyboxSunrise);
                transition.Start(profile.TransitionSeconds, x => {
                    sky.SetBlend01(x);
                    lightDir.ApplyColor(profile.GradientNightToSunrise, x);
                }, () => sky.SetPair(profile.SkyboxSunrise, profile.SkyboxSunrise));
                break;

            case DayPhase.Day:
                sky.SetPair(profile.SkyboxSunrise, profile.SkyboxDay);
                transition.Start(profile.TransitionSeconds, x => {
                    sky.SetBlend01(x);
                    lightDir.ApplyColor(profile.GradientSunriseToDay, x);
                }, () => sky.SetPair(profile.SkyboxDay, profile.SkyboxDay));
                break;

            case DayPhase.Sunset:
                sky.SetPair(profile.SkyboxDay, profile.SkyboxSunset);
                transition.Start(profile.TransitionSeconds, x => {
                    sky.SetBlend01(x);
                    lightDir.ApplyColor(profile.GradientDayToSunset, x);
                }, () => sky.SetPair(profile.SkyboxSunset, profile.SkyboxSunset));
                break;

            case DayPhase.Night:
                sky.SetPair(profile.SkyboxSunset, profile.SkyboxNight);
                transition.Start(profile.TransitionSeconds, x => {
                    sky.SetBlend01(x);
                    lightDir.ApplyColor(profile.GradientSunsetToNight, x);
                }, () => sky.SetPair(profile.SkyboxNight, profile.SkyboxNight));
                break;
        }
    }

    private void ApplyPhaseImmediate(DayPhase _p)
    {
        switch (_p)
        {
            case DayPhase.Night:
                sky.SetPair(profile.SkyboxNight, profile.SkyboxNight);
                lightDir.ApplyColor(profile.GradientSunsetToNight, 1f);
                break;
            case DayPhase.Sunrise:
                sky.SetPair(profile.SkyboxSunrise, profile.SkyboxSunrise);
                lightDir.ApplyColor(profile.GradientNightToSunrise, 1f);
                break;
            case DayPhase.Day:
                sky.SetPair(profile.SkyboxDay, profile.SkyboxDay);
                lightDir.ApplyColor(profile.GradientSunriseToDay, 1f);
                break;
            case DayPhase.Sunset:
                sky.SetPair(profile.SkyboxSunset, profile.SkyboxSunset);
                lightDir.ApplyColor(profile.GradientDayToSunset, 1f);
                break;
        }
    }

    private void ApplyPost()
    {
        if (post == null || profile == null) return;

        float facing = 0f;
        if (cameraTransform && sun)
        {
            float dot = Vector3.Dot(cameraTransform.forward, -sun.transform.forward);
            facing = Mathf.Clamp01(Mathf.InverseLerp(
            profile.FacingBloomDotMin,
            profile.FacingBloomDotMax,
            dot
            ));
        }

        float t01 = TimeSource != null ? TimeSource.TimeOfDay01 : 0f;
        post.ApplyTime(t01, facing, profile);
    }
}