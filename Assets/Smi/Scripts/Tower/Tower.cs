using UnityEngine;

[RequireComponent(typeof(MOBA_Health))]
public class Tower : MonoBehaviour
{
    public Team m_team;
    [SerializeField] private SO_TowerStats m_towerStats;

    private float currentHealth;

    private void OnEnable()
    {
        RegisterTower();
    }

    private void Start()
    {
        if (m_towerStats != null && MOBA_Manager.Instance != null)
        {
            currentHealth = m_towerStats.m_targetHealth;
            MOBA_Manager.Instance.SetSlider(currentHealth, m_towerStats.m_isEnemyBuilding);
        }
    }

    private void RegisterTower()
    {
        if (m_team == Team.Blue)
            EntityManager.BlueTowers.Add(this);
        else
            EntityManager.RedTowers.Add(this);
    }

    private void UnregisterTower()
    {
        if (m_team == Team.Blue)
            EntityManager.BlueTowers.Remove(this);
        else
            EntityManager.RedTowers.Remove(this);
    }

    private void OnDisable()
    {
        UnregisterTower();
    }
}