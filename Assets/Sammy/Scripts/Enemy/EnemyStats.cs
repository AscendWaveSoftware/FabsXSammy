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

    private int m_currentHealth;
    private bool m_isDead;

    public int AttackDamage => attackDamage;
    public int CurrentHealth => m_currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => m_isDead;

    public event Action<int, int> OnHealthChanged;

    private void Awake()
    {
        m_currentHealth = maxHealth;
    }

    private void Start()
    {
        OnHealthChanged?.Invoke(m_currentHealth, maxHealth);
    }

    public bool TakeDamage(int _damageAmount, PlayerResources _playerResources, bool _isCriticalHit = false)
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

        if (m_currentHealth <= 0)
            Die(_playerResources);

        return true;
    }

    private Vector3 GetDamageTextPosition()
    {
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

        return new Vector3(transform.position.x, highestPoint + damageTextHeightOffset, transform.position.z);
    }

    private void OnValidate()
    {
        damageTextScale = Mathf.Max(0.05f, damageTextScale);
        criticalDamageTextScale = Mathf.Max(0.05f, criticalDamageTextScale);
        damageTextHeightOffset = Mathf.Max(0f, damageTextHeightOffset);
    }

    private void Die(PlayerResources _playerResources)
    {
        if (m_isDead) return;

        m_isDead = true;

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
