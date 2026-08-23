using System;
using UnityEngine;

public class EnemyStats : MonoBehaviour
{
    [Header("Enemy Stats")]
    [SerializeField] private int maxHealth = 50;
    [SerializeField] private int attackDamage = 10;

    [Header("Reward")]
    [SerializeField] private int xpReward = 25;
    [SerializeField] private int scrapReward = 5;

    [Header("Damage Text")]
    [SerializeField, Min(0.05f), Tooltip("World-space size for regular damage numbers.")]
    private float damageTextScale = 0.3f;
    [SerializeField, Min(0.05f), Tooltip("World-space size for critical damage numbers.")]
    private float criticalDamageTextScale = 0.44f;
    [SerializeField, Min(0f), Tooltip("Additional height above the enemy sprite.")]
    private float damageTextHeightOffset = 0.25f;
    [SerializeField, Min(0f), Tooltip(
        "Height of the artwork above the root. Leave at 0 to measure the sprite bounds instead.")]
    private float damageTextHeadOverride;

    private int m_currentHealth;
    private bool m_isDead;
    private EnemyAttack m_enemyAttack;

    public int AttackDamage => attackDamage;
    public int CurrentHealth => m_currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => m_isDead;

    public event Action<int, int> OnHealthChanged;

    /// <summary>Raised on every hit that lands. Carries the damage and whether it killed.</summary>
    public event Action<int, bool> OnDamageTaken;

    /// <summary>Raised once, just before this enemy is removed from the arena.</summary>
    public event Action OnDied;

    private void Awake()
    {
        m_currentHealth = maxHealth;
        m_enemyAttack = GetComponent<EnemyAttack>();
    }

    private void Start()
    {
        OnHealthChanged?.Invoke(m_currentHealth, maxHealth);
    }

    /// <summary>
    /// Toughens this enemy according to how far the run has progressed. Called by
    /// the spawner right after it creates the enemy.
    ///
    /// The arena used to scale only in how many enemies it sent and how fast, never
    /// in what they could take. A player stacking damage and crit therefore stayed
    /// able to delete a full health enemy in one swing for the entire run, which is
    /// what made every defensive upgrade and every spell look pointless.
    ///
    /// Only ever touches the instance. Values are serialised on the prefab, but
    /// Instantiate hands out a clone, so the asset itself is never written to.
    /// </summary>
    public void ApplyDifficultyScaling(float _healthMultiplier, float _damageMultiplier)
    {
        maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealth * Mathf.Max(0.1f, _healthMultiplier)));
        attackDamage = Mathf.Max(1, Mathf.RoundToInt(attackDamage * Mathf.Max(0.1f, _damageMultiplier)));

        // Refilled rather than left where Awake put it, or a scaled enemy would
        // walk in already missing the health it just gained.
        m_currentHealth = maxHealth;
        OnHealthChanged?.Invoke(m_currentHealth, maxHealth);
    }

    /// <param name="_interruptsEnemy">
    /// False for damage over time. A poison tick every half second would otherwise
    /// reset the hit stun forever and lock the enemy out of attacking entirely.
    /// </param>
    public bool TakeDamage(
        int _damageAmount,
        PlayerResources _playerResources,
        bool _isCriticalHit = false,
        bool _interruptsEnemy = true)
    {
        if (m_isDead) return false;
        if (_damageAmount <= 0) return false;

        int appliedDamage = Mathf.Min(_damageAmount, m_currentHealth);
        m_currentHealth -= _damageAmount;
        m_currentHealth = Mathf.Clamp(m_currentHealth, 0, maxHealth);

        OnHealthChanged?.Invoke(m_currentHealth, maxHealth);
        EnemyDamageText.Show(
            GetDamageTextPosition(),
            appliedDamage,
            _isCriticalHit,
            damageTextScale,
            criticalDamageTextScale
        );

        bool isLethal = m_currentHealth <= 0;

        // Raised before Die, so a listener still has a live enemy to read from.
        OnDamageTaken?.Invoke(appliedDamage, isLethal);

        if (isLethal)
            Die(_playerResources);
        else if (_interruptsEnemy)
            m_enemyAttack?.NotifyHit();

        return true;
    }

    private Vector3 GetDamageTextPosition()
    {
        return new Vector3(
            transform.position.x,
            transform.position.y + GetHeadOffset() + damageTextHeightOffset,
            transform.position.z
        );
    }

    /// <summary>How far the top of the enemy sits above its root.</summary>
    private float GetHeadOffset()
    {
        // An animated sheet reports its whole frame as the sprite bounds, transparent
        // padding included, which would float the numbers metres above the monster.
        // The override is the measured height of the artwork itself.
        if (damageTextHeadOverride > 0f)
            return damageTextHeadOverride;

        float highestPoint = transform.position.y + 0.8f;

        Collider enemyCollider = GetComponent<Collider>();
        if (enemyCollider != null)
            highestPoint = Mathf.Max(highestPoint, enemyCollider.bounds.max.y);

        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer != null && spriteRenderer.enabled)
                highestPoint = Mathf.Max(highestPoint, spriteRenderer.bounds.max.y);
        }

        return highestPoint - transform.position.y;
    }

    private void OnValidate()
    {
        damageTextScale = Mathf.Max(0.05f, damageTextScale);
        criticalDamageTextScale = Mathf.Max(0.05f, criticalDamageTextScale);
        damageTextHeightOffset = Mathf.Max(0f, damageTextHeightOffset);
        damageTextHeadOverride = Mathf.Max(0f, damageTextHeadOverride);
    }

    private void Die(PlayerResources _playerResources)
    {
        if (m_isDead) return;

        m_isDead = true;

        OnDied?.Invoke();
        GiveReward(_playerResources);

        Destroy(gameObject);
    }

    private void GiveReward(PlayerResources _playerResources)
    {
        if(_playerResources == null)
        {
            Debug.Log("Player died, but no PlayerResources reference was provided.");
            return;
        }

        PlayerExperience playerExperience = _playerResources.GetComponent<PlayerExperience>();

        if (playerExperience != null && xpReward > 0)
            playerExperience.AddXP(xpReward);

        if (scrapReward > 0)
            _playerResources.AddScrap(scrapReward, Resources.SCRAP);
    }
}
