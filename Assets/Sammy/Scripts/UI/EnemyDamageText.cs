using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(TextMeshPro))]
public class EnemyDamageText : MonoBehaviour
{
    private static readonly Queue<EnemyDamageText> Pool = new Queue<EnemyDamageText>();
    private static readonly Color NormalColor = new Color(1f, 0.92f, 0.82f, 1f);
    private static readonly Color CriticalTopColor = new Color(1f, 1f, 0.76f, 1f);
    private static readonly Color CriticalBottomColor = new Color(1f, 0.3f, 0.08f, 1f);

    private TextMeshPro m_text;
    private TextMeshPro m_criticalEcho;
    private Transform m_cameraTransform;
    private Vector3 m_startPosition;
    private float m_elapsedTime;
    private float m_duration;
    private float m_baseScale;
    private float m_horizontalDrift;
    private float m_startingTilt;
    private bool m_isCritical;
    private bool m_isRunning;

    public static void Show(
        Vector3 _worldPosition,
        int _damageAmount,
        bool _isCritical,
        float _normalScale,
        float _criticalScale)
    {
        if (_damageAmount <= 0)
            return;

        EnemyDamageText damageText = GetOrCreate();
        damageText.gameObject.SetActive(true);
        damageText.Initialize(
            _worldPosition,
            _damageAmount,
            _isCritical,
            Mathf.Max(0.05f, _normalScale),
            Mathf.Max(0.05f, _criticalScale)
        );
    }

    private static EnemyDamageText GetOrCreate()
    {
        while (Pool.Count > 0)
        {
            EnemyDamageText pooledText = Pool.Dequeue();
            if (pooledText != null)
                return pooledText;
        }

        GameObject textObject = new GameObject("Enemy Damage Text", typeof(TextMeshPro), typeof(EnemyDamageText));
        return textObject.GetComponent<EnemyDamageText>();
    }

    private void Awake()
    {
        m_text = GetComponent<TextMeshPro>();
        ConfigureText(m_text, 250);
        CreateCriticalEcho();
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
        float riseProgress = 1f - Mathf.Pow(1f - normalizedTime, 2f);
        float fade = normalizedTime < 0.58f
            ? 1f
            : 1f - Mathf.InverseLerp(0.58f, 1f, normalizedTime);

        Vector3 cameraRight = m_cameraTransform != null ? m_cameraTransform.right : Vector3.right;
        float riseHeight = m_isCritical ? 1.05f : 0.72f;
        transform.position = m_startPosition
                             + Vector3.up * riseHeight * riseProgress
                             + cameraRight * m_horizontalDrift * normalizedTime;

        float popProgress = Mathf.Clamp01(normalizedTime / (m_isCritical ? 0.2f : 0.16f));
        float pop = EaseOutBack(popProgress);
        if (normalizedTime > 0.28f)
            pop = Mathf.Lerp(pop, 0.88f, Mathf.InverseLerp(0.28f, 1f, normalizedTime));

        transform.localScale = Vector3.one * m_baseScale * pop;
        SetTextAlpha(m_text, fade);
        UpdateCriticalEcho(normalizedTime);

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

        float wobble = m_isCritical
            ? Mathf.Sin(m_elapsedTime * 24f) * 4.5f * (1f - Mathf.Clamp01(m_elapsedTime / 0.55f))
            : 0f;
        float tilt = Mathf.Lerp(m_startingTilt, 0f, Mathf.Clamp01(m_elapsedTime / m_duration));

        transform.LookAt(
            transform.position + m_cameraTransform.rotation * -Vector3.forward,
            m_cameraTransform.rotation * Vector3.up
        );
        transform.rotation *= Quaternion.Euler(0f, 0f, tilt + wobble);
    }

