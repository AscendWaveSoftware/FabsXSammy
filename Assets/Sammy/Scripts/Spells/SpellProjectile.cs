using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Pooled spell projectile. Steers towards the enemy it was fired at, detonates
/// on contact or when its lifetime runs out, and owns the area damage it deals.
/// </summary>
public class SpellProjectile : MonoBehaviour
{
    private static readonly Queue<SpellProjectile> Pool = new Queue<SpellProjectile>();

    // Shared scratch buffers for the per frame contact checks, which would
    // otherwise allocate for every projectile in every single frame.
    private static readonly Collider[] ContactBuffer = new Collider[8];
    private static readonly RaycastHit[] SweepBuffer = new RaycastHit[8];

    private readonly HashSet<EnemyStats> m_damagedEnemies = new();

    private SpriteRenderer m_coreRenderer;
    private SpriteRenderer m_glowRenderer;
    private Transform m_cameraTransform;
    private SpellDefinition m_definition;
    private PlayerResources m_playerResources;
    private EnemyStats m_target;
    private Collider m_targetCollider;
    private LayerMask m_enemyLayer;
    private Vector3 m_direction;
    private Vector3 m_lastTrailPosition;
    private float m_elapsedTime;
    private bool m_isRunning;

    public static void Launch(
        SpellDefinition _definition,
        Vector3 _origin,
        Vector3 _direction,
        EnemyStats _target,
        LayerMask _enemyLayer,
        PlayerResources _playerResources)
    {
        if (_definition == null || !_definition.IsUsable || _direction.sqrMagnitude < 0.0001f)
            return;

        SpellProjectile projectile = GetOrCreate();
        projectile.gameObject.SetActive(true);
        projectile.Initialize(_definition, _origin, _direction.normalized, _target, _enemyLayer, _playerResources);
    }

    private static SpellProjectile GetOrCreate()
    {
        while (Pool.Count > 0)
        {
            SpellProjectile pooledProjectile = Pool.Dequeue();

            if (pooledProjectile != null)
                return pooledProjectile;
        }

        GameObject projectileObject = new GameObject("Spell Projectile", typeof(SpellProjectile));
        return projectileObject.GetComponent<SpellProjectile>();
    }

    private void Awake()
    {
        CombatFeedbackSprites.Prewarm();

        m_glowRenderer = CreateRenderer("Glow", 205);
        m_glowRenderer.sprite = CombatFeedbackSprites.Glow;

        m_coreRenderer = CreateRenderer("Core", 206);
    }

    private void Update()
    {
        // Frozen with the arena while the player is on the tower camera.
        if (PveRuntime.IsPaused)
            return;

        if (!m_isRunning)
            return;

        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
            return;

        m_elapsedTime += deltaTime;
        UpdateVisuals();
        Steer(deltaTime);

        Vector3 previousPosition = transform.position;
        Vector3 nextPosition = previousPosition + m_direction * (m_definition.Speed * deltaTime);

        if (TryGetHitPosition(previousPosition, nextPosition, out Vector3 hitPosition))
        {
            transform.position = hitPosition;
            Detonate();
            return;
        }

        transform.position = nextPosition;
        LeaveTrail();

        if (m_elapsedTime >= m_definition.Lifetime)
            Detonate();
    }

    private void LateUpdate()
    {
        if (!m_isRunning)
            return;

        if (m_cameraTransform == null)
            ResolveCamera();

        if (m_cameraTransform == null)
            return;

        transform.LookAt(
            transform.position + m_cameraTransform.rotation * Vector3.forward,
            m_cameraTransform.rotation * Vector3.up
        );
    }

    private void Initialize(
        SpellDefinition _definition,
        Vector3 _origin,
        Vector3 _direction,
        EnemyStats _target,
        LayerMask _enemyLayer,
        PlayerResources _playerResources)
    {
        ResolveCamera();

        m_definition = _definition;
        m_playerResources = _playerResources;
        m_enemyLayer = _enemyLayer;
        m_target = _target;
        m_targetCollider = _target != null ? _target.GetComponent<Collider>() : null;
        m_direction = _direction;
        m_elapsedTime = 0f;
        m_isRunning = true;

        transform.position = _origin;
        transform.rotation = Quaternion.identity;
        m_lastTrailPosition = _origin;

        m_coreRenderer.sprite = _definition.ProjectileSprite;
        m_coreRenderer.color = Color.white;
        m_glowRenderer.color = _definition.GlowColor;

        SpellEmission.Apply(m_coreRenderer, _definition);
        SpellEmission.Apply(m_glowRenderer, _definition);

        // Sized before the first frame renders, otherwise the projectile would
        // flash at full size for one frame before the pop even starts.
        UpdateVisuals();

        // The launch needs its own event. Without it the orb simply appears out
        // of nothing, which is what made the cast feel weightless.
        SpellImpact.ShowCastFlash(_definition, _origin);
    }

    private void UpdateVisuals()
    {
        float pop = m_definition.SpawnPopDuration > 0f
            ? EaseOutBack(Mathf.Clamp01(m_elapsedTime / m_definition.SpawnPopDuration))
            : 1f;

        float pulse = 1f + Mathf.Sin(m_elapsedTime * 14f) * m_definition.GlowPulse;

        m_coreRenderer.transform.localScale = Vector3.one * (m_definition.CoreScale * pop);
        m_glowRenderer.transform.localScale = Vector3.one * (m_definition.GlowDiameter * pop * pulse);
    }

