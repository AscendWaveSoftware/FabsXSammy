using UnityEngine;
using System.Collections;

public class AI_Manager : MonoBehaviour
{
    [Header("Units")]
    [SerializeField] private GameObject[] m_units;
    [SerializeField] private Factory factory;

    [Header("Spawn Timing")]
    [SerializeField] private float m_minSpawnTime = 8f;
    [SerializeField] private float m_maxSpawnTime = 15f;

    [Header("AI Economy")]
    [SerializeField] private int m_aiScrap = 0;
    [SerializeField] private int m_incomeAmount = 10;
    [SerializeField] private float m_incomeInterval = 5f;

    [Header("Wave Settings")]
    [SerializeField] private int m_maxUnitsPerWave = 4;
    [SerializeField] private float m_spawnDelay = 0.5f;

    [Header("Big Units")]
    [SerializeField] private int m_bigUnitUnlockLevel = 10;
    [SerializeField] private float m_startBigUnitChance = 0.15f;
    [SerializeField] private float m_bigChanceIncreasePerLevel = 0.03f;
    [SerializeField] private float m_maxBigUnitChance = 0.5f;

    [Header("References")]
    private PlayerExperience m_playerXP;

    [SerializeField] private SO_CurrencySystem m_prices;


    private float m_incomeTimer;
    private bool m_isSpawning;


    private void Start()
    {
        m_playerXP = FindAnyObjectByType<PlayerExperience>();
    }


    private void Update()
    {
        GenerateIncome();
        HandleSpawning();
    }


    private void GenerateIncome()
    {
        m_incomeTimer += Time.deltaTime;

        if (m_incomeTimer >= m_incomeInterval)
        {
            m_aiScrap += m_incomeAmount;
            m_incomeTimer = 0f;
        }
    }


    private void HandleSpawning()
    {
        if (m_isSpawning)
            return;

        if (m_aiScrap < m_prices.m_PriceForUnit1)
            return;

        StartCoroutine(SpawnRoutine());
    }


    private IEnumerator SpawnRoutine()
    {
        m_isSpawning = true;

        yield return new WaitForSeconds(Random.Range(m_minSpawnTime, m_maxSpawnTime));

        int spawnedUnits = 0;

        while (m_aiScrap >= m_prices.m_PriceForUnit1 &&
               spawnedUnits < m_maxUnitsPerWave)
        {
            bool spawnBig = CanSpawnBigUnit();


            if (spawnBig)
            {
                factory.GetMinion(m_units[1]);
                m_aiScrap -= m_prices.m_PriceForUnit2;
            }
            else
            {
                factory.GetMinion(m_units[0]);
                m_aiScrap -= m_prices.m_PriceForUnit1;
            }
            spawnedUnits++;

            yield return new WaitForSeconds(m_spawnDelay);
        }

        m_isSpawning = false;
    }


    private bool CanSpawnBigUnit()
    {
        if (m_playerXP.CurrentLevel < m_bigUnitUnlockLevel)
            return false;

        if (m_aiScrap < m_prices.m_PriceForUnit2)
            return false;

        float chance = Mathf.Clamp(m_startBigUnitChance + ((m_playerXP.CurrentLevel - m_bigUnitUnlockLevel) * m_bigChanceIncreasePerLevel), 0f, m_maxBigUnitChance);

        return Random.value < chance;
    }
}