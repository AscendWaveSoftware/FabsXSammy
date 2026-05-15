using UnityEngine;

public class UnitSpawner : MonoBehaviour
{
    public GameObject m_minionPrefab;
    float timer = 0f;

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= 5)
        {
            SpawnUnit(m_minionPrefab);
            timer = 0f;
        }
    }

    public void SpawnUnit(GameObject _unit)
    {
        Instantiate(_unit, transform.position, Quaternion.identity);
    }
}
