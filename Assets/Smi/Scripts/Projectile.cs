using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float m_speed = 10f;
    private Transform m_target;

    private Stats m_towerStats;

    private void Start()
    {
        m_towerStats = GetComponentInParent<Stats>();
    }

    public void SeekTarget(Transform _target)
    {
        m_target = _target;
    }

    private void Update()
    {
        if (m_target == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 dir = m_target.position - transform.position;
        float dist = m_speed * Time.deltaTime;

        if (dir.magnitude <= dist)
        {
            HitTarget();
            return;
        }
        transform.Translate(dir.normalized * dist, Space.World);
    }

    private void HitTarget()
    {
        Stats targetStats = m_target.gameObject.GetComponent<Stats>();
        targetStats?.TakeDamage(m_target.gameObject, m_towerStats._stats.m_damage);

        Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision _other)
    {
        if (_other.gameObject == m_target.gameObject)
        {
            HitTarget();
            Destroy(gameObject);
        }
    }
}
