using UnityEngine;

/// <summary>
/// Short burst of sparks where an enemy died.
///
/// Built from the pooled combat effects rather than a ParticleSystem on purpose:
/// the enemy GameObject is destroyed in the same frame, so anything parented to
/// it would vanish before it could play. These live on their own and clean
/// themselves up.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyStats))]
public class EnemyDeathBurst : MonoBehaviour
{
    [Header("Sparks")]
    [SerializeField, Range(1, 24)] private int m_sparkCount = 10;
    [SerializeField] private Color m_sparkColor = new(0.85f, 0.16f, 0.3f, 0.9f);
    [SerializeField, Min(0.01f)] private float m_sparkStartDiameter = 0.34f;
    [SerializeField, Min(0f)] private float m_sparkEndDiameter = 0.04f;
    [SerializeField, Min(0.05f)] private float m_sparkLifetime = 0.5f;
    [SerializeField, Min(0f), Tooltip("How fast the sparks fly outwards before drag settles them.")]
    private float m_sparkSpeed = 3.2f;
    [SerializeField, Min(0f)] private float m_upwardBias = 1.4f;

    [Header("Flash")]
    [SerializeField] private Color m_flashColor = new(1f, 0.42f, 0.42f, 0.9f);
    [SerializeField, Min(0f), Tooltip("0 skips the ring, leaving only the sparks.")]
    private float m_flashEndDiameter = 1.6f;
    [SerializeField, Min(0.02f)] private float m_flashDuration = 0.3f;

    private EnemyStats m_stats;

    private void Awake()
    {
        m_stats = GetComponent<EnemyStats>();
        CombatFeedbackSprites.Prewarm();
    }

    private void OnEnable()
    {
        if (m_stats != null)
            m_stats.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        if (m_stats != null)
            m_stats.OnDied -= HandleDied;
    }

    private void HandleDied()
    {
        Vector3 burstCenter = GetBodyCenter();

        if (m_flashEndDiameter > 0f)
            CombatRingFlash.Show(burstCenter, m_flashColor, m_flashEndDiameter * 0.25f, m_flashEndDiameter, m_flashDuration);

        for (int i = 0; i < m_sparkCount; i++)
            SpawnSpark(burstCenter);
    }

    private void SpawnSpark(Vector3 _center)
    {
        // Spread over a sphere and then pushed upwards, so the burst blooms out
        // and away rather than sinking into the ground.
        Vector3 direction = Random.onUnitSphere;
        direction.y = Mathf.Abs(direction.y) * m_upwardBias;
        direction.Normalize();

        float speed = m_sparkSpeed * Random.Range(0.55f, 1.25f);
        float lifetime = m_sparkLifetime * Random.Range(0.7f, 1.2f);

        CombatGlowPuff.Show(
            _center + direction * 0.1f,
            m_sparkColor,
            m_sparkStartDiameter * Random.Range(0.7f, 1.2f),
            m_sparkEndDiameter,
            lifetime,
            199,
            null,
            1f,
            direction * speed
        );
    }

    private Vector3 GetBodyCenter()
    {
        Collider enemyCollider = GetComponent<Collider>();
        return enemyCollider != null ? enemyCollider.bounds.center : transform.position;
    }

    private void OnValidate()
    {
        m_sparkEndDiameter = Mathf.Min(m_sparkEndDiameter, m_sparkStartDiameter);
    }
}
