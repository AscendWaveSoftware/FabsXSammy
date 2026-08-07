using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Pooled, purely visual detonation. Plays the impact frames of a spell sheet at
/// the size the spell actually damages, so what the player sees is what hurts.
/// Damage itself is applied by <see cref="SpellProjectile"/>.
/// </summary>
public class SpellImpact : MonoBehaviour
{
    private static readonly Queue<SpellImpact> Pool = new Queue<SpellImpact>();

    private SpriteRenderer m_renderer;
    private Transform m_cameraTransform;
    private SpellDefinition m_definition;
    private float m_elapsedTime;
    private float m_frameDuration;
    private int m_frameCount;
    private int m_currentFrame;
    private bool m_isRunning;

    /// <summary>Full detonation, sized to the radius the spell actually damages.</summary>
    public static void Show(SpellDefinition _definition, Vector3 _worldPosition)
    {
        if (_definition == null)
            return;

        float targetWidth = _definition.ImpactRadius * 2f * _definition.ImpactVisualScale;
        Spawn(_definition, _worldPosition, _definition.ImpactFrameCount, targetWidth, _definition.ImpactFrameRate);
    }

    /// <summary>
    /// Short muzzle flash where the spell leaves the caster. Borrows the opening
    /// frames of the impact so the launch speaks the same visual language.
    /// </summary>
    public static void ShowCastFlash(SpellDefinition _definition, Vector3 _worldPosition)
    {
        if (_definition == null || _definition.CastFlashDiameter <= 0f || _definition.CastFlashFrames <= 0)
            return;

        int frameCount = Mathf.Min(_definition.CastFlashFrames, _definition.ImpactFrameCount);
        Spawn(_definition, _worldPosition, frameCount, _definition.CastFlashDiameter, _definition.CastFlashFrameRate);
    }

    private static void Spawn(
        SpellDefinition _definition,
        Vector3 _worldPosition,
        int _frameCount,
        float _worldWidth,
        float _frameRate)
    {
        if (_frameCount <= 0 || _worldWidth <= 0f)
            return;

        Sprite firstFrame = _definition.GetImpactFrame(0);

        if (firstFrame == null)
            return;

        // Derived from the sprite's own world size, so a different sheet or a
        // different pixels per unit setting cannot silently change the scale.
        float spriteWorldSize = Mathf.Max(0.01f, firstFrame.bounds.size.x);

        SpellImpact impact = GetOrCreate();
        impact.gameObject.SetActive(true);
        impact.Initialize(_definition, _worldPosition, _frameCount, _worldWidth / spriteWorldSize, _frameRate);
    }

    private static SpellImpact GetOrCreate()
    {
        while (Pool.Count > 0)
        {
            SpellImpact pooledImpact = Pool.Dequeue();

            if (pooledImpact != null)
                return pooledImpact;
        }

        GameObject impactObject = new GameObject("Spell Impact", typeof(SpellImpact));
        return impactObject.GetComponent<SpellImpact>();
    }

    private void Awake()
    {
        m_renderer = gameObject.AddComponent<SpriteRenderer>();

        // In front of the characters, an explosion that hides behind an enemy
        // would defeat the point of showing it.
        m_renderer.sortingOrder = 210;
        m_renderer.shadowCastingMode = ShadowCastingMode.Off;
        m_renderer.receiveShadows = false;
        m_renderer.lightProbeUsage = LightProbeUsage.Off;
        m_renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private void Update()
    {
        // Frozen with the arena while the player is on the tower camera.
        if (PveRuntime.IsPaused)
            return;

        if (!m_isRunning)
            return;

        m_elapsedTime += Time.deltaTime;

        int targetFrame = Mathf.FloorToInt(m_elapsedTime / m_frameDuration);

        if (targetFrame >= m_frameCount)
        {
            ReturnToPool();
            return;
        }

        if (targetFrame == m_currentFrame)
            return;

        m_currentFrame = targetFrame;
        ApplyFrame(targetFrame);
    }

    private void LateUpdate()
    {
        if (!m_isRunning)
            return;

        if (m_cameraTransform == null)
            ResolveCamera();

        if (m_cameraTransform == null)
            return;

        transform.LookAt(
            transform.position + m_cameraTransform.rotation * -Vector3.forward,
            m_cameraTransform.rotation * Vector3.up
        );
    }

    private void Initialize(
        SpellDefinition _definition,
        Vector3 _worldPosition,
        int _frameCount,
        float _scale,
        float _frameRate)
    {
        ResolveCamera();

        m_definition = _definition;
        m_frameCount = _frameCount;
        m_frameDuration = 1f / Mathf.Max(1f, _frameRate);
        m_elapsedTime = 0f;
        m_currentFrame = 0;
        m_isRunning = true;

        transform.position = _worldPosition;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one * _scale;

        SpellEmission.Apply(m_renderer, _definition);
        ApplyFrame(0);
    }

    private void ApplyFrame(int _impactIndex)
    {
        Sprite frame = m_definition.GetImpactFrame(_impactIndex);

        // A partially filled sheet must not throw every frame. Hiding the
        // renderer keeps the timeline intact until a valid frame comes back.
        m_renderer.sprite = frame;
        m_renderer.enabled = frame != null;
    }

    private void ResolveCamera()
    {
        if (CameraReferences.Instance != null)
            m_cameraTransform = CameraReferences.Instance.PlayerCameraTransform;

        if (m_cameraTransform == null && Camera.main != null)
            m_cameraTransform = Camera.main.transform;
    }

    private void ReturnToPool()
    {
        if (!m_isRunning)
            return;

        m_isRunning = false;
        m_definition = null;
        m_renderer.sprite = null;
        gameObject.SetActive(false);
        Pool.Enqueue(this);
    }
}
