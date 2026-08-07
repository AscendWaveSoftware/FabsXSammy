using UnityEngine;

/// <summary>How a spell reaches its target.</summary>
public enum SpellDelivery
{
    /// <summary>Fires a homing projectile that detonates on contact.</summary>
    Projectile,

    /// <summary>Places a lingering cloud on the ground at the target.</summary>
    Cloud,

    /// <summary>Bursts instantly around the caster.</summary>
    Nova
}

/// <summary>A single value a spell upgrade card can improve.</summary>
public enum SpellStat
{
    Damage,
    Radius,
    Cooldown,
    PoisonDamage,
    PoisonDuration,
    StunDuration
}

/// <summary>
/// Data for a single castable spell. The sprite sheet is split into a travelling
/// projectile frame and the impact frames that play where the spell detonates.
/// A cloud spell has no projectile and uses the whole sheet as its animation.
/// </summary>
[CreateAssetMenu(fileName = "New Spell", menuName = "Spells/Spell Definition")]
public class SpellDefinition : ScriptableObject
{
    [Header("Display")]
    public string SpellName = "Astral";
    public SpellDelivery Delivery = SpellDelivery.Projectile;
    [Tooltip("Accent colour used by the HUD slot for this spell.")]
    public Color UiColor = new(0.72f, 0.45f, 1f, 1f);
    [Tooltip("Played once when the spell is cast. Optional, a spell without one stays silent.")]
    public AudioClip CastClip;

    [Header("Frames")]
    [Tooltip("Sliced sprite sheet, filled by the spell asset builder.")]
    public Sprite[] Frames;
    [Tooltip("Frame that travels through the air as the projectile core.")]
    [Min(0)] public int ProjectileFrame;
    [Tooltip("First frame of the impact animation. Everything from here to the end plays on detonation.")]
    [Min(0)] public int ImpactStartFrame = 1;
    [Min(1f)] public float ImpactFrameRate = 18f;

    [Header("Casting")]
    [Min(0f)] public float Cooldown = 3f;
    [Tooltip("Radius the caster searches for a target. Without a target the spell fires straight ahead.")]
    [Min(1f)] public float TargetSearchRange = 16f;
    [Tooltip("Height above the player pivot the spell originates at. The body spans -1 to +0.94, so this is chest height.")]
    [Min(0f)] public float SpawnHeight = 0.25f;

    [Header("Projectile")]
    [Min(0.1f)] public float Speed = 14f;
    [Tooltip("Degrees per second the projectile may steer towards its target. 0 flies dead straight.")]
    [Min(0f)] public float TurnRate = 220f;
    [Min(0.1f)] public float Lifetime = 2.5f;
    [Tooltip("Radius around the projectile that triggers the detonation.")]
    [Min(0.05f)] public float HitRadius = 0.5f;
    [Tooltip("Scale applied to the projectile frame. The core is only 6 of 48 pixels, so this runs high on purpose.")]
    [Min(0.01f)] public float CoreScale = 9f;
    [Min(0f)] public float GlowDiameter = 1.2f;
    [Tooltip("How strongly the glow breathes while flying.")]
    [Range(0f, 0.5f)] public float GlowPulse = 0.12f;
    [Tooltip("Time the projectile takes to pop up to full size. It overshoots slightly on the way.")]
    [Min(0f)] public float SpawnPopDuration = 0.12f;
    [Tooltip("Distance between two trail sparks. 0 disables the trail.")]
    [Min(0f)] public float TrailSpacing = 0.22f;
    [Min(0.05f)] public float TrailLifetime = 0.35f;

    [Header("Cast Flash")]
    [Tooltip("Muzzle flash width where the spell leaves the caster. 0 disables it.")]
    [Min(0f)] public float CastFlashDiameter = 1.6f;
    [Tooltip("How many of the impact frames the launch burst borrows.")]
    [Min(0)] public int CastFlashFrames = 3;
    [Min(1f)] public float CastFlashFrameRate = 26f;

    [Header("Cloud (Delivery = Cloud)")]
    [Tooltip("How often the cloud poisons everything standing in it, in seconds.")]
    [Min(0.05f)] public float CloudTickInterval = 0.4f;
    [Tooltip("Cloud width relative to the radius it poisons. 1 makes the gas exactly as wide as it reaches.")]
    [Min(0.1f)] public float CloudVisualScale = 1.15f;
    [Tooltip("Lifts the cloud off the ground if the artwork needs it.")]
    public float CloudGroundOffset;
    [Tooltip("How far ahead the cloud lands when no enemy is in range.")]
    [Min(0.5f)] public float CloudPlacementDistance = 4.5f;

