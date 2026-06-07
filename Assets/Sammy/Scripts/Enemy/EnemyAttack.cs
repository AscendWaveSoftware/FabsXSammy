using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private float m_attackRange = 1.4f;
    [SerializeField] private float m_attackCooldown = 1f;

    [Header("Target")]
    [SerializeField] private Transform m_playerTarget;

    private EnemyStats m_stats;
    private PlayerHealth m_playerHealth;
    private float m_nextAttackTime;

    private void Awake()
    {
        m_stats = GetComponent<EnemyStats>();
    }

    private void Start()
    {
        if(m_playerTarget == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if(playerObject != null)
                m_playerTarget = playerObject.transform;

            if (m_playerTarget != null)
                m_playerHealth = m_playerTarget.GetComponent<PlayerHealth>();
        }
    }

    private void Update()
    {
        TryAttackPlayer();
    }

    private void TryAttackPlayer()
    {
        if (m_stats != null && m_stats.IsDead)
            return;

        if (m_playerTarget == null || m_playerHealth == null)
            return;

        if (!m_playerHealth.IsAlive)
            return;

        float distanceToPlayer = Vector3.Distance(transform.position, m_playerTarget.position);

        if (distanceToPlayer > m_attackRange)
            return;

        if (Time.time < m_nextAttackTime)
            return;

        m_nextAttackTime = Time.time + m_attackCooldown;

        m_playerHealth.TakeDamage(m_stats.AttackDamage);
    }
}
