using UnityEngine;
using System.Collections.Generic;

public class SpawnManager : MonoBehaviour
{
    private Queue<GameObject> m_spawnPool = new Queue<GameObject>();
    [SerializeField] private GameObject[] m_spawners;
    public int m_spawnID;

    public GameObject GetMinion(GameObject _unit)
    {
        if (m_spawnPool.Count > 0)
        {
             _unit = m_spawnPool.Dequeue();
            _unit.SetActive(true);
            return _unit;
        }

        var health = _unit.GetComponent<MOBA_Health>();
      //  health.m_minionPool = this;
        return Instantiate(_unit, m_spawners[m_spawnID].transform.position, Quaternion.identity, this.transform);
    }

    public void ReturnMinion(GameObject _minion)
    {
        _minion.SetActive(false);
        _minion.transform.position = m_spawners[m_spawnID].transform.position;
        _minion.transform.rotation = m_spawners[m_spawnID].transform.rotation;
        m_spawnPool.Enqueue(_minion);
    }
}
