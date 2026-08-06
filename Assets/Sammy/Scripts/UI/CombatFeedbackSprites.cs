using System;
using UnityEngine;

/// <summary>
/// Procedurally generated sprites shared by the combat feedback effects. Building
/// them in code keeps the effects free of art dependencies, so they work in every
/// scene without anyone having to wire textures into a prefab.
/// </summary>
public static class CombatFeedbackSprites
{
    private const int TextureSize = 256;

    private static Sprite s_ring;
    private static Sprite s_glow;

    /// <summary>Soft hollow ring, one world unit wide at scale 1.</summary>
    public static Sprite Ring
    {
        get
        {
            if (s_ring == null)
                s_ring = CreateRadialSprite("Combat Feedback Ring", SampleRingAlpha);

            return s_ring;
        }
    }

    /// <summary>Soft filled glow, one world unit wide at scale 1.</summary>
    public static Sprite Glow
    {
        get
        {
            if (s_glow == null)
                s_glow = CreateRadialSprite("Combat Feedback Glow", SampleGlowAlpha);

            return s_glow;
        }
    }

    /// <summary>
    /// Builds both sprites up front. Generating them lazily would otherwise cost
    /// a texture upload in the exact frame a block or a strike has to feel snappy.
    /// </summary>
    public static void Prewarm()
    {
        _ = Ring;
        _ = Glow;
    }

    private static float SampleRingAlpha(float _normalizedDistance)
    {
        const float ringCenter = 0.76f;
        const float ringThickness = 0.24f;

        float falloff = 1f - Mathf.Clamp01(Mathf.Abs(_normalizedDistance - ringCenter) / ringThickness);
        return falloff * falloff;
    }

    private static float SampleGlowAlpha(float _normalizedDistance)
    {
        float falloff = 1f - Mathf.Clamp01(_normalizedDistance);
        return falloff * falloff * falloff;
    }

    private static Sprite CreateRadialSprite(string _name, Func<float, float> _alphaProfile)
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = _name,
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[TextureSize * TextureSize];
        float center = (TextureSize - 1) * 0.5f;

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float offsetX = (x - center) / center;
                float offsetY = (y - center) / center;
                float distance = Mathf.Sqrt(offsetX * offsetX + offsetY * offsetY);

                pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(_alphaProfile(distance)));
            }
        }

        texture.SetPixels(pixels);

        // The pixels are never read back, so the CPU copy can be dropped.
        texture.Apply(false, true);

        // One pixel unit per texel makes the sprite exactly one world unit wide,
        // so a transform scale reads directly as the effect diameter.
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, TextureSize, TextureSize),
            new Vector2(0.5f, 0.5f),
            TextureSize
        );

        sprite.name = _name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
