using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class PlayerOcclusionFader : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private UnityEngine.Camera renderCamera;
    [SerializeField] private LayerMask occluderMask;

    [Header("Fade")]
    [Range(0.05f, 1f)]
    [SerializeField] private float fadedAlpha = 0.35f;

    [Min(0.01f)]
    [SerializeField] private float fadeSpeed = 8f;

    [Header("Raycast Target")]
    [SerializeField] private Vector3 playerTargetOffset = new Vector3(0f, 1f, 0f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
    private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
    private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

    private readonly Dictionary<Renderer, float> fadeValues = new();
    private readonly Dictionary<Renderer, Material[]> runtimeMaterials = new();
    private readonly Dictionary<Material, Color> originalColors = new();

    private readonly List<Renderer> cachedRenderers = new();
    private readonly HashSet<Renderer> currentOccluders = new();

    private void Awake()
    {
        if (!renderCamera)
            renderCamera = UnityEngine.Camera.main;
    }

    private void LateUpdate()
    {
        if (!player || !renderCamera) return;

        currentOccluders.Clear();
        DetectOccluders();

        cachedRenderers.Clear();
        cachedRenderers.AddRange(fadeValues.Keys);

        foreach (Renderer renderer in cachedRenderers)
        {
            if (!renderer)
            {
                fadeValues.Remove(renderer);
                runtimeMaterials.Remove(renderer);
                continue;
            }

            float targetFade = currentOccluders.Contains(renderer) ? 1f : 0f;

            fadeValues[renderer] = Mathf.MoveTowards(
                fadeValues[renderer],
                targetFade,
                fadeSpeed * Time.deltaTime
            );
        }

        ApplyFade();
    }

    private void DetectOccluders()
    {
        Vector3 origin = renderCamera.transform.position;
        Vector3 target = player.position + playerTargetOffset;
        Vector3 direction = target - origin;
        float distance = direction.magnitude;

        if (distance <= 0.001f) return;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction.normalized,
            distance,
            occluderMask,
            QueryTriggerInteraction.Ignore
        );

        foreach (RaycastHit hit in hits)
        {
            Renderer renderer = hit.collider.GetComponentInParent<Renderer>();
            if (!renderer) continue;

            RegisterRenderer(renderer);
            currentOccluders.Add(renderer);
        }
    }

    private void ApplyFade()
    {
        cachedRenderers.Clear();
        cachedRenderers.AddRange(fadeValues.Keys);

        foreach (Renderer renderer in cachedRenderers)
        {
            if (!renderer) continue;

            float fade = fadeValues[renderer];

            if (!runtimeMaterials.TryGetValue(renderer, out Material[] materials))
                continue;

            foreach (Material mat in materials)
            {
                if (!mat || !mat.HasProperty(BaseColorId)) continue;

                if (!originalColors.TryGetValue(mat, out Color original))
                    continue;

                Color current = original;
                current.a = Mathf.Lerp(original.a, fadedAlpha, fade);

                mat.SetColor(BaseColorId, current);

                if (fade > 0.001f)
                    SetTransparent(mat);
                else
                    SetOpaque(mat);
            }
        }
    }

    private void RegisterRenderer(Renderer renderer)
    {
        if (fadeValues.ContainsKey(renderer)) return;

        fadeValues.Add(renderer, 0f);

        Material[] mats = renderer.materials;
        runtimeMaterials.Add(renderer, mats);

        foreach (Material mat in mats)
        {
            if (!mat || !mat.HasProperty(BaseColorId)) continue;

            if (!originalColors.ContainsKey(mat))
                originalColors.Add(mat, mat.GetColor(BaseColorId));
        }
    }

    private void SetTransparent(Material mat)
    {
        if (!mat.HasProperty(SurfaceId)) return;

        mat.SetFloat(SurfaceId, 1f);

        if (mat.HasProperty(SrcBlendId))
            mat.SetInt(SrcBlendId, (int)BlendMode.SrcAlpha);

        if (mat.HasProperty(DstBlendId))
            mat.SetInt(DstBlendId, (int)BlendMode.OneMinusSrcAlpha);

        if (mat.HasProperty(ZWriteId))
            mat.SetInt(ZWriteId, 0);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
    }

    private void SetOpaque(Material mat)
    {
        if (originalColors.TryGetValue(mat, out Color original))
            mat.SetColor(BaseColorId, original);

        if (!mat.HasProperty(SurfaceId)) return;

        mat.SetFloat(SurfaceId, 0f);

        if (mat.HasProperty(SrcBlendId))
            mat.SetInt(SrcBlendId, (int)BlendMode.One);

        if (mat.HasProperty(DstBlendId))
            mat.SetInt(DstBlendId, (int)BlendMode.Zero);

        if (mat.HasProperty(ZWriteId))
            mat.SetInt(ZWriteId, 1);

        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Geometry;
    }
}