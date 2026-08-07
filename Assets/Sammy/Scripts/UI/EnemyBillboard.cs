using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(SpriteRenderer))]
public class EnemyBillboard : MonoBehaviour
{
    [Header("Occlusion Outline")]
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private Color outlineColor = new Color(1f, 0.16f, 0.05f, 1f);
    [SerializeField, Range(0.002f, 0.05f)] private float outlineWidth = 0.018f;

    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    private static readonly int SpriteUvRectId = Shader.PropertyToID("_SpriteUVRect");
    private static readonly int UvExpansionId = Shader.PropertyToID("_UVExpansion");
    private static readonly int BoundsCenterId = Shader.PropertyToID("_BoundsCenter");

    private Transform m_cameraTransform;
    private SpriteRenderer m_sourceRenderer;
    private SpriteRenderer m_outlineRenderer;
    private MaterialPropertyBlock m_outlineProperties;
    private Sprite m_lastSprite;

    private void Awake()
    {
        m_sourceRenderer = GetComponent<SpriteRenderer>();
        CreateOutlineRenderer();
    }

    private void Start()
    {
        if (CameraReferences.Instance == null)
            return;

        m_cameraTransform = CameraReferences.Instance.PlayerCameraTransform;

        if (m_cameraTransform == null)
            Debug.LogWarning("PlayerCameraTransform is not assigned in CameraReferences.");
    }

    private void LateUpdate()
    {
        SyncOutlineRenderer();

        if (m_cameraTransform == null)
            return;

        // Along the camera forward, not against it. Pointing the sprite's +Z back
        // at the camera shows its reverse side, which mirrors the artwork - only
        // invisible while the enemies were symmetrical placeholder circles.
        transform.LookAt(
            transform.position + m_cameraTransform.rotation * Vector3.forward,
            m_cameraTransform.rotation * Vector3.up
        );
    }

    private void CreateOutlineRenderer()
    {
        if (m_sourceRenderer == null || outlineMaterial == null)
            return;

        GameObject outlineObject = new GameObject("Occlusion Outline");
        outlineObject.layer = gameObject.layer;
        outlineObject.transform.SetParent(transform, false);

        m_outlineRenderer = outlineObject.AddComponent<SpriteRenderer>();
        m_outlineRenderer.sharedMaterial = outlineMaterial;
        m_outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        m_outlineRenderer.receiveShadows = false;
        m_outlineRenderer.lightProbeUsage = LightProbeUsage.Off;
        m_outlineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        m_outlineRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        m_outlineProperties = new MaterialPropertyBlock();
        SyncOutlineRenderer(true);
    }

    private void SyncOutlineRenderer(bool _forcePropertyRefresh = false)
    {
        if (m_outlineRenderer == null || m_sourceRenderer == null)
            return;

        m_outlineRenderer.enabled = m_sourceRenderer.enabled && m_sourceRenderer.sprite != null;
        m_outlineRenderer.sprite = m_sourceRenderer.sprite;
        m_outlineRenderer.flipX = m_sourceRenderer.flipX;
        m_outlineRenderer.flipY = m_sourceRenderer.flipY;
        m_outlineRenderer.drawMode = m_sourceRenderer.drawMode;
        m_outlineRenderer.size = m_sourceRenderer.size;
        m_outlineRenderer.tileMode = m_sourceRenderer.tileMode;
        m_outlineRenderer.sortingLayerID = m_sourceRenderer.sortingLayerID;
        m_outlineRenderer.sortingOrder = m_sourceRenderer.sortingOrder;
        m_outlineRenderer.maskInteraction = m_sourceRenderer.maskInteraction;

        if (_forcePropertyRefresh || m_lastSprite != m_sourceRenderer.sprite)
            UpdateOutlineProperties();
    }

    private void UpdateOutlineProperties()
    {
        Sprite sprite = m_sourceRenderer.sprite;
        m_lastSprite = sprite;

        if (sprite == null || sprite.texture == null)
            return;

        Rect textureRect = sprite.textureRect;
        float textureWidth = Mathf.Max(1f, sprite.texture.width);
        float textureHeight = Mathf.Max(1f, sprite.texture.height);
        Vector4 uvRect = new Vector4(
            textureRect.xMin / textureWidth,
            textureRect.yMin / textureHeight,
            textureRect.xMax / textureWidth,
            textureRect.yMax / textureHeight
        );

        Vector2 spriteSize = sprite.bounds.size;
        Vector2 uvExpansion = new Vector2(
            outlineWidth / Mathf.Max(0.001f, spriteSize.x),
            outlineWidth / Mathf.Max(0.001f, spriteSize.y)
        );

        m_outlineRenderer.GetPropertyBlock(m_outlineProperties);
        m_outlineProperties.SetColor(OutlineColorId, outlineColor);
        m_outlineProperties.SetFloat(OutlineWidthId, outlineWidth);
        m_outlineProperties.SetVector(SpriteUvRectId, uvRect);
        m_outlineProperties.SetVector(UvExpansionId, uvExpansion);
        m_outlineProperties.SetVector(BoundsCenterId, sprite.bounds.center);
        m_outlineRenderer.SetPropertyBlock(m_outlineProperties);
    }

    private void OnValidate()
    {
        outlineWidth = Mathf.Clamp(outlineWidth, 0.002f, 0.05f);

        if (Application.isPlaying && m_outlineRenderer != null)
            UpdateOutlineProperties();
    }
}
