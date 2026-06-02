using UnityEngine;

public class AI_Manager : MonoBehaviour
{
    [SerializeField] GameObject[] m_units;
    [SerializeField] float m_spawnTime = 5f;

    private float m_timer;
    SpawnManager m_unitSpawner;

    private void Start()
    {
        m_unitSpawner = FindAnyObjectByType<SpawnManager>();
        m_unitSpawner.m_spawnID = 1;
    }

    void Update()
    {

        m_timer += Time.deltaTime;
        if (m_timer >= m_spawnTime)
        {
            m_unitSpawner.GetMinion(m_units[0]);
            m_timer = 0f;
        }
    }
}
