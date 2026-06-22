using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MOBA_Health))]
public class Factory : MonoBehaviour, IMinionPool
{
    private Dictionary<GameObject, Queue<GameObject>> m_spawnPools =
        new Dictionary<GameObject, Queue<GameObject>>();

    [SerializeField] private SO_FactoryStats m_stats;
    private float m_currentHealth;

    private void OnEnable()
    {
        RegisterFactory();
    }

    private void Start()
    {
        if (m_stats != null && MOBA_Manager.Instance != null)
        {
            m_currentHealth = m_stats.m_targetHealth;
            MOBA_Manager.Instance.SetSlider(m_currentHealth, m_stats.m_isEnemyBuilding);
        }
    }

    public void OnDamage(float _damage)
    {
        MOBA_Manager.Instance.OnDamage(_damage, m_stats.m_isEnemyBuilding);
    }

    private void LoseState()
    {
        Debug.Log("YOU LOSE");
    }

    private void RegisterFactory()
    {
        if (m_stats.m_team == Team.Blue)
            EntityManager.BlueFactory = this;
        else
            EntityManager.RedFactory = this;
    }

    public GameObject GetMinion(GameObject minionPrefab)
    {
        Queue<GameObject> pool = GetPool(minionPrefab);

        if (pool.Count > 0)
        {
            GameObject minion = pool.Dequeue();
            minion.SetActive(true);
            return minion;
        }

        GameObject newMinion = Instantiate(
            minionPrefab,
            transform.position,
            Quaternion.identity,
            transform
        );

        var health = newMinion.GetComponent<MOBA_Health>();
        if (health != null)
            health.SetPool(this, minionPrefab);

        return newMinion;
    }

    public void Return(GameObject minion, GameObject prefab)
    {
        minion.SetActive(false);

        minion.transform.position = transform.position;
        minion.transform.rotation = transform.rotation;
        minion.transform.SetParent(transform);

        GetPool(prefab).Enqueue(minion);
    }

    private Queue<GameObject> GetPool(GameObject prefab)
    {
        if (!m_spawnPools.TryGetValue(prefab, out Queue<GameObject> pool))
        {
            pool = new Queue<GameObject>();
            m_spawnPools[prefab] = pool;
        }

        return pool;
    }

    private void OnDestroy()
    {
        if (m_stats.m_isEnemyBuilding)
        {
            PlaytestAnalyticsManager.Instance.RegisterVictory();
            PlaytestAnalyticsManager.Instance.EndRun();
        }
        else
            LoseState();
    }

}