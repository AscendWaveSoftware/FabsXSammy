using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float stoppingDistance = 1.2f;

  
    [Header("Target")]
    [SerializeField] private Transform playerTarget;

    private Rigidbody m_rb;
    private EnemyStats m_enemyStats;

    private void Awake()
    {
        m_rb = GetComponent<Rigidbody>();
        m_enemyStats = GetComponent<EnemyStats>();

        m_rb.useGravity = false;
        m_rb.constraints = RigidbodyConstraints.FreezeRotationX |
                           RigidbodyConstraints.FreezeRotationY |
                           RigidbodyConstraints.FreezeRotationZ |
                           RigidbodyConstraints.FreezePositionY;

        m_rb.interpolation = RigidbodyInterpolation.Interpolate;
        m_rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    private void Start()
    {
        if(playerTarget == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
                playerTarget = playerObject.transform;
        }
    }

    private void FixedUpdate()
    {
        MoveToPlayer();
    }

    public void SetMoveSpeed(float _newMoveSpeed)
    {
        moveSpeed = Mathf.Max(0f, _newMoveSpeed);
    }

    public void SetPlayerTarget(Transform _playerTarget)
    {
        playerTarget = _playerTarget;
    }

    private void MoveToPlayer()
    {
        if (playerTarget == null)
        {
            StopMoving();
            return;
        }

        if (m_enemyStats != null && m_enemyStats.IsDead)
        {
            StopMoving();
            return;
        }

        Vector3 directionToPlayer = playerTarget.position - transform.position;
        directionToPlayer.y = 0f;

        float distanceToPlayer = directionToPlayer.magnitude;

        if (distanceToPlayer <= stoppingDistance)
        {
            StopMoving();
            return;
        }

        Vector3 moveDirection = directionToPlayer.normalized;
        Vector3 targetVelocity = moveDirection * moveSpeed;

        m_rb.linearVelocity = new Vector3(
            targetVelocity.x,
            0f,
            targetVelocity.z
        );
    }

    private void StopMoving()
    {
        m_rb.linearVelocity = Vector3.zero;
    }
}
