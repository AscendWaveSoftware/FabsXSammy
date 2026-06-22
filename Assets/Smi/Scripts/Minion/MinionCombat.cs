using UnityEngine;
public class MinionCombat : MonoBehaviour
{
    [SerializeField] private SO_MinionStats m_minonStats;

    private float m_lastAttackTime;
    private bool m_isAttacking = false;

    private AI_Minion m_minionAI;

    private void Start()
    {
        m_minionAI = GetComponent<AI_Minion>();
        m_minonStats = m_minionAI.m_stats;
    }

    private void Update()
    {
        if (m_isAttacking)
            if (Time.time - m_lastAttackTime >= m_minonStats.m_attackCooldown)
                m_isAttacking = false;

        if (CanAttack())
            Attack();
    }
    private bool CanAttack()
    {
        if (!m_isAttacking && m_minionAI.m_currentTarget != null)
        {
            float distToTarget = Vector3.Distance(transform.position, m_minionAI.m_currentTarget.position);
            return distToTarget <= m_minonStats.m_stopDistance + 0.5f;
        }
        return false;
    }

    private void Attack()
    {
        m_lastAttackTime = Time.time;
        m_isAttacking = true;

            m_minionAI.m_currentTarget.TryGetComponent<IDamageable>(out var target);
            target.TakeDamage(m_minonStats.m_attackDamage);

        if (m_minionAI.m_currentTarget != enabled)
            m_minionAI.m_currentTarget = null;
    }
}
