using UnityEngine;

public class EnemyStats : MonoBehaviour
{
    [Header("Enemy Stats")]
    [SerializeField] private int maxHealth = 50;
    [SerializeField] private int attackDamage = 10;

    [Header("Reward")]
    [SerializeField] private int goldReward = 10;
    [SerializeField] private int scrapReward = 5;

    private int m_currentHealth;
    private bool m_isDead;

    public int AttackDamage => attackDamage;
    public int CurrentHealth => m_currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => m_isDead;

    private void Awake()
    {
        m_currentHealth = maxHealth;
    }

    public void TakeDamage(int _damageAmount, PlayerResources _playerResources)
    {
        if (m_isDead) return;

        if (_damageAmount <= 0) return;

        m_currentHealth -= _damageAmount;

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

        if (goldReward > 0)
            _playerResources.AddGold(goldReward, Resources.GOLD);

        if (scrapReward > 0)
            _playerResources.AddScrap(scrapReward, Resources.SCRAP);
    }
}
