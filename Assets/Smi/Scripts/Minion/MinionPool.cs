using UnityEngine;
using System.Collections.Generic;

public class MinionPool : MonoBehaviour
{
    public GameObject m_minionPrefab;
    [SerializeField] float m_spawnTime = 5f;
    private float m_timer;

    private Queue<GameObject> m_spawnPool = new Queue<GameObject>();

    public GameObject GetMinion(GameObject _unit)
    {
        if (m_spawnPool.Count > 0)
        {
             _unit = m_spawnPool.Dequeue();
            _unit.SetActive(true);
            return _unit;
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
        if (m_timer >= m_spawnTime)
        {
            GetMinion(m_minionPrefab);
            m_timer = 0f;
        }
    }
}
