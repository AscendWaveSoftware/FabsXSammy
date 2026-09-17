using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class SpellChainLightning : MonoBehaviour
{
    private const int PointsPerJump = 6;
    private const float JumpRevealDelay = 0.045f;
    private const float HoldDuration = 0.08f;
    private const float FadeDuration = 0.22f;
    private const float StartWidth = 0.17f;
    private const float EndWidth = 0.06f;
    private const float MaximumJitter = 0.34f;

    private static readonly Queue<SpellChainLightning> Pool = new Queue<SpellChainLightning>();
    private static readonly List<Vector3> PointBuffer = new List<Vector3>();
    private static readonly List<int> JumpEndBuffer = new List<int>();

    private static Material s_lineMaterial;
    private static bool s_hasLoggedMissingMaterial;

    private readonly List<Vector3> m_points = new();
    private readonly List<int> m_jumpEndIndices = new();

    private LineRenderer m_lineRenderer;
    private Color m_color;
    private float m_elapsedTime;
    private float m_revealDuration;
    private int m_revealedJumps;
    private bool m_isRunning;

    public static void Show(IReadOnlyList<Vector3> _anchors, Color _color, SpellDefinition _definition = null)
    {
        if (_anchors == null || _anchors.Count < 2)
            return;

        Material lineMaterial = _definition != null && _definition.EmissiveMaterial != null
            ? _definition.EmissiveMaterial
            : GetLineMaterial();

        if (lineMaterial == null)
            return;

        BuildJaggedPath(_anchors);

        SpellChainLightning bolt = GetOrCreate();
        bolt.gameObject.SetActive(true);
        bolt.Initialize(_color, lineMaterial);

        if (_definition != null && _definition.EmissiveMaterial != null)
            SpellEmission.Apply(bolt.m_lineRenderer, _definition);
    }

    private static SpellChainLightning GetOrCreate()
    {
        while (Pool.Count > 0)
        {
            SpellChainLightning pooledBolt = Pool.Dequeue();

            if (pooledBolt != null)
                return pooledBolt;
        }

        GameObject boltObject = new GameObject("Spell Chain Lightning", typeof(SpellChainLightning));
        return boltObject.GetComponent<SpellChainLightning>();
    }

    private static Material GetLineMaterial()
    {
        if (s_lineMaterial != null)
            return s_lineMaterial;

        GameObject probe = new GameObject("Sprite Material Probe");
        probe.hideFlags = HideFlags.HideAndDontSave;
        s_lineMaterial = probe.AddComponent<SpriteRenderer>().sharedMaterial;
        Destroy(probe);

        if (s_lineMaterial == null)
        {
            Shader spriteShader = Shader.Find("Sprites/Default");

            if (spriteShader != null)
                s_lineMaterial = new Material(spriteShader) { hideFlags = HideFlags.HideAndDontSave };
        }

        if (s_lineMaterial == null && !s_hasLoggedMissingMaterial)
        {
            Debug.LogWarning("Chain lightning found no usable sprite material and stays invisible.");
            s_hasLoggedMissingMaterial = true;
        }

        return s_lineMaterial;
    }

    private static void BuildJaggedPath(IReadOnlyList<Vector3> _anchors)
    {
        PointBuffer.Clear();
        JumpEndBuffer.Clear();
        PointBuffer.Add(_anchors[0]);

        for (int anchorIndex = 1; anchorIndex < _anchors.Count; anchorIndex++)
        {
            Vector3 from = _anchors[anchorIndex - 1];
            Vector3 to = _anchors[anchorIndex];
            Vector3 segment = to - from;
            float length = segment.magnitude;

            if (length < 0.0001f)
            {
                PointBuffer.Add(to);
                JumpEndBuffer.Add(PointBuffer.Count - 1);
                continue;
            }

            Vector3 direction = segment / length;
            Vector3 side = Vector3.Cross(direction, Vector3.up);

            if (side.sqrMagnitude < 0.001f)
                side = Vector3.Cross(direction, Vector3.forward);

            side.Normalize();
            Vector3 up = Vector3.Cross(side, direction).normalized;
            float jitter = Mathf.Min(MaximumJitter, length * 0.12f);

            for (int step = 1; step < PointsPerJump; step++)
            {
                float travel = step / (float)PointsPerJump;

                float taper = Mathf.Sin(travel * Mathf.PI);
                Vector3 offset = (side * Random.Range(-1f, 1f) + up * Random.Range(-1f, 1f)) * (jitter * taper);
                PointBuffer.Add(from + direction * (length * travel) + offset);
            }

            PointBuffer.Add(to);
            JumpEndBuffer.Add(PointBuffer.Count - 1);
        }
    }

    private void Awake()
    {
        m_lineRenderer = gameObject.AddComponent<LineRenderer>();
        m_lineRenderer.useWorldSpace = true;
        m_lineRenderer.alignment = LineAlignment.View;
        m_lineRenderer.textureMode = LineTextureMode.Stretch;
        m_lineRenderer.numCapVertices = 2;
        m_lineRenderer.numCornerVertices = 2;
        m_lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        m_lineRenderer.receiveShadows = false;
        m_lineRenderer.lightProbeUsage = LightProbeUsage.Off;
        m_lineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        m_lineRenderer.sortingOrder = 212;
    }

    private void Update()
    {
        if (PveRuntime.IsPaused)
            return;

        if (!m_isRunning)
            return;

        m_elapsedTime += Time.deltaTime;

        if (m_elapsedTime < m_revealDuration)
        {
            UpdateReveal();
            return;
        }

        ShowAllJumps();

        float fadeProgress = Mathf.Clamp01((m_elapsedTime - m_revealDuration - HoldDuration) / FadeDuration);

        if (fadeProgress >= 1f)
        {
            ReturnToPool();
            return;
        }

        ApplyColor(1f - fadeProgress);
        ApplyWidth(1f - fadeProgress);
    }

    private void Initialize(Color _color, Material _lineMaterial)
    {
        m_points.Clear();
        m_points.AddRange(PointBuffer);
        m_jumpEndIndices.Clear();
        m_jumpEndIndices.AddRange(JumpEndBuffer);

        m_color = _color;
        m_elapsedTime = 0f;
        m_revealedJumps = 0;
        m_revealDuration = m_jumpEndIndices.Count * JumpRevealDelay;
        m_isRunning = true;

        m_lineRenderer.sharedMaterial = _lineMaterial;
        ApplyColor(1f);
        ApplyWidth(1f);
        UpdateReveal();
    }

    private void UpdateReveal()
    {
        int visibleJumps = Mathf.Clamp(
            Mathf.FloorToInt(m_elapsedTime / JumpRevealDelay) + 1,
            1,
            m_jumpEndIndices.Count
        );

        if (visibleJumps == m_revealedJumps)
            return;

        m_revealedJumps = visibleJumps;
        int pointCount = m_jumpEndIndices[visibleJumps - 1] + 1;

        m_lineRenderer.positionCount = pointCount;

        for (int i = 0; i < pointCount; i++)
            m_lineRenderer.SetPosition(i, m_points[i]);
    }

    private void ShowAllJumps()
    {
        if (m_revealedJumps >= m_jumpEndIndices.Count)
            return;

        m_revealedJumps = m_jumpEndIndices.Count;
        m_lineRenderer.positionCount = m_points.Count;

        for (int i = 0; i < m_points.Count; i++)
            m_lineRenderer.SetPosition(i, m_points[i]);
    }

    private void ApplyColor(float _alpha)
    {
        Color color = m_color;
        color.a = m_color.a * Mathf.Clamp01(_alpha);

        Color tailColor = color;
        tailColor.a *= 0.55f;

        m_lineRenderer.startColor = color;
        m_lineRenderer.endColor = tailColor;
    }

    private void ApplyWidth(float _scale)
    {
        float scale = Mathf.Clamp01(_scale);
        m_lineRenderer.startWidth = StartWidth * scale;
        m_lineRenderer.endWidth = EndWidth * scale;
    }

    private void ReturnToPool()
    {
        if (!m_isRunning)
            return;

        m_isRunning = false;
        m_lineRenderer.positionCount = 0;
        gameObject.SetActive(false);
        Pool.Enqueue(this);
    }
}
