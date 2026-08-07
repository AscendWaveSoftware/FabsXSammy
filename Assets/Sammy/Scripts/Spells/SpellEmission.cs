using UnityEngine;

/// <summary>
/// Pushes a spell renderer past white so the bloom pass picks it up.
///
/// A SpriteRenderer keeps its colour as a Color32 vertex attribute, which cannot
/// hold anything above 1 and therefore can never cross the bloom threshold. The
/// emissive material takes the brightness from a shader property instead, set
/// here through a property block where full float precision survives.
/// </summary>
public static class SpellEmission
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private static MaterialPropertyBlock s_propertyBlock;

    /// <summary>
    /// Switches a renderer over to the spell's emissive material. The renderer's
    /// own colour keeps driving fades and tints as before, because the shader
    /// multiplies the two together.
    /// </summary>
    public static void Apply(Renderer _renderer, SpellDefinition _definition)
    {
        if (_renderer == null || _definition == null || _definition.EmissiveMaterial == null)
            return;

        Apply(_renderer, _definition.EmissiveMaterial, _definition.EmissionIntensity);
    }

    public static void Apply(Renderer _renderer, Material _emissiveMaterial, float _intensity)
    {
        if (_renderer == null || _emissiveMaterial == null)
            return;

        _renderer.sharedMaterial = _emissiveMaterial;

        s_propertyBlock ??= new MaterialPropertyBlock();
        _renderer.GetPropertyBlock(s_propertyBlock);

        // Alpha stays at 1: it is a pure brightness multiplier, and scaling alpha
        // as well would blow every fade out to fully opaque.
        float brightness = Mathf.Max(1f, _intensity);
        s_propertyBlock.SetColor(EmissionColorId, new Color(brightness, brightness, brightness, 1f));
        _renderer.SetPropertyBlock(s_propertyBlock);
    }

    /// <summary>
    /// Puts a pooled renderer back on its plain material, so an object that once
    /// carried a spell does not keep glowing when something else reuses it.
    /// </summary>
    public static void Clear(Renderer _renderer, Material _defaultMaterial)
    {
        if (_renderer == null)
            return;

        if (_defaultMaterial != null)
            _renderer.sharedMaterial = _defaultMaterial;

        _renderer.SetPropertyBlock(null);
    }
}
