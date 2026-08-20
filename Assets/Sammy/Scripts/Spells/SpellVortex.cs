using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Pooled singularity. Runs in three beats: it tears open while the pull ramps
/// up, holds at full size dragging everything nearby into its centre, then
/// implodes and dissipates. The artwork already grows and shrinks across its own
/// frames, so the transform keeps a constant scale and the sheet does the work.
/// </summary>
public class SpellVortex : MonoBehaviour
{
    private enum Phase
    {
        Forming,
        Holding,
        Collapsing
    }

    /// <summary>
    /// Slightly longer than a frame. The pull is refreshed every frame, so this
    /// only has to outlive one; the moment the hole stops renewing it the enemy
    /// steers for itself again.
    /// </summary>
    private const float PullHoldDuration = 0.12f;

    private static readonly Queue<SpellVortex> Pool = new Queue<SpellVortex>();
    private static readonly Collider[] PullBuffer = new Collider[48];
    private static readonly Collider[] DamageBuffer = new Collider[48];

    private sealed class CaughtEnemy
    {
        public EnemyMovement Movement;
        public EnemyAttack Attack;
        public Collider Body;
        public VortexTether Tether;
    }

    private readonly Dictionary<EnemyStats, CaughtEnemy> m_caught = new();
    private readonly HashSet<EnemyStats> m_seenThisFrame = new();
    private readonly HashSet<EnemyStats> m_damagedThisTick = new();
    private readonly List<EnemyStats> m_lostThisFrame = new();

    private Transform m_visual;
    private SpriteRenderer m_renderer;
    private Transform m_cameraTransform;
    private SpellDefinition m_definition;
    private PlayerResources m_playerResources;
    private LayerMask m_enemyLayer;

    private Phase m_phase;
    private float m_phaseElapsed;
    private float m_totalElapsed;
    private float m_nextTickTime;
    private float m_debrisCredit;
    private float m_spinAngle;
    private float m_baseScale;
    private int m_currentFrame;
    private bool m_isRunning;

    public static void Show(
        SpellDefinition _definition,
        Vector3 _groundPosition,
        LayerMask _enemyLayer,
        PlayerResources _playerResources)
    {
        if (_definition == null || _definition.Frames == null || _definition.Frames.Length == 0)
            return;

        if (_definition.Frames[0] == null)
            return;

        SpellVortex vortex = GetOrCreate();
        vortex.gameObject.SetActive(true);
        vortex.Initialize(_definition, _groundPosition, _enemyLayer, _playerResources);
    }

    private static SpellVortex GetOrCreate()
    {
        while (Pool.Count > 0)
        {
            SpellVortex pooledVortex = Pool.Dequeue();

            if (pooledVortex != null)
                return pooledVortex;
        }

        GameObject vortexObject = new GameObject("Spell Vortex", typeof(SpellVortex));
        return vortexObject.GetComponent<SpellVortex>();
    }

