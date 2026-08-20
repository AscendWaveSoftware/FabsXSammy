using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// One mote of the accretion disc. Spawned at the rim and wound inwards on a
/// spiral that speeds up as it falls, which is what sells the hole as something
/// that is actually eating the arena rather than a sprite sitting on top of it.
/// </summary>
public class VortexDebris : MonoBehaviour
{
    private static readonly Queue<VortexDebris> Pool = new Queue<VortexDebris>();

    private SpriteRenderer m_renderer;
    private Vector3 m_center;
    private Color m_color;
    private float m_startRadius;
    private float m_startAngle;
    private float m_sweep;
    private float m_startDiameter;
    private float m_duration;
    private float m_elapsedTime;
    private float m_verticalOffset;
    private bool m_isRunning;

    public static void Spawn(
        Vector3 _center,
        float _startRadius,
        float _startDiameter,
        float _duration,
        float _sweep,
        Color _color,
        SpellDefinition _definition)
    {
        if (_duration <= 0f || _startRadius <= 0f)
            return;

        VortexDebris debris = GetOrCreate();
        debris.gameObject.SetActive(true);
        debris.Initialize(_center, _startRadius, _startDiameter, _duration, _sweep, _color, _definition);
    }

    private static VortexDebris GetOrCreate()
    {
        while (Pool.Count > 0)
        {
            VortexDebris pooledDebris = Pool.Dequeue();

            if (pooledDebris != null)
                return pooledDebris;
        }

        GameObject debrisObject = new GameObject("Vortex Debris", typeof(VortexDebris));
        return debrisObject.GetComponent<VortexDebris>();
    }

    private void Awake()
    {
        m_renderer = gameObject.AddComponent<SpriteRenderer>();
        m_renderer.sprite = CombatFeedbackSprites.Glow;
        m_renderer.sortingOrder = 205;
        m_renderer.shadowCastingMode = ShadowCastingMode.Off;
        m_renderer.receiveShadows = false;
        m_renderer.lightProbeUsage = LightProbeUsage.Off;
        m_renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private void Initialize(
        Vector3 _center,
        float _startRadius,
        float _startDiameter,
        float _duration,
        float _sweep,
        Color _color,
        SpellDefinition _definition)
    {
        m_center = _center;
        m_startRadius = _startRadius;
        m_startDiameter = _startDiameter;
        m_duration = _duration;
        m_sweep = _sweep;
        m_color = _color;
        m_elapsedTime = 0f;
        m_startAngle = Random.Range(0f, Mathf.PI * 2f);
        m_verticalOffset = Random.Range(-0.35f, 0.35f);
        m_isRunning = true;

        SpellEmission.Apply(m_renderer, _definition);
        ApplyState(0f);
    }

    private void Update()
    {
        // Frozen with the arena while the player is on the tower camera.
        if (PveRuntime.IsPaused)
            return;

        if (!m_isRunning)
            return;

        m_elapsedTime += Time.deltaTime;
        float progress = Mathf.Clamp01(m_elapsedTime / m_duration);
        ApplyState(progress);

        if (progress >= 1f)
            ReturnToPool();
    }

    private void ApplyState(float _progress)
    {
        // Squared, so the mote loiters at the rim and then drops away sharply.
        // A linear fall in looks like it is being reeled in on a string.
        float fall = _progress * _progress;
        float radius = Mathf.Lerp(m_startRadius, 0f, fall);
        float angle = m_startAngle + m_sweep * _progress;

        transform.position = m_center + new Vector3(
            Mathf.Cos(angle) * radius,
            m_verticalOffset * (1f - fall),
            Mathf.Sin(angle) * radius
        );

        // Shrinks towards the centre, which reads as being crushed rather than as
        // simply fading out.
        float diameter = m_startDiameter * Mathf.Lerp(1f, 0.15f, fall);
        transform.localScale = Vector3.one * diameter;

        Color color = m_color;
        color.a = m_color.a * Mathf.Clamp01(1f - _progress * _progress * 0.8f);
        m_renderer.color = color;
    }

    private void ReturnToPool()
    {
        if (!m_isRunning)
            return;

        m_isRunning = false;
        gameObject.SetActive(false);
        Pool.Enqueue(this);
    }
}
