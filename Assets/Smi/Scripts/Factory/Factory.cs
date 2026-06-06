using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class Factory : MonoBehaviour, IMinionPool

{
    private Queue<GameObject> m_spawnPool = new Queue<GameObject>();
    [SerializeField] private SO_FactoryStats m_stats;

    private void OnEnable()
    {
        RegisterFactory();
    }

    private void OnDestroy()
    {
        LoseState();
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

    public GameObject GetMinion(GameObject _minionPrefab)
    {
        if (m_spawnPool.Count > 0)
        {
            GameObject minion = m_spawnPool.Dequeue();
            minion.SetActive(true);
            return minion;
        }

        GameObject newMinon = Instantiate(_minionPrefab, transform.position, Quaternion.identity, transform);

        var health = newMinon.GetComponent<Health>();
        if (health != null)
        {
            health.SetPool(this);
        }

        return newMinon;
    }

    public void Return(GameObject _minion)
    {
        _minion.SetActive(false);
        _minion.transform.position = transform.position;
        _minion.transform.rotation = transform.rotation;
        m_spawnPool.Enqueue(_minion);
    }
}