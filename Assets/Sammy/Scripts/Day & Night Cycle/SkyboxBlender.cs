using UnityEngine;

public sealed class SkyboxBlender
{
    private readonly Material skyMat;
    public SkyboxBlender(Material _mat)
    {
        skyMat = _mat;
    }

    public void SetPair(Texture _a, Texture _b)
    {
        if (!skyMat) return;
        skyMat.SetTexture("_Texture1", _a);
        skyMat.SetTexture("_Texture2", _b);
        skyMat.SetFloat("_Blend", 0f);
    }

    public void SetBlend01(float _x)
    {
        if (!skyMat) return;
        skyMat.SetFloat("_Blend", Mathf.Clamp01(_x));
    }
}