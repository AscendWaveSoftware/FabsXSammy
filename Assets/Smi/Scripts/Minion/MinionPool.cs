using UnityEngine;
using System.Collections.Generic;

public class MinionPool : MonoBehaviour
{
    public GameObject m_minionPrefab;
    public float m_moveSpeed;
    public float m_spawnInterval, m_spawnDelay;   
    private float m_timer;

    private Queue<GameObject> m_spawnPool = new Queue<GameObject>();

    public GameObject GetMinion()
    {
        if (m_spawnPool.Count > 0)
        {
            GameObject minion = m_spawnPool.Dequeue();
            minion.SetActive(true);
            return minion;
        }
        var health = m_minionPrefab.GetComponent<Health>();
        health.m_minionPool = this;
        return Instantiate(m_minionPrefab, transform.position, Quaternion.identity, this.transform);
    }

    public void ReturnMinion(GameObject _minion)
    {
        _minion.SetActive(false);
        _minion.transform.position = transform.position;
        _minion.transform.rotation = transform.rotation;
        m_spawnPool.Enqueue(_minion);
    }
    void Update()
    {
        m_timer += Time.deltaTime;
        if (m_timer >= 5)
        {
            GetMinion();
            m_timer = 0f;
        }
    }
}