    private void Awake()
    {
        GameObject visualObject = new GameObject("Visual");
        visualObject.transform.SetParent(transform, false);
        m_visual = visualObject.transform;

        m_renderer = visualObject.AddComponent<SpriteRenderer>();
        // Above the streams and the debris: the event horizon has to swallow them
        // rather than let them draw over the black.
        m_renderer.sortingOrder = 210;
        m_renderer.shadowCastingMode = ShadowCastingMode.Off;
        m_renderer.receiveShadows = false;
        m_renderer.lightProbeUsage = LightProbeUsage.Off;
        m_renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private void Initialize(
        SpellDefinition _definition,
        Vector3 _groundPosition,
        LayerMask _enemyLayer,
        PlayerResources _playerResources)
    {
        ResolveCamera();

        m_definition = _definition;
        m_playerResources = _playerResources;
        m_enemyLayer = _enemyLayer;

        m_phase = Phase.Forming;
        m_phaseElapsed = 0f;
        m_totalElapsed = 0f;
        m_debrisCredit = 0f;
        m_spinAngle = 0f;
        m_currentFrame = -1;
        m_isRunning = true;

        // The object itself stays on the ground: that is where the enemies are and
        // where every query has to be centred. Only the sprite hovers.
        transform.position = _groundPosition;
        transform.rotation = Quaternion.identity;
        m_visual.localPosition = Vector3.up * _definition.VortexGroundOffset;

        m_baseScale = GetBaseScale(_definition);
        m_visual.localScale = Vector3.one * m_baseScale;

        SpellEmission.Apply(m_renderer, _definition);
        ApplyFrame(0);

        m_nextTickTime = PveRuntime.Time + _definition.VortexTickInterval;

        // A ring rushing inwards rather than outwards. It reads as the space around
        // the cast point being pulled in before anything is even visible there.
        CombatRingFlash.Show(
            m_visual.position,
            _definition.GlowColor,
            _definition.VortexPullRadius * 2f,
            _definition.ImpactRadius * 0.5f,
            Mathf.Max(0.1f, _definition.VortexFormDuration)
        );
    }

    /// <summary>
    /// Scale that makes the widest frame cover the damage radius. The artwork sits
    /// in a square canvas with padding around it, so the sprite is measured rather
    /// than assumed.
    /// </summary>
    private float GetBaseScale(SpellDefinition _definition)
    {
        Sprite peakFrame = GetFrame(_definition.VortexPeakFrame) ?? GetFrame(0);
        float spriteWidth = peakFrame != null ? Mathf.Max(0.01f, peakFrame.bounds.size.x) : 1f;
        float targetWidth = _definition.ImpactRadius * 2f * Mathf.Max(0.01f, _definition.ImpactVisualScale);
        return targetWidth / spriteWidth;
    }

    private void Update()
    {
        // Frozen with the arena while the player is on the tower camera.
        if (PveRuntime.IsPaused)
            return;

        if (!m_isRunning)
            return;

        float deltaTime = Time.deltaTime;
        m_phaseElapsed += deltaTime;
        m_totalElapsed += deltaTime;
        m_spinAngle += m_definition.VortexSpinSpeed * deltaTime;

        // A loop rather than a single check, so a long frame cannot skip a phase
        // and leave the implosion unfired. The durations are clamped above zero by
        // the definition, so this always terminates.
        while (m_isRunning && m_phaseElapsed >= GetPhaseDuration())
        {
            m_phaseElapsed -= GetPhaseDuration();

            if (!AdvancePhase())
            {
                ReturnToPool();
                return;
            }
        }

        float phaseDuration = GetPhaseDuration();
        float progress = phaseDuration > 0f ? Mathf.Clamp01(m_phaseElapsed / phaseDuration) : 1f;
        float pull = GetPullStrength(progress);

        ApplyFrame(GetFrameIndex(progress));
        UpdateVisual(pull);

        if (pull > 0.01f)
            PullEnemies(pull);
        else
            ReleaseAllTethers();

        if (pull > 0.25f)
        {
            SpawnDebris(deltaTime, pull);

            if (PveRuntime.Time >= m_nextTickTime)
            {
                m_nextTickTime = PveRuntime.Time + Mathf.Max(0.05f, m_definition.VortexTickInterval);

                // Never interrupting: a tick every third of a second would otherwise
                // reset the hit stun forever and lock the enemy out of everything.
                DamageInside(m_definition.VortexDamagePerTick, false);
            }
        }
    }

    private void LateUpdate()
    {
        if (!m_isRunning)
            return;

        if (m_cameraTransform == null)
            ResolveCamera();

        if (m_cameraTransform == null)
            return;

        // Turned around the vertical axis only. A full billboard would tip the disc
        // away as soon as the camera looks down at the arena. Along the camera
        // forward, not against it, or the spiral would be drawn mirrored and appear
        // to turn the wrong way.
        Vector3 cameraForward = m_cameraTransform.rotation * Vector3.forward;
        cameraForward.y = 0f;

        if (cameraForward.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(cameraForward.normalized, Vector3.up);
    }

    private float GetPhaseDuration() => m_phase switch
    {
        Phase.Forming => Mathf.Max(0.05f, m_definition.VortexFormDuration),
        Phase.Holding => Mathf.Max(0.05f, m_definition.VortexHoldDuration),
        _ => Mathf.Max(0.05f, m_definition.VortexCollapseDuration)
    };

    /// <summary>Moves on to the next beat. False once the spell is finished.</summary>
    private bool AdvancePhase()
    {
        switch (m_phase)
        {
            case Phase.Forming:
                m_phase = Phase.Holding;
                return true;

            case Phase.Holding:
                // The hole snapping shut is the payoff, so the big hit lands here
                // and the remaining frames are the aftermath.
                Implode();
                m_phase = Phase.Collapsing;
                return true;

            default:
                return false;
        }
    }

    /// <summary>0 to 1 share of the full pull for the current moment.</summary>
    private float GetPullStrength(float _progress) => m_phase switch
    {
        // Eased in, so the hole takes hold instead of yanking everything at once.
        Phase.Forming => _progress * _progress,
        Phase.Holding => 1f,
        // Fades out over the first part of the collapse and is gone after that.
        _ => Mathf.Clamp01(1f - _progress * 2f)
    };

    private int GetFrameIndex(float _progress)
    {
        int frameCount = m_definition.Frames.Length;
        int peak = Mathf.Clamp(m_definition.VortexPeakFrame, 0, frameCount - 1);

        switch (m_phase)
        {
            case Phase.Forming:
                return peak > 0 ? Mathf.Clamp(Mathf.FloorToInt(_progress * peak), 0, peak - 1) : peak;

            case Phase.Holding:
                return peak;

            default:
                int tailCount = frameCount - peak - 1;

                if (tailCount <= 0)
                    return peak;

                return peak + 1 + Mathf.Clamp(Mathf.FloorToInt(_progress * tailCount), 0, tailCount - 1);
        }
    }

    private void ApplyFrame(int _frameIndex)
    {
        if (_frameIndex == m_currentFrame)
            return;

        m_currentFrame = _frameIndex;
        Sprite frame = GetFrame(_frameIndex);
        m_renderer.sprite = frame;
        m_renderer.enabled = frame != null;
    }

    private Sprite GetFrame(int _index) =>
        m_definition.Frames != null && _index >= 0 && _index < m_definition.Frames.Length
            ? m_definition.Frames[_index]
            : null;

    private void UpdateVisual(float _pull)
    {
        m_visual.localRotation = Quaternion.Euler(0f, 0f, m_spinAngle);

        // Breathes a little while it is feeding. Held completely still the disc
        // looks like a decal rather than something under strain.
        float breath = 1f + Mathf.Sin(m_totalElapsed * 9f) * 0.035f * _pull;
        m_visual.localScale = Vector3.one * (m_baseScale * breath);
    }

    private void PullEnemies(float _strength)
    {
        float pullRadius = m_definition.VortexPullRadius;
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, pullRadius, PullBuffer, m_enemyLayer);

        m_seenThisFrame.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            EnemyStats enemyStats = PullBuffer[i] != null
                ? PullBuffer[i].GetComponentInParent<EnemyStats>()
                : null;

            // One enemy can own several colliders, and pulling it once per collider
            // would multiply its speed by however many it happens to have.
            if (enemyStats == null || enemyStats.IsDead || !m_seenThisFrame.Add(enemyStats))
                continue;

            CaughtEnemy caught = GetOrCatch(enemyStats);

            Vector3 toCenter = transform.position - enemyStats.transform.position;
            toCenter.y = 0f;
            float distance = toCenter.magnitude;

            if (distance > 0.001f)
            {
                Vector3 inward = toCenter / distance;

                // Perpendicular to the pull, which is what turns a straight drag
                // into an orbit that decays inwards.
                Vector3 tangent = Vector3.Cross(Vector3.up, inward);
                float proximity = 1f - Mathf.Clamp01(distance / pullRadius);
                float speed = m_definition.VortexPullSpeed * Mathf.Lerp(0.55f, 1.5f, proximity) * _strength;

                caught.Movement?.ApplyVortexPull(
                    inward * speed + tangent * (speed * m_definition.VortexSpiral),
                    PullHoldDuration
                );
            }

            UpdateTether(enemyStats, caught, _strength);
        }

        ReleaseLostTethers();
    }

