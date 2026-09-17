using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerGuard : MonoBehaviour
{
    [Header("Capacity")]
    [SerializeField, Min(1f)] private float m_maximumGuard = 100f;

    [Header("Cost")]
    [SerializeField, Min(0f), Tooltip("Guard spent by a blocked hit, before the damage share below.")]
    private float m_costPerBlockedHit = 16f;
    [SerializeField, Min(0f), Tooltip("Extra guard spent per point of damage the hit would have dealt. Heavier attackers wear the guard down faster.")]
    private float m_costPerDamagePoint = 1.7f;
    [SerializeField, Min(0f), Tooltip("Guard drained per second just for holding the block up, so pre-emptive turtling is not free.")]
    private float m_holdDrainPerSecond = 13f;

    [Header("Recovery")]
    [SerializeField, Min(0f), Tooltip("Seconds after the block drops before the guard starts refilling.")]
    private float m_regenerationDelay = 1f;
    [SerializeField, Min(0f)] private float m_regenerationPerSecond = 36f;
    [SerializeField, Min(0f), Tooltip("Seconds the guard stays unusable after being broken outright. This is the punishment for over-blocking.")]
    private float m_breakRecovery = 1.6f;

    private float m_currentGuard;
    private float m_regenerationAllowedAt;
    private float m_brokenUntil;

    public event Action<float, float> OnGuardChanged;

    public event Action OnGuardBroken;

    public float MaximumGuard => m_maximumGuard;
    public float CurrentGuard => m_currentGuard;
    public float Normalized => m_maximumGuard > 0f ? Mathf.Clamp01(m_currentGuard / m_maximumGuard) : 0f;

    public bool IsBroken => PveRuntime.Time < m_brokenUntil;

    public float BreakRecoveryRemaining => Mathf.Max(0f, m_brokenUntil - PveRuntime.Time);

    public bool CanBlock => !IsBroken && m_currentGuard > 0f;

    private void Awake()
    {
        m_currentGuard = m_maximumGuard;

        if (GetComponent<PlayerGuardBarUI>() == null)
            gameObject.AddComponent<PlayerGuardBarUI>();
    }

    private void Start()
    {
        OnGuardChanged?.Invoke(m_currentGuard, m_maximumGuard);
    }

    public bool SpendOnBlockedHit(int _incomingDamage)
    {
        float cost = m_costPerBlockedHit + Mathf.Max(0, _incomingDamage) * m_costPerDamagePoint;
        return Spend(cost);
    }

    public bool SpendOnHold(float _deltaTime)
    {
        if (m_holdDrainPerSecond <= 0f || _deltaTime <= 0f)
            return true;

        return Spend(m_holdDrainPerSecond * _deltaTime);
    }

    private bool Spend(float _amount)
    {
        if (_amount <= 0f)
            return true;

        m_currentGuard = Mathf.Max(0f, m_currentGuard - _amount);

        m_regenerationAllowedAt = PveRuntime.Time + m_regenerationDelay;
        OnGuardChanged?.Invoke(m_currentGuard, m_maximumGuard);

        if (m_currentGuard > 0f)
            return true;

        Break();
        return false;
    }

    private void Break()
    {
        m_brokenUntil = PveRuntime.Time + m_breakRecovery;

        m_regenerationAllowedAt = Mathf.Max(m_regenerationAllowedAt, m_brokenUntil);
        OnGuardBroken?.Invoke();
    }

    private void Update()
    {
        if (PveRuntime.IsPaused)
            return;

        if (m_currentGuard >= m_maximumGuard || PveRuntime.Time < m_regenerationAllowedAt)
            return;

        float previousGuard = m_currentGuard;
        m_currentGuard = Mathf.Min(m_maximumGuard, m_currentGuard + m_regenerationPerSecond * Time.deltaTime);

        if (!Mathf.Approximately(previousGuard, m_currentGuard))
            OnGuardChanged?.Invoke(m_currentGuard, m_maximumGuard);
    }

    private void OnValidate()
    {
        m_maximumGuard = Mathf.Max(1f, m_maximumGuard);

        if (!Application.isPlaying)
            m_currentGuard = m_maximumGuard;
    }
}
