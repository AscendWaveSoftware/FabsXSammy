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
    private PostDirector post;
    private Material runtimeSkyboxMaterial;
    private Material previousSkyboxMaterial;
    private System.Action<int> onHourChanged;

    private void Awake()
    {
        if (!profile)
        {
            Debug.LogError("EnvironmentDirector needs a TimeProfile.", this);
            enabled = false;
            return;
        }

        clock = new ClockService();
        clock.SetTime(profile.StartHour, profile.StartMinute);

        phase = new PhaseService(profile.SunriseHour, profile.DayHour, profile.SunsetHour, profile.NightHour);
        phase.Initialize(clock.Hours);

        previousSkyboxMaterial = RenderSettings.skybox;
        Material sourceSkyboxMaterial = skyboxMat ? skyboxMat : previousSkyboxMaterial;
        if (sourceSkyboxMaterial)
        {
            runtimeSkyboxMaterial = new Material(sourceSkyboxMaterial)
            {
                name = sourceSkyboxMaterial.name + " (Runtime)",
                hideFlags = HideFlags.DontSave
            };
            RenderSettings.skybox = runtimeSkyboxMaterial;
        }

        sky = new SkyboxBlender(runtimeSkyboxMaterial);
        lightDir = new LightDirector(sun);
        if (sun)
        {
            RenderSettings.sun = sun;
        }

        post = new PostDirector(postVolume);

        onHourChanged = OnHourChanged;
        clock.HourChanged += onHourChanged;

        ApplyEnvironment();
        ApplyPost();
    }

    private void Update()
    {
        if (clock == null || !profile)
        {
            return;
        }

        clock.Tick(Time.deltaTime, profile.RealSecondsPerGameMinute);

        if (sun)
        {
            sun.transform.rotation = SunRotation.FromTime01(clock.TimeOfDay01);
        }

        ApplyEnvironment();
        ApplyPost();
    }

    private void OnDestroy()
    {
        if (clock != null && onHourChanged != null)
        {
            clock.HourChanged -= onHourChanged;
        }

        if (runtimeSkyboxMaterial)
        {
            if (RenderSettings.skybox == runtimeSkyboxMaterial)
            {
                RenderSettings.skybox = previousSkyboxMaterial;
            }

            Destroy(runtimeSkyboxMaterial);
        }
    }

    private void OnHourChanged(int _hour)
    {
        phase?.Update(_hour);
    }

    private void ApplyEnvironment()
    {
        if (phase == null || profile == null)
        {
            return;
        }

        float secondsPerGameMinute = Mathf.Max(0.01f, profile.RealSecondsPerGameMinute);
        float transitionGameMinutes = profile.TransitionSeconds / secondsPerGameMinute;
        PhaseTransitionState state = phase.EvaluateTransition(clock.TimeOfDayMinutes, transitionGameMinutes);

        sky?.Apply(GetSkybox(state.From), GetSkybox(state.To), state.Blend01);

        Color fromColor = GetPhaseLightColor(state.From);
        Color toColor = GetPhaseLightColor(state.To);
        lightDir?.ApplyColor(Color.Lerp(fromColor, toColor, state.Blend01));
    }

    private Texture GetSkybox(DayPhase _phase)
    {
        return _phase switch
        {
            DayPhase.Sunrise => profile.SkyboxSunrise,
            DayPhase.Day => profile.SkyboxDay,
            DayPhase.Sunset => profile.SkyboxSunset,
            _ => profile.SkyboxNight
        };
    }

    private Color GetPhaseLightColor(DayPhase _phase)
    {
        return _phase switch
        {
            DayPhase.Sunrise => Average(
                EvaluateGradient(profile.GradientNightToSunrise, 1f),
                EvaluateGradient(profile.GradientSunriseToDay, 0f)),
            DayPhase.Day => Average(
                EvaluateGradient(profile.GradientSunriseToDay, 1f),
                EvaluateGradient(profile.GradientDayToSunset, 0f)),
            DayPhase.Sunset => Average(
                EvaluateGradient(profile.GradientDayToSunset, 1f),
                EvaluateGradient(profile.GradientSunsetToNight, 0f)),
            _ => Average(
                EvaluateGradient(profile.GradientSunsetToNight, 1f),
                EvaluateGradient(profile.GradientNightToSunrise, 0f))
        };
    }

    private static Color EvaluateGradient(Gradient _gradient, float _time)
    {
        return _gradient != null ? _gradient.Evaluate(_time) : Color.white;
    }

    private static Color Average(Color _a, Color _b)
    {
        return (_a + _b) * 0.5f;
    }

    private void ApplyPost()
    {
        if (post == null || profile == null)
        {
            return;
        }

        float facing = 0f;
        if (cameraTransform && sun)
        {
            float dot = Vector3.Dot(cameraTransform.forward, -sun.transform.forward);
            facing = Mathf.Clamp01(Mathf.InverseLerp(
                profile.FacingBloomDotMin,
                profile.FacingBloomDotMax,
                dot));
        }

        float time01 = TimeSource != null ? TimeSource.TimeOfDay01 : 0f;
        post.ApplyTime(time01, facing, profile);
    }
}
