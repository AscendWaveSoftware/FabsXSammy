using UnityEngine;
using System.Collections;

public class AI_Manager : MonoBehaviour
{
    [Header("Units")]
    [SerializeField] private GameObject[] m_units;
    [SerializeField] private Factory factory;

    [Header("Spawn Timing")]
    [SerializeField] private float m_minSpawnTime = 5f;
    [SerializeField] private float m_maxSpawnTime = 15f;
    [SerializeField] private float m_randomUnitSpawnTime = 60f;

    private float m_checkTime;

    [Header("References")]
    private PlayerResources m_playerResources;
    [SerializeField] private SO_CurrencySystem m_prices;

    [Header("AI State")]
    [SerializeField] private int m_pendingUnits = 0;
    private int m_lastThreshold = 0;
    private float aiTimer = 0f;
    private const float AI_INTERVAL = 0.5f;

    int cost;
    int current;
    int unit;

    private bool m_isSpawning = false;

    private void Start()
    {
        m_playerResources = FindAnyObjectByType<PlayerResources>();
        cost = m_prices.m_PriceForUnit1;

        StartCoroutine(RandomSpawn());
    }

    private IEnumerator RandomSpawn()
    {

        yield return new WaitForSeconds(m_randomUnitSpawnTime);

        SpawnMinion();

    }

    void Update()
    {
        aiTimer += Time.deltaTime;
        if (aiTimer >= AI_INTERVAL)
        {
            CheckScrap();
            HandleSpawning();
            aiTimer = 0f;
        }
    }

    private void CheckScrap()
    {
        current = m_playerResources.CurrentScrap;

        int currentThreshold = current / cost;

        if (currentThreshold < m_lastThreshold)
        {
            m_lastThreshold = currentThreshold;
            return;
        }

        if (currentThreshold > m_lastThreshold)
        {
            int diff = currentThreshold - m_lastThreshold;
            m_pendingUnits += diff;
            m_lastThreshold = currentThreshold;
        }
    }

    private void HandleSpawning()
    {
        if (m_isSpawning)
            return;

        if (m_pendingUnits <= 0)
            return;

        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        m_isSpawning = true;

        yield return new WaitForSeconds(m_checkTime);

        int groupSize = Random.value < 0.5f ? 3 : 5;
        int bigUnits = m_pendingUnits / groupSize;
        int smallUnits = m_pendingUnits % groupSize;

        for (int i = 0; i < bigUnits; i++)
            factory.GetMinion(m_units[1]);

        for (int i = 0; i < smallUnits; i++)
            factory.GetMinion(m_units[0]);

        m_pendingUnits = 0;

        m_checkTime = Random.Range(m_minSpawnTime, m_maxSpawnTime);
        m_isSpawning = false;
    }

    private void SpawnMinion()
    {
        if (factory == null || m_units.Length == 0)
            return;

        factory.GetMinion(m_units[unit]);
    }
}