using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PostDirector
{
    private readonly Volume volume;
    private readonly ColorAdjustments colorAdj;
    private readonly Bloom bloom;
    private readonly Vignette vignette;
    private readonly Tonemapping tone;

    public float exposureDamp = 3.0f;
    private float evSmoothed;
    private bool init;

    public PostDirector(Volume _v)
    {
        volume = _v;
        if (!volume || !volume.profile) return;

        volume.profile.TryGet(out colorAdj);
        volume.profile.TryGet(out bloom);
        volume.profile.TryGet(out vignette);
        volume.profile.TryGet(out tone);
    }

    public void ApplyTime(float _t01, float _facingSun01, TimeProfile _p)
    {
        if (_p == null || colorAdj == null) return;

        // Color Filter
        if (_p.PostColorFilterOverTime != null)
        {
            colorAdj.active = true;
            colorAdj.colorFilter.Override(_p.PostColorFilterOverTime.Evaluate(_t01));
        }

        // Exposure
        if (_p.PostExposureEVOverTime != null)
        {
            float evTarget = _p.PostExposureEVOverTime.Evaluate(_t01);
            if (!init) { evSmoothed = evTarget; init = true; }
            evSmoothed = Mathf.Lerp(evSmoothed, evTarget, 1f - Mathf.Exp(-exposureDamp * Time.deltaTime));
            colorAdj.postExposure.Override(evSmoothed);
        }

        // Bloom
        if (bloom != null)
        {
            float baseBloom = _p.BloomIntensityOverTime != null ? _p.BloomIntensityOverTime.Evaluate(_t01) : 0f;
            float facingBonus = Mathf.Lerp(0f, _p.FacingBloomBonus, Mathf.Clamp01(_facingSun01));
            bloom.active = true;
            bloom.intensity.Override(baseBloom + facingBonus);
        }

        // Vignette
        if (vignette != null)
        {
            vignette.active = true;
            if (_p.VignetteIntensityOverTime != null)
                vignette.intensity.Override(_p.VignetteIntensityOverTime.Evaluate(_t01));
            if (_p.VignetteColorOverTime != null)
                vignette.color.Override(_p.VignetteColorOverTime.Evaluate(_t01));
        }

        // Tonemapping
        if (tone != null)
        {
            tone.active = true;
            tone.mode.Override(_p.UseACES ? TonemappingMode.ACES : TonemappingMode.None);
        }
    }
}