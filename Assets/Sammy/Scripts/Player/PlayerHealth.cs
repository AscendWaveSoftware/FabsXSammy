using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour
{
    [Header("Health Stats")]
    [SerializeField] private int m_maxHealth = 100;

    [Header("Respawn")]
    [SerializeField] private Transform m_respawnPoint;

    [Header("References")]
    [SerializeField] private PlayerResources m_playerResources;

    public int CurrentHealth => m_currentHealth;
    public int MaxHealth => m_maxHealth;
    public bool IsAlive { get; private set; }

    public event Action<int, int> OnHealthChanged;
    public event Action<int, bool> OnDamageTaken;
    public event Action<int> OnDamageBlocked;

    private int m_currentHealth;
    private Rigidbody m_rb;
    private PlayerCombat m_playerCombat;
    private float m_damageReduction;
    private float m_healthRegenerationPerSecond;
    private float m_regenerationAccumulator;

    private void Awake()
    {
        m_rb = GetComponent<Rigidbody>();
        m_playerCombat = GetComponent<PlayerCombat>();

        if(m_playerResources == null)
            m_playerResources = GetComponent<PlayerResources>();

        m_currentHealth = m_maxHealth;
        IsAlive = true;
    }

    private void Start()
    {
        OnHealthChanged?.Invoke(m_currentHealth, m_maxHealth);
    }

    private void Update()
    {
        if (!IsAlive || m_healthRegenerationPerSecond <= 0f || m_currentHealth >= m_maxHealth)
            return;

        m_regenerationAccumulator += m_healthRegenerationPerSecond * Time.deltaTime;
        int healthToRestore = Mathf.FloorToInt(m_regenerationAccumulator);

        if (healthToRestore <= 0)
            return;

        m_regenerationAccumulator -= healthToRestore;
        Heal(healthToRestore);
    }

    public void TakeDamage(int _damageAmount)
    {
        if (!IsAlive) return;
        if (_damageAmount <= 0) return;

        if (m_playerCombat != null && m_playerCombat.IsBlocking)
        {
            OnDamageBlocked?.Invoke(_damageAmount);
            return;
        }

        int reducedDamage = Mathf.Max(1, Mathf.CeilToInt(_damageAmount * (1f - m_damageReduction)));
        int appliedDamage = Mathf.Min(reducedDamage, m_currentHealth);
        m_currentHealth -= appliedDamage;
        m_currentHealth = Mathf.Clamp(m_currentHealth, 0, m_maxHealth);

        OnHealthChanged?.Invoke(m_currentHealth, m_maxHealth);

        bool isLethal = m_currentHealth <= 0;
        OnDamageTaken?.Invoke(appliedDamage, isLethal);

        if (isLethal)
            Die();
    }

    public void Heal(int _healAmount)
    {
        if (!IsAlive) return;
        if (_healAmount <= 0) return;

        m_currentHealth += _healAmount;
        m_currentHealth = Mathf.Clamp(m_currentHealth, 0, m_maxHealth);

        OnHealthChanged?.Invoke(m_currentHealth, m_maxHealth);
    }

    public void AddMaxHealth(int _amount)
    {
        if (_amount <= 0) return;

        m_maxHealth += _amount;
        m_currentHealth += _amount;

        OnHealthChanged?.Invoke(m_currentHealth, m_maxHealth);
    }

    public void AddDamageReduction(float _percentage)
    {
        if (_percentage <= 0f)
            return;

        m_damageReduction = Mathf.Clamp(m_damageReduction + _percentage, 0f, 0.75f);
    }

    public void AddHealthRegeneration(float _healthPerSecond)
    {
        if (_healthPerSecond <= 0f)
            return;

        m_healthRegenerationPerSecond += _healthPerSecond;
    }

    private void Die()
    {
        if (!IsAlive) return;

        IsAlive = false;

        PlaytestAnalyticsManager analytics = PlaytestAnalyticsManager.Instance;
        if (analytics != null)
        {
            analytics.RegisterDeath();
            analytics.RegisterVictory();
            analytics.EndRun();
        }

        ResetPlayerResources();
        Respawn();
    }

    private void Respawn()
    {
        if (m_respawnPoint != null)
            transform.position = m_respawnPoint.position;
        else
            Debug.LogWarning("No respawn point assigned to PlayerHealth.");

        if(m_rb != null)
        {
            m_rb.linearVelocity = Vector3.zero;
            m_rb.angularVelocity = Vector3.zero;
        }

        m_currentHealth = m_maxHealth;
        m_regenerationAccumulator = 0f;
        IsAlive = true;

        OnHealthChanged?.Invoke(m_currentHealth, m_maxHealth);
    }

    private void ResetPlayerResources()
    {
        if(m_playerResources == null)
        {
            Debug.LogWarning("No PlayerResources reference assigned to PlayerHealth.");
            return;
        }

        m_playerResources.ResetResources();
    }

}
