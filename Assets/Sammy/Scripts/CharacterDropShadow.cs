using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class CharacterDropShadow : MonoBehaviour
{
    [Header("Shape")]
    [SerializeField, Min(0.05f), Tooltip("Shadow width in world units. Roughly the footprint of the character, not the size of its sprite.")]
    private float m_diameter = 1.3f;
    [SerializeField, Range(0f, 1f), Tooltip("Kept below opaque. A shadow that reads as a hole in the ground looks worse than none at all.")]
    private float m_opacity = 0.6f;
    [SerializeField] private Color m_shadowColor = new(0.04f, 0.05f, 0.09f, 1f);

    [Header("Placement")]
    [SerializeField, Min(0f), Tooltip("Lift off the ground, so the shadow does not fight the terrain for the same pixels.")]
    private float m_groundClearance = 0.03f;
    [SerializeField, Min(0f), Tooltip("Slides the shadow away from the camera along the ground, so the character reads as standing in front of it rather than on a symmetric blob.")]
    private float m_backwardOffset = 0.18f;

    private Transform m_shadowTransform;
    private SpriteRenderer m_shadowRenderer;
    private Transform m_cameraTransform;
    private Collider m_ownerCollider;
    private float m_feetOffset;

    private void Awake()
    {
        CombatFeedbackSprites.Prewarm();

        m_ownerCollider = GetComponent<Collider>();
        m_feetOffset = m_ownerCollider != null
            ? m_ownerCollider.bounds.min.y - transform.position.y
            : -1f;

        BuildShadow();
        UpdateShadow();
    }

    private void LateUpdate()
    {
        if (PveRuntime.IsPaused)
            return;

        UpdateShadow();
    }

    private void UpdateShadow()
    {
        if (m_shadowTransform == null)
            return;

        float groundHeight = m_ownerCollider != null
            ? m_ownerCollider.bounds.min.y
            : transform.position.y + m_feetOffset;

        Vector3 position = transform.position;
        position.y = groundHeight + m_groundClearance;
        position += GetBackwardDirection() * m_backwardOffset;

        m_shadowTransform.position = position;

        m_shadowTransform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        m_shadowTransform.localScale = Vector3.one * m_diameter;
    }

    private void BuildShadow()
    {
        GameObject shadowObject = new GameObject("Drop Shadow");
        shadowObject.transform.SetParent(transform, false);
        m_shadowTransform = shadowObject.transform;

        m_shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
        m_shadowRenderer.sprite = CombatFeedbackSprites.SoftShadow;
        m_shadowRenderer.color = GetShadowColor();

        m_shadowRenderer.sortingOrder = -50;
        m_shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
        m_shadowRenderer.receiveShadows = false;
        m_shadowRenderer.lightProbeUsage = LightProbeUsage.Off;
        m_shadowRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private Vector3 GetBackwardDirection()
    {
        if (m_backwardOffset <= 0f)
            return Vector3.zero;

        if (m_cameraTransform == null)
            ResolveCamera();

        if (m_cameraTransform == null)
            return Vector3.zero;

        Vector3 awayFromCamera = m_cameraTransform.rotation * Vector3.forward;
        awayFromCamera.y = 0f;

        return awayFromCamera.sqrMagnitude > 0.0001f ? awayFromCamera.normalized : Vector3.zero;
    }

    private void ResolveCamera()
    {
        if (CameraReferences.Instance != null)
            m_cameraTransform = CameraReferences.Instance.PlayerCameraTransform;

        if (m_cameraTransform == null && Camera.main != null)
            m_cameraTransform = Camera.main.transform;
    }

    private Color GetShadowColor()
    {
        Color color = m_shadowColor;
        color.a = m_opacity;
        return color;
    }

    private void OnValidate()
    {
        m_diameter = Mathf.Max(0.05f, m_diameter);
        m_opacity = Mathf.Clamp01(m_opacity);

        if (Application.isPlaying && m_shadowRenderer != null)
            m_shadowRenderer.color = GetShadowColor();
    }
}