    private void Initialize(
        Vector3 _worldPosition,
        int _damageAmount,
        bool _isCritical,
        float _normalScale,
        float _criticalScale)
    {
        ResolveCamera();

        Vector3 cameraRight = m_cameraTransform != null ? m_cameraTransform.right : Vector3.right;
        m_startPosition = _worldPosition + cameraRight * Random.Range(-0.16f, 0.16f);
        transform.position = m_startPosition;
        transform.rotation = Quaternion.identity;

        m_isCritical = _isCritical;
        m_elapsedTime = 0f;
        m_duration = _isCritical ? 1.05f : 0.78f;
        m_baseScale = _isCritical ? _criticalScale : _normalScale;
        m_horizontalDrift = Random.Range(-0.22f, 0.22f);
        m_startingTilt = Random.Range(-7f, 7f);
        m_isRunning = true;

        m_text.text = _isCritical
            ? $"<size=52%>CRITICAL</size>\n{_damageAmount}!"
            : _damageAmount.ToString();
        m_text.fontSize = _isCritical ? 5.2f : 4.4f;
        m_text.color = _isCritical ? Color.white : NormalColor;
        m_text.enableVertexGradient = _isCritical;
        m_text.colorGradient = _isCritical
            ? new VertexGradient(CriticalTopColor, CriticalTopColor, CriticalBottomColor, CriticalBottomColor)
            : new VertexGradient(NormalColor);
        m_text.outlineColor = _isCritical
            ? new Color32(92, 15, 4, 255)
            : new Color32(55, 20, 16, 255);
        m_text.outlineWidth = _isCritical ? 0.3f : 0.23f;

        m_criticalEcho.gameObject.SetActive(_isCritical);
        if (_isCritical)
        {
            m_criticalEcho.text = _damageAmount + "!";
            m_criticalEcho.fontSize = 5.4f;
            m_criticalEcho.transform.localScale = Vector3.one;
            m_criticalEcho.color = new Color(1f, 0.47f, 0.08f, 0.5f);
        }

        transform.localScale = Vector3.one * m_baseScale * 0.45f;
    }

    private void ConfigureText(TextMeshPro _text, int _sortingOrder)
    {
        _text.font = TMP_Settings.defaultFontAsset;
        _text.fontStyle = FontStyles.Bold;
        _text.alignment = TextAlignmentOptions.Center;
        _text.enableAutoSizing = false;
        _text.richText = true;
        _text.rectTransform.sizeDelta = new Vector2(6f, 3f);
        _text.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        MeshRenderer textRenderer = _text.GetComponent<MeshRenderer>();
        textRenderer.shadowCastingMode = ShadowCastingMode.Off;
        textRenderer.receiveShadows = false;
        textRenderer.lightProbeUsage = LightProbeUsage.Off;
        textRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        textRenderer.sortingOrder = _sortingOrder;
    }

    private void CreateCriticalEcho()
    {
        GameObject echoObject = new GameObject("Critical Echo", typeof(TextMeshPro));
        echoObject.transform.SetParent(transform, false);
        echoObject.transform.localPosition = new Vector3(0f, -0.32f, 0.015f);

        m_criticalEcho = echoObject.GetComponent<TextMeshPro>();
        ConfigureText(m_criticalEcho, 249);
        m_criticalEcho.outlineColor = new Color32(115, 22, 2, 220);
        m_criticalEcho.outlineWidth = 0.34f;
        m_criticalEcho.gameObject.SetActive(false);
    }

    private void UpdateCriticalEcho(float _normalizedTime)
    {
        if (!m_isCritical || !m_criticalEcho.gameObject.activeSelf)
            return;

        float echoProgress = Mathf.Clamp01(_normalizedTime / 0.32f);
        m_criticalEcho.transform.localScale = Vector3.one * Mathf.Lerp(0.92f, 1.9f, echoProgress);
        SetTextAlpha(m_criticalEcho, (1f - echoProgress) * 0.48f);

        if (echoProgress >= 1f)
            m_criticalEcho.gameObject.SetActive(false);
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
        m_criticalEcho.gameObject.SetActive(false);
        gameObject.SetActive(false);
        Pool.Enqueue(this);
    }

    private static void SetTextAlpha(TMP_Text _text, float _alpha)
    {
        Color color = _text.color;
        color.a = Mathf.Clamp01(_alpha);
        _text.color = color;
    }

    private static float EaseOutBack(float _value)
    {
        const float overshoot = 1.7f;
        float shiftedValue = _value - 1f;
        return 1f + (overshoot + 1f) * shiftedValue * shiftedValue * shiftedValue
                  + overshoot * shiftedValue * shiftedValue;
    }
}
