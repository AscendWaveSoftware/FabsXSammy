using UnityEngine;

/// <summary>
/// Shared look for the code built interface: dark iron plates framed in riveted
/// brass, matching the game's logo. Every sprite is generated once in code, so
/// the HUD keeps working without any texture having to be wired into a prefab.
/// </summary>
public static class SteampunkUI
{
    public static readonly Color IronDeep = new(0.07f, 0.06f, 0.05f, 1f);
    public static readonly Color Brass = new(0.78f, 0.6f, 0.28f, 1f);
    public static readonly Color BrassLight = new(0.93f, 0.8f, 0.52f, 1f);
    public static readonly Color Copper = new(0.86f, 0.55f, 0.28f, 1f);
    public static readonly Color Parchment = new(0.96f, 0.9f, 0.78f, 1f);
    public static readonly Color ParchmentMuted = new(0.74f, 0.64f, 0.48f, 1f);
    public static readonly Color TextOnBrass = new(0.13f, 0.08f, 0.03f, 1f);

    private static Sprite s_frame;
    private static Sprite s_plate;
    private static Sprite s_track;
    private static Sprite s_barFill;
    private static Sprite s_gear;
    private static Sprite s_disc;

    /// <summary>Iron plate with a bevelled brass rim and corner rivets. Nine sliced.</summary>
    public static Sprite Frame => s_frame != null ? s_frame : s_frame = Build("Steampunk Frame", 96, 96, 22, FramePixel);

    /// <summary>Polished brass plate for buttons and badges. Nine sliced.</summary>
    public static Sprite Plate => s_plate != null ? s_plate : s_plate = Build("Steampunk Plate", 64, 28, 11, PlatePixel);

    /// <summary>Recessed slot a bar runs in. Nine sliced.</summary>
    public static Sprite Track => s_track != null ? s_track : s_track = Build("Steampunk Track", 32, 20, 8, TrackPixel);

    /// <summary>Glossy bar body. Greyscale, so the image colour decides the hue.</summary>
    public static Sprite BarFill => s_barFill != null ? s_barFill : s_barFill = Build("Steampunk Bar", 4, 32, 0, BarPixel);

    /// <summary>Twelve toothed cog. Greyscale, tinted by the image colour.</summary>
    public static Sprite Gear => s_gear != null ? s_gear : s_gear = Build("Steampunk Gear", 128, 128, 0, GearPixel);

    /// <summary>Soft edged disc, darker towards the rim, for gauge faces.</summary>
    public static Sprite Disc => s_disc != null ? s_disc : s_disc = Build("Steampunk Disc", 64, 64, 0, DiscPixel);

    private delegate Color PixelShader(float _x, float _y, int _width, int _height);

    private static Sprite Build(string _name, int _width, int _height, int _border, PixelShader _shader)
    {
        Texture2D texture = new(_width, _height, TextureFormat.RGBA32, false)
        {
            name = _name,
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[_width * _height];

        for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
                pixels[y * _width + x] = _shader(x + 0.5f, y + 0.5f, _width, _height);

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, _width, _height), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, Vector4.one * _border);
        sprite.name = _name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    /// <summary>Signed distance to a rounded rectangle filling the texture; negative inside.</summary>
    private static float RoundedRect(float _x, float _y, int _w, int _h, float _radius)
    {
        float qx = Mathf.Abs(_x - _w * 0.5f) - (_w * 0.5f - _radius);
        float qy = Mathf.Abs(_y - _h * 0.5f) - (_h * 0.5f - _radius);
        float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
        return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - _radius;
    }

    private static Color Shade(Color _color, float _amount) =>
        new(_color.r * _amount, _color.g * _amount, _color.b * _amount, _color.a);

