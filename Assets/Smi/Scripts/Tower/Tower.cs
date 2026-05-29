using UnityEngine;

public class Tower : MonoBehaviour
{
    [SerializeField] private TowerStats m_towerStats;
    [SerializeField] private GameObject m_towerTurret;

    public GameObject m_projectilePrefab;
    public Transform m_spawnPoint;
    public LineRenderer m_lineRenderer;

    private float m_nextAttackTime = 0;
    private GameObject m_currentTarget;
    private Health m_health;

    private void Start()
    {
        m_health = GetComponent<Health>();
        m_health.m_currentHealth = m_towerStats.m_health;
    }

    void Update()
    {
        if (m_currentTarget == null || Vector3.Distance(transform.position, m_currentTarget.transform.position) > m_towerStats.m_attackRange)
            FindNewTarget();

        UpdateLineToCurrentTarget();
        if (m_currentTarget != null)
        {
            Vector3 targetPos = m_currentTarget.transform.position;
            targetPos.y = m_towerTurret.transform.position.y;

            m_towerTurret.transform.LookAt(targetPos);

            if (Time.time >= m_nextAttackTime)
            {
                AttackCurrentTarget();
                m_nextAttackTime = Time.time + m_towerStats.m_attackCooldown;
            }

        }
    }
    private void FindNewTarget()
    {
        GameObject[] minions = GameObject.FindGameObjectsWithTag(m_towerStats.m_enemyMinionTag);
        float closestDistance = float.MaxValue;

        foreach (GameObject minion in minions)
        {
            float dist = Vector3.Distance(transform.position, minion.transform.position);
            if (dist <= m_towerStats.m_attackRange && dist < closestDistance)
            {
                closestDistance = dist;
                m_currentTarget = minion;
            }
        }
    }
    private void UpdateLineToCurrentTarget()
    {
        if (m_currentTarget != null)
        {
            m_lineRenderer.enabled = true;
            m_lineRenderer.SetPosition(0, m_spawnPoint.position);
            m_lineRenderer.SetPosition(1, m_currentTarget.transform.position);
        }
        else
        {
            m_lineRenderer.enabled = false;
        }
    }

    private void AttackCurrentTarget()
    {
        if (m_currentTarget.GetComponent<Health>().m_destroyed == true)
            m_currentTarget = null;

        else
        {
            GameObject projectileSpawn = Instantiate(m_projectilePrefab, m_spawnPoint.position, Quaternion.identity, this.transform);
            Projectile projectile = projectileSpawn.GetComponent<Projectile>();
            projectile.m_damage = m_towerStats.m_damage;
            projectile.m_speed = m_towerStats.m_projectileSpeed;
            projectile.SeekTarget(m_currentTarget.transform);
        }
    }
}
