using System;
using UnityEngine;

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

    private int m_currentHealth;
    private Rigidbody m_rb;

    private void Awake()
    {
        m_rb = GetComponent<Rigidbody>();

        if(m_playerResources == null)
            m_playerResources = GetComponent<PlayerResources>();

        m_currentHealth = m_maxHealth;
        IsAlive = true;
    }

    private void Start()
    {
        OnHealthChanged?.Invoke(m_currentHealth, m_maxHealth);
    }

    public void TakeDamage(int _damageAmount)
    {
        if (!IsAlive) return;
        if (_damageAmount <= 0) return;

        m_currentHealth -= _damageAmount;
        m_currentHealth = Mathf.Clamp(m_currentHealth, 0, m_maxHealth);

        OnHealthChanged?.Invoke(m_currentHealth, m_maxHealth);

        if (m_currentHealth <= 0)
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

    private void Die()
    {
        if (!IsAlive) return;

        IsAlive = false;

        PlaytestAnalyticsManager.Instance.RegisterDeath();
        PlaytestAnalyticsManager.Instance.RegisterVictory();
        PlaytestAnalyticsManager.Instance.EndRun();
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
