using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class Turret : MonoBehaviour
{
    [SerializeField] private SO_TowerStats m_towerStats;

    public GameObject m_projectilePrefab;
    public Transform m_projectileSpawnPoint;

    private LineRenderer m_lineRenderer;

    private float m_nextAttackTime;
    private GameObject m_currentTarget;

    private Team m_team;

    private void Awake()
    {
        m_lineRenderer = GetComponent<LineRenderer>();
        m_team = m_towerStats.m_team;
    }

    private void Update()
    {
        if (m_currentTarget == null || !IsTargetInRange(m_currentTarget))
        {
            FindNewTarget();
        }

        UpdateLineToCurrentTarget();

        if (m_currentTarget == null)
            return;

        RotateTowardsTarget();

        if (Time.time >= m_nextAttackTime)
        {
            AttackCurrentTarget();
            m_nextAttackTime = Time.time + m_towerStats.m_attackCooldown;
        }
    }

    private bool IsTargetInRange(GameObject target)
    {
        float distSqr =
            (target.transform.position - transform.position).sqrMagnitude;

        float rangeSqr =
            m_towerStats.m_attackRange * m_towerStats.m_attackRange;

        return distSqr <= rangeSqr;
    }

    private void FindNewTarget()
    {
        List<AI_Minion> enemies =
            m_team == Team.Blue
                ? EntityManager.RedMinions
                : EntityManager.BlueMinions;

        float closestDistSqr = float.MaxValue;
        GameObject closest = null;

        Vector3 pos = transform.position;
        float rangeSqr = m_towerStats.m_attackRange * m_towerStats.m_attackRange;

        foreach (AI_Minion minion in enemies)
        {
            if (minion == null) continue;

            float distSqr =
                (minion.transform.position - pos).sqrMagnitude;

            if (distSqr <= rangeSqr && distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                closest = minion.gameObject;
            }
        }

        m_currentTarget = closest;
    }

    private void RotateTowardsTarget()
    {
        Vector3 targetPos = m_currentTarget.transform.position;
        targetPos.y = transform.position.y;

        transform.LookAt(targetPos);
        transform.Rotate(0f, -90f, 0f);
    }

    private void UpdateLineToCurrentTarget()
    {
        if (m_currentTarget != null)
        {
            m_lineRenderer.enabled = true;

            m_lineRenderer.SetPosition(0, m_projectileSpawnPoint.position);
            m_lineRenderer.SetPosition(1, m_currentTarget.transform.position);
        }
        else
        {
            m_lineRenderer.enabled = false;
        }
    }

    private void AttackCurrentTarget()
    {
        if (m_currentTarget == null)
            return;

        MOBA_Health health = m_currentTarget.GetComponent<MOBA_Health>();

        if (health != null && health.m_destroyed)
        {
            m_currentTarget = null;
            return;
        }

        else if (!health.m_destroyed)
        {
            GameObject projectileObj =
             Instantiate(
                 m_projectilePrefab,
                 m_projectileSpawnPoint.position,
                 Quaternion.identity
             );

            Projectile projectile = projectileObj.GetComponent<Projectile>();

            projectile.m_damage = m_towerStats.m_damage;
            projectile.m_speed = m_towerStats.m_projectileSpeed;
            projectile.SeekTarget(m_currentTarget.transform);
        }
    }
}