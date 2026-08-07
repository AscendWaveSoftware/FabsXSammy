using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Soft blob shadow on the ground beneath a character.
///
/// Billboarded sprites give the eye nothing to judge depth by, because they look
/// the same wherever they stand. The shadow puts a mark on the ground plane and
/// gives the sprite a place to belong to.
/// </summary>
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

        // Taken from the collider rather than a fixed height, so the shadow keeps
        // sitting at the feet when a character stands on higher ground.
        float groundHeight = m_ownerCollider != null
            ? m_ownerCollider.bounds.min.y
            : transform.position.y + m_feetOffset;

        Vector3 position = transform.position;
        position.y = groundHeight + m_groundClearance;
        position += GetBackwardDirection() * m_backwardOffset;

        m_shadowTransform.position = position;

        // World rotation, so neither the character's own rotation nor a rotated
        // spawn point can tip the shadow up off the ground plane.
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

        // Behind everything else in the arena. The attack telegraph rings sit at
        // -20 and have to stay readable on top of a shadow.
        m_shadowRenderer.sortingOrder = -50;
        m_shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
        m_shadowRenderer.receiveShadows = false;
        m_shadowRenderer.lightProbeUsage = LightProbeUsage.Off;
        m_shadowRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    /// <summary>
    /// Ground direction pointing away from the viewer.
    ///
    /// Taken from the camera rather than from a fixed world axis or from the sun:
    /// "behind the character" is something the player perceives on screen, so the
    /// offset has to stay put in screen terms even if the camera angle changes.
    /// A sun driven shadow would swing right around over the day and lose exactly
    /// the depth cue this is here to provide.
    /// </summary>
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
