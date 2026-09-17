using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class VortexTether : MonoBehaviour
{
    private const int PointCount = 16;

    private const float SpiralTwist = 1.35f;

    private static readonly Queue<VortexTether> Pool = new Queue<VortexTether>();

    private LineRenderer m_line;
    private Vector3[] m_points;

    public static VortexTether Acquire(SpellDefinition _definition)
    {
        VortexTether tether = GetOrCreate();
        tether.gameObject.SetActive(true);
        tether.Configure(_definition);
        return tether;
    }

    private static VortexTether GetOrCreate()
    {
        while (Pool.Count > 0)
        {
            VortexTether pooledTether = Pool.Dequeue();

            if (pooledTether != null)
                return pooledTether;
        }

        GameObject tetherObject = new GameObject("Vortex Tether", typeof(VortexTether));
        return tetherObject.GetComponent<VortexTether>();
    }

    private void Awake()
    {
        m_points = new Vector3[PointCount];

        m_line = gameObject.AddComponent<LineRenderer>();
        m_line.useWorldSpace = true;
        m_line.positionCount = PointCount;
        m_line.numCapVertices = 2;
        m_line.textureMode = LineTextureMode.Stretch;
        m_line.alignment = LineAlignment.View;
        m_line.sortingOrder = 206;
        m_line.shadowCastingMode = ShadowCastingMode.Off;
        m_line.receiveShadows = false;
        m_line.lightProbeUsage = LightProbeUsage.Off;
        m_line.reflectionProbeUsage = ReflectionProbeUsage.Off;

        m_line.widthCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.45f, 0.66f),
            new Keyframe(1f, 0.22f)
        );
    }

    private void Configure(SpellDefinition _definition)
    {
        m_line.widthMultiplier = Mathf.Max(0.01f, _definition.VortexTetherWidth);

        Color nearColor = _definition.GlowColor;
        Color farColor = _definition.TrailColor;

        m_line.colorGradient = new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(nearColor, 0f),
                new GradientColorKey(farColor, 1f)
            },
            alphaKeys = new[]
            {
                new GradientAlphaKey(nearColor.a, 0f),
                new GradientAlphaKey(farColor.a * 0.55f, 1f)
            }
        };

        SpellEmission.Apply(m_line, _definition);
    }

    public void UpdateShape(Vector3 _holeCenter, Vector3 _enemyPoint, float _strength)
    {
        Vector3 offset = _enemyPoint - _holeCenter;
        offset.y = 0f;

        float radius = offset.magnitude;

        if (radius < 0.001f)
        {
            m_line.enabled = false;
            return;
        }

        m_line.enabled = true;

        float baseAngle = Mathf.Atan2(offset.z, offset.x);

        for (int i = 0; i < PointCount; i++)
        {
            float along = i / (float)(PointCount - 1);

            float angle = baseAngle + SpiralTwist * (1f - along) * (1f - along);
            float pointRadius = radius * along;

            Vector3 point = _holeCenter + new Vector3(
                Mathf.Cos(angle) * pointRadius,
                0f,
                Mathf.Sin(angle) * pointRadius
            );

            point.y = Mathf.Lerp(_holeCenter.y, _enemyPoint.y, along) +
                      Mathf.Sin(along * Mathf.PI) * 0.28f;

            m_points[i] = point;
        }

        m_line.SetPositions(m_points);

        Color startColor = m_line.startColor;
        Color endColor = m_line.endColor;
        startColor.a = Mathf.Clamp01(_strength);
        endColor.a = Mathf.Clamp01(_strength) * 0.55f;
        m_line.startColor = startColor;
        m_line.endColor = endColor;
    }

    public void Release()
    {
        m_line.enabled = false;
        gameObject.SetActive(false);
        Pool.Enqueue(this);
    }
}
