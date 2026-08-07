using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Light dust puffs kicked up at the player's feet while moving. Emission is
/// driven by ground actually covered, so the dust spaces itself out like
/// footsteps instead of pouring out at a fixed rate.
/// </summary>
[DisallowMultipleComponent]
public class PlayerFootstepDust : MonoBehaviour
{
    private sealed class DustPuff
    {
        public Transform Transform;
        public SpriteRenderer Renderer;
        public Vector3 Drift;
        public float Elapsed;
        public float Lifetime;
        public float StartDiameter;
        public float EndDiameter;
        public float StartAlpha;
        public bool IsActive;
    }

    [Header("References")]
    [SerializeField] private Rigidbody m_rigidbody;
    [SerializeField] private PlayerMovementHandler m_movement;
    [SerializeField] private PlayerHealth m_health;

    [Header("Emission")]
    [SerializeField, Min(0.02f), Tooltip("Ground covered between two dust puffs.")]
    private float m_stepDistance = 0.6f;
    [SerializeField, Range(0f, 1f), Tooltip("Normalized speed below which the player is too slow to raise dust.")]
    private float m_minimumNormalizedSpeed = 0.1f;
    [SerializeField, Range(1, 48), Tooltip("Pool size. Must cover lifetime x emission rate at full sprint, or puffs get recycled while still visible.")]
    private int m_maximumPuffs = 20;
    [SerializeField, Min(0.1f), Tooltip("Movement in a single frame above this counts as a teleport, not as travel.")]
    private float m_teleportDistance = 2f;

    [Header("Appearance")]
    [SerializeField] private Color m_dustColor = new(0.79f, 0.73f, 0.61f, 1f);
    [SerializeField, Range(0f, 1f)] private float m_walkAlpha = 0.2f;
    [SerializeField, Range(0f, 1f)] private float m_runAlpha = 0.38f;
    [SerializeField, Min(0.01f)] private float m_startDiameter = 0.16f;
    [SerializeField, Min(0.01f)] private float m_walkEndDiameter = 0.36f;
    [SerializeField, Min(0.01f)] private float m_runEndDiameter = 0.58f;
    [SerializeField, Min(0.05f)] private float m_lifetime = 0.55f;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float m_riseSpeed = 0.34f;
    [SerializeField, Min(0f), Tooltip("How strongly the dust trails behind the running player.")]
    private float m_backwardDrift = 0.6f;
    [SerializeField, Min(0f)] private float m_sidewaysScatter = 0.13f;
    [SerializeField, Min(0f)] private float m_groundClearance = 0.05f;

    private DustPuff[] m_puffs;
    private Transform m_puffRoot;
    private Transform m_cameraTransform;
    private Vector3 m_lastPosition;
    private float m_travelledDistance;
    private float m_feetOffset;
    private int m_nextPuffIndex;

    private void Awake()
    {
        if (m_rigidbody == null)
            m_rigidbody = GetComponent<Rigidbody>();

        if (m_movement == null)
            m_movement = GetComponent<PlayerMovementHandler>();

        if (m_health == null)
            m_health = GetComponent<PlayerHealth>();

        MeasureFeetOffset();
        BuildPuffs();
    }

    private void OnEnable()
    {
        m_lastPosition = transform.position;
        m_travelledDistance = 0f;
    }

    private void OnDisable()
    {
        if (m_puffs == null)
            return;

        foreach (DustPuff puff in m_puffs)
            DeactivatePuff(puff);
    }

    private void OnDestroy()
    {
        if (m_puffRoot != null)
            Destroy(m_puffRoot.gameObject);
    }

    private void Update()
    {
        if (PveRuntime.IsPaused)
            return;

        UpdatePuffs();
        UpdateEmission();
    }

    private void LateUpdate()
    {
        if (m_cameraTransform == null)
            ResolveCamera();

        if (m_cameraTransform == null)
            return;

        // Sprite front faces back towards the camera, matching the other
        // billboarded combat effects.
        Quaternion facing = Quaternion.LookRotation(
            m_cameraTransform.rotation * -Vector3.forward,
            m_cameraTransform.rotation * Vector3.up
        );

        foreach (DustPuff puff in m_puffs)
        {
            if (puff.IsActive)
                puff.Transform.rotation = facing;
        }
    }

    private void UpdateEmission()
    {
        Vector3 currentPosition = transform.position;
        Vector3 frameMovement = currentPosition - m_lastPosition;
        frameMovement.y = 0f;
        m_lastPosition = currentPosition;

        float frameDistance = frameMovement.magnitude;

        // Death respawns teleport the player across the map. That is not ground
        // covered on foot and must not erupt into a cloud of dust.
        if (frameDistance > m_teleportDistance)
        {
            m_travelledDistance = 0f;
            return;
        }

        if (!CanEmit())
        {
            m_travelledDistance = 0f;
            return;
        }

        float normalizedSpeed = GetNormalizedSpeed();

        if (normalizedSpeed < m_minimumNormalizedSpeed)
        {
            m_travelledDistance = 0f;
            return;
        }

        m_travelledDistance += frameDistance;

        if (m_travelledDistance < m_stepDistance)
            return;

        m_travelledDistance -= m_stepDistance;
        SpawnPuff(frameMovement / Mathf.Max(0.0001f, frameDistance), normalizedSpeed);
    }

    private bool CanEmit()
    {
        return Time.timeScale > 0f &&
               m_rigidbody != null &&
               (m_health == null || m_health.IsAlive);
    }

