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

    private float m_checkTime;

    [Header("References")]
    private PlayerResources m_playerResources;
    [SerializeField] private SO_CurrencySystem m_prices;

    [Header("AI State")]
    [SerializeField] private int m_pendingUnits = 0;
    private int m_lastThreshold = 0;

    int cost;
    int current;

    private bool m_isSpawning = false;

    private Coroutine m_spawnCoroutine;

    private void Start()
    {
        m_playerResources = FindAnyObjectByType<PlayerResources>();

        cost = m_prices.m_PriceForUnit1;
    }

    private void Update()
    {
        CheckScrap();
        HandleSpawning();
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
        for (int i = 0; i < m_pendingUnits; i++)
        {
            SpawnMinion();
            m_pendingUnits--;
        }

        m_checkTime = Random.Range(m_minSpawnTime, m_maxSpawnTime);
        m_isSpawning = false;
    }

    private void SpawnMinion()
    {
        if (factory == null || m_units.Length == 0)
            return;

        factory.GetMinion(m_units[0]);
    }
}