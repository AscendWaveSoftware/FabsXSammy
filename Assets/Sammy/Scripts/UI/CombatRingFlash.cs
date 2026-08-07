using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Pooled one-shot impact flash: an expanding ring with a short glow core,
/// billboarded towards the player camera.
/// </summary>
public class CombatRingFlash : MonoBehaviour
{
    private static readonly Queue<CombatRingFlash> Pool = new Queue<CombatRingFlash>();

    private SpriteRenderer m_ringRenderer;
    private SpriteRenderer m_coreRenderer;
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
        float _duration)
    {
        if (_duration <= 0f)
            return;

        CombatRingFlash flash = GetOrCreate();
        flash.gameObject.SetActive(true);
        flash.Initialize(_worldPosition, _color, _startDiameter, _endDiameter, _duration);
    }

    private static CombatRingFlash GetOrCreate()
    {
        while (Pool.Count > 0)
        {
            CombatRingFlash pooledFlash = Pool.Dequeue();

            if (pooledFlash != null)
                return pooledFlash;
        }

        GameObject flashObject = new GameObject("Combat Ring Flash", typeof(CombatRingFlash));
        return flashObject.GetComponent<CombatRingFlash>();
    }

    private void Awake()
    {
        m_ringRenderer = CreateRenderer("Ring", CombatFeedbackSprites.Ring, 201);
        m_coreRenderer = CreateRenderer("Core", CombatFeedbackSprites.Glow, 200);
    }

    private void Update()
    {
        // Frozen with the arena while the player is on the tower camera.
        if (PveRuntime.IsPaused)
            return;

        if (!m_isRunning)
            return;

        // Combat hits briefly slow the game down. The flash reads as an instant
        // reaction, so it runs on unscaled time just like the damage numbers.
        m_elapsedTime += Time.unscaledDeltaTime;
        float normalizedTime = Mathf.Clamp01(m_elapsedTime / m_duration);

        float expansion = 1f - Mathf.Pow(1f - normalizedTime, 3f);
        float diameter = Mathf.Lerp(m_startDiameter, m_endDiameter, expansion);
        m_ringRenderer.transform.localScale = Vector3.one * diameter;
        SetRendererAlpha(m_ringRenderer, 1f - normalizedTime);

        float coreProgress = Mathf.Clamp01(normalizedTime / 0.45f);
        m_coreRenderer.transform.localScale = Vector3.one * Mathf.Lerp(m_startDiameter * 1.1f, m_startDiameter * 0.2f, coreProgress);
        SetRendererAlpha(m_coreRenderer, (1f - coreProgress) * 0.85f);

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
            transform.position + m_cameraTransform.rotation * Vector3.forward,
            m_cameraTransform.rotation * Vector3.up
        );
    }

    private void Initialize(
        Vector3 _worldPosition,
        Color _color,
        float _startDiameter,
        float _endDiameter,
        float _duration)
    {
        ResolveCamera();

        transform.position = _worldPosition;
        transform.rotation = Quaternion.identity;

        m_color = _color;
        m_elapsedTime = 0f;
        m_duration = Mathf.Max(0.01f, _duration);
        m_startDiameter = Mathf.Max(0.01f, _startDiameter);
        m_endDiameter = Mathf.Max(m_startDiameter, _endDiameter);
        m_isRunning = true;

        m_ringRenderer.color = m_color;
        m_ringRenderer.transform.localScale = Vector3.one * m_startDiameter;
        m_coreRenderer.color = m_color;
        m_coreRenderer.transform.localScale = Vector3.one * m_startDiameter * 1.1f;
    }

    private SpriteRenderer CreateRenderer(string _name, Sprite _sprite, int _sortingOrder)
    {
        GameObject rendererObject = new GameObject(_name);
        rendererObject.transform.SetParent(transform, false);

        SpriteRenderer spriteRenderer = rendererObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = _sprite;
        spriteRenderer.sortingOrder = _sortingOrder;
        spriteRenderer.shadowCastingMode = ShadowCastingMode.Off;
        spriteRenderer.receiveShadows = false;
        spriteRenderer.lightProbeUsage = LightProbeUsage.Off;
        spriteRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return spriteRenderer;
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

    private static void SetRendererAlpha(SpriteRenderer _renderer, float _alpha)
    {
        Color color = _renderer.color;
        color.a = Mathf.Clamp01(_alpha);
        _renderer.color = color;
    }
}
