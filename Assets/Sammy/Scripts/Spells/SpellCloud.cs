using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Pooled ground cloud. Plays the spell's sheet standing on the ground and
/// poisons everything inside its radius for as long as it is visible.
/// </summary>
public class SpellCloud : MonoBehaviour
{
    private static readonly Queue<SpellCloud> Pool = new Queue<SpellCloud>();
    private static readonly Collider[] SoakBuffer = new Collider[32];

    private readonly HashSet<EnemyStats> m_poisonedEnemies = new();

    private Transform m_visual;
    private SpriteRenderer m_renderer;
    private Transform m_cameraTransform;
    private SpellDefinition m_definition;
    private PlayerResources m_playerResources;
    private LayerMask m_enemyLayer;
    private float m_elapsedTime;
    private float m_frameDuration;
    private float m_nextTickTime;
    private int m_frameCount;
    private int m_currentFrame;
    private bool m_isRunning;

    public static void Show(
        SpellDefinition _definition,
        Vector3 _groundPosition,
        LayerMask _enemyLayer,
        PlayerResources _playerResources)
    {
        if (_definition == null || _definition.ImpactFrameCount <= 0)
            return;

        Sprite firstFrame = _definition.GetImpactFrame(0);

        if (firstFrame == null)
            return;

        SpellCloud cloud = GetOrCreate();
        cloud.gameObject.SetActive(true);
        cloud.Initialize(_definition, _groundPosition, firstFrame, _enemyLayer, _playerResources);
    }

    private static SpellCloud GetOrCreate()
    {
        while (Pool.Count > 0)
        {
            SpellCloud pooledCloud = Pool.Dequeue();

            if (pooledCloud != null)
                return pooledCloud;
        }

        GameObject cloudObject = new GameObject("Spell Cloud", typeof(SpellCloud));
        return cloudObject.GetComponent<SpellCloud>();
    }

    private void Awake()
    {
        GameObject visualObject = new GameObject("Visual");
        visualObject.transform.SetParent(transform, false);
        m_visual = visualObject.transform;

        m_renderer = visualObject.AddComponent<SpriteRenderer>();
        m_renderer.sortingOrder = 208;
        m_renderer.shadowCastingMode = ShadowCastingMode.Off;
        m_renderer.receiveShadows = false;
        m_renderer.lightProbeUsage = LightProbeUsage.Off;
        m_renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private void Update()
    {
        if (!m_isRunning)
            return;

        m_elapsedTime += Time.deltaTime;

        int targetFrame = Mathf.FloorToInt(m_elapsedTime / m_frameDuration);

        if (targetFrame >= m_frameCount)
        {
            ReturnToPool();
            return;
        }

        if (targetFrame != m_currentFrame)
        {
            m_currentFrame = targetFrame;
            ApplyFrame(targetFrame);
        }

        if (Time.time >= m_nextTickTime)
        {
            m_nextTickTime = Time.time + m_definition.CloudTickInterval;
            PoisonEnemiesInside();
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

        // Turns around the vertical axis only. A full billboard would tip the
        // rising skull over as soon as the camera looks down at the arena.
        Vector3 cameraForward = m_cameraTransform.rotation * Vector3.forward;
        cameraForward.y = 0f;

        if (cameraForward.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(-cameraForward.normalized, Vector3.up);
    }

    private void Initialize(
        SpellDefinition _definition,
        Vector3 _groundPosition,
        Sprite _firstFrame,
        LayerMask _enemyLayer,
        PlayerResources _playerResources)
    {
        ResolveCamera();

        m_definition = _definition;
        m_playerResources = _playerResources;
        m_enemyLayer = _enemyLayer;
        m_frameCount = _definition.ImpactFrameCount;
        m_frameDuration = 1f / Mathf.Max(1f, _definition.ImpactFrameRate);
        m_elapsedTime = 0f;
        m_currentFrame = 0;
        m_isRunning = true;

        // The object itself sits on the ground and is the centre of the damage
        // query. Only the sprite is lifted, so a Y rotation leaves it in place.
        transform.position = _groundPosition;
        transform.rotation = Quaternion.identity;

        float spriteWidth = Mathf.Max(0.01f, _firstFrame.bounds.size.x);
        float spriteHeight = Mathf.Max(0.01f, _firstFrame.bounds.size.y);
        float targetWidth = _definition.ImpactRadius * 2f * _definition.CloudVisualScale;
        float scale = targetWidth / spriteWidth;

        m_visual.localScale = Vector3.one * scale;
        m_visual.localPosition = Vector3.up * (spriteHeight * scale * 0.5f + _definition.CloudGroundOffset);

        ApplyFrame(0);

        m_nextTickTime = Time.time + _definition.CloudTickInterval;
        PoisonEnemiesInside();
    }

    private void ApplyFrame(int _frameIndex)
    {
        Sprite frame = m_definition.GetImpactFrame(_frameIndex);
        m_renderer.sprite = frame;
        m_renderer.enabled = frame != null;
    }

    private void PoisonEnemiesInside()
    {
        int soakCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            m_definition.ImpactRadius,
            SoakBuffer,
            m_enemyLayer
        );

        m_poisonedEnemies.Clear();

        for (int i = 0; i < soakCount; i++)
        {
            EnemyStats enemyStats = SoakBuffer[i] != null
                ? SoakBuffer[i].GetComponentInParent<EnemyStats>()
                : null;

            // One enemy can own several colliders, and a double application would
            // stack the poison on itself.
            if (enemyStats == null || enemyStats.IsDead || !m_poisonedEnemies.Add(enemyStats))
                continue;

            EnemyPoison.Apply(
                enemyStats,
                m_definition.PoisonDamagePerTick,
                m_definition.PoisonTickInterval,
                m_definition.PoisonDuration,
                m_definition.PoisonColor,
                m_playerResources
            );
        }

        m_poisonedEnemies.Clear();
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
        m_renderer.sprite = null;
        gameObject.SetActive(false);
        Pool.Enqueue(this);
    }
}