    private CaughtEnemy GetOrCatch(EnemyStats _enemyStats)
    {
        if (m_caught.TryGetValue(_enemyStats, out CaughtEnemy existing))
            return existing;

        CaughtEnemy caught = new CaughtEnemy
        {
            Movement = _enemyStats.GetComponent<EnemyMovement>(),
            Attack = _enemyStats.GetComponent<EnemyAttack>(),
            Body = _enemyStats.GetComponent<Collider>()
        };

        m_caught[_enemyStats] = caught;

        // Held for the rest of the hole's life in one call rather than refreshed
        // every frame, because staggering also cancels the windup and doing that
        // repeatedly would hammer the telegraph.
        float remaining = Mathf.Max(0.1f, m_definition.VortexLifetime - m_totalElapsed);
        caught.Attack?.Stagger(remaining);

        return caught;
    }

    private void UpdateTether(EnemyStats _enemyStats, CaughtEnemy _caught, float _strength)
    {
        if (m_definition.VortexTetherWidth <= 0f)
            return;

        _caught.Tether ??= VortexTether.Acquire(m_definition);

        Vector3 enemyPoint = _caught.Body != null
            ? _caught.Body.bounds.center
            : _enemyStats.transform.position;

        _caught.Tether.UpdateShape(m_visual.position, enemyPoint, _strength);
    }

