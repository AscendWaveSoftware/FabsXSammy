//Code by Fabian Schmiedel

using UnityEngine;
using UnityEngine.Events;
using static UnityEngine.ParticleSystem;

[RequireComponent(typeof(Health_Building))]
public class Tower : MonoBehaviour
{
    public Team m_team;
    [Header("Resource Data")]
    [SerializeField] private AI_Manager m_aiResources;
    [SerializeField] private PlayerResources m_resources;

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
    }

    private void Start()
    {
        if (m_towerStats)
            m_currentHealth = m_towerStats.m_targetHealth;

        if (m_towerStats != null && MOBA_Manager.Instance != null)
        {
            m_currentHealth = m_towerStats.m_targetHealth;
            MOBA_Manager.Instance.SetSlider(m_currentHealth, m_towerStats.m_isEnemyBuilding);
        }

        m_aiResources = FindAnyObjectByType<AI_Manager>();
        m_resources = FindAnyObjectByType<PlayerResources>();
    }

    public void OnDamage(float _damage)
    {
        m_currentHealth -= _damage;

        if (m_currentHealth <= 0)
            TowerDestroyed();

        else
        {
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
        {
            EntityManager.BlueTowers.Remove(this);
            m_resources.AddScrap(1000);
        }
        else
        {
            EntityManager.RedTowers.Remove(this);
            m_aiResources.AddScrap(1000);
        }
    }

    private void TowerDestroyed()
    {
        if (MOBA_Manager.Instance != null)
            MOBA_Manager.Instance.DestroyedTower(new Vector3(this.transform.position.x, -0.5f, this.transform.position.z), m_towerStats.m_isEnemyBuilding);

        UnregisterTower();
    }
}