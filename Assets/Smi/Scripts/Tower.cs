using UnityEngine;

public class Tower : MonoBehaviour
{
    public float m_attackRange = 5f;
    public float m_attackCooldown = 2f;
    public GameObject m_projectilePrefab;
    public Transform m_spawnPoint;
    public LineRenderer m_lineRenderer;

    private float m_nextAttackTime = 0;
    private GameObject m_currentTarget;

    void Update()
    {
        if (m_currentTarget == null || Vector3.Distance(transform.position, m_currentTarget.transform.position) > m_attackRange)
        {
            FindNewTarget();
        }

        UpdateLineToCurrentTarget();

        if (Time.time >= m_nextAttackTime)
        {
            AttackCurrentTarget();
            m_nextAttackTime = Time.time + m_attackCooldown;
        }
    }
    private void FindNewTarget()
    {
        GameObject[] minions = GameObject.FindGameObjectsWithTag("AllyMinion");
        float closestDistance = float.MaxValue;

        foreach (GameObject minion in minions)
        {
            float dist = Vector3.Distance(transform.position, minion.transform.position);
            if (dist <= m_attackRange && dist < closestDistance)
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
        if (m_currentTarget != null)
        {
            GameObject projectileSpawn = Instantiate(m_projectilePrefab, m_spawnPoint.position, Quaternion.identity, this.transform);
            Projectile projectile = projectileSpawn.GetComponent<Projectile>();
            projectile.SeekTarget(m_currentTarget.transform);
        }
    }
}