    private static Color FramePixel(float _x, float _y, int _w, int _h)
    {
        float depth = -RoundedRect(_x, _y, _w, _h, 11f);
        float t = _y / _h;
        Color color;

        if (depth < 1.6f)
            color = new Color(0.07f, 0.05f, 0.03f, 1f);
        else if (depth < 6.5f)
        {
            color = Color.Lerp(new Color(0.46f, 0.31f, 0.11f, 1f), BrassLight, 0.15f + t * 0.7f);
            if (depth < 3f) color = Shade(color, 1.15f);
            else if (depth > 5.2f) color = Shade(color, 0.8f);
        }
        else if (depth < 8f)
            color = new Color(0.05f, 0.04f, 0.03f, 1f);
        else
        {
            color = Color.Lerp(new Color(0.08f, 0.07f, 0.058f, 1f), new Color(0.15f, 0.13f, 0.105f, 1f), t);
            if (depth < 11f) color = Shade(color, 0.7f + (depth - 8f) * 0.1f);
        }

        // Rivets sit inside the corner slices, so they stay put however far the frame stretches.
        foreach (Vector2 corner in new[] { new Vector2(14.5f, 14.5f), new Vector2(_w - 14.5f, 14.5f), new Vector2(14.5f, _h - 14.5f), new Vector2(_w - 14.5f, _h - 14.5f) })
        {
            float distance = Vector2.Distance(new Vector2(_x, _y), corner);
            if (distance > 3.6f)
                continue;

            float light = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(_x, _y), corner + new Vector2(-1.2f, 1.2f)) / 3.6f);
            Color rivet = Color.Lerp(Shade(Brass, 0.55f), BrassLight, light);
            color = Color.Lerp(color, rivet, Mathf.Clamp01(3.6f - distance));
        }

        color.a = Mathf.Clamp01(depth + 0.5f);
        return color;
    }

    private static Color PlatePixel(float _x, float _y, int _w, int _h)
    {
        float depth = -RoundedRect(_x, _y, _w, _h, 7f);
        float t = _y / _h;
        Color color = depth < 1.3f
            ? new Color(0.22f, 0.13f, 0.04f, 1f)
            : Color.Lerp(new Color(0.6f, 0.42f, 0.14f, 1f), new Color(0.97f, 0.85f, 0.55f, 1f), t);

        if (depth >= 1.3f && depth < 2.6f && t > 0.5f)
            color = Shade(color, 1.12f);

        color.a = Mathf.Clamp01(depth + 0.5f);
        return color;
    }

    private static Color TrackPixel(float _x, float _y, int _w, int _h)
    {
        float depth = -RoundedRect(_x, _y, _w, _h, 5f);
        float t = _y / _h;
        Color color = depth < 1.2f
            ? new Color(0.45f, 0.32f, 0.15f, 1f)
            : Color.Lerp(new Color(0.075f, 0.065f, 0.055f, 1f), new Color(0.02f, 0.018f, 0.015f, 1f), t);
        color.a = Mathf.Clamp01(depth + 0.5f);
        return color;
    }

    private static Color BarPixel(float _x, float _y, int _w, int _h)
    {
        float t = _y / _h;
        float value = 0.62f + 0.38f * t;
        if (t > 0.68f && t < 0.84f) value += 0.16f;
        if (t < 0.14f) value -= 0.14f;
        return new Color(value, value, value, 1f);
    }

    private static Color GearPixel(float _x, float _y, int _w, int _h)
    {
        Vector2 offset = new(_x - _w * 0.5f, _y - _h * 0.5f);
        float radius = offset.magnitude;
        float angle = Mathf.Atan2(offset.y, offset.x) / (Mathf.PI * 2f) + 0.5f;
        float phase = Mathf.Repeat(angle * 12f, 1f);
        float tooth = Mathf.Clamp01(Mathf.Min(phase - 0.2f, 0.8f - phase) * 10f);
        float outer = Mathf.Lerp(47f, 60f, tooth);

        float alpha = Mathf.Clamp01(outer - radius + 0.5f) * Mathf.Clamp01(radius - 17f + 0.5f);
        float value = 0.72f + 0.2f * (1f - radius / 60f) + 0.1f * (offset.y / Mathf.Max(1f, radius));
        if (radius > 30f && radius < 34f) value *= 0.6f;
        return new Color(value, value, value, alpha);
    }

    private static Color DiscPixel(float _x, float _y, int _w, int _h)
    {
        float radius = Vector2.Distance(new Vector2(_x, _y), new Vector2(_w * 0.5f, _h * 0.5f));
        float value = Mathf.Lerp(1f, 0.72f, radius / (_w * 0.5f));
        return new Color(value, value, value, Mathf.Clamp01(_w * 0.5f - 0.5f - radius + 0.5f));
    }
}
