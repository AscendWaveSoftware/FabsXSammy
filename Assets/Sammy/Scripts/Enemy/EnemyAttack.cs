using System.Collections.Generic;
using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private float m_attackRange = 1.4f;
    [SerializeField] private float m_attackCooldown = 1f;
    [SerializeField, Min(0f), Tooltip("Readable anticipation before the enemy deals damage.")]
    private float m_attackWindup = 0.28f;

    [Header("Hit Reaction")]
    [SerializeField, Min(0f), Tooltip("Time after taking a hit during which this enemy cannot attack.")]
    private float m_hitStunDuration = 0.24f;
    [SerializeField] private Color m_windupTint = new(1f, 0.28f, 0.18f, 1f);

    [Header("Target")]
    [SerializeField] private Transform m_playerTarget;

    private EnemyStats m_stats;
    private EnemyAttackTelegraph m_telegraph;
    private PlayerHealth m_playerHealth;
    private SpriteRenderer[] m_renderers;
    private Color[] m_baseColors;
    private float m_nextAttackTime;
    private float m_stunnedUntil;
    private float m_windupStartedAt;
    private float m_attackExecutesAt;
    private bool m_isWindingUp;

    public float AttackRange => m_attackRange;
    public bool IsWindingUp => m_isWindingUp;

    private void Awake()
    {
        m_stats = GetComponent<EnemyStats>();

        // Added here rather than in the prefab so every enemy variant gets the
        // same telegraph without anyone having to remember to wire it up.
        m_telegraph = GetComponent<EnemyAttackTelegraph>();

        if (m_telegraph == null)
            m_telegraph = gameObject.AddComponent<EnemyAttackTelegraph>();

        CacheBodyRenderers();
    }

    private void CacheBodyRenderers()
    {
        SpriteRenderer[] childRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        List<SpriteRenderer> bodyRenderers = new(childRenderers.Length);

        foreach (SpriteRenderer childRenderer in childRenderers)
        {
            // The telegraph builds its own renderers underneath this enemy. Tinting
            // those as body would overwrite the warning colours every frame.
            if (childRenderer == null || m_telegraph.OwnsRenderer(childRenderer))
                continue;

            bodyRenderers.Add(childRenderer);
        }

        m_renderers = bodyRenderers.ToArray();
        m_baseColors = new Color[m_renderers.Length];

        for (int i = 0; i < m_renderers.Length; i++)
            m_baseColors[i] = m_renderers[i].color;
    }

    private void Start()
    {
        if (m_playerTarget == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                SetPlayerTarget(playerObject.transform);
        }
        else
        {
            SetPlayerTarget(m_playerTarget);
        }
    }

    private void Update()
    {
        if (m_stats == null || m_stats.IsDead || m_playerTarget == null ||
            m_playerHealth == null || !m_playerHealth.IsAlive)
        {
            CancelWindup();
            return;
        }

        if (Time.time < m_stunnedUntil)
        {
            CancelWindup();
            return;
        }

        bool playerInRange = IsPlayerInRange();

        if (m_isWindingUp)
        {
            if (!playerInRange)
            {
                CancelWindup();
                return;
            }

            UpdateWindupVisual();

            if (Time.time >= m_attackExecutesAt)
                ExecuteAttack();

            return;
        }

        if (playerInRange && Time.time >= m_nextAttackTime)
            BeginWindup();
    }

    private void OnDisable()
    {
        CancelWindup();
    }

    public void SetPlayerTarget(Transform _playerTarget)
    {
        m_playerTarget = _playerTarget;
        m_playerHealth = m_playerTarget != null
            ? m_playerTarget.GetComponent<PlayerHealth>()
            : null;
    }

    public void NotifyHit()
    {
        Stun(m_hitStunDuration);
    }

    /// <summary>
    /// Prevents this enemy from attacking for a while and cancels a windup that
    /// is already running, so the telegraph visibly snaps off.
    /// </summary>
    public void Stun(float _duration)
    {
        if (_duration <= 0f || (m_stats != null && m_stats.IsDead))
            return;

        m_stunnedUntil = Mathf.Max(m_stunnedUntil, Time.time + _duration);
        m_nextAttackTime = Mathf.Max(m_nextAttackTime, m_stunnedUntil);
        CancelWindup();
    }

    private void BeginWindup()
    {
        m_isWindingUp = true;
        m_windupStartedAt = Time.time;
        m_attackExecutesAt = Time.time + m_attackWindup;
        m_telegraph?.BeginWindup(m_attackRange);
        UpdateWindupVisual();

        if (m_attackWindup <= 0f)
            ExecuteAttack();
    }

    private void ExecuteAttack()
    {
        RestoreBaseColors();
        m_isWindingUp = false;
        m_nextAttackTime = Time.time + m_attackCooldown;
        m_telegraph?.NotifyStrike();

        if (m_playerHealth == null || !m_playerHealth.IsAlive || !IsPlayerInRange())
            return;

        m_playerHealth.TakeDamage(m_stats.AttackDamage, transform.position);
    }

    private void CancelWindup()
    {
        if (!m_isWindingUp)
            return;

        m_isWindingUp = false;
        RestoreBaseColors();
        m_telegraph?.CancelWindup();
    }

    private bool IsPlayerInRange()
    {
        Vector3 offset = m_playerTarget.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= m_attackRange * m_attackRange;
    }

    private void UpdateWindupVisual()
    {
        float progress = m_attackWindup > 0f
            ? Mathf.InverseLerp(m_windupStartedAt, m_attackExecutesAt, Time.time)
            : 1f;
        float intensity = Mathf.SmoothStep(0.18f, 0.82f, progress);

        m_telegraph?.UpdateWindup(progress);

        for (int i = 0; i < m_renderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = m_renderers[i];
            if (spriteRenderer == null)
                continue;

            Color telegraphColor = MultiplyRgb(m_baseColors[i], m_windupTint);
            spriteRenderer.color = Color.Lerp(m_baseColors[i], telegraphColor, intensity);
        }
    }

    private void RestoreBaseColors()
    {
        for (int i = 0; i < m_renderers.Length; i++)
        {
            if (m_renderers[i] != null)
                m_renderers[i].color = m_baseColors[i];
        }
    }

    private static Color MultiplyRgb(Color _baseColor, Color _tint)
    {
        return new Color(
            _baseColor.r * _tint.r,
            _baseColor.g * _tint.g,
            _baseColor.b * _tint.b,
            _baseColor.a);
    }

    private void OnValidate()
    {
        m_attackRange = Mathf.Max(0.05f, m_attackRange);
        m_attackCooldown = Mathf.Max(0f, m_attackCooldown);
        m_attackWindup = Mathf.Max(0f, m_attackWindup);
        m_hitStunDuration = Mathf.Max(0f, m_hitStunDuration);
    }
}
