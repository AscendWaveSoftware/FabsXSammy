using UnityEngine;
using UnityEngine.AI;

public class MinionAI : MonoBehaviour
{
    private NavMeshAgent m_agent;
    private Transform m_currentTarget;

    public string m_enemyMinionTag = "EnemyMinion";
    public string m_turretTag = "EnemyTower";
    public float m_stopDistance = 2.0f;
    public float m_detectRange = 5.0f;
    public float m_targetSwitchInterval = 2.0f;

    private float m_timeSinceLastTarget = 0;

    void Start()
    {
        m_agent = GetComponent<NavMeshAgent>();
        FindAndSetTarget();
    }

    void Update()
    {
        m_timeSinceLastTarget += Time.deltaTime;

        if (m_timeSinceLastTarget >= m_targetSwitchInterval)
        {
            FindAndSetTarget();
            m_timeSinceLastTarget = 0.0f;
        }

        if (m_currentTarget != null)
        {
            Vector3 directionToTarget = m_currentTarget.position - transform.position;
            Vector3 stopPosition = m_currentTarget.position - directionToTarget.normalized * m_stopDistance;

            m_agent.SetDestination(stopPosition);
        }
    }

    private void FindAndSetTarget()
    {
        GameObject[] enemyMinions = GameObject.FindGameObjectsWithTag(m_enemyMinionTag);
        Transform closestEnemy = GetClosesObjectInRadius(enemyMinions, m_detectRange);

        if (closestEnemy != null)
        {
            m_currentTarget = closestEnemy;
        }

        else
        {
            GameObject[] turrets = GameObject.FindGameObjectsWithTag(m_turretTag);
            m_currentTarget = GetClosestObject(turrets);
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
