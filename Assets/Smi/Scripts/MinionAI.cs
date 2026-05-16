using UnityEngine;
using UnityEngine.AI;
[RequireComponent(typeof(MinionCombat))]
[RequireComponent(typeof(Health))]
public class MinionAI : MonoBehaviour
{
    public Transform m_currentTarget;
    [SerializeField] public MinionStats m_stats;

    private NavMeshAgent m_agent;
    private Health m_health;

    private float m_timeSinceLastTarget = 0;

    void Awake()
    {
        m_agent = GetComponent<NavMeshAgent>();
        m_health = GetComponent<Health>();
        m_health.m_currentHealth = m_stats.m_health;
        FindAndSetTarget();
    }

    void Update()
    {
        m_timeSinceLastTarget += Time.deltaTime;

        if (m_timeSinceLastTarget >= m_stats.m_targetSwitchInterval)
        {
            FindAndSetTarget();
            m_timeSinceLastTarget = 0.0f;
        }

        if (m_currentTarget != null)
        {
            Vector3 directionToTarget = m_currentTarget.position - transform.position;
            Vector3 stopPosition = m_currentTarget.position - directionToTarget.normalized * m_stats.m_stopDistance;

            m_agent.SetDestination(stopPosition);
        }
    }

    private void FindAndSetTarget()
    {
        GameObject[] enemyMinions = GameObject.FindGameObjectsWithTag(m_stats.m_enemyMinionTag);
        Transform closestEnemy = GetClosesObjectInRadius(enemyMinions, m_stats.m_detectRange);

        if (closestEnemy != null)
            m_currentTarget = closestEnemy;

        else
        {
            GameObject[] tower = GameObject.FindGameObjectsWithTag(m_stats.m_enemyTowerTag);
            m_currentTarget = GetClosestObject(tower);
        }
    }


    private Transform GetClosestObject(GameObject[] _objects)
    {
        float closestDistance = Mathf.Infinity;
        Transform closesObject = null;
        Vector3 currentPosition = transform.position;

        foreach (GameObject obj in _objects)
        {
            float distance = Vector3.Distance(currentPosition, obj.transform.position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closesObject = obj.transform;
            }
        }
        return closesObject;
    }

    private Transform GetClosesObjectInRadius(GameObject[] _objects, float _detectRange)
    {
        float closestDistance = Mathf.Infinity;
        Transform closesObject = null;
        Vector3 currentPosition = transform.position;

        foreach (GameObject obj in _objects)
        {
            float distance = Vector3.Distance(currentPosition, obj.transform.position);

            if (distance < closestDistance && distance <= _detectRange)
            {
                closestDistance = distance;
                closesObject = obj.transform;
            }
        }
        return closesObject;
    }
}
