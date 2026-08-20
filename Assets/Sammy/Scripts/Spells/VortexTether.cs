using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Pooled stream of matter drawn from a caught enemy into the singularity. The
/// line curves rather than running straight, which is what makes the pull read as
/// orbital instead of as a rope, and it tells the player at a glance exactly who
/// the hole has hold of.
/// </summary>
public class VortexTether : MonoBehaviour
{
    private const int PointCount = 16;

    /// <summary>How far the stream winds around the hole on its way in, in radians.</summary>
    private const float SpiralTwist = 1.35f;

    private static readonly Queue<VortexTether> Pool = new Queue<VortexTether>();

    private LineRenderer m_line;
    private Vector3[] m_points;

    /// <summary>Takes a tether out of the pool and puts it on the spell's material.</summary>
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

        // Fat where the matter disappears into the hole, drawn out to a thread at
        // the enemy end, so the stream looks stretched by the gravity.
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

    /// <summary>
    /// Redraws the stream. Called every frame by the vortex, because both ends
    /// keep moving while the enemy is dragged in.
    /// </summary>
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
            // 0 sits in the hole, 1 sits on the enemy.
            float along = i / (float)(PointCount - 1);

            // The twist is spent near the centre, where an orbit would be fastest.
            float angle = baseAngle + SpiralTwist * (1f - along) * (1f - along);
            float pointRadius = radius * along;

            Vector3 point = _holeCenter + new Vector3(
                Mathf.Cos(angle) * pointRadius,
                0f,
                Mathf.Sin(angle) * pointRadius
            );

            // Lifted onto an arc between the hovering hole and the enemy's feet,
            // so the stream does not cut through the ground on the way.
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
