using UnityEngine;

public static class SunRotation
{
    public static Quaternion FromTime01(float t01, float yRotation = 170f)
    {
        // 0.00 = Mitternacht
        // 0.25 = Sonnenaufgang
        // 0.50 = Mittag
        // 0.75 = Sonnenuntergang
        float xRotation = (t01 - 0.25f) * 360f;

        return Quaternion.Euler(xRotation, yRotation, 0f);
    }
}