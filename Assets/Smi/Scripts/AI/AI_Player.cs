using UnityEngine;

public class AI_Player : MonoBehaviour
{
    [SerializeField] GameObject[] m_units;
    [SerializeField] float m_spawnTime = 5f;

    private float m_timer;
    [SerializeField] SpawnManager m_unitSpawner;


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
