using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator), typeof(Rigidbody))]
public class PlayerAnimationController : MonoBehaviour
{
    public const int ComboStepCount = 3;

    private enum AnimationMode
    {
        Locomotion,
        Attack,
        Block,
        Hurt
    }

    private enum LocomotionState
    {
        Idle,
        Walk,
        Run
    }

    private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
    private static readonly int WalkState = Animator.StringToHash("Base Layer.Walk");
    private static readonly int RunState = Animator.StringToHash("Base Layer.Run");
    private static readonly int Attack1State = Animator.StringToHash("Base Layer.Attack1");
    private static readonly int Attack2State = Animator.StringToHash("Base Layer.Attack2");
    private static readonly int Attack3State = Animator.StringToHash("Base Layer.Attack3");
    private static readonly int DefendState = Animator.StringToHash("Base Layer.Defend");
    private static readonly int HurtState = Animator.StringToHash("Base Layer.Hurt");

    private static readonly int[] AttackStates = { Attack1State, Attack2State, Attack3State };

    [Header("References")]
    [SerializeField] private Animator m_animator;
    [SerializeField] private Rigidbody m_rigidbody;
    [SerializeField] private PlayerMovementHandler m_movement;
    [SerializeField] private PlayerCombat m_combat;
    [SerializeField] private PlayerHealth m_health;

    [Header("Animation Clips")]
    [SerializeField] private AnimationClip[] m_attackClips = new AnimationClip[ComboStepCount];
    [SerializeField] private AnimationClip m_defendClip;
    [SerializeField] private AnimationClip m_hurtClip;

    [Header("Locomotion")]
    [SerializeField, Min(0f)] private float m_idleWorldSpeed = 0.08f;
    [SerializeField, Range(1f, 3f)] private float m_idleEnterMultiplier = 1.35f;
    [SerializeField, Range(0f, 1f)] private float m_runEnterSpeed = 0.55f;
    [SerializeField, Range(0f, 1f)] private float m_runExitSpeed = 0.42f;
    [SerializeField, Min(0.01f)] private float m_speedSmoothing = 0.08f;
    [SerializeField, Min(0f)] private float m_transitionDuration = 0.07f;

    [Header("Attack Timing")]
    [SerializeField] private float[] m_attackImpactTimes = { 0.50f, 0.34f, 0.43f };

    /// <summary>
    /// Raised when a combo swing actually starts playing. The argument is the
    /// 1-based combo step, so step 1 belongs to the Attack1 state.
    /// </summary>
    public event Action<int> OnAttackSwingStarted;

    private AnimationMode m_mode;
    private LocomotionState m_locomotionState;
    private float m_smoothedNormalizedSpeed;
    private float m_speedSmoothVelocity;
    private float m_animatorSpeedBeforePause = 1f;
    private float m_stateElapsed;
    private int m_attackStep;
    private int m_bufferedAttackCount;
    private bool m_attackImpactTriggered;
    private uint m_activeSwingSequence;
    private bool m_hasLoggedInvalidCombatSetup;
    private bool m_hasLoggedInvalidBlockSetup;
    private bool m_hasLoggedInvalidHurtSetup;
    private bool m_hasStarted;

    private void Awake()
    {
        if (m_animator == null)
            m_animator = GetComponent<Animator>();

        if (m_rigidbody == null)
            m_rigidbody = GetComponent<Rigidbody>();

        if (m_movement == null)
            m_movement = GetComponent<PlayerMovementHandler>();

        if (m_combat == null)
            m_combat = GetComponent<PlayerCombat>();

        if (m_health == null)
            m_health = GetComponent<PlayerHealth>();
    }

    private void OnEnable()
    {
        PveRuntime.PauseChanged += HandlePauseChanged;

        if (m_health != null)
            m_health.OnDamageTaken += HandleDamageTaken;

        if (m_hasStarted)
        {
            ResetActionState();
            SynchronizeLocomotionSpeed();
            TryPlayState(IdleState, 0f);
        }
    }

    private void Start()
    {
        m_hasStarted = true;
        m_mode = AnimationMode.Locomotion;
        m_locomotionState = LocomotionState.Idle;
        SynchronizeLocomotionSpeed();
        TryPlayState(IdleState, 0f);
    }

    private void OnDisable()
    {
        PveRuntime.PauseChanged -= HandlePauseChanged;

        if (m_health != null)
            m_health.OnDamageTaken -= HandleDamageTaken;

        ResetActionState();
        m_locomotionState = LocomotionState.Idle;
        m_smoothedNormalizedSpeed = 0f;
        m_speedSmoothVelocity = 0f;
    }

