using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Readable warning for an incoming enemy attack. A ring on the ground marks the
/// reach, a second ring closes in to show how long is left, and a warning sign
/// above the enemy makes the threat obvious even when the ring is off-screen.
/// Driven entirely by <see cref="EnemyAttack"/>, which owns the actual timing.
/// </summary>
[DisallowMultipleComponent]
public class EnemyAttackTelegraph : MonoBehaviour
{
    [Header("Ground Rings")]
    [SerializeField] private Color m_rangeRingColor = new(1f, 0.68f, 0.16f, 0.45f);
    [SerializeField] private Color m_closingRingStartColor = new(1f, 0.74f, 0.22f, 0.8f);
    [SerializeField] private Color m_closingRingEndColor = new(1f, 0.2f, 0.1f, 1f);
    [SerializeField, Min(0f), Tooltip("Height above the enemy's feet, so the rings do not clip into the ground.")]
    private float m_groundClearance = 0.05f;
    [SerializeField, Min(0f)] private float m_minimumClosingDiameter = 0.35f;

    [Header("Warning Sign")]
    [SerializeField] private string m_warningMessage = "!";
    [SerializeField] private Color m_warningStartColor = new(1f, 0.78f, 0.25f, 1f);
    [SerializeField] private Color m_warningEndColor = new(1f, 0.26f, 0.14f, 1f);
    [SerializeField] private Color m_warningOutlineColor = new(0.24f, 0.04f, 0f, 1f);
    [SerializeField, Min(0.05f)] private float m_warningScale = 0.65f;
    [SerializeField, Min(0f)] private float m_warningHeightOffset = 0.32f;

    [Header("Stagger Sign")]
    [SerializeField] private string m_staggerMessage = "?";
    [SerializeField] private Color m_staggerColor = new(1f, 0.93f, 0.6f, 1f);
    [SerializeField] private Color m_staggerOutlineColor = new(0.18f, 0.1f, 0f, 1f);
    [SerializeField, Min(0.05f), Tooltip("Sized to match the damage numbers, a single glyph needs the room to read at a glance.")]
    private float m_staggerScale = 0.75f;
    [SerializeField, Min(0f)] private float m_staggerHeightOffset = 0.45f;

    [Header("Strike Flash")]
    [SerializeField] private Color m_strikeFlashColor = new(1f, 0.32f, 0.16f, 1f);
    [SerializeField, Min(0.05f)] private float m_strikeFlashStartDiameter = 0.4f;
    [SerializeField, Min(0.05f)] private float m_strikeFlashEndDiameter = 1.9f;
    [SerializeField, Min(0.02f)] private float m_strikeFlashDuration = 0.26f;

    private Transform m_groundRoot;
    private SpriteRenderer m_rangeRing;
    private SpriteRenderer m_closingRing;
    private TextMeshPro m_warningText;
    private TextMeshPro m_staggerText;
    private Transform m_cameraTransform;
    private float m_feetOffset;
    private float m_headOffset;
    private float m_ringDiameter;
    private float m_windupProgress;
    private float m_staggerStartedAt;
    private float m_staggerEndsAt;
    private bool m_isVisible;

    private bool IsStaggerSignVisible => Time.time < m_staggerEndsAt;

    private void Awake()
    {
        CombatFeedbackSprites.Prewarm();
        MeasureBounds();
        BuildGroundRings();
        BuildWarningSign();
        BuildStaggerSign();
        SetVisible(false);
        HideStagger();
    }

    private void OnDisable()
    {
        SetVisible(false);
        HideStagger();
    }

    /// <summary>
    /// Shows the dazed sign above this enemy for as long as the stagger lasts.
    /// Held in place on purpose: a sign that floats away reads as "something
    /// happened", while the player needs to see "this one is out right now".
    /// </summary>
    public void ShowStagger(float _duration)
    {
        if (_duration <= 0f || m_staggerText == null)
            return;

        m_staggerStartedAt = Time.time;
        m_staggerEndsAt = Mathf.Max(m_staggerEndsAt, Time.time + _duration);
        m_staggerText.gameObject.SetActive(true);
        UpdateStaggerSign();
    }

    public void HideStagger()
    {
        m_staggerEndsAt = 0f;

        if (m_staggerText != null)
            m_staggerText.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        UpdateStaggerVisibility();

        if (!m_isVisible)
            return;

        // Held in world space so a rotated spawn point can never tip the rings
        // out of the ground plane.
        m_groundRoot.rotation = Quaternion.Euler(-90f, 0f, 0f);

        if (m_cameraTransform == null)
            ResolveCamera();

        if (m_cameraTransform == null)
            return;

        Transform warningTransform = m_warningText.transform;
        warningTransform.LookAt(
            warningTransform.position + m_cameraTransform.rotation * -Vector3.forward,
            m_cameraTransform.rotation * Vector3.up
        );
    }

