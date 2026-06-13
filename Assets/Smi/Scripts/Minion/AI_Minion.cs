using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
[RequireComponent(typeof(MinionCombat))]
[RequireComponent(typeof(MOBA_Health))]
public class AI_Minion : MonoBehaviour
{
    public Transform m_currentTarget;
    [SerializeField] public SO_MinionStats m_stats;

    private NavMeshAgent m_agent;

    private float m_timeSinceLastTarget = 0;
    private float m_destinationUpdateTimer;


    private void OnEnable()
    {
        m_agent = GetComponent<NavMeshAgent>();

        m_agent.speed = m_stats.m_moveSpeed;

        if (m_stats.m_team == Team.Blue)
            EntityManager.BlueMinions.Add(this);
        else
            EntityManager.RedMinions.Add(this);

        FindAndSetTarget();
    }
    void Update()
    {
        m_timeSinceLastTarget += Time.deltaTime;
        m_destinationUpdateTimer += Time.deltaTime;

        if (m_timeSinceLastTarget >= m_stats.m_targetSwitchInterval)
        {
            FindAndSetTarget();
            m_timeSinceLastTarget = 0.0f;
        }

        if (m_destinationUpdateTimer >= 0.2f)
        {
            UpdateDestination();
            m_destinationUpdateTimer = 0.0f;
        }
    }

    private void UpdateDestination()
    {
        if (m_currentTarget == null)
            return;

        Vector3 directionToTarget =
            m_currentTarget.position - transform.position;

        Vector3 stopPosition =
            m_currentTarget.position -
            directionToTarget.normalized * m_stats.m_stopDistance;

        m_agent.SetDestination(stopPosition);
    }

    private void FindAndSetTarget()
    {
        List<AI_Minion> enemyMinions =
            m_stats.m_team == Team.Blue
                ? EntityManager.RedMinions
                : EntityManager.BlueMinions;

        Transform closestEnemy =
            GetClosestMinionInRadius(enemyMinions, m_stats.m_detectRange);

        if (closestEnemy != null)
        {
            m_currentTarget = closestEnemy;
            return;
        }

        List<Tower> enemyTowers =
           m_stats.m_team == Team.Blue
                ? EntityManager.RedTowers
                : EntityManager.BlueTowers;

        Transform closestTower = GetClosestTower(enemyTowers);

        if (closestTower != null)
        {
            m_currentTarget = closestTower;
            return;
        }

        Factory factory = 
            m_stats.m_team == Team.Blue
                ? EntityManager.RedFactory
                : EntityManager.BlueFactory;

        if (!factory)
        {
            m_currentTarget = null;
            return;
        }
        m_currentTarget = factory.transform;
    }

    private Transform GetClosestMinionInRadius(List<AI_Minion> _minions, float _detectRange)
    {
        float closestDistanceSqr = Mathf.Infinity;
        Transform closest = null;

        Vector3 currentPosition = transform.position;
        float detectRangeSqr = _detectRange * _detectRange;

        foreach (AI_Minion minion in _minions)
        {
            if (minion == null) continue;

            float distanceSqr =
                (minion.transform.position - currentPosition).sqrMagnitude;

            if (distanceSqr <= detectRangeSqr && distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                closest = minion.transform;
            }
        }

        return closest;
    }

    private Transform GetClosestTower(List<Tower> _towers)
    {
        float closestDistanceSqr = Mathf.Infinity;
        Transform closest = null;

        Vector3 currentPosition = transform.position;

        foreach (Tower tower in _towers)
        {
            if (tower == null) continue;

            float distanceSqr =
                (tower.transform.position - currentPosition).sqrMagnitude;

            if (distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                closest = tower.transform;
            }
        }

        return closest;
    }
}
