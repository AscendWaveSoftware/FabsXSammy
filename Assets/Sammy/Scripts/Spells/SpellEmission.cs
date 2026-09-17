using UnityEngine;

public static class SpellEmission
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private static MaterialPropertyBlock s_propertyBlock;

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

        float brightness = Mathf.Max(1f, _intensity);
        s_propertyBlock.SetColor(EmissionColorId, new Color(brightness, brightness, brightness, 1f));
        _renderer.SetPropertyBlock(s_propertyBlock);
    }

    public static void Clear(Renderer _renderer, Material _defaultMaterial)
    {
        if (_renderer == null)
            return;

        if (_defaultMaterial != null)
            _renderer.sharedMaterial = _defaultMaterial;

        _renderer.SetPropertyBlock(null);
    }
}