    /// <summary>Drops everything that left the pull or died while being dragged.</summary>
    private void ReleaseLostTethers()
    {
        m_lostThisFrame.Clear();

        foreach (KeyValuePair<EnemyStats, CaughtEnemy> entry in m_caught)
        {
            if (entry.Key == null || entry.Key.IsDead || !m_seenThisFrame.Contains(entry.Key))
                m_lostThisFrame.Add(entry.Key);
        }

        foreach (EnemyStats lostEnemy in m_lostThisFrame)
        {
            if (m_caught.TryGetValue(lostEnemy, out CaughtEnemy caught))
                caught.Tether?.Release();

            m_caught.Remove(lostEnemy);
        }

        m_lostThisFrame.Clear();
    }

    private void ReleaseAllTethers()
    {
        foreach (KeyValuePair<EnemyStats, CaughtEnemy> entry in m_caught)
            entry.Value.Tether?.Release();

        m_caught.Clear();
    }

    private void SpawnDebris(float _deltaTime, float _strength)
    {
        if (m_definition.VortexDebrisPerSecond <= 0f)
            return;

        m_debrisCredit += m_definition.VortexDebrisPerSecond * _strength * _deltaTime;

        while (m_debrisCredit >= 1f)
        {
            m_debrisCredit -= 1f;

            VortexDebris.Spawn(
                m_visual.position,
                m_definition.ImpactRadius * Random.Range(0.85f, 1.5f),
                Random.Range(0.16f, 0.34f),
                Random.Range(0.5f, 0.85f),
                Mathf.Sign(m_definition.VortexSpinSpeed) * Random.Range(2.2f, 3.6f),
                m_definition.TrailColor,
                m_definition
            );
        }
    }

    private void Implode()
    {
        // Damage first. Returning to the pool clears the resources reference, and
        // an enemy killed after that would hand out no experience or scrap.
        DamageInside(m_definition.Damage, true);

        Vector3 core = m_visual.position;

        CombatRingFlash.Show(
            core,
            m_definition.GlowColor,
            m_definition.ImpactRadius * 0.3f,
            m_definition.ImpactRadius * 2.8f,
            0.44f
        );

        for (int i = 0; i < 16; i++)
        {
            float angle = i / 16f * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
            Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            CombatGlowPuff.Show(
                core + outward * (m_definition.ImpactRadius * 0.25f),
                m_definition.GlowColor,
                Random.Range(0.3f, 0.55f),
                Random.Range(0.05f, 0.12f),
                Random.Range(0.32f, 0.55f),
                204,
                m_definition.EmissiveMaterial,
                m_definition.EmissionIntensity,
                outward * Random.Range(2.4f, 4.2f) + Vector3.up * Random.Range(0.4f, 1.4f)
            );
        }
    }

    private void DamageInside(int _damage, bool _interruptsEnemy)
    {
        if (_damage <= 0)
            return;

        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            m_definition.ImpactRadius,
            DamageBuffer,
            m_enemyLayer
        );

        m_damagedThisTick.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            EnemyStats enemyStats = DamageBuffer[i] != null
                ? DamageBuffer[i].GetComponentInParent<EnemyStats>()
                : null;

            if (enemyStats == null || enemyStats.IsDead || !m_damagedThisTick.Add(enemyStats))
                continue;

            enemyStats.TakeDamage(_damage, m_playerResources, false, _interruptsEnemy);
        }

        m_damagedThisTick.Clear();
    }

    private void ResolveCamera()
    {
        if (CameraReferences.Instance != null)
            m_cameraTransform = CameraReferences.Instance.PlayerCameraTransform;

        if (m_cameraTransform == null && Camera.main != null)
            m_cameraTransform = Camera.main.transform;
    }

    private void ReturnToPool()
    {
        if (!m_isRunning)
            return;

        m_isRunning = false;
        ReleaseAllTethers();

        m_definition = null;
        m_playerResources = null;
        m_renderer.sprite = null;
        m_currentFrame = -1;

        gameObject.SetActive(false);
        Pool.Enqueue(this);
    }
}