    /// <summary>Height of this enemy's head above its pivot, measured once at startup.</summary>
    public float HeadOffset => m_headOffset;

    /// <summary>
    /// True when the renderer belongs to this telegraph instead of the enemy
    /// body, so the windup tint can leave it alone.
    /// </summary>
    public bool OwnsRenderer(Renderer _renderer) =>
        _renderer != null && m_groundRoot != null && _renderer.transform.IsChildOf(m_groundRoot);

    public void BeginWindup(float _attackRange)
    {
        m_ringDiameter = Mathf.Max(0.1f, _attackRange * 2f);
        m_windupProgress = 0f;
        SetVisible(true);
        UpdateWindup(0f);
    }

    public void UpdateWindup(float _normalizedProgress)
    {
        if (!m_isVisible)
            return;

        m_windupProgress = Mathf.Clamp01(_normalizedProgress);

        m_rangeRing.transform.localScale = Vector3.one * m_ringDiameter;

        Color rangeColor = m_rangeRingColor;
        rangeColor.a = m_rangeRingColor.a * Mathf.Lerp(0.55f, 1f, m_windupProgress);
        m_rangeRing.color = rangeColor;

        // The closing ring is the actual clock: when it reaches the centre, the
        // hit lands. That is far easier to read than a colour ramp alone.
        float closingDiameter = Mathf.Lerp(m_ringDiameter, m_minimumClosingDiameter, m_windupProgress);
        m_closingRing.transform.localScale = Vector3.one * closingDiameter;
        m_closingRing.color = Color.Lerp(m_closingRingStartColor, m_closingRingEndColor, m_windupProgress);

        float warningPop = 1f + Mathf.Sin(m_windupProgress * Mathf.PI) * 0.22f;
        m_warningText.transform.localScale = Vector3.one * m_warningScale * warningPop;
        m_warningText.color = Color.Lerp(m_warningStartColor, m_warningEndColor, m_windupProgress);
    }

    public void CancelWindup()
    {
        SetVisible(false);
    }

    public void NotifyStrike()
    {
        SetVisible(false);

        CombatRingFlash.Show(
            transform.position + Vector3.up * (m_headOffset * 0.55f),
            m_strikeFlashColor,
            m_strikeFlashStartDiameter,
            m_strikeFlashEndDiameter,
            m_strikeFlashDuration
        );
    }

