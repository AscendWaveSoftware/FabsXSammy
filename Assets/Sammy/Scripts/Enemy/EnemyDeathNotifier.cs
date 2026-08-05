using UnityEngine;

public class EnemyDeathNotifier : MonoBehaviour
{
    private EnemySpawner m_enemySpawner;
    private bool m_hasNotified;

    public void Initialize(EnemySpawner _enemySpawner)
    {
        m_enemySpawner = _enemySpawner;
    }

    private void OnDestroy()
    {
        if (m_hasNotified) return;

        m_hasNotified = true;

        if (m_enemySpawner != null)
            m_enemySpawner.NotifyEnemyDied();
    }
}
