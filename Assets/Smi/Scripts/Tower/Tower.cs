using UnityEngine;
using UnityEngine.Events;
using static UnityEngine.ParticleSystem;

[RequireComponent(typeof(Health_Building))]
public class Tower : MonoBehaviour
{
    public Team m_team;
    [SerializeField] private SO_TowerStats m_towerStats;
    public UnityEvent OnDamagedEvent;
    [SerializeField] private GameObject OnDestroyed;

    [SerializeField] private ParticleSystem mainModule;
    [SerializeField] private MainModule main;

    [SerializeField] private float m_currentHealth;


    private bool m_bIsDamaged = false;

    private void OnEnable()
    {
        RegisterTower();
        main = mainModule.main;

        if (m_towerStats != null && MOBA_Manager.Instance != null)
        {
            m_currentHealth = m_towerStats.m_targetHealth;
            MOBA_Manager.Instance.SetSlider(m_currentHealth, m_towerStats.m_isEnemyBuilding);
        }
    }

    private void Start()
    {
        if (m_towerStats)
            m_currentHealth = m_towerStats.m_targetHealth;
    }

    public void OnDamage(float _damage)
    {
        m_currentHealth -= _damage;

        if (MOBA_Manager.Instance != null)
            MOBA_Manager.Instance.OnDamage(_damage, m_towerStats.m_isEnemyBuilding);

        if (m_currentHealth <= m_towerStats.m_targetHealth / 2)
        {
            if (mainModule)
                main.startLifetimeMultiplier = 10f * (1f - (m_currentHealth / m_towerStats.m_targetHealth));

            if (!m_bIsDamaged)
            {
                OnDamagedEvent?.Invoke();
                m_bIsDamaged = true;
            }
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
        if (MOBA_Manager.Instance != null)
            MOBA_Manager.Instance.DestroyedTower(new Vector3(this.transform.position.x, -0.5f, this.transform.position.z), m_towerStats.m_isEnemyBuilding);

        UnregisterTower();
    }
}