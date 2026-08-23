using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject[] enemyPrefabs;

    [Header("Spawn Ponits")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Spawn Settings")]
    [SerializeField, Tooltip("Enemies allowed in the arena at the same time when a run starts.")]
    private int maxEnemiesAlive = 7;
    [SerializeField, Tooltip("Seconds between two spawns when a run starts.")]
    private float spawnInterval = 3f;
    [SerializeField] private bool spawnOnStart = true;

    [Header("Difficulty Scaling")]
    [SerializeField, Min(0f), Tooltip("Seconds of play until the arena reaches its late game pressure. 0 starts there right away.")]
    private float difficultyRampDuration = 300f;
    [SerializeField, Min(1), Tooltip("Enemies allowed at the same time once the ramp is finished.")]
    private int lateGameMaxEnemiesAlive = 20;
    [SerializeField, Min(0.05f), Tooltip("Seconds between two spawns once the ramp is finished.")]
    private float lateGameSpawnInterval = 1f;
    [SerializeField, Min(1f), Tooltip("Enemy health once the ramp is finished, relative to the prefab. Keeps a pure damage build from one shotting everything for the whole run.")]
    private float lateGameHealthMultiplier = 2.8f;
    [SerializeField, Min(1f), Tooltip("Enemy attack damage once the ramp is finished, relative to the prefab. This is what eventually makes defensive upgrades worth taking.")]
    private float lateGameDamageMultiplier = 1.8f;
    [SerializeField, Tooltip("Shapes the ramp between the starting and the late game values.")]
    private AnimationCurve difficultyCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Target")]
    [SerializeField] private Transform playerTarget;

    private float m_spawnTimer;
    private float m_elapsedRunTime;
    private float m_difficultyProgress;
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
        // No spawning and no difficulty ramp while the player is on the tower
        // camera, so the arena is exactly as they left it.
        if (PveRuntime.IsPaused)
            return;

        // Scaled time on purpose: a level up pause or the combat hit slow motion
        // must not push the difficulty forward while the player cannot play.
        m_elapsedRunTime += Time.deltaTime;
        m_difficultyProgress = CalculateDifficultyProgress();

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
        if (m_currentEnemiesAlive >= GetCurrentMaxEnemiesAlive())
            return;

        m_spawnTimer += Time.deltaTime;

        if(m_spawnTimer >= GetCurrentSpawnInterval())
        {
            m_spawnTimer = 0;
            SpawnEnemy();
        }
    }

    private float CalculateDifficultyProgress()
    {
        if (difficultyRampDuration <= 0f)
            return 1f;

        float rampProgress = Mathf.Clamp01(m_elapsedRunTime / difficultyRampDuration);

        // A curve without keys evaluates to zero and would silently freeze the
        // difficulty at its starting values, so fall back to a linear ramp.
        if (difficultyCurve == null || difficultyCurve.length < 2)
            return rampProgress;

        return Mathf.Clamp01(difficultyCurve.Evaluate(rampProgress));
    }

    private int GetCurrentMaxEnemiesAlive()
    {
        float scaledMaximum = Mathf.Lerp(maxEnemiesAlive, lateGameMaxEnemiesAlive, m_difficultyProgress);
        return Mathf.Max(1, Mathf.RoundToInt(scaledMaximum));
    }

    private float GetCurrentSpawnInterval()
    {
        float scaledInterval = Mathf.Lerp(spawnInterval, lateGameSpawnInterval, m_difficultyProgress);
        return Mathf.Max(0.05f, scaledInterval);
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

        // Applied before anything else reads the stats, so the health bar and the
        // first attack already use the scaled values.
        EnemyStats enemyStats = spawnedEnemy.GetComponent<EnemyStats>();

        if (enemyStats != null)
        {
            enemyStats.ApplyDifficultyScaling(
                Mathf.Lerp(1f, lateGameHealthMultiplier, m_difficultyProgress),
                Mathf.Lerp(1f, lateGameDamageMultiplier, m_difficultyProgress)
            );
        }

        EnemyMovement enemyMovement = spawnedEnemy.GetComponent<EnemyMovement>();

        if (enemyMovement != null)
            enemyMovement.SetPlayerTarget(playerTarget);

        EnemyAttack enemyAttack = spawnedEnemy.GetComponent<EnemyAttack>();

        if (enemyAttack != null)
            enemyAttack.SetPlayerTarget(playerTarget);

        EnemyDeathNotifier deathNotifier = spawnedEnemy.GetComponent<EnemyDeathNotifier>();

        if (deathNotifier == null)
            deathNotifier = spawnedEnemy.AddComponent<EnemyDeathNotifier>();

        deathNotifier.Initialize(this);
    }

    private void OnValidate()
    {
        maxEnemiesAlive = Mathf.Max(1, maxEnemiesAlive);
        spawnInterval = Mathf.Max(0.05f, spawnInterval);
        difficultyRampDuration = Mathf.Max(0f, difficultyRampDuration);

        // The ramp may only ever add pressure, never take it away again.
        lateGameMaxEnemiesAlive = Mathf.Max(maxEnemiesAlive, lateGameMaxEnemiesAlive);
        lateGameSpawnInterval = Mathf.Clamp(lateGameSpawnInterval, 0.05f, spawnInterval);
    }
}