    [Header("Poison (Delivery = Cloud)")]
    [Min(0)] public int PoisonDamagePerTick = 9;
    [Min(0.05f)] public float PoisonTickInterval = 0.5f;
    [Tooltip("How long an enemy keeps taking damage after being poisoned. Re-entering the cloud refreshes it.")]
    [Min(0f)] public float PoisonDuration = 5f;
    public Color PoisonColor = new(0.45f, 0.95f, 0.3f, 0.75f);

    [Header("Nova (Delivery = Nova)")]
    [Tooltip("How long caught enemies are unable to attack. Their windup is cancelled immediately.")]
    [Min(0f)] public float NovaStunDuration = 1.6f;

    [Header("Chain Lightning (Delivery = Nova)")]
    [Tooltip("How many times the bolt jumps on after the blast. 0 disables the chain entirely.")]
    [Min(0)] public int ChainJumps;
    [Tooltip("How far the bolt can reach for its next target, measured from the previous one.")]
    [Min(0.5f)] public float ChainJumpRange = 5f;
    [Tooltip("Damage left after each jump. 0.55 means the first arc deals 55% of the blast, the second 30%, and so on.")]
    [Range(0.05f, 1f)] public float ChainDamageFalloff = 0.55f;
    public Color ChainColor = new(1f, 0.9f, 0.55f, 1f);

    [Header("Impact")]
    [Min(1)] public int Damage = 45;
    [Tooltip("Everything inside this radius takes the full damage.")]
    [Min(0.1f)] public float ImpactRadius = 2.2f;
    [Tooltip("Explosion width relative to the damage radius. 1 makes it exactly as wide as it hurts.")]
    [Min(0.1f)] public float ImpactVisualScale = 1.1f;

    [Header("Colour")]
    public Color GlowColor = new(0.85f, 0.52f, 1f, 0.85f);
    public Color TrailColor = new(0.74f, 0.44f, 1f, 0.7f);

    /// <summary>Frame that travels as the projectile, or null when unsliced.</summary>
    public Sprite ProjectileSprite => GetFrame(ProjectileFrame);

    public int ImpactFrameCount => Frames == null ? 0 : Mathf.Max(0, Frames.Length - ImpactStartFrame);

    /// <summary>
    /// A spell without usable frames cannot be cast at all. Only a projectile
    /// spell needs a travelling core; a cloud is animation only.
    /// </summary>
    public bool IsUsable
    {
        get
        {
            if (ImpactFrameCount <= 0 || GetImpactFrame(0) == null)
                return false;

            return Delivery != SpellDelivery.Projectile || ProjectileSprite != null;
        }
    }

    public Sprite GetImpactFrame(int _impactIndex) => GetFrame(ImpactStartFrame + _impactIndex);

    /// <summary>
    /// Improves one value of this spell. Only ever call this on the runtime copy
    /// held by <see cref="PlayerSpellCaster"/>: writing to the asset itself would
    /// bake the upgrade into the project file and survive leaving play mode.
    /// </summary>
    public void ApplyStatUpgrade(SpellStat _stat, float _value)
    {
        switch (_stat)
        {
            case SpellStat.Damage:
                Damage = Mathf.Max(1, Damage + Mathf.RoundToInt(_value));
                break;
            case SpellStat.Radius:
                ImpactRadius = Mathf.Max(0.1f, ImpactRadius + _value);
                break;
            case SpellStat.Cooldown:
                // A positive value shortens the cooldown, which is what an upgrade
                // card means by "faster".
                Cooldown = Mathf.Max(0.25f, Cooldown - _value);
                break;
            case SpellStat.PoisonDamage:
                PoisonDamagePerTick = Mathf.Max(0, PoisonDamagePerTick + Mathf.RoundToInt(_value));
                break;
            case SpellStat.PoisonDuration:
                PoisonDuration = Mathf.Max(0f, PoisonDuration + _value);
                break;
            case SpellStat.StunDuration:
                NovaStunDuration = Mathf.Max(0f, NovaStunDuration + _value);
                break;
        }
    }

    private Sprite GetFrame(int _index) =>
        Frames != null && _index >= 0 && _index < Frames.Length ? Frames[_index] : null;

    private void OnValidate()
    {
        ImpactStartFrame = Mathf.Max(0, ImpactStartFrame);
        ProjectileFrame = Mathf.Max(0, ProjectileFrame);
        ImpactFrameRate = Mathf.Max(1f, ImpactFrameRate);
    }
}
