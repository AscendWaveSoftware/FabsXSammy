using UnityEngine;

/// <summary>
/// Visual confirmation for a successful block. A block prevents all damage, so
/// without a clear reaction the player cannot tell it worked at all.
/// </summary>
[DisallowMultipleComponent]
public class PlayerBlockFeedback : MonoBehaviour
{
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineStrengthId = Shader.PropertyToID("_OutlineStrength");

    [Header("References")]
    [SerializeField] private PlayerHealth m_playerHealth;
    [SerializeField, Tooltip("Player sprite whose aura flares up on a block. Found in the children when empty.")]
    private SpriteRenderer m_playerSprite;

    [Header("Callout")]
    [SerializeField] private string m_blockMessage = "BLOCKED!";
    [SerializeField] private Color m_calloutColor = new(0.71f, 0.95f, 1f, 1f);
    [SerializeField] private Color m_calloutOutlineColor = new(0.03f, 0.16f, 0.34f, 1f);
    [SerializeField, Min(0.05f)] private float m_calloutScale = 0.32f;
    [SerializeField, Min(0f)] private float m_calloutHeightOffset = 0.3f;
    [SerializeField, Min(0.05f)] private float m_calloutDuration = 0.75f;

    [Header("Shield Flash")]
    [SerializeField] private Color m_flashColor = new(0.55f, 0.9f, 1f, 1f);
    [SerializeField, Min(0f), Tooltip("How far the flash sits in front of the player, towards the attacker.")]
    private float m_flashForwardOffset = 0.62f;
    [SerializeField, Min(0f)] private float m_flashHeightOffset = 0.55f;
    [SerializeField, Min(0.05f)] private float m_flashStartDiameter = 0.55f;
    [SerializeField, Min(0.05f)] private float m_flashEndDiameter = 2.5f;
    [SerializeField, Min(0.02f)] private float m_flashDuration = 0.34f;

    [Header("Aura Flare")]
    [SerializeField] private Color m_flareColor = new(0.5f, 0.92f, 1f, 1f);
    [SerializeField, Min(0f)] private float m_flareStrength = 4.5f;
    [SerializeField, Min(0.02f)] private float m_flareDuration = 0.28f;

    private MaterialPropertyBlock m_spriteProperties;
    private Color m_baseOutlineColor;
    private float m_baseOutlineStrength;
    private float m_flareRemaining;
    private bool m_supportsAuraFlare;
    private bool m_flareNeedsReset;

    private void Awake()
    {
        if (m_playerHealth == null)
            m_playerHealth = GetComponent<PlayerHealth>();

        if (m_playerSprite == null)
            m_playerSprite = GetComponentInChildren<SpriteRenderer>(true);

        CacheAuraFlareBaseValues();
    }

    private void OnEnable()
    {
        if (m_playerHealth != null)
            m_playerHealth.OnDamageBlocked += HandleDamageBlocked;
    }

    private void OnDisable()
    {
        if (m_playerHealth != null)
            m_playerHealth.OnDamageBlocked -= HandleDamageBlocked;

        m_flareRemaining = 0f;
        ApplyAuraFlare(0f);
    }

    private void Update()
    {
        if (m_flareRemaining <= 0f)
            return;

        // Unscaled, because the flare has to keep reading during the short hit
        // slow motion that the combat system applies.
        m_flareRemaining -= Time.unscaledDeltaTime;
        ApplyAuraFlare(Mathf.Clamp01(m_flareRemaining / m_flareDuration));
    }

    private void HandleDamageBlocked(int _blockedDamage, Vector3 _attackerPosition)
    {
        if (_blockedDamage <= 0)
            return;

        Vector3 playerPosition = transform.position;
        Vector3 towardsAttacker = _attackerPosition - playerPosition;
        towardsAttacker.y = 0f;

        // A hit without usable direction still deserves a flash, so fall back to
        // the player's own position instead of dropping the effect.
        Vector3 flashDirection = towardsAttacker.sqrMagnitude > 0.0001f
            ? towardsAttacker.normalized
            : Vector3.zero;

        CombatRingFlash.Show(
            playerPosition + flashDirection * m_flashForwardOffset + Vector3.up * m_flashHeightOffset,
            m_flashColor,
            m_flashStartDiameter,
            m_flashEndDiameter,
            m_flashDuration
        );

        CombatCalloutText.Show(
            GetCalloutPosition(),
            m_blockMessage,
            m_calloutColor,
            m_calloutOutlineColor,
            m_calloutScale,
            m_calloutDuration
        );

        m_flareRemaining = m_flareDuration;
        ApplyAuraFlare(1f);
    }

    private Vector3 GetCalloutPosition()
    {
        float highestPoint = transform.position.y + 1f;

        Collider playerCollider = GetComponent<Collider>();

        if (playerCollider != null)
            highestPoint = Mathf.Max(highestPoint, playerCollider.bounds.max.y);

        if (m_playerSprite != null && m_playerSprite.enabled)
            highestPoint = Mathf.Max(highestPoint, m_playerSprite.bounds.max.y);

        return new Vector3(
            transform.position.x,
            highestPoint + m_calloutHeightOffset,
            transform.position.z
        );
    }

    private void CacheAuraFlareBaseValues()
    {
        if (m_playerSprite == null)
            return;

        Material spriteMaterial = m_playerSprite.sharedMaterial;

        // The aura flare is a bonus on top of the ring and the callout. Materials
        // without the aura shader simply skip it instead of erroring.
        if (spriteMaterial == null ||
            !spriteMaterial.HasProperty(OutlineColorId) ||
            !spriteMaterial.HasProperty(OutlineStrengthId))
        {
            return;
        }

        m_baseOutlineColor = spriteMaterial.GetColor(OutlineColorId);
        m_baseOutlineStrength = spriteMaterial.GetFloat(OutlineStrengthId);
        m_spriteProperties = new MaterialPropertyBlock();
        m_supportsAuraFlare = true;
    }

    private void ApplyAuraFlare(float _intensity)
    {
        if (!m_supportsAuraFlare || m_playerSprite == null)
            return;

        if (_intensity <= 0f)
        {
            if (!m_flareNeedsReset)
                return;

            m_playerSprite.SetPropertyBlock(null);
            m_flareNeedsReset = false;
            return;
        }

        // Ease the tail so the flare snaps in and bleeds out smoothly.
        float eased = _intensity * _intensity;

        m_playerSprite.GetPropertyBlock(m_spriteProperties);
        m_spriteProperties.SetColor(OutlineColorId, Color.Lerp(m_baseOutlineColor, m_flareColor, eased));
        m_spriteProperties.SetFloat(OutlineStrengthId, Mathf.Lerp(m_baseOutlineStrength, m_flareStrength, eased));
        m_playerSprite.SetPropertyBlock(m_spriteProperties);
        m_flareNeedsReset = true;
    }

    private void OnValidate()
    {
        m_flashEndDiameter = Mathf.Max(m_flashStartDiameter, m_flashEndDiameter);
        m_flareDuration = Mathf.Max(0.02f, m_flareDuration);
        m_flashDuration = Mathf.Max(0.02f, m_flashDuration);
        m_calloutDuration = Mathf.Max(0.05f, m_calloutDuration);
    }
}
