using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject[] enemyPrefabs;

    [Header("Spawn Ponits")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Spawn Settings")]
    [SerializeField] private int maxEnemiesAlive = 7;
    [SerializeField] private float spawnInterval = 3f;
    [SerializeField] private bool spawnOnStart = true;

    [Header("Target")]
    [SerializeField] private Transform playerTarget;

    private float m_spawnTimer;
    private int m_currentEnemiesAlive;

    private void Start()
    {
        if(playerTarget == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if(playerObject != null)
                playerTarget = playerObject.transform;
        }

        if (spawnOnStart)
            SpawnEnemy();
    }

    private void Update()
    {
        HandleSpawning();
    }

    public void NotifyEnemyDied()
    {
        m_currentEnemiesAlive--;

        if (m_currentEnemiesAlive < 0)
            m_currentEnemiesAlive = 0;
    }

    private void HandleSpawning()
    {
        if (m_currentEnemiesAlive >= maxEnemiesAlive)
            return;

        m_spawnTimer += Time.deltaTime;

        if(m_spawnTimer >= spawnInterval)
        {
            m_spawnTimer = 0;
            SpawnEnemy();
        }
    }

    private void SpawnEnemy()
    {
        if(enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            Debug.LogWarning("No enemy prefabs assigned to EnemySpawner.");
            return;
        }

        if(spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("No spawn points assigned to EnemySpawner.");
            return;
        }

        GameObject selectedEnemyPrefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        Transform selectedSpawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

        GameObject spawnedEnemy = Instantiate(
                selectedEnemyPrefab,
                selectedSpawnPoint.position,
                selectedSpawnPoint.rotation
        );

        m_currentEnemiesAlive++;

        EnemyMovement enemyMovement = spawnedEnemy.GetComponent<EnemyMovement>();

        if (enemyMovement != null)
            enemyMovement.SetPlayerTarget(playerTarget);

        EnemyDeathNotifier deathNotifier = spawnedEnemy.GetComponent<EnemyDeathNotifier>();

        if (deathNotifier == null)
            deathNotifier = spawnedEnemy.AddComponent<EnemyDeathNotifier>();

        deathNotifier.Initialize(this);
    }
    
}
