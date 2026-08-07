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
    [SerializeField] private PlayerAnimationController m_playerAnimation;

    [Header("Hit Camera Shake")]
    [SerializeField, Min(0f)] private float m_shakeStrength = 0.16f;
    [SerializeField, Min(0.01f)] private float m_shakeDuration = 0.12f;
    [SerializeField, Min(0f)] private float m_minimumShakeInterval = 0.04f;
    [SerializeField, Min(0f)] private float m_shakeListenerGain = 1f;

    [Header("Hit Slow Motion")]
    [SerializeField, Range(0.1f, 1f)] private float m_hitTimeScale = 0.72f;
    [SerializeField, Min(0f)] private float m_hitSlowMotionDuration = 0.065f;
    [SerializeField, Range(0.1f, 1f)] private float m_criticalHitTimeScale = 0.6f;
    [SerializeField, Min(0f)] private float m_criticalHitSlowMotionDuration = 0.085f;

    [Header("Block")]
    [SerializeField, Min(0.1f), Tooltip("Maximum time a held block remains active before the guard needs to be reset.")]
    private float m_maxBlockDuration = 0.85f;
    [SerializeField, Min(0f), Tooltip("Short recovery after releasing or exhausting a block.")]
    private float m_blockCooldown = 0.3f;
    [SerializeField, Range(0f, 2f), Tooltip("A successful block should punch harder than a normal hit so it reads as a win.")]
    private float m_blockImpactShakeMultiplier = 0.9f;

    public bool IsBlocking => m_isBlocking;

    private readonly HashSet<EnemyStats> m_damagedEnemies = new();
    private float m_nextAllowedShakeTime;
    private uint m_lastProcessedAnimationSwing;
    private float m_criticalChance;
    private int m_healthOnHit;
    private CinemachineImpulseSource m_impulseSource;
    private bool m_hasShakeListener;
    private bool m_hasLoggedMissingAnimationController;
    private bool m_hitSlowMotionActive;
    private float m_hitSlowMotionEndsAt;
    private float m_timeScaleBeforeHitSlowMotion;
    private float m_fixedDeltaTimeBeforeHitSlowMotion;
    private float m_appliedHitTimeScale;
    private bool m_isBlocking;
    private bool m_blockRequiresRelease;
    private float m_blockEndsAt;
    private float m_nextBlockAllowedAt;

    private void Awake()
    {
        if (m_playerHealth == null)
            m_playerHealth = GetComponent<PlayerHealth>();

        if (m_playerAnimation == null)
            m_playerAnimation = GetComponent<PlayerAnimationController>();

        m_impulseSource = GetComponent<CinemachineImpulseSource>();

        if (m_impulseSource == null)
            m_impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();

        ConfigureImpulseSource();
        EnsurePlayerCameraShakeListener();
    }

    private void OnEnable()
    {
        if (m_playerHealth != null)
            m_playerHealth.OnDamageBlocked += HandleDamageBlocked;
    }

    private void Update()
    {
        // Deliberately outside the pause guard: this owns Time.timeScale, and a
        // hit slow motion left half applied would drag the tower defence side
        // down to 72% speed for as long as the player stays on the other camera.
        UpdateHitSlowMotion();

        if (PveRuntime.IsPaused)
            return;

        UpdateBlockInput();
        UpdateBlockState();
    }

    private void OnDisable()
    {
        if (m_playerHealth != null)
            m_playerHealth.OnDamageBlocked -= HandleDamageBlocked;

        StopBlocking(false);

        if (m_hitSlowMotionActive && Time.timeScale > 0f)
            FinishHitSlowMotion(Mathf.Approximately(Time.timeScale, m_appliedHitTimeScale));
    }

    public void OnAttack(InputValue _value)
    {
        // The input system keeps delivering while the arena is paused, so the
        // attack has to be turned away here rather than in Update.
        if (!_value.isPressed || Time.timeScale <= 0f || PveRuntime.IsPaused)
            return;

        if (m_isBlocking)
        {
            m_blockRequiresRelease = true;
            StopBlocking(true);
        }

        if (m_playerAnimation != null)
        {
            m_playerAnimation.RequestAttack();
            return;
        }

        if (!m_hasLoggedMissingAnimationController)
        {
            Debug.LogError("PlayerCombat requires a PlayerAnimationController. Attack was ignored to prevent invisible damage.", this);
            m_hasLoggedMissingAnimationController = true;
        }
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

    internal bool TryPerformAnimationImpact(int _comboStep, uint _swingSequence)
    {
        if (_comboStep < 1 || _comboStep > 3 || _swingSequence == 0 ||
            _swingSequence <= m_lastProcessedAnimationSwing)
        {
            return false;
        }

        // Consume the impact before hit detection. A missed swing must not become
        // eligible to deal damage later if another callback is raised.
        m_lastProcessedAnimationSwing = _swingSequence;
        ApplyAttackHit(_comboStep);
        return true;
    }

    private void ApplyAttackHit(int _comboStep)
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
            PlayHitSlowMotion(isCriticalHit);
        }
    }

    private void UpdateBlockInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        if (mouse.rightButton.wasPressedThisFrame)
            TryStartBlocking();

        if (mouse.rightButton.wasReleasedThisFrame)
        {
            m_blockRequiresRelease = false;
            StopBlocking(true);
        }

        // A release that happened while the arena was paused, or while the window
        // was out of focus, never arrives as an event. Without this the guard
        // would stay up on its own until the block timer ran out.
        else if (m_isBlocking && !mouse.rightButton.isPressed)
        {
            m_blockRequiresRelease = false;
            StopBlocking(true);
        }
    }

    private void UpdateBlockState()
    {
        if (!m_isBlocking)
            return;

        if (Time.timeScale <= 0f)
            return;

        if (m_playerHealth != null && !m_playerHealth.IsAlive)
        {
            StopBlocking(false);
            return;
        }

        if (PveRuntime.Time >= m_blockEndsAt)
        {
            m_blockRequiresRelease = true;
            StopBlocking(true);
        }
    }

    private bool TryStartBlocking()
    {
        if (m_isBlocking)
            return true;

        if (m_blockRequiresRelease || Time.timeScale <= 0f || PveRuntime.Time < m_nextBlockAllowedAt ||
            (m_playerHealth != null && !m_playerHealth.IsAlive) || m_playerAnimation == null)
        {
            return false;
        }

        if (!m_playerAnimation.RequestBlock())
            return false;

        m_isBlocking = true;
        m_blockEndsAt = PveRuntime.Time + m_maxBlockDuration;
        return true;
    }

    private void StopBlocking(bool _startCooldown)
    {
        if (!m_isBlocking)
            return;

        m_isBlocking = false;
        m_blockEndsAt = 0f;

        if (_startCooldown)
            m_nextBlockAllowedAt = Mathf.Max(m_nextBlockAllowedAt, PveRuntime.Time + m_blockCooldown);

        m_playerAnimation?.ReleaseBlock();
    }

    private void HandleDamageBlocked(int _incomingDamage, Vector3 _attackerPosition)
    {
        if (!m_isBlocking || _incomingDamage <= 0)
            return;

        m_playerAnimation?.ReplayBlockImpact();

        // Shaking from the attacker's side makes the block read directionally
        // instead of as a generic screen wobble.
        PlayHitShake(_attackerPosition, m_blockImpactShakeMultiplier);
    }

    private void PlayHitSlowMotion(bool _isCriticalHit)
    {
        float duration = _isCriticalHit
            ? m_criticalHitSlowMotionDuration
            : m_hitSlowMotionDuration;

        if (duration <= 0f || Time.timeScale <= 0f)
            return;

        if (m_hitSlowMotionActive && !Mathf.Approximately(Time.timeScale, m_appliedHitTimeScale))
            FinishHitSlowMotion(false);

        if (!m_hitSlowMotionActive)
        {
            m_hitSlowMotionActive = true;
            m_timeScaleBeforeHitSlowMotion = Time.timeScale;
            m_fixedDeltaTimeBeforeHitSlowMotion = Time.fixedDeltaTime;
            m_appliedHitTimeScale = m_timeScaleBeforeHitSlowMotion;
        }

        float timeScaleMultiplier = _isCriticalHit
            ? m_criticalHitTimeScale
            : m_hitTimeScale;
        float requestedTimeScale = Mathf.Max(0.01f, m_timeScaleBeforeHitSlowMotion * timeScaleMultiplier);

        m_appliedHitTimeScale = Mathf.Min(m_appliedHitTimeScale, requestedTimeScale);
        m_hitSlowMotionEndsAt = Mathf.Max(m_hitSlowMotionEndsAt, Time.unscaledTime + duration);

        Time.timeScale = m_appliedHitTimeScale;
        Time.fixedDeltaTime = m_fixedDeltaTimeBeforeHitSlowMotion *
                              (m_appliedHitTimeScale / Mathf.Max(0.01f, m_timeScaleBeforeHitSlowMotion));
    }

    private void UpdateHitSlowMotion()
    {
        if (!m_hitSlowMotionActive)
            return;

        // A shop or level-up pause owns timeScale=0. Wait until it resumes so
        // restoring this short effect can never accidentally close the pause.
        if (Time.timeScale <= 0f)
            return;

        if (!Mathf.Approximately(Time.timeScale, m_appliedHitTimeScale))
        {
            FinishHitSlowMotion(false);
            return;
        }

        if (Time.unscaledTime >= m_hitSlowMotionEndsAt)
            FinishHitSlowMotion(true);
    }

    private void FinishHitSlowMotion(bool _restoreTimeScale)
    {
        if (!m_hitSlowMotionActive)
            return;

        if (_restoreTimeScale)
        {
            Time.timeScale = m_timeScaleBeforeHitSlowMotion;
            Time.fixedDeltaTime = m_fixedDeltaTimeBeforeHitSlowMotion;
        }
        else if (Time.timeScale > 0f)
        {
            // Another system intentionally changed the time scale. Preserve it,
            // but keep the physics step proportional instead of leaving our value.
            Time.fixedDeltaTime = m_fixedDeltaTimeBeforeHitSlowMotion *
                                  (Time.timeScale / Mathf.Max(0.01f, m_timeScaleBeforeHitSlowMotion));
        }

        m_hitSlowMotionActive = false;
        m_hitSlowMotionEndsAt = 0f;
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
        m_hitTimeScale = Mathf.Clamp(m_hitTimeScale, 0.1f, 1f);
        m_hitSlowMotionDuration = Mathf.Max(0f, m_hitSlowMotionDuration);
        m_criticalHitTimeScale = Mathf.Clamp(m_criticalHitTimeScale, 0.1f, m_hitTimeScale);
        m_criticalHitSlowMotionDuration = Mathf.Max(0f, m_criticalHitSlowMotionDuration);
        m_maxBlockDuration = Mathf.Max(0.1f, m_maxBlockDuration);
        m_blockCooldown = Mathf.Max(0f, m_blockCooldown);
        m_blockImpactShakeMultiplier = Mathf.Clamp(m_blockImpactShakeMultiplier, 0f, 2f);

        if (Application.isPlaying && m_impulseSource != null)
            ConfigureImpulseSource();
    }
}