    private float GetNormalizedSpeed()
    {
        Vector3 velocity = m_rigidbody.linearVelocity;
        float planarSpeed = new Vector2(velocity.x, velocity.z).magnitude;
        float maximumSpeed = m_movement != null ? m_movement.CurrentMaxPlanarSpeed : 1f;

        return Mathf.Clamp01(planarSpeed / Mathf.Max(0.01f, maximumSpeed));
    }

    private void SpawnPuff(Vector3 _moveDirection, float _normalizedSpeed)
    {
        DustPuff puff = m_puffs[m_nextPuffIndex];
        m_nextPuffIndex = (m_nextPuffIndex + 1) % m_puffs.Length;

        // Running kicks up noticeably more than a careful walk.
        float intensity = Mathf.InverseLerp(m_minimumNormalizedSpeed, 1f, _normalizedSpeed);

        Vector2 scatter = Random.insideUnitCircle * m_sidewaysScatter;
        Vector3 spawnPosition = transform.position;
        spawnPosition.x += scatter.x;
        spawnPosition.z += scatter.y;
        spawnPosition.y += m_feetOffset + m_groundClearance;

        puff.Transform.position = spawnPosition;
        puff.Drift = -_moveDirection * (m_backwardDrift * intensity) + Vector3.up * m_riseSpeed;
        puff.Elapsed = 0f;
        puff.Lifetime = m_lifetime * Random.Range(0.85f, 1.15f);
        puff.StartDiameter = m_startDiameter;
        puff.EndDiameter = Mathf.Lerp(m_walkEndDiameter, m_runEndDiameter, intensity);
        puff.StartAlpha = Mathf.Lerp(m_walkAlpha, m_runAlpha, intensity);
        puff.IsActive = true;

        puff.Transform.localScale = Vector3.one * puff.StartDiameter;
        puff.Renderer.color = GetPuffColor(puff.StartAlpha);
        puff.Renderer.gameObject.SetActive(true);
    }

    private void UpdatePuffs()
    {
        // Scaled time on purpose: the dust belongs to the world, so it slows down
        // together with everything else during the combat hit slow motion.
        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
            return;

        foreach (DustPuff puff in m_puffs)
        {
            if (!puff.IsActive)
                continue;

            puff.Elapsed += deltaTime;
            float normalizedTime = Mathf.Clamp01(puff.Elapsed / puff.Lifetime);

            if (normalizedTime >= 1f)
            {
                DeactivatePuff(puff);
                continue;
            }

            puff.Transform.position += puff.Drift * deltaTime;

            // Air resistance, so the puff settles instead of shooting away.
            puff.Drift = Vector3.Lerp(puff.Drift, Vector3.zero, Mathf.Clamp01(deltaTime * 2.6f));

            float expansion = 1f - Mathf.Pow(1f - normalizedTime, 2f);
            puff.Transform.localScale = Vector3.one * Mathf.Lerp(puff.StartDiameter, puff.EndDiameter, expansion);

            float fadeIn = Mathf.Clamp01(normalizedTime / 0.18f);
            float fadeOut = 1f - Mathf.InverseLerp(0.35f, 1f, normalizedTime);
            puff.Renderer.color = GetPuffColor(puff.StartAlpha * fadeIn * fadeOut);
        }
    }

    private Color GetPuffColor(float _alpha)
    {
        Color color = m_dustColor;
        color.a = Mathf.Clamp01(_alpha);
        return color;
    }

    private void MeasureFeetOffset()
    {
        Collider playerCollider = GetComponent<Collider>();

        m_feetOffset = playerCollider != null
            ? playerCollider.bounds.min.y - transform.position.y
            : -1f;
    }

    private void BuildPuffs()
    {
        CombatFeedbackSprites.Prewarm();

        // Kept outside the player hierarchy: dust settles on the ground where it
        // was raised, it must not be dragged along by the player transform.
        GameObject rootObject = new GameObject("Player Footstep Dust");
        m_puffRoot = rootObject.transform;

        m_puffs = new DustPuff[m_maximumPuffs];

        for (int i = 0; i < m_puffs.Length; i++)
        {
            GameObject puffObject = new GameObject("Dust Puff");
            puffObject.transform.SetParent(m_puffRoot, false);

            SpriteRenderer puffRenderer = puffObject.AddComponent<SpriteRenderer>();
            puffRenderer.sprite = CombatFeedbackSprites.Glow;

            // Behind the characters, so the dust never washes out the sprites.
            puffRenderer.sortingOrder = -30;
            puffRenderer.shadowCastingMode = ShadowCastingMode.Off;
            puffRenderer.receiveShadows = false;
            puffRenderer.lightProbeUsage = LightProbeUsage.Off;
            puffRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            puffObject.SetActive(false);

            m_puffs[i] = new DustPuff
            {
                Transform = puffObject.transform,
                Renderer = puffRenderer
            };
        }
    }

    private static void DeactivatePuff(DustPuff _puff)
    {
        if (!_puff.IsActive)
            return;

        _puff.IsActive = false;
        _puff.Renderer.gameObject.SetActive(false);
    }

    private void ResolveCamera()
    {
        if (CameraReferences.Instance != null)
            m_cameraTransform = CameraReferences.Instance.PlayerCameraTransform;

        if (m_cameraTransform == null && Camera.main != null)
            m_cameraTransform = Camera.main.transform;
    }

    private void OnValidate()
    {
        m_walkEndDiameter = Mathf.Max(m_startDiameter, m_walkEndDiameter);
        m_runEndDiameter = Mathf.Max(m_walkEndDiameter, m_runEndDiameter);
        m_runAlpha = Mathf.Max(m_walkAlpha, m_runAlpha);
    }
}
