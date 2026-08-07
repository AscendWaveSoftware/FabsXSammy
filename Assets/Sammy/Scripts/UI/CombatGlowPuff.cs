using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Pooled soft glow that fades away where it was spawned. Used for spell trails
/// and any other short lived spark that should not be tied to a moving object.
/// </summary>
public class CombatGlowPuff : MonoBehaviour
{
    private static readonly Queue<CombatGlowPuff> Pool = new Queue<CombatGlowPuff>();

    private SpriteRenderer m_renderer;
    private Material m_defaultMaterial;
    private Transform m_cameraTransform;
    private Color m_color;
    private float m_elapsedTime;
    private float m_duration;
    private float m_startDiameter;
    private float m_endDiameter;
    private bool m_isRunning;

    public static void Show(
        Vector3 _worldPosition,
        Color _color,
        float _startDiameter,
        float _endDiameter,
        float _duration,
        int _sortingOrder = 199,
        Material _emissiveMaterial = null,
        float _emissionIntensity = 1f)
    {
        if (_duration <= 0f || _startDiameter <= 0f)
            return;

        CombatGlowPuff puff = GetOrCreate();
        puff.gameObject.SetActive(true);
        puff.Initialize(_worldPosition, _color, _startDiameter, _endDiameter, _duration, _sortingOrder);
        puff.ApplyEmission(_emissiveMaterial, _emissionIntensity);
    }

    /// <summary>
    /// Pooled objects outlive the effect that spawned them, so a puff that once
    /// carried a spell has to be put back on its plain material before something
    /// else, like a poison tick, reuses it.
    /// </summary>
    private void ApplyEmission(Material _emissiveMaterial, float _emissionIntensity)
    {
        if (_emissiveMaterial != null)
            SpellEmission.Apply(m_renderer, _emissiveMaterial, _emissionIntensity);
        else
            SpellEmission.Clear(m_renderer, m_defaultMaterial);
    }

    private static CombatGlowPuff GetOrCreate()
    {
        while (Pool.Count > 0)
        {
            CombatGlowPuff pooledPuff = Pool.Dequeue();

            if (pooledPuff != null)
                return pooledPuff;
        }

        GameObject puffObject = new GameObject("Combat Glow Puff", typeof(CombatGlowPuff));
        return puffObject.GetComponent<CombatGlowPuff>();
    }

    private void Awake()
    {
        m_renderer = gameObject.AddComponent<SpriteRenderer>();
        m_renderer.sprite = CombatFeedbackSprites.Glow;
        m_defaultMaterial = m_renderer.sharedMaterial;
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

        // Scaled time: a spell trail belongs to the world and should slow down
        // together with it during the combat hit slow motion.
        m_elapsedTime += Time.deltaTime;
        float normalizedTime = Mathf.Clamp01(m_elapsedTime / m_duration);

        float diameter = Mathf.Lerp(m_startDiameter, m_endDiameter, normalizedTime);
        transform.localScale = Vector3.one * diameter;

        Color color = m_color;
        color.a = m_color.a * (1f - normalizedTime);
        m_renderer.color = color;

        if (normalizedTime >= 1f)
            ReturnToPool();
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
        Vector3 _worldPosition,
        Color _color,
        float _startDiameter,
        float _endDiameter,
        float _duration,
        int _sortingOrder)
    {
        ResolveCamera();

        transform.position = _worldPosition;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one * _startDiameter;

        m_color = _color;
        m_elapsedTime = 0f;
        m_duration = Mathf.Max(0.01f, _duration);
        m_startDiameter = _startDiameter;
        m_endDiameter = Mathf.Max(0f, _endDiameter);
        m_isRunning = true;

        m_renderer.color = _color;
        m_renderer.sortingOrder = _sortingOrder;
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
        gameObject.SetActive(false);
        Pool.Enqueue(this);
    }
}
