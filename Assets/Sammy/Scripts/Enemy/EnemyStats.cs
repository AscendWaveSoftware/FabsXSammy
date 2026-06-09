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

    public void TakeDamage(int _damageAmount, PlayerResources _playerResources)
    {
        if (m_isDead) return;
        if (_damageAmount <= 0) return;

        m_currentHealth -= _damageAmount;
        m_currentHealth = Mathf.Clamp(m_currentHealth, 0, maxHealth);

        OnHealthChanged?.Invoke(m_currentHealth, maxHealth);

        if (m_currentHealth <= 0)
            Die(_playerResources);
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