    private void MeasureBounds()
    {
        float lowestPoint = transform.position.y;
        float highestPoint = transform.position.y + 1f;

        Collider enemyCollider = GetComponent<Collider>();

        if (enemyCollider != null)
        {
            lowestPoint = Mathf.Min(lowestPoint, enemyCollider.bounds.min.y);
            highestPoint = Mathf.Max(highestPoint, enemyCollider.bounds.max.y);
        }

        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer != null)
                highestPoint = Mathf.Max(highestPoint, spriteRenderer.bounds.max.y);
        }

        m_feetOffset = lowestPoint - transform.position.y;
        m_headOffset = highestPoint - transform.position.y;
    }

    private void BuildGroundRings()
    {
        GameObject groundObject = new GameObject("Attack Telegraph Ground");
        groundObject.transform.SetParent(transform, false);
        groundObject.transform.localPosition = Vector3.up * (m_feetOffset + m_groundClearance);
        m_groundRoot = groundObject.transform;

        // Negative order keeps the rings behind every character sprite, so the
        // enemy visibly stands on the telegraph instead of being painted over.
        m_rangeRing = CreateRingRenderer("Range Ring", -20);
        m_closingRing = CreateRingRenderer("Closing Ring", -19);
    }

    private SpriteRenderer CreateRingRenderer(string _name, int _sortingOrder)
    {
        GameObject ringObject = new GameObject(_name);
        ringObject.transform.SetParent(m_groundRoot, false);

        SpriteRenderer ringRenderer = ringObject.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = CombatFeedbackSprites.Ring;
        ringRenderer.sortingOrder = _sortingOrder;
        ringRenderer.shadowCastingMode = ShadowCastingMode.Off;
        ringRenderer.receiveShadows = false;
        ringRenderer.lightProbeUsage = LightProbeUsage.Off;
        ringRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return ringRenderer;
    }

    private void BuildWarningSign()
    {
        GameObject warningObject = new GameObject("Attack Warning", typeof(TextMeshPro));
        warningObject.transform.SetParent(transform, false);
        warningObject.transform.localPosition = Vector3.up * (m_headOffset + m_warningHeightOffset);

        m_warningText = warningObject.GetComponent<TextMeshPro>();
        m_warningText.font = TMP_Settings.defaultFontAsset;
        m_warningText.text = m_warningMessage;
        m_warningText.fontStyle = FontStyles.Bold;
        m_warningText.alignment = TextAlignmentOptions.Center;
        m_warningText.enableAutoSizing = false;
        m_warningText.fontSize = 6f;
        m_warningText.color = m_warningStartColor;
        m_warningText.outlineColor = m_warningOutlineColor;
        m_warningText.outlineWidth = 0.3f;
        m_warningText.rectTransform.sizeDelta = new Vector2(6f, 3f);
        m_warningText.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        MeshRenderer warningRenderer = warningObject.GetComponent<MeshRenderer>();
        warningRenderer.shadowCastingMode = ShadowCastingMode.Off;
        warningRenderer.receiveShadows = false;
        warningRenderer.lightProbeUsage = LightProbeUsage.Off;
        warningRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        warningRenderer.sortingOrder = 252;
    }

    private void BuildStaggerSign()
    {
        GameObject staggerObject = new GameObject("Stagger Sign", typeof(TextMeshPro));
        staggerObject.transform.SetParent(transform, false);
        staggerObject.transform.localPosition = Vector3.up * (m_headOffset + m_staggerHeightOffset);

        m_staggerText = staggerObject.GetComponent<TextMeshPro>();
        m_staggerText.font = TMP_Settings.defaultFontAsset;
        m_staggerText.text = m_staggerMessage;
        m_staggerText.fontStyle = FontStyles.Bold;
        m_staggerText.alignment = TextAlignmentOptions.Center;
        m_staggerText.enableAutoSizing = false;
        m_staggerText.fontSize = 6f;
        m_staggerText.color = m_staggerColor;
        m_staggerText.outlineColor = m_staggerOutlineColor;

        // A single glyph has far less ink than a damage number, so it leans
        // harder on the outline to stay readable against a bright arena.
        m_staggerText.outlineWidth = 0.36f;
        m_staggerText.rectTransform.sizeDelta = new Vector2(6f, 4f);
        m_staggerText.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        MeshRenderer staggerRenderer = staggerObject.GetComponent<MeshRenderer>();
        staggerRenderer.shadowCastingMode = ShadowCastingMode.Off;
        staggerRenderer.receiveShadows = false;
        staggerRenderer.lightProbeUsage = LightProbeUsage.Off;
        staggerRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        staggerRenderer.sortingOrder = 253;
    }

    private void UpdateStaggerVisibility()
    {
        if (m_staggerText == null)
            return;

        if (!IsStaggerSignVisible)
        {
            if (m_staggerText.gameObject.activeSelf)
                HideStagger();

            return;
        }

        UpdateStaggerSign();

        if (m_cameraTransform == null)
            ResolveCamera();

        if (m_cameraTransform == null)
            return;

        Transform staggerTransform = m_staggerText.transform;
        staggerTransform.LookAt(
            staggerTransform.position + m_cameraTransform.rotation * -Vector3.forward,
            m_cameraTransform.rotation * Vector3.up
        );

        // A lazy tilt back and forth sells "dazed" better than a rigid symbol.
        float wobble = Mathf.Sin((Time.time - m_staggerStartedAt) * 8f) * 11f;
        staggerTransform.rotation *= Quaternion.Euler(0f, 0f, wobble);
    }

    private void UpdateStaggerSign()
    {
        float elapsed = Time.time - m_staggerStartedAt;
        float pop = EaseOutBack(Mathf.Clamp01(elapsed / 0.16f));
        float bob = Mathf.Sin(elapsed * 6.5f) * 0.1f;

        m_staggerText.transform.localPosition = Vector3.up * (m_headOffset + m_staggerHeightOffset + bob);
        m_staggerText.transform.localScale = Vector3.one * (m_staggerScale * pop);
    }

    private static float EaseOutBack(float _value)
    {
        const float overshoot = 2.2f;
        float shiftedValue = _value - 1f;
        return 1f + (overshoot + 1f) * shiftedValue * shiftedValue * shiftedValue
                  + overshoot * shiftedValue * shiftedValue;
    }

    private void SetVisible(bool _visible)
    {
        m_isVisible = _visible;

        if (m_groundRoot != null)
            m_groundRoot.gameObject.SetActive(_visible);

        if (m_warningText != null)
            m_warningText.gameObject.SetActive(_visible);
    }

    private void ResolveCamera()
    {
        if (CameraReferences.Instance != null)
            m_cameraTransform = CameraReferences.Instance.PlayerCameraTransform;

        if (m_cameraTransform == null && Camera.main != null)
            m_cameraTransform = Camera.main.transform;
    }

    private void OnValidate()
    {
        m_strikeFlashEndDiameter = Mathf.Max(m_strikeFlashStartDiameter, m_strikeFlashEndDiameter);
        m_strikeFlashDuration = Mathf.Max(0.02f, m_strikeFlashDuration);

        if (Application.isPlaying && m_warningText != null)
            m_warningText.text = m_warningMessage;
    }
}
