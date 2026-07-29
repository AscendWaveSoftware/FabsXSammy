using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(MOBA_Health))]
public class Tower : MonoBehaviour
{
    public Team m_team;
    [SerializeField] private SO_TowerStats m_towerStats;
    public UnityEvent OnDamagedEvent;
    [SerializeField] private GameObject OnDestroyed;

    [SerializeField] private float currentHealth;


    private bool m_bIsDamaged = false;

    private void OnEnable()
    {
        RegisterTower();
    }

    private void Start()
    {
        if (MOBA_Manager.Instance != null)
        {
            if (m_team == Team.Red)
                MOBA_Manager.Instance.SetSlider(currentHealth, true);
            else
                MOBA_Manager.Instance.SetSlider(currentHealth, false);
        }

        if (m_towerStats)
            currentHealth = m_towerStats.m_targetHealth;
    }

    public void OnDamage(float _damage)
    {
        currentHealth -= _damage;

        if (MOBA_Manager.Instance != null)
        {
            MOBA_Manager.Instance.OnDamage(_damage, m_towerStats.m_isEnemyBuilding);
        }

        if (currentHealth <= m_towerStats.m_targetHealth / 2 && !m_bIsDamaged)
        {
            OnDamagedEvent?.Invoke();
            m_bIsDamaged = true;
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
        MOBA_Manager.Instance.DestroyedTower(new Vector3(this.transform.position.x, -0.5f, this.transform.position.z), m_towerStats.m_isEnemyBuilding);
        UnregisterTower();
    }
}