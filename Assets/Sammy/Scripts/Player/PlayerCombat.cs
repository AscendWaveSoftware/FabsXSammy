using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
    private const int CombatImpulseChannel = 1;

    [Header("Combat Settings")]
    [SerializeField] private int m_attackDamage = 25;
    [SerializeField] private float m_attackRange = 1.5f;
    [SerializeField, Range(1f, 4f)] private float m_criticalDamageMultiplier = 2f;
    //[SerializeField] private float attackCooldown = 0.4f;

    [Header("Hit Detection")]
    [SerializeField] private LayerMask m_enemyLayer;
    [SerializeField] private Transform m_attackPoint;

    [Header("References")]
    [SerializeField] private PlayerResources m_playerResources;
    [SerializeField] private PlayerHealth m_playerHealth;

    [Header("Hit Camera Shake")]
    [SerializeField, Min(0f)] private float m_shakeStrength = 0.16f;
    [SerializeField, Min(0.01f)] private float m_shakeDuration = 0.12f;
    [SerializeField, Min(0f)] private float m_minimumShakeInterval = 0.04f;
    [SerializeField, Min(0f)] private float m_shakeListenerGain = 1f;

    private readonly HashSet<EnemyStats> m_damagedEnemies = new();
    private float m_nextAttackTime;
    private float m_nextAllowedShakeTime;
    private float m_criticalChance;
    private int m_healthOnHit;
    private CinemachineImpulseSource m_impulseSource;
    private bool m_hasShakeListener;

    private void Awake()
    {
        if (m_playerHealth == null)
            m_playerHealth = GetComponent<PlayerHealth>();

        m_impulseSource = GetComponent<CinemachineImpulseSource>();

        if (m_impulseSource == null)
            m_impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();

        ConfigureImpulseSource();
        EnsurePlayerCameraShakeListener();
    }

    public void OnAttack(InputValue _value)
    {
        if (!_value.isPressed || Time.timeScale <= 0f)
            return;

        TryAttack();
    }

    public void AddDamage(int _amount)
    {
        if (_amount <= 0) return;

        m_attackDamage += _amount;
    }

    public void AddAttackRange(float _amount)
    {
        if (_amount <= 0f)
            return;

        m_attackRange += _amount;
    }

    public void AddCriticalChance(float _percentage)
    {
        if (_percentage <= 0f)
            return;

        m_criticalChance = Mathf.Clamp01(m_criticalChance + _percentage);
    }

    public void AddHealthOnHit(int _amount)
    {
        if (_amount <= 0)
            return;

        m_healthOnHit += _amount;
    }

    private void TryAttack()
    {
        if(m_attackPoint == null)
        {
            Debug.LogWarning("No attack point assigned to PlayerCombat.");
            return;
        }

        Collider[] hitEnemies = Physics.OverlapSphere(
                m_attackPoint.position,
                m_attackRange,
                m_enemyLayer
        );

        if(hitEnemies.Length == 0){
            Debug.Log("Attack missed.");
            return;
        }

        m_damagedEnemies.Clear();
        Vector3 combinedHitPosition = Vector3.zero;
        int confirmedHitCount = 0;
        bool isCriticalHit = Random.value < m_criticalChance;
        int attackDamage = isCriticalHit
            ? Mathf.Max(1, Mathf.RoundToInt(m_attackDamage * m_criticalDamageMultiplier))
            : m_attackDamage;

        foreach (Collider enemyCollider in hitEnemies)
        {
            EnemyStats enemyStats = enemyCollider.GetComponentInParent<EnemyStats>();

            if (enemyStats == null || !m_damagedEnemies.Add(enemyStats))
                continue;

            if (!enemyStats.TakeDamage(attackDamage, m_playerResources, isCriticalHit))
                continue;

            combinedHitPosition += enemyCollider.ClosestPoint(m_attackPoint.position);
            confirmedHitCount++;
        }

        if (confirmedHitCount > 0)
        {
            if (m_healthOnHit > 0 && m_playerHealth != null)
                m_playerHealth.Heal(m_healthOnHit * confirmedHitCount);

            PlayHitShake(combinedHitPosition / confirmedHitCount, isCriticalHit ? 1.45f : 1f);
        }
    }

    private void PlayHitShake(Vector3 _hitPosition, float _strengthMultiplier)
    {
        if (m_shakeStrength <= 0f || Time.unscaledTime < m_nextAllowedShakeTime)
            return;

        if (!m_hasShakeListener)
            EnsurePlayerCameraShakeListener();

        if (!m_hasShakeListener || m_impulseSource == null)
            return;

        m_nextAllowedShakeTime = Time.unscaledTime + m_minimumShakeInterval;

        Vector2 randomDirection = Random.insideUnitCircle;

        if (randomDirection.sqrMagnitude < 0.001f)
            randomDirection = Vector2.up;

        randomDirection.Normalize();
        Vector3 cameraSpaceVelocity = new Vector3(randomDirection.x, randomDirection.y, 0f) * m_shakeStrength * _strengthMultiplier;
        m_impulseSource.GenerateImpulseAtPositionWithVelocity(_hitPosition, cameraSpaceVelocity);
    }

    private void ConfigureImpulseSource()
    {
        CinemachineImpulseDefinition definition = m_impulseSource.ImpulseDefinition;
        definition.ImpulseChannel = CombatImpulseChannel;
        definition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
        definition.ImpulseDuration = m_shakeDuration;
        definition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
        definition.DissipationDistance = 100f;
        definition.DissipationRate = 0.25f;
        definition.PropagationSpeed = 343f;
    }

    private void EnsurePlayerCameraShakeListener()
    {
        CinemachineCamera[] cameras = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include);
        CinemachineCamera fallbackCamera = null;

        foreach (CinemachineCamera camera in cameras)
        {
            if (camera == null)
                continue;

            Transform trackingTarget = camera.Target.TrackingTarget;
            bool tracksThisPlayer = trackingTarget != null && trackingTarget.root == transform.root;
            bool looksLikePlayerCamera = camera.gameObject.activeInHierarchy &&
                                         camera.CompareTag("PlayerCamera");

            if (looksLikePlayerCamera)
                fallbackCamera = camera;

            if (!tracksThisPlayer)
                continue;

            ConfigureShakeListener(camera);
            m_hasShakeListener = true;
            return;
        }

        if (fallbackCamera != null)
        {
            ConfigureShakeListener(fallbackCamera);
            m_hasShakeListener = true;
        }
    }

    private void ConfigureShakeListener(CinemachineCamera _camera)
    {
        CinemachineImpulseListener listener = _camera.GetComponent<CinemachineImpulseListener>();

        if (listener == null)
            listener = _camera.gameObject.AddComponent<CinemachineImpulseListener>();

        listener.ApplyAfter = CinemachineCore.Stage.Noise;
        listener.ChannelMask = CombatImpulseChannel;
        listener.Gain = m_shakeListenerGain;
        listener.Use2DDistance = false;
        listener.UseCameraSpace = true;
        listener.SignalCombinationMode = CinemachineImpulseListener.SignalCombinationModes.UseLargest;
    }

    private void OnDrawGizmosSelected()
    {
        if (m_attackPoint == null)
            return;

        Gizmos.DrawWireSphere(m_attackPoint.position, m_attackRange);
    }

    private void OnValidate()
    {
        m_shakeStrength = Mathf.Max(0f, m_shakeStrength);
        m_shakeDuration = Mathf.Max(0.01f, m_shakeDuration);
        m_minimumShakeInterval = Mathf.Max(0f, m_minimumShakeInterval);
        m_shakeListenerGain = Mathf.Max(0f, m_shakeListenerGain);
        m_criticalDamageMultiplier = Mathf.Max(1f, m_criticalDamageMultiplier);

        if (Application.isPlaying && m_impulseSource != null)
            ConfigureImpulseSource();
    }
}