    private static float EaseOutBack(float _value)
    {
        const float overshoot = 2.4f;
        float shiftedValue = _value - 1f;
        return 1f + (overshoot + 1f) * shiftedValue * shiftedValue * shiftedValue
                  + overshoot * shiftedValue * shiftedValue;
    }

    private void Steer(float _deltaTime)
    {
        if (m_definition.TurnRate <= 0f || !HasLivingTarget())
            return;

        Vector3 toTarget = GetTargetPoint() - transform.position;

        if (toTarget.sqrMagnitude < 0.0001f)
            return;

        float maxTurn = m_definition.TurnRate * Mathf.Deg2Rad * _deltaTime;
        m_direction = Vector3.RotateTowards(m_direction, toTarget.normalized, maxTurn, 0f).normalized;
    }

    private bool HasLivingTarget() => m_target != null && !m_target.IsDead;

    private Vector3 GetTargetPoint()
    {
        // Aim at the body centre rather than the pivot at the feet, otherwise the
        // projectile dives into the ground on its way in.
        if (m_targetCollider != null)
            return m_targetCollider.bounds.center;

        return m_target.transform.position;
    }

    private void LeaveTrail()
    {
        if (m_definition.TrailSpacing <= 0f)
            return;

        Vector3 currentPosition = transform.position;
        float spacing = m_definition.TrailSpacing;

        if ((currentPosition - m_lastTrailPosition).sqrMagnitude < spacing * spacing)
            return;

        m_lastTrailPosition = currentPosition;

        // Clearly smaller than the orb itself, so the trail tapers away behind it
        // instead of reading as one thick tube.
        CombatGlowPuff.Show(
            currentPosition,
            m_definition.TrailColor,
            m_definition.GlowDiameter * 0.55f,
            m_definition.GlowDiameter * 0.1f,
            m_definition.TrailLifetime,
            199,
            m_definition.EmissiveMaterial,
            m_definition.EmissionIntensity
        );
    }

    private bool TryGetHitPosition(Vector3 _fromPosition, Vector3 _toPosition, out Vector3 _hitPosition)
    {
        _hitPosition = _toPosition;

        Vector3 movement = _toPosition - _fromPosition;
        float distance = movement.magnitude;

        if (distance > 0.0001f)
        {
            Vector3 movementDirection = movement / distance;

            // Swept instead of a simple check at the new position, so a fast
            // projectile cannot tunnel straight through an enemy between frames.
            int hitCount = Physics.SphereCastNonAlloc(
                _fromPosition,
                m_definition.HitRadius,
                movementDirection,
                SweepBuffer,
                distance,
                m_enemyLayer
            );

            float closestHitDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                if (!IsLivingEnemy(SweepBuffer[i].collider))
                    continue;

                closestHitDistance = Mathf.Min(closestHitDistance, SweepBuffer[i].distance);
            }

            if (closestHitDistance < float.MaxValue)
            {
                _hitPosition = _fromPosition + movementDirection * closestHitDistance;
                return true;
            }
        }

        // A sweep never reports colliders it already starts inside, so the end
        // position still needs its own overlap test.
        return HasEnemyOverlap(_toPosition);
    }

    private bool HasEnemyOverlap(Vector3 _position)
    {
        int contactCount = Physics.OverlapSphereNonAlloc(
            _position,
            m_definition.HitRadius,
            ContactBuffer,
            m_enemyLayer
        );

        for (int i = 0; i < contactCount; i++)
        {
            if (IsLivingEnemy(ContactBuffer[i]))
                return true;
        }

        return false;
    }

    private static bool IsLivingEnemy(Collider _collider)
    {
        if (_collider == null)
            return false;

        EnemyStats enemyStats = _collider.GetComponentInParent<EnemyStats>();
        return enemyStats != null && !enemyStats.IsDead;
    }

    private void Detonate()
    {
        Vector3 detonationPosition = transform.position;
        SpellDefinition definition = m_definition;

        ApplyAreaDamage(definition, detonationPosition);
        SpellImpact.Show(definition, detonationPosition);
        ReturnToPool();
    }

    private void ApplyAreaDamage(SpellDefinition _definition, Vector3 _center)
    {
        // Allocating on purpose: a detonation is a rare event and must never
        // silently drop enemies because a fixed buffer ran out.
        Collider[] hitColliders = Physics.OverlapSphere(_center, _definition.ImpactRadius, m_enemyLayer);

        m_damagedEnemies.Clear();

        foreach (Collider hitCollider in hitColliders)
        {
            EnemyStats enemyStats = hitCollider != null
                ? hitCollider.GetComponentInParent<EnemyStats>()
                : null;

            // One enemy can own several colliders, and taking damage may destroy
            // it, so every enemy is only ever resolved once.
            if (enemyStats == null || enemyStats.IsDead || !m_damagedEnemies.Add(enemyStats))
                continue;

            enemyStats.TakeDamage(_definition.Damage, m_playerResources);
        }

        m_damagedEnemies.Clear();
    }

    private SpriteRenderer CreateRenderer(string _name, int _sortingOrder)
    {
        GameObject rendererObject = new GameObject(_name);
        rendererObject.transform.SetParent(transform, false);

        SpriteRenderer spriteRenderer = rendererObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = _sortingOrder;
        spriteRenderer.shadowCastingMode = ShadowCastingMode.Off;
        spriteRenderer.receiveShadows = false;
        spriteRenderer.lightProbeUsage = LightProbeUsage.Off;
        spriteRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return spriteRenderer;
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
        m_definition = null;
        m_playerResources = null;
        m_target = null;
        m_targetCollider = null;
        m_coreRenderer.sprite = null;
        gameObject.SetActive(false);
        Pool.Enqueue(this);
    }
}
