using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float stoppingDistance = 1.2f;
    [SerializeField] private float acceleration = 10f;

    [Header("Group Behaviour")]
    [Tooltip("Distance at which enemies begin moving around the player instead of running straight at them.")]
    [SerializeField] private float orbitActivationDistance = 3.5f;
    [SerializeField] private Vector2 orbitSpeedRange = new Vector2(12f, 24f);
    [SerializeField] private float maximumApproachSpread = 2.5f;
    [SerializeField] private float separationRadius = 1.25f;
    [SerializeField] private float separationWeight = 1.5f;

    [Header("Navigation")]
    [SerializeField] private float repathInterval = 0.18f;
    [SerializeField] private float navMeshSampleRadius = 2.5f;
    [SerializeField] private float obstacleLookAhead = 2f;
    [SerializeField] private float obstacleProbeRadius = 0.3f;
    [SerializeField] private float stuckTimeBeforeRecovery = 0.8f;

    [Header("Knockback")]
    [SerializeField, Min(0.02f), Tooltip("How long a knockback push takes to cover its distance.")]
    private float knockbackDuration = 0.18f;

    [Header("Target")]
    [SerializeField] private Transform playerTarget;

    private const float GoldenAngle = 137.50776f;
    private const float MinimumDestinationDistance = 0.08f;
    private const int MaximumNeighbours = 16;
    private const int MaximumProbeHits = 12;

    private static int s_nextFormationIndex;
    private static readonly float[] s_probeAngleOffsets = { 0f, 35f, -35f, 70f, -70f, 110f, -110f };

    private readonly Collider[] m_neighbourBuffer = new Collider[MaximumNeighbours];
    private readonly RaycastHit[] m_probeBuffer = new RaycastHit[MaximumProbeHits];

    private Rigidbody m_rb;
    private Collider m_collider;
    private EnemyStats m_enemyStats;
    private EnemyAttack m_enemyAttack;
    private NavMeshAgent m_agent;

    private float m_formationAngle;
    private float m_orbitSpeed;
    private float m_personalStoppingDistance;
    private float m_nextRepathTime;
    private float m_lastFormationUpdateTime;
    private float m_stuckTimer;
    private float m_recoveryUntil;
    private int m_orbitDirection;
    private bool m_usesNavMesh;
    private Vector3 m_lastPosition;
    private Vector3 m_knockbackVelocity;
    private Vector3 m_pausedVelocity;
    private float m_knockbackEndsAt;

    public bool IsKnockedBack => PveRuntime.Time < m_knockbackEndsAt;

    /// <summary>
    /// Pushes this enemy away over a short moment. Goes through the NavMeshAgent
    /// rather than the rigidbody, because the agent drives a kinematic body and
    /// would ignore a physics force outright.
    /// </summary>
    public void ApplyKnockback(Vector3 _direction, float _distance)
    {
        if (_distance <= 0f || knockbackDuration <= 0f)
            return;

        Vector3 flatDirection = _direction;
        flatDirection.y = 0f;

        if (flatDirection.sqrMagnitude < 0.0001f)
            return;

        m_knockbackVelocity = flatDirection.normalized * (_distance / knockbackDuration);
        m_knockbackEndsAt = PveRuntime.Time + knockbackDuration;

        // The agent steers along its own path every frame. Without dropping that
        // path first it would simply walk straight back through the push.
        if (m_usesNavMesh && m_agent != null && m_agent.enabled && m_agent.isOnNavMesh)
        {
            m_agent.ResetPath();
            m_agent.velocity = Vector3.zero;
        }

        // Repath the moment the push is over instead of waiting out the interval.
        m_nextRepathTime = 0f;
    }

    private bool UpdateKnockback(float _deltaTime)
    {
        if (!IsKnockedBack)
            return false;

        if (m_usesNavMesh)
        {
            // Move keeps the agent on the navmesh, so a push can never shove an
            // enemy through a wall or off the walkable area.
            if (m_agent != null && m_agent.enabled && m_agent.isOnNavMesh)
                m_agent.Move(m_knockbackVelocity * _deltaTime);
        }
        else if (m_rb != null && !m_rb.isKinematic)
        {
            m_rb.linearVelocity = new Vector3(m_knockbackVelocity.x, 0f, m_knockbackVelocity.z);
        }

        return true;
    }

    private void Awake()
    {
        m_rb = GetComponent<Rigidbody>();
        m_collider = GetComponent<Collider>();
        m_enemyStats = GetComponent<EnemyStats>();
        m_enemyAttack = GetComponent<EnemyAttack>();

        m_rb.useGravity = false;
        m_rb.constraints = RigidbodyConstraints.FreezeRotationX |
                           RigidbodyConstraints.FreezeRotationY |
                           RigidbodyConstraints.FreezeRotationZ |
                           RigidbodyConstraints.FreezePositionY;
        m_rb.interpolation = RigidbodyInterpolation.Interpolate;
        m_rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        InitializePersonality();
        m_lastPosition = transform.position;
    }

    private void Start()
    {
        FindPlayerTarget();
        m_usesNavMesh = TryInitializeNavMeshAgent();

        // The agent only exists from here on, so an enemy that came into being
        // during a pause still has to be told to hold still.
        ApplyPauseState(PveRuntime.IsPaused);
    }

    private void OnEnable()
    {
        PveRuntime.PauseChanged += HandlePauseChanged;
        ApplyPauseState(PveRuntime.IsPaused);
    }

    private void OnDisable()
    {
        PveRuntime.PauseChanged -= HandlePauseChanged;
    }

    private void HandlePauseChanged(bool _paused) => ApplyPauseState(_paused);

    /// <summary>
    /// An Update guard alone is not enough here: the NavMeshAgent walks its own
    /// path and the rigidbody keeps its velocity, both without asking this
    /// script. The path itself is kept, so the enemy simply carries on.
    /// </summary>
    private void ApplyPauseState(bool _paused)
    {
        if (m_agent != null && m_agent.enabled && m_agent.isOnNavMesh)
            m_agent.isStopped = _paused;

        if (m_rb == null || m_rb.isKinematic)
            return;

        if (_paused)
        {
            m_pausedVelocity = m_rb.linearVelocity;
            m_rb.linearVelocity = Vector3.zero;
        }
        else
        {
            m_rb.linearVelocity = m_pausedVelocity;
            m_pausedVelocity = Vector3.zero;
        }
    }

    private void FixedUpdate()
    {
        if (PveRuntime.IsPaused || m_usesNavMesh)
            return;

        // Checked before CanMove, because a knockback drives the movement itself
        // and StopMoving would cancel it straight away.
        if (UpdateKnockback(Time.fixedDeltaTime))
            return;

        if (!CanMove())
        {
            StopMoving();
            return;
        }

        MoveWithPhysicsFallback();
    }

    private void Update()
    {
        if (PveRuntime.IsPaused || !m_usesNavMesh)
            return;

        if (UpdateKnockback(Time.deltaTime))
            return;

        if (!CanMove())
        {
            StopMoving();
            return;
        }

        UpdateNavMeshMovement();
    }

    public void SetMoveSpeed(float _newMoveSpeed)
    {
        moveSpeed = Mathf.Max(0f, _newMoveSpeed);

        if (m_agent != null)
            m_agent.speed = moveSpeed;
    }

    public void SetPlayerTarget(Transform _playerTarget)
    {
        playerTarget = _playerTarget;
    }

    private void InitializePersonality()
    {
        int formationIndex = s_nextFormationIndex++;
        m_formationAngle = Mathf.Repeat(formationIndex * GoldenAngle + Random.Range(-18f, 18f), 360f);
        m_orbitDirection = Random.value < 0.5f ? -1 : 1;
        m_orbitSpeed = Random.Range(
            Mathf.Min(orbitSpeedRange.x, orbitSpeedRange.y),
            Mathf.Max(orbitSpeedRange.x, orbitSpeedRange.y)
        );

        float maximumAttackDistance = m_enemyAttack != null
            ? Mathf.Max(0.2f, m_enemyAttack.AttackRange - 0.1f)
            : stoppingDistance;

        m_personalStoppingDistance = Mathf.Min(
            stoppingDistance * Random.Range(0.9f, 1.08f),
            maximumAttackDistance
        );
        m_personalStoppingDistance = Mathf.Max(0.2f, m_personalStoppingDistance);
        m_nextRepathTime = PveRuntime.Time + Random.Range(0f, repathInterval);
        m_lastFormationUpdateTime = PveRuntime.Time;
    }

    private void FindPlayerTarget()
    {
        if (playerTarget != null)
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
            playerTarget = playerObject.transform;
    }

    private bool TryInitializeNavMeshAgent()
    {
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit spawnHit, navMeshSampleRadius, NavMesh.AllAreas))
            return false;

        float verticalOffset = transform.position.y - spawnHit.position.y;

        m_rb.linearVelocity = Vector3.zero;
        m_rb.angularVelocity = Vector3.zero;
        m_rb.isKinematic = true;
        m_agent = GetComponent<NavMeshAgent>();

        if (m_agent == null)
            m_agent = gameObject.AddComponent<NavMeshAgent>();

        float colliderRadius = 0.45f;
        float colliderHeight = 1f;

        if (m_collider is CapsuleCollider capsule)
        {
            colliderRadius = capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
            colliderHeight = capsule.height * transform.lossyScale.y;
        }

        m_agent.speed = moveSpeed;
        m_agent.acceleration = acceleration;
        m_agent.angularSpeed = 720f;
        m_agent.radius = Mathf.Max(0.1f, colliderRadius * 0.9f);
        m_agent.height = Mathf.Max(m_agent.radius * 2f, colliderHeight);
        m_agent.baseOffset = verticalOffset;
        m_agent.stoppingDistance = MinimumDestinationDistance;
        m_agent.autoBraking = true;
        m_agent.autoRepath = true;
        m_agent.updateRotation = false;
        m_agent.updateUpAxis = false;
        m_agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        m_agent.avoidancePriority = Random.Range(25, 76);

        if (!m_agent.isOnNavMesh)
            m_agent.Warp(spawnHit.position);

        if (m_agent.isOnNavMesh)
            return true;

        m_agent.enabled = false;
        m_rb.isKinematic = false;
        return false;
    }

    private bool CanMove()
    {
        if (playerTarget == null)
            FindPlayerTarget();

        return playerTarget != null &&
               (m_enemyStats == null || !m_enemyStats.IsDead) &&
               (m_enemyAttack == null || (!m_enemyAttack.IsWindingUp && !m_enemyAttack.IsStaggered));
    }

    private void UpdateNavMeshMovement()
    {
        if (m_agent == null || !m_agent.enabled || !m_agent.isOnNavMesh)
        {
            SwitchToPhysicsFallback();
            return;
        }

        UpdateStuckRecovery(m_agent.velocity);

        if (PveRuntime.Time < m_nextRepathTime)
            return;

        m_nextRepathTime = PveRuntime.Time + repathInterval * Random.Range(0.8f, 1.2f);
        float formationDeltaTime = PveRuntime.Time - m_lastFormationUpdateTime;
        m_lastFormationUpdateTime = PveRuntime.Time;
        Vector3 destination = CalculateDesiredDestination(formationDeltaTime);

        if (NavMesh.SamplePosition(destination, out NavMeshHit targetHit, navMeshSampleRadius, m_agent.areaMask))
            m_agent.SetDestination(targetHit.position);
        else
            m_agent.SetDestination(playerTarget.position);
    }

    private Vector3 CalculateDesiredDestination(float _deltaTime)
    {
        Vector3 toPlayer = playerTarget.position - transform.position;
        toPlayer.y = 0f;
        float distanceToPlayer = toPlayer.magnitude;

        if (distanceToPlayer <= orbitActivationDistance || PveRuntime.Time < m_recoveryUntil)
            m_formationAngle = Mathf.Repeat(m_formationAngle + m_orbitDirection * m_orbitSpeed * _deltaTime, 360f);

        float extraSpread = Mathf.Clamp(distanceToPlayer - orbitActivationDistance, 0f, maximumApproachSpread);

        if (PveRuntime.Time < m_recoveryUntil)
            extraSpread = Mathf.Max(extraSpread, separationRadius);

        float angleInRadians = m_formationAngle * Mathf.Deg2Rad;
        Vector3 slotDirection = new Vector3(Mathf.Cos(angleInRadians), 0f, Mathf.Sin(angleInRadians));
        return playerTarget.position + slotDirection * (m_personalStoppingDistance + extraSpread);
    }

    private void UpdateStuckRecovery(Vector3 _velocity)
    {
        bool hasMeaningfulPath = !m_agent.pathPending &&
                                 m_agent.hasPath &&
                                 m_agent.remainingDistance > 0.35f;
        bool isBarelyMoving = _velocity.sqrMagnitude < 0.01f;

        if (hasMeaningfulPath && isBarelyMoving)
            m_stuckTimer += Time.deltaTime;
        else
            m_stuckTimer = Mathf.Max(0f, m_stuckTimer - Time.deltaTime * 2f);

        if (m_stuckTimer < stuckTimeBeforeRecovery)
            return;

        m_stuckTimer = 0f;
        m_orbitDirection *= -1;
        m_formationAngle = Mathf.Repeat(
            m_formationAngle + m_orbitDirection * Random.Range(75f, 135f),
            360f
        );
        m_recoveryUntil = PveRuntime.Time + Random.Range(0.8f, 1.25f);
        m_nextRepathTime = 0f;
    }

    private void SwitchToPhysicsFallback()
    {
        if (m_agent != null)
            m_agent.enabled = false;

        m_rb.isKinematic = false;
        m_usesNavMesh = false;
        m_lastPosition = transform.position;
    }

    private void MoveWithPhysicsFallback()
    {
        Vector3 destination = CalculateDesiredDestination(Time.fixedDeltaTime);
        Vector3 desiredDirection = destination - transform.position;
        desiredDirection.y = 0f;

        if (desiredDirection.sqrMagnitude <= MinimumDestinationDistance * MinimumDestinationDistance)
        {
            StopMoving();
            return;
        }

        desiredDirection.Normalize();
        Vector3 separation = CalculateSeparation();
        Vector3 combinedDirection = desiredDirection + separation * separationWeight;

        if (combinedDirection.sqrMagnitude > 0.001f)
            combinedDirection.Normalize();
        else
            combinedDirection = desiredDirection;

        Vector3 steeringDirection = FindClearDirection(combinedDirection);
        Vector3 currentVelocity = new Vector3(m_rb.linearVelocity.x, 0f, m_rb.linearVelocity.z);
        Vector3 targetVelocity = steeringDirection * moveSpeed;
        Vector3 nextVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
        m_rb.linearVelocity = new Vector3(nextVelocity.x, 0f, nextVelocity.z);

        UpdateFallbackStuckRecovery();
    }

    private Vector3 CalculateSeparation()
    {
        int neighbourCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            separationRadius,
            m_neighbourBuffer,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        Vector3 separation = Vector3.zero;

        for (int i = 0; i < neighbourCount; i++)
        {
            Collider neighbourCollider = m_neighbourBuffer[i];

            if (neighbourCollider == null || neighbourCollider == m_collider)
                continue;

            EnemyMovement neighbour = neighbourCollider.GetComponentInParent<EnemyMovement>();

            if (neighbour == null || neighbour == this)
                continue;

            Vector3 away = transform.position - neighbour.transform.position;
            away.y = 0f;
            float distanceSquared = Mathf.Max(away.sqrMagnitude, 0.01f);
            separation += away.normalized / distanceSquared;
        }

        return Vector3.ClampMagnitude(separation, 1f);
    }

    private Vector3 FindClearDirection(Vector3 _desiredDirection)
    {
        Vector3 bestDirection = _desiredDirection;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < s_probeAngleOffsets.Length; i++)
        {
            float signedAngle = s_probeAngleOffsets[i];

            if (PveRuntime.Time < m_recoveryUntil && signedAngle != 0f)
                signedAngle = Mathf.Abs(signedAngle) * m_orbitDirection;

            Vector3 candidate = Quaternion.Euler(0f, signedAngle, 0f) * _desiredDirection;
            float clearance = GetObstacleClearance(candidate);
            float alignment = Vector3.Dot(candidate, _desiredDirection);
            float score = clearance * 2f + alignment;

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestDirection = candidate;
        }

        return bestDirection.normalized;
    }

    private float GetObstacleClearance(Vector3 _direction)
    {
        int hitCount = Physics.SphereCastNonAlloc(
            transform.position,
            obstacleProbeRadius,
            _direction,
            m_probeBuffer,
            obstacleLookAhead,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        float nearestObstacle = obstacleLookAhead;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = m_probeBuffer[i].collider;

            if (hitCollider == null || hitCollider == m_collider)
                continue;

            if (hitCollider.GetComponentInParent<EnemyMovement>() != null)
                continue;

            if (playerTarget != null && hitCollider.transform.root == playerTarget.root)
                continue;

            nearestObstacle = Mathf.Min(nearestObstacle, m_probeBuffer[i].distance);
        }

        return nearestObstacle / Mathf.Max(0.01f, obstacleLookAhead);
    }

    private void UpdateFallbackStuckRecovery()
    {
        float movedDistance = Vector3.Distance(transform.position, m_lastPosition);
        m_lastPosition = transform.position;

        if (movedDistance < 0.0025f)
            m_stuckTimer += Time.fixedDeltaTime;
        else
            m_stuckTimer = Mathf.Max(0f, m_stuckTimer - Time.fixedDeltaTime * 2f);

        if (m_stuckTimer < stuckTimeBeforeRecovery)
            return;

        m_stuckTimer = 0f;
        m_orbitDirection *= -1;
        m_formationAngle = Mathf.Repeat(m_formationAngle + m_orbitDirection * 100f, 360f);
        m_recoveryUntil = PveRuntime.Time + 1f;
    }

    private void StopMoving()
    {
        if (m_usesNavMesh && m_agent != null && m_agent.enabled && m_agent.isOnNavMesh)
        {
            m_agent.ResetPath();
            return;
        }

        if (m_rb != null && !m_rb.isKinematic)
            m_rb.linearVelocity = Vector3.zero;
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        stoppingDistance = Mathf.Max(0.2f, stoppingDistance);
        acceleration = Mathf.Max(0.1f, acceleration);
        orbitActivationDistance = Mathf.Max(stoppingDistance, orbitActivationDistance);
        separationRadius = Mathf.Max(0.1f, separationRadius);
        repathInterval = Mathf.Max(0.05f, repathInterval);
        navMeshSampleRadius = Mathf.Max(0.1f, navMeshSampleRadius);
        obstacleLookAhead = Mathf.Max(0.1f, obstacleLookAhead);
        obstacleProbeRadius = Mathf.Max(0.05f, obstacleProbeRadius);
        stuckTimeBeforeRecovery = Mathf.Max(0.2f, stuckTimeBeforeRecovery);
    }
}
