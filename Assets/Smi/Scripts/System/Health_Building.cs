//Code by Fabian Schmiedel

using UnityEngine;

public class Health_Building : MOBA_Health, IDamageable
{
    Tower m_tower;
    Factory m_factory;

    protected override void StartProcess()
    {
        m_tower = GetComponent<Tower>();
        m_factory = GetComponent<Factory>();
    }

    public void TakeDamage(float _damage)
    {
        if (!gameObject.activeInHierarchy)
            return;

        m_currentHealth -= _damage;
        m_currentHealth = Mathf.Max(m_currentHealth, 0);

        UpdateSlider(m_currentHealth);

        if (m_tower)
        {
            m_tower.OnDamage(_damage);
            if (m_currentHealth <= 0)
                gameObject.SetActive(false);
        }
        else if (m_factory)
            m_factory.OnDamage(_damage);
    }
}
