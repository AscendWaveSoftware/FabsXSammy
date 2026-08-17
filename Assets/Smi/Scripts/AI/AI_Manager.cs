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
    [SerializeField] private int m_startIncomeAmount;
    [SerializeField] private float m_incomeInterval = 5f;
    [SerializeField] private int m_maxIncomeAmount = 20;

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

    private bool m_wantsBigUnit;

    private void Start()
    {
        m_playerXP = FindAnyObjectByType<PlayerExperience>();
        m_startIncomeAmount = m_incomeAmount;
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
            if (m_incomeAmount < m_maxIncomeAmount)
                m_incomeAmount = m_startIncomeAmount + m_playerXP.CurrentLevel;

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

        yield return new WaitForSeconds(
            Random.Range(m_minSpawnTime, m_maxSpawnTime)
        );

        int spawnedUnits = 0;

        bool wantsBigUnit = RollForBigUnit();

        if (wantsBigUnit)
        {
            if (m_aiScrap < m_prices.m_PriceForUnit2)
            {
                m_isSpawning = false;
                yield break;
            }

            factory.GetMinion(m_units[1]);
            m_aiScrap -= m_prices.m_PriceForUnit2;

            spawnedUnits++;

            yield return new WaitForSeconds(m_spawnDelay);
        }

        while (m_aiScrap >= m_prices.m_PriceForUnit1 &&
               spawnedUnits < m_maxUnitsPerWave)
        {
            factory.GetMinion(m_units[0]);
            m_aiScrap -= m_prices.m_PriceForUnit1;

            spawnedUnits++;

            yield return new WaitForSeconds(m_spawnDelay);
        }

        m_isSpawning = false;
    }

    private bool RollForBigUnit()
    {
        if (!m_playerXP)
            return false;

        if (m_playerXP.CurrentLevel < m_bigUnitUnlockLevel)
            return false;

        float chance = Mathf.Clamp(m_startBigUnitChance + ((m_playerXP.CurrentLevel - m_bigUnitUnlockLevel) * m_bigChanceIncreasePerLevel), 0f, m_maxBigUnitChance);

        return Random.value < chance;
    }
}