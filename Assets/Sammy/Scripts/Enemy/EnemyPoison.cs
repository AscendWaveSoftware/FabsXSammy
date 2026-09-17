using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyStats))]
public class EnemyPoison : MonoBehaviour
{
    private EnemyStats m_stats;
    private PlayerResources m_playerResources;
    private Color m_poisonColor;
    private int m_damagePerTick;
    private float m_tickInterval;
    private float m_expiresAt;
    private float m_nextTickTime;
    private float m_topOffset;
    private bool m_isPoisoned;

    public static void Apply(
        EnemyStats _enemy,
        int _damagePerTick,
        float _tickInterval,
        float _duration,
        Color _poisonColor,
        PlayerResources _playerResources)
    {
        if (_enemy == null || _enemy.IsDead || _damagePerTick <= 0 || _duration <= 0f)
            return;

        EnemyPoison poison = _enemy.GetComponent<EnemyPoison>();

        if (poison == null)
            poison = _enemy.gameObject.AddComponent<EnemyPoison>();

        poison.Refresh(_damagePerTick, _tickInterval, _duration, _poisonColor, _playerResources);
    }

    private void Awake()
    {
        m_stats = GetComponent<EnemyStats>();
        m_topOffset = MeasureTopOffset();

        enabled = false;
    }

    private void Refresh(
        int _damagePerTick,
        float _tickInterval,
        float _duration,
        Color _poisonColor,
        PlayerResources _playerResources)
    {
        m_playerResources = _playerResources;
        m_poisonColor = _poisonColor;
        m_tickInterval = Mathf.Max(0.05f, _tickInterval);

        if (m_isPoisoned)
        {
            m_damagePerTick = Mathf.Max(m_damagePerTick, _damagePerTick);
            m_expiresAt = Mathf.Max(m_expiresAt, PveRuntime.Time + _duration);
            return;
        }

        m_isPoisoned = true;
        m_damagePerTick = _damagePerTick;
        m_expiresAt = PveRuntime.Time + _duration;

        m_nextTickTime = PveRuntime.Time;
        enabled = true;
    }

    private void Update()
    {
        if (PveRuntime.IsPaused)
            return;

        if (m_stats == null || m_stats.IsDead || PveRuntime.Time >= m_expiresAt)
        {
            m_isPoisoned = false;
            enabled = false;
            return;
        }

        if (PveRuntime.Time < m_nextTickTime)
            return;

        m_nextTickTime = PveRuntime.Time + m_tickInterval;
        SpawnPoisonPuff();

        m_stats.TakeDamage(m_damagePerTick, m_playerResources, false, false);
    }

    private void SpawnPoisonPuff()
    {
        Vector3 puffPosition = transform.position + Vector3.up * m_topOffset;
        puffPosition.x += Random.Range(-0.25f, 0.25f);
        puffPosition.z += Random.Range(-0.25f, 0.25f);

        CombatGlowPuff.Show(puffPosition, m_poisonColor, 0.28f, 0.7f, 0.55f);
    }

    private float MeasureTopOffset()
    {
        float highestPoint = transform.position.y + 1f;

        Collider enemyCollider = GetComponent<Collider>();

        if (enemyCollider != null)
            highestPoint = Mathf.Max(highestPoint, enemyCollider.bounds.max.y);

        return highestPoint - transform.position.y;
    }
}
