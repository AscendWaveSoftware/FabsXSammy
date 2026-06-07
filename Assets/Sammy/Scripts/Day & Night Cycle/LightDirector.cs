using UnityEngine;

public sealed class LightDirector
{
    private readonly Light sun;

    public LightDirector(Light _sun)
    {
        this.sun = _sun;
    }

    public void ApplyColor(Gradient _grad, float _t01)
    {
        if (!sun || _grad == null) return;
        var c = _grad.Evaluate(_t01);
        sun.color = c;
        RenderSettings.fogColor = c;
    }
}