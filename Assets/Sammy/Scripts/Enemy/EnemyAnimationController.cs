using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class EnemyAnimationController : MonoBehaviour
{
    public const int AttackVariantCount = 2;

    private enum AnimationMode
    {
        Locomotion,
        Attack,
        Hurt
    }

    private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
    private static readonly int WalkState = Animator.StringToHash("Base Layer.Walk");
    private static readonly int Attack1State = Animator.StringToHash("Base Layer.Attack1");
    private static readonly int Attack2State = Animator.StringToHash("Base Layer.Attack2");
    private static readonly int HurtState = Animator.StringToHash("Base Layer.Hurt");

    private static readonly int[] AttackStates = { Attack1State, Attack2State };

    [Header("References")]
    [SerializeField] private Animator m_animator;
    [SerializeField] private SpriteRenderer m_bodyRenderer;
    [SerializeField] private EnemyMovement m_movement;
    [SerializeField] private EnemyAttack m_attack;
    [SerializeField] private EnemyStats m_stats;

    [Header("Clips")]
    [SerializeField] private AnimationClip[] m_attackClips = new AnimationClip[AttackVariantCount];
    [SerializeField] private AnimationClip m_hurtClip;

    [Header("Locomotion")]
    [SerializeField, Min(0f), Tooltip("Ground speed below which the enemy is considered standing still.")]
    private float m_idleSpeedThreshold = 0.12f;
    [SerializeField, Min(0f)] private float m_transitionDuration = 0.06f;

    [Header("Facing")]
    [SerializeField, Tooltip("Tick when the artwork faces right, so it is flipped for a target on the left.")]
    private bool m_artworkFacesRight = true;

    private Transform m_cameraTransform;
    private AnimationMode m_mode;
    private bool m_isWalking;
    private float m_stateElapsed;
    private float m_animatorSpeedBeforePause = 1f;
    private int m_nextAttackVariant;
    private bool m_hasLoggedMissingClips;

    private void Awake()
    {
        if (m_animator == null)
            m_animator = GetComponent<Animator>();

        if (m_movement == null)
            m_movement = GetComponent<EnemyMovement>();

        if (m_attack == null)
            m_attack = GetComponent<EnemyAttack>();

        if (m_stats == null)
            m_stats = GetComponent<EnemyStats>();

        if (m_bodyRenderer == null)
            m_bodyRenderer = GetComponentInChildren<SpriteRenderer>(true);

        m_nextAttackVariant = Random.Range(0, AttackVariantCount);
    }

    private void OnEnable()
    {
        PveRuntime.PauseChanged += HandlePauseChanged;

        if (m_attack != null)
            m_attack.OnWindupStarted += HandleWindupStarted;

        if (m_stats != null)
            m_stats.OnDamageTaken += HandleDamageTaken;
    }

    private void OnDisable()
    {
        PveRuntime.PauseChanged -= HandlePauseChanged;

        if (m_attack != null)
            m_attack.OnWindupStarted -= HandleWindupStarted;

        if (m_stats != null)
            m_stats.OnDamageTaken -= HandleDamageTaken;
    }

    private void Start()
    {
        ResolveCamera();

        m_mode = AnimationMode.Locomotion;
        m_isWalking = false;

        if (HasValidSetup())
            PlayState(IdleState, 0f, Random.Range(0f, 1f));
    }

    private void Update()
    {
        if (PveRuntime.IsPaused || m_animator == null)
            return;

        switch (m_mode)
        {
            case AnimationMode.Attack:
                UpdateTimedState(GetAttackDuration());
                break;
            case AnimationMode.Hurt:
                UpdateTimedState(GetHurtDuration());
                break;
            default:
                UpdateLocomotion();
                break;
        }

        UpdateFacing();
    }

    private void UpdateLocomotion()
    {
        if (m_movement == null)
            return;

        bool shouldWalk = m_movement.CurrentPlanarSpeed > m_idleSpeedThreshold;

        if (shouldWalk == m_isWalking)
            return;

        m_isWalking = shouldWalk;
        PlayState(shouldWalk ? WalkState : IdleState, m_transitionDuration);
    }

    private void UpdateTimedState(float _duration)
    {
        m_stateElapsed += Time.deltaTime;

        if (m_stateElapsed < _duration)
            return;

        ReturnToLocomotion();
    }

    /// <summary>
    /// The sprite is billboarded, so which way the enemy should face is decided
    /// in screen terms rather than in world space.
    /// </summary>
    private void UpdateFacing()
    {
        if (m_bodyRenderer == null || m_movement == null)
            return;

        Vector3 toTarget = m_movement.FacingTarget - transform.position;

        // Camera relative while a camera is known, world space otherwise. Facing
        // must never depend on an optional reference: BetaScene has neither a
        // CameraReferences object nor a camera tagged MainCamera, and the sprite
        // silently stopped flipping there. World X is also what the player sprite
        // has always used, so both stay consistent.
        float sideways = m_cameraTransform != null
            ? Vector3.Dot(toTarget, m_cameraTransform.right)
            : toTarget.x;

        // A target almost dead ahead would flip back and forth on tiny wobbles.
        if (Mathf.Abs(sideways) < 0.05f)
            return;

        bool targetIsOnTheRight = sideways > 0f;
        m_bodyRenderer.flipX = m_artworkFacesRight ? !targetIsOnTheRight : targetIsOnTheRight;
    }

    /// <summary>
    /// Resolved once. Staying null is a valid outcome and only means facing falls
    /// back to world space, which is correct as long as the camera has no yaw.
    /// </summary>
    private void ResolveCamera()
    {
        if (CameraReferences.Instance != null)
            m_cameraTransform = CameraReferences.Instance.PlayerCameraTransform;

        if (m_cameraTransform == null && Camera.main != null)
            m_cameraTransform = Camera.main.transform;
    }

    private void HandleWindupStarted()
    {
        if (!HasValidSetup() || m_mode == AnimationMode.Hurt)
            return;

        int variant = m_nextAttackVariant;
        m_nextAttackVariant = (m_nextAttackVariant + 1) % AttackVariantCount;

        m_mode = AnimationMode.Attack;
        m_stateElapsed = 0f;

        float clipLength = GetAttackClipLength(variant);
        float windup = m_attack != null ? m_attack.AttackWindup : clipLength;
        m_animator.speed = windup > 0.01f ? Mathf.Clamp(clipLength / windup, 0.25f, 4f) : 1f;

        PlayState(AttackStates[variant], 0f);
    }

    private void HandleDamageTaken(int _damage, bool _isLethal)
    {
        if (_damage <= 0 || _isLethal || m_hurtClip == null || !HasValidSetup())
            return;

        m_mode = AnimationMode.Hurt;
        m_stateElapsed = 0f;
        m_animator.speed = 1f;
        PlayState(HurtState, 0f);
    }

    private void ReturnToLocomotion()
    {
        m_mode = AnimationMode.Locomotion;
        m_stateElapsed = 0f;
        m_animator.speed = 1f;

        m_isWalking = m_movement != null && m_movement.CurrentPlanarSpeed > m_idleSpeedThreshold;
        PlayState(m_isWalking ? WalkState : IdleState, m_transitionDuration);
    }

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

    private void PlayState(int _stateHash, float _fadeDuration, float _normalizedTime = 0f)
    {
        if (!CanPlayState(_stateHash))
            return;

        if (_fadeDuration <= 0f)
            m_animator.Play(_stateHash, 0, _normalizedTime);
        else
            m_animator.CrossFadeInFixedTime(_stateHash, _fadeDuration, 0, _normalizedTime);
    }

    private bool CanPlayState(int _stateHash) =>
        m_animator != null && m_animator.runtimeAnimatorController != null && m_animator.HasState(0, _stateHash);

    private bool HasValidSetup()
    {
        if (m_animator == null || m_animator.runtimeAnimatorController == null ||
            m_attackClips == null || m_attackClips.Length != AttackVariantCount)
        {
            LogInvalidSetupOnce();
            return false;
        }

        for (int i = 0; i < AttackVariantCount; i++)
        {
            if (m_attackClips[i] == null || !CanPlayState(AttackStates[i]))
            {
                LogInvalidSetupOnce();
                return false;
            }
        }

        return true;
    }

    private void LogInvalidSetupOnce()
    {
        if (m_hasLoggedMissingClips)
            return;

        Debug.LogError(
            "Enemy animation setup is incomplete. Run Tools/Sammy/Rebuild Enemy Animations to build it.", this);
        m_hasLoggedMissingClips = true;
    }

    private float GetAttackClipLength(int _variant) =>
        m_attackClips != null && _variant >= 0 && _variant < m_attackClips.Length && m_attackClips[_variant] != null
            ? Mathf.Max(0.05f, m_attackClips[_variant].length)
            : 0.3f;

    private float GetAttackDuration() => m_attack != null ? Mathf.Max(0.05f, m_attack.AttackWindup) : 0.3f;

    private float GetHurtDuration() => m_hurtClip != null ? Mathf.Max(0.05f, m_hurtClip.length) : 0.25f;

    private void OnValidate()
    {
        m_transitionDuration = Mathf.Max(0f, m_transitionDuration);

        if (m_attackClips == null || m_attackClips.Length != AttackVariantCount)
            System.Array.Resize(ref m_attackClips, AttackVariantCount);
    }
}
