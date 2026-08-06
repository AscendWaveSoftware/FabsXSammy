using UnityEngine;

public sealed class SkyboxBlender
{
    private readonly Material skyMat;
    private Texture currentA;
    private Texture currentB;

    public SkyboxBlender(Material _mat)
    {
        skyMat = _mat;
    }

    public void SetPair(Texture _a, Texture _b)
    {
        if (!skyMat) return;

        if (currentA == _a && currentB == _b)
        {
            return;
        }

        currentA = _a;
        currentB = _b;
        skyMat.SetTexture("_Texture1", _a);
        skyMat.SetTexture("_Texture2", _b);
    }

    public void SetBlend01(float _x)
    {
        if (!skyMat) return;
        skyMat.SetFloat("_Blend", Mathf.Clamp01(_x));
    }

    public void Apply(Texture _a, Texture _b, float _blend01)
    {
        SetPair(_a, _b);
        SetBlend01(_blend01);
    }
}
