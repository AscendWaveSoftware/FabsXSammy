using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Pooled world-space callout for short combat messages such as a successful
/// block. Damage numbers stay with <see cref="EnemyDamageText"/>; this one exists
/// for readable words that need a punchier pop and a longer hold.
/// </summary>
[RequireComponent(typeof(TextMeshPro))]
public class CombatCalloutText : MonoBehaviour
{
    private static readonly Queue<CombatCalloutText> Pool = new Queue<CombatCalloutText>();

    private TextMeshPro m_text;
    private Transform m_cameraTransform;
    private Vector3 m_startPosition;
    private float m_elapsedTime;
    private float m_duration;
    private float m_baseScale;
    private float m_riseHeight;
    private bool m_isRunning;

    public static void Show(
        Vector3 _worldPosition,
        string _message,
        Color _textColor,
        Color _outlineColor,
        float _scale,
        float _duration,
        float _riseHeight = 0.55f)
    {
        if (string.IsNullOrWhiteSpace(_message) || _duration <= 0f)
            return;

        CombatCalloutText callout = GetOrCreate();
        callout.gameObject.SetActive(true);
        callout.Initialize(
            _worldPosition,
            _message,
            _textColor,
            _outlineColor,
            Mathf.Max(0.05f, _scale),
            _duration,
            _riseHeight
        );
    }

    private static CombatCalloutText GetOrCreate()
    {
        while (Pool.Count > 0)
        {
            CombatCalloutText pooledCallout = Pool.Dequeue();

            if (pooledCallout != null)
                return pooledCallout;
        }

        GameObject calloutObject = new GameObject("Combat Callout Text", typeof(TextMeshPro), typeof(CombatCalloutText));
        return calloutObject.GetComponent<CombatCalloutText>();
    }

    private void Awake()
    {
        m_text = GetComponent<TextMeshPro>();
        m_text.font = TMP_Settings.defaultFontAsset;
        m_text.fontStyle = FontStyles.Bold;
        m_text.alignment = TextAlignmentOptions.Center;
        m_text.enableAutoSizing = false;
        m_text.richText = true;
        m_text.fontSize = 5f;
        m_text.rectTransform.sizeDelta = new Vector2(10f, 3f);
        m_text.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        MeshRenderer textRenderer = m_text.GetComponent<MeshRenderer>();
        textRenderer.shadowCastingMode = ShadowCastingMode.Off;
        textRenderer.receiveShadows = false;
        textRenderer.lightProbeUsage = LightProbeUsage.Off;
        textRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        textRenderer.sortingOrder = 251;
    }

    private void Update()
    {
        // Frozen with the arena while the player is on the tower camera.
        if (PveRuntime.IsPaused)
            return;

        if (!m_isRunning)
            return;

        m_elapsedTime += Time.unscaledDeltaTime;
        float normalizedTime = Mathf.Clamp01(m_elapsedTime / m_duration);

        float riseProgress = 1f - Mathf.Pow(1f - normalizedTime, 2.5f);
        transform.position = m_startPosition + Vector3.up * m_riseHeight * riseProgress;

        // A hard overshoot on the way in sells the impact, then the callout
        // settles and only starts fading once it has been readable for a moment.
        float popProgress = Mathf.Clamp01(normalizedTime / 0.22f);
        float pop = EaseOutBack(popProgress);

        if (normalizedTime > 0.4f)
            pop = Mathf.Lerp(pop, 0.86f, Mathf.InverseLerp(0.4f, 1f, normalizedTime));

        transform.localScale = Vector3.one * m_baseScale * pop;

        float fade = normalizedTime < 0.62f
            ? 1f
            : 1f - Mathf.InverseLerp(0.62f, 1f, normalizedTime);

        Color color = m_text.color;
        color.a = Mathf.Clamp01(fade);
        m_text.color = color;

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
        string _message,
        Color _textColor,
        Color _outlineColor,
        float _scale,
        float _duration,
        float _riseHeight)
    {
        ResolveCamera();

        m_startPosition = _worldPosition;
        transform.position = m_startPosition;
        transform.rotation = Quaternion.identity;

        m_elapsedTime = 0f;
        m_duration = Mathf.Max(0.05f, _duration);
        m_baseScale = _scale;
        m_riseHeight = Mathf.Max(0f, _riseHeight);
        m_isRunning = true;

        m_text.text = _message;
        m_text.color = _textColor;
        m_text.outlineColor = _outlineColor;
        m_text.outlineWidth = 0.28f;

        transform.localScale = Vector3.one * m_baseScale * 0.4f;
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

    private static float EaseOutBack(float _value)
    {
        const float overshoot = 2.1f;
        float shiftedValue = _value - 1f;
        return 1f + (overshoot + 1f) * shiftedValue * shiftedValue * shiftedValue
                  + overshoot * shiftedValue * shiftedValue;
    }
}