    private void Update()
    {
        // Held mid-swing rather than reset, so the combo resumes exactly where
        // the player left it.
        if (PveRuntime.IsPaused)
            return;

        if (m_animator == null || m_rigidbody == null)
            return;

        switch (m_mode)
        {
            case AnimationMode.Attack:
                UpdateAttack();
                break;
            case AnimationMode.Block:
                break;
            case AnimationMode.Hurt:
                UpdateHurt();
                break;
            default:
                UpdateLocomotion();
                break;
        }
    }

    /// <summary>
    /// The Animator plays its clips on its own, entirely outside this script's
    /// Update. Without stopping it the sprite would keep swinging while the
    /// combat logic stands still, and the two would come back out of sync.
    /// </summary>
    private void HandlePauseChanged(bool _paused)
    {
        if (m_animator == null)
            return;

        if (_paused)
        {
            m_animatorSpeedBeforePause = m_animator.speed;
            m_animator.speed = 0f;
            return;
        }

        m_animator.speed = m_animatorSpeedBeforePause;
    }

    public bool RequestAttack()
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f ||
            (m_health != null && !m_health.IsAlive))
            return false;

        if (!HasValidCombatSetup())
        {
            if (!m_hasLoggedInvalidCombatSetup)
            {
                Debug.LogError("Player attack animation setup is incomplete. Attack was ignored to prevent invisible damage.", this);
                m_hasLoggedInvalidCombatSetup = true;
            }

            return false;
        }

        if (m_mode == AnimationMode.Hurt)
        {
            m_bufferedAttackCount = 1;
            return true;
        }

        if (m_mode == AnimationMode.Block)
            return false;

        if (m_mode == AnimationMode.Attack)
        {
            int remainingComboSwings = AttackStates.Length - 1 - m_attackStep;
            m_bufferedAttackCount = Mathf.Min(m_bufferedAttackCount + 1, remainingComboSwings);

            return true;
        }

        m_bufferedAttackCount = 0;
        return StartAttack(0);
    }

    public bool RequestBlock()
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f ||
            (m_health != null && !m_health.IsAlive) || m_mode == AnimationMode.Hurt)
        {
            return false;
        }

        if (m_mode == AnimationMode.Block)
            return true;

        if (m_animator == null || m_rigidbody == null || m_defendClip == null || !CanPlayState(DefendState))
        {
            if (!m_hasLoggedInvalidBlockSetup)
            {
                Debug.LogError("Player block animation setup is incomplete. Blocking was ignored to prevent invisible invulnerability.", this);
                m_hasLoggedInvalidBlockSetup = true;
            }

            return false;
        }

        // Blocking is an intentional attack cancel. A swing that has not reached
        // its impact frame loses its damage instead of firing invisibly later.
        m_bufferedAttackCount = 0;
        m_attackImpactTriggered = true;
        m_mode = AnimationMode.Block;
        m_stateElapsed = 0f;
        m_animator.speed = 1f;
        SetActionLocked(true);
        TryPlayState(DefendState, 0.035f);
        return true;
    }

    public void ReleaseBlock()
    {
        if (m_mode == AnimationMode.Block)
            ReturnToLocomotion();
    }

    public void ReplayBlockImpact()
    {
        if (m_mode == AnimationMode.Block)
            TryPlayState(DefendState, 0.015f);
    }

    private void UpdateLocomotion(bool _forceAnimatorState = false)
    {
        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f && !_forceAnimatorState)
            return;

        float maxSpeed = m_movement != null ? m_movement.CurrentMaxPlanarSpeed : 1f;
        Vector3 velocity = m_rigidbody.linearVelocity;
        float worldSpeed = new Vector2(velocity.x, velocity.z).magnitude;
        float normalizedSpeed = Mathf.Clamp01(worldSpeed / Mathf.Max(0.01f, maxSpeed));
        float idleExitThreshold = Mathf.Max(m_idleWorldSpeed, maxSpeed * 0.025f);
        float idleEnterThreshold = idleExitThreshold * m_idleEnterMultiplier;

        if (deltaTime > 0f)
        {
            m_smoothedNormalizedSpeed = Mathf.SmoothDamp(
                m_smoothedNormalizedSpeed,
                normalizedSpeed,
                ref m_speedSmoothVelocity,
                m_speedSmoothing,
                Mathf.Infinity,
                deltaTime
            );
        }

        LocomotionState nextState;

        bool shouldIdle = m_locomotionState == LocomotionState.Idle
            ? worldSpeed < idleEnterThreshold
            : worldSpeed < idleExitThreshold;

        if (shouldIdle)
        {
            nextState = LocomotionState.Idle;
        }
        else if (m_locomotionState == LocomotionState.Run)
        {
            nextState = m_smoothedNormalizedSpeed > m_runExitSpeed
                ? LocomotionState.Run
                : LocomotionState.Walk;
        }
        else
        {
            nextState = m_smoothedNormalizedSpeed >= m_runEnterSpeed
                ? LocomotionState.Run
                : LocomotionState.Walk;
        }

        if (_forceAnimatorState || nextState != m_locomotionState)
        {
            m_locomotionState = nextState;
            TryPlayState(GetLocomotionStateHash(nextState), m_transitionDuration);
        }

        m_animator.speed = nextState switch
        {
            LocomotionState.Walk => Mathf.Lerp(0.82f, 1.15f, Mathf.InverseLerp(0.05f, m_runEnterSpeed, m_smoothedNormalizedSpeed)),
            LocomotionState.Run => Mathf.Lerp(0.92f, 1.25f, Mathf.InverseLerp(m_runExitSpeed, 1f, m_smoothedNormalizedSpeed)),
            _ => 1f
        };
    }

    private bool StartAttack(int _step)
    {
        int clampedStep = Mathf.Clamp(_step, 0, AttackStates.Length - 1);

        if (!CanPlayState(AttackStates[clampedStep]))
        {
            ReturnToLocomotion();
            return false;
        }

        m_mode = AnimationMode.Attack;
        m_attackStep = clampedStep;
        m_attackImpactTriggered = false;
        m_stateElapsed = 0f;
        m_activeSwingSequence++;

        if (m_activeSwingSequence == 0)
            m_activeSwingSequence = 1;

        m_animator.speed = 1f;
        SetActionLocked(true);
        TryPlayState(AttackStates[m_attackStep], m_attackStep == 0 ? m_transitionDuration : 0.025f);
        OnAttackSwingStarted?.Invoke(m_attackStep + 1);
        return true;
    }

    private void UpdateAttack()
    {
        m_stateElapsed += Time.deltaTime;
        float duration = GetAttackDuration(m_attackStep);
        float normalizedTime = duration > 0f ? m_stateElapsed / duration : 1f;
        float impactTime = GetAttackImpactTime(m_attackStep);

        if (!m_attackImpactTriggered && normalizedTime >= impactTime)
        {
            m_attackImpactTriggered = true;
            m_combat.TryPerformAnimationImpact(m_attackStep + 1, m_activeSwingSequence);
        }

        if (m_stateElapsed < duration)
            return;

        if (m_bufferedAttackCount > 0 && m_attackStep < AttackStates.Length - 1)
        {
            m_bufferedAttackCount--;

            if (!StartAttack(m_attackStep + 1))
                ReturnToLocomotion();
        }
        else
            ReturnToLocomotion();
    }

    private void HandleDamageTaken(int _damage, bool _isLethal)
    {
        if (_damage <= 0)
            return;

        m_bufferedAttackCount = 0;
        m_attackImpactTriggered = true;

        if (_isLethal)
        {
            ReturnToLocomotion();
            return;
        }

        if (m_mode == AnimationMode.Hurt)
            return;

        if (m_hurtClip == null || !CanPlayState(HurtState))
        {
            if (!m_hasLoggedInvalidHurtSetup)
            {
                Debug.LogError("Player hurt animation setup is incomplete. Hurt playback was skipped safely.", this);
                m_hasLoggedInvalidHurtSetup = true;
            }

            ReturnToLocomotion();
            return;
        }

        m_mode = AnimationMode.Hurt;
        m_stateElapsed = 0f;
        m_animator.speed = 1f;
        SetActionLocked(true);
        TryPlayState(HurtState, 0.035f);
    }

    private void UpdateHurt()
    {
        m_stateElapsed += Time.deltaTime;

        if (m_stateElapsed < GetHurtDuration())
            return;

        bool startBufferedAttack = m_bufferedAttackCount > 0;
        m_bufferedAttackCount = 0;

        if (startBufferedAttack && (m_health == null || m_health.IsAlive))
            StartAttack(0);
        else
            ReturnToLocomotion();
    }

    private void ReturnToLocomotion()
    {
        m_mode = AnimationMode.Locomotion;
        m_attackStep = 0;
        m_bufferedAttackCount = 0;
        m_attackImpactTriggered = false;
        m_stateElapsed = 0f;

        if (m_animator != null)
            m_animator.speed = 1f;

        SetActionLocked(false);

        if (m_animator != null && m_rigidbody != null)
        {
            SynchronizeLocomotionSpeed();
            UpdateLocomotion(true);
        }
    }

    private bool TryPlayState(int _stateHash, float _fadeDuration)
    {
        if (!CanPlayState(_stateHash))
            return false;

        if (_fadeDuration <= 0f)
            m_animator.Play(_stateHash, 0, 0f);
        else
            m_animator.CrossFadeInFixedTime(_stateHash, _fadeDuration, 0, 0f);

        return true;
    }

    private bool HasValidCombatSetup()
    {
        if (m_animator == null || m_rigidbody == null || m_combat == null ||
            m_animator.runtimeAnimatorController == null || m_attackClips == null ||
            m_attackClips.Length != AttackStates.Length)
        {
            return false;
        }

        for (int i = 0; i < AttackStates.Length; i++)
        {
            if (m_attackClips[i] == null || !CanPlayState(AttackStates[i]))
                return false;
        }

        return true;
    }

    private bool CanPlayState(int _stateHash) =>
        m_animator != null && m_animator.runtimeAnimatorController != null && m_animator.HasState(0, _stateHash);

    private void ResetActionState()
    {
        m_mode = AnimationMode.Locomotion;
        m_attackStep = 0;
        m_bufferedAttackCount = 0;
        m_attackImpactTriggered = false;
        m_stateElapsed = 0f;

        if (m_animator != null)
            m_animator.speed = 1f;

        SetActionLocked(false);
    }

    private void SynchronizeLocomotionSpeed()
    {
        m_speedSmoothVelocity = 0f;

        if (m_rigidbody == null)
        {
            m_smoothedNormalizedSpeed = 0f;
            return;
        }

        float maxSpeed = m_movement != null ? m_movement.CurrentMaxPlanarSpeed : 1f;
        Vector3 velocity = m_rigidbody.linearVelocity;
        float worldSpeed = new Vector2(velocity.x, velocity.z).magnitude;
        m_smoothedNormalizedSpeed = Mathf.Clamp01(worldSpeed / Mathf.Max(0.01f, maxSpeed));
    }

    private float GetAttackDuration(int _step)
    {
        if (m_attackClips != null && _step >= 0 && _step < m_attackClips.Length && m_attackClips[_step] != null)
            return Mathf.Max(0.05f, m_attackClips[_step].length);

        return _step == 1 ? 5f / 14f : 6f / 14f;
    }

    private float GetAttackImpactTime(int _step)
    {
        if (m_attackImpactTimes == null || _step < 0 || _step >= m_attackImpactTimes.Length)
            return 0.5f;

        return Mathf.Clamp01(m_attackImpactTimes[_step]);
    }

    private float GetHurtDuration() => m_hurtClip != null ? Mathf.Max(0.05f, m_hurtClip.length) : 4f / 12f;

    private static int GetLocomotionStateHash(LocomotionState _state) => _state switch
    {
        LocomotionState.Walk => WalkState,
        LocomotionState.Run => RunState,
        _ => IdleState
    };

    private void SetActionLocked(bool _locked)
    {
        if (m_movement != null)
        {
            m_movement.SetFacingLocked(_locked);
            m_movement.SetMovementLocked(_locked);
        }
    }

    private void OnValidate()
    {
        m_idleWorldSpeed = Mathf.Max(0f, m_idleWorldSpeed);
        m_idleEnterMultiplier = Mathf.Clamp(m_idleEnterMultiplier, 1f, 3f);
        m_runEnterSpeed = Mathf.Clamp(m_runEnterSpeed, 0.05f, 1f);
        m_runExitSpeed = Mathf.Clamp(m_runExitSpeed, 0f, m_runEnterSpeed);
        m_speedSmoothing = Mathf.Max(0.01f, m_speedSmoothing);
        m_transitionDuration = Mathf.Max(0f, m_transitionDuration);

        if (m_attackClips == null || m_attackClips.Length != ComboStepCount)
            Array.Resize(ref m_attackClips, ComboStepCount);

        if (m_attackImpactTimes == null || m_attackImpactTimes.Length != ComboStepCount)
        {
            m_attackImpactTimes = new float[ComboStepCount];
            m_attackImpactTimes[0] = 0.50f;
            m_attackImpactTimes[1] = 0.34f;
            m_attackImpactTimes[2] = 0.43f;
        }
    }
}
