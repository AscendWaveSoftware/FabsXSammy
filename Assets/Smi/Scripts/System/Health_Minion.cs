using UnityEngine;
using System.Collections;

public class Health_Minion : MOBA_Health, IDamageable
{

    private IMinionPool m_minionPool;
    private GameObject m_prefab;
    AI_Minion m_aiMinion;

    protected override void StartProcess()
    {
        m_aiMinion = GetComponent<AI_Minion>();
        StartCoroutine(MinionAlive());
    }

    IEnumerator MinionAlive()
    {
        yield return new WaitForSeconds(3);
        m_destroyed = false;
    }


    public void TakeDamage(float _damage)
    {
        if (!gameObject.activeInHierarchy)
            return;

        m_currentHealth -= _damage;
        m_currentHealth = Mathf.Max(m_currentHealth, 0);

        UpdateSlider(m_currentHealth);

        if (m_currentHealth <= 0)
        {
            if (m_minionPool != null)
                m_minionPool.Return(gameObject, m_prefab);
        }
    }

        public void SetPool(IMinionPool pool, GameObject prefab)
    {
        m_minionPool = pool;
        m_prefab = prefab;
    }

}
