using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float m_speed { private get; set; }
    public float m_damage { private get; set; }

    private Transform m_target;

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

        transform.Translate(dir.normalized * dist, Space.World);
    }

    private void OnCollisionEnter(Collision _other)
    {
        if (_other.gameObject == m_target.gameObject)
        {
            _other.gameObject.TryGetComponent<IDamageable>(out var target);
            target.TakeDamage(m_damage);
            Destroy(gameObject);
        }
    }
}
