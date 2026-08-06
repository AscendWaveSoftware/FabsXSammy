using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Casts the player's spells on the number keys. Each slot seeks the nearest
/// enemy and fires a homing projectile at it; without a target the spell is fired
/// straight ahead so the key never feels dead.
/// </summary>
[DisallowMultipleComponent]
public class PlayerSpellCaster : MonoBehaviour
{
    public const int SpellSlotCount = 3;

    [Header("Spells")]
    [SerializeField, Tooltip("Slot 0 is key 1, slot 1 is key 2, slot 2 is key 3. Empty slots are simply ignored.")]
    private SpellDefinition[] m_spells = new SpellDefinition[SpellSlotCount];

    [Header("Targeting")]
    [SerializeField] private LayerMask m_enemyLayer;

    [Header("References")]
    [SerializeField] private PlayerHealth m_playerHealth;
    [SerializeField] private PlayerResources m_playerResources;
    [SerializeField] private Rigidbody m_rigidbody;
    [SerializeField] private PlayerMovementHandler m_movement;

    private readonly float[] m_nextCastTime = new float[SpellSlotCount];
    private readonly bool[] m_hasLoggedUnusableSpell = new bool[SpellSlotCount];
    private readonly bool[] m_unlockedSlots = new bool[SpellSlotCount];

    /// <summary>Raised when a slot becomes available, so the HUD can show it.</summary>
    public event System.Action<int> OnSpellUnlocked;

    /// <summary>Spell in a slot, or null when the slot is empty.</summary>
    public SpellDefinition GetSpell(int _slotIndex) =>
        m_spells != null && _slotIndex >= 0 && _slotIndex < m_spells.Length ? m_spells[_slotIndex] : null;

    public bool IsSlotUnlocked(int _slotIndex) =>
        _slotIndex >= 0 && _slotIndex < m_unlockedSlots.Length && m_unlockedSlots[_slotIndex];

    /// <summary>True while this spell sits in a slot that is still locked.</summary>
    public bool CanUnlock(SpellDefinition _spell) => FindLockedSlot(_spell) >= 0;

    /// <summary>Hands a spell to the player. Returns false if there was nothing to unlock.</summary>
    public bool UnlockSpell(SpellDefinition _spell)
    {
        int slotIndex = FindLockedSlot(_spell);

        if (slotIndex < 0)
            return false;

        m_unlockedSlots[slotIndex] = true;

        // Ready right away. Starting a freshly unlocked spell on cooldown would
        // read as the upgrade not having worked.
        m_nextCastTime[slotIndex] = 0f;
        OnSpellUnlocked?.Invoke(slotIndex);
        return true;
    }

    private int FindLockedSlot(SpellDefinition _spell)
    {
        if (_spell == null || m_spells == null)
            return -1;

        for (int i = 0; i < m_spells.Length && i < m_unlockedSlots.Length; i++)
        {
            if (m_spells[i] == _spell && !m_unlockedSlots[i])
                return i;
        }

        return -1;
    }

    private void Awake()
    {
        if (m_playerHealth == null)
            m_playerHealth = GetComponent<PlayerHealth>();

        if (m_playerResources == null)
            m_playerResources = GetComponent<PlayerResources>();

        if (m_rigidbody == null)
            m_rigidbody = GetComponent<Rigidbody>();

        if (m_movement == null)
            m_movement = GetComponent<PlayerMovementHandler>();
    }

    private void Update()
    {
        // A shop or level up pause owns timeScale, casting through it would let
        // the player empty every cooldown while the game is frozen.
        if (Time.timeScale <= 0f)
            return;

        // Read directly rather than through an input action, matching how the
        // block is polled. The shared input asset stays untouched.
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (keyboard.digit1Key.wasPressedThisFrame)
            TryCast(0);

        if (keyboard.digit2Key.wasPressedThisFrame)
            TryCast(1);

        if (keyboard.digit3Key.wasPressedThisFrame)
            TryCast(2);
    }

    /// <summary>Seconds left on a slot's cooldown, 0 when it is ready.</summary>
    public float GetRemainingCooldown(int _slotIndex)
    {
        if (_slotIndex < 0 || _slotIndex >= m_nextCastTime.Length)
            return 0f;

        return Mathf.Max(0f, m_nextCastTime[_slotIndex] - Time.time);
    }

    private void TryCast(int _slotIndex)
    {
        if (m_spells == null || _slotIndex < 0 || _slotIndex >= m_spells.Length)
            return;

        SpellDefinition spell = m_spells[_slotIndex];

        if (spell == null)
            return;

        // Spells are earned through a level up card, not owned from the start.
        if (!IsSlotUnlocked(_slotIndex))
            return;

        if (m_playerHealth != null && !m_playerHealth.IsAlive)
            return;

        if (Time.time < m_nextCastTime[_slotIndex])
            return;

        if (!spell.IsUsable)
        {
            if (!m_hasLoggedUnusableSpell[_slotIndex])
            {
                Debug.LogError(
                    $"Spell '{spell.name}' has no sliced frames and was not cast. " +
                    "Run Tools/Sammy/Rebuild Spell Assets to slice the sheet.", spell);
                m_hasLoggedUnusableSpell[_slotIndex] = true;
            }

            return;
        }

        EnemyStats target = FindNearestEnemy(spell.TargetSearchRange);

        bool wasCast = spell.Delivery switch
        {
            SpellDelivery.Cloud => CastCloud(spell, target),
            SpellDelivery.Nova => CastNova(spell),
            _ => CastProjectile(spell, target)
        };

        if (wasCast)
            m_nextCastTime[_slotIndex] = Time.time + spell.Cooldown;
    }

    private bool CastNova(SpellDefinition _spell)
    {
        // Centred on the caster, which is the whole point of the spell: it is the
        // answer to being surrounded, not a way to reach something far away.
        Vector3 center = transform.position + Vector3.up * _spell.SpawnHeight;
        SpellNova.Detonate(_spell, center, m_enemyLayer, m_playerResources);
        return true;
    }

    private bool CastProjectile(SpellDefinition _spell, EnemyStats _target)
    {
        Vector3 origin = transform.position + Vector3.up * _spell.SpawnHeight;
        Vector3 direction = GetCastDirection(_target, origin);

        if (direction.sqrMagnitude < 0.0001f)
            return false;

        SpellProjectile.Launch(_spell, origin, direction, _target, m_enemyLayer, m_playerResources);
        return true;
    }

    private bool CastCloud(SpellDefinition _spell, EnemyStats _target)
    {
        SpellCloud.Show(_spell, GetCloudGroundPosition(_spell, _target), m_enemyLayer, m_playerResources);
        return true;
    }

    private Vector3 GetCloudGroundPosition(SpellDefinition _spell, EnemyStats _target)
    {
        if (_target != null)
        {
            Collider targetCollider = _target.GetComponent<Collider>();
            Vector3 targetPosition = _target.transform.position;

            // The cloud grows out of the ground, so it is placed at the enemy's
            // feet rather than at its centre.
            float groundHeight = targetCollider != null ? targetCollider.bounds.min.y : targetPosition.y;
            return new Vector3(targetPosition.x, groundHeight, targetPosition.z);
        }

        Vector3 placement = transform.position + GetFallbackDirection() * _spell.CloudPlacementDistance;
        Collider playerCollider = GetComponent<Collider>();
        placement.y = playerCollider != null ? playerCollider.bounds.min.y : transform.position.y;
        return placement;
    }

    private EnemyStats FindNearestEnemy(float _searchRange)
    {
        Collider[] candidates = Physics.OverlapSphere(transform.position, _searchRange, m_enemyLayer);

        EnemyStats nearestEnemy = null;
        float nearestSquaredDistance = float.MaxValue;

        foreach (Collider candidate in candidates)
        {
            EnemyStats enemyStats = candidate != null
                ? candidate.GetComponentInParent<EnemyStats>()
                : null;

            if (enemyStats == null || enemyStats.IsDead)
                continue;

            float squaredDistance = (enemyStats.transform.position - transform.position).sqrMagnitude;

            if (squaredDistance >= nearestSquaredDistance)
                continue;

            nearestSquaredDistance = squaredDistance;
            nearestEnemy = enemyStats;
        }

        return nearestEnemy;
    }

    private Vector3 GetCastDirection(EnemyStats _target, Vector3 _origin)
    {
        if (_target != null)
        {
            Collider targetCollider = _target.GetComponent<Collider>();
            Vector3 targetPoint = targetCollider != null
                ? targetCollider.bounds.center
                : _target.transform.position;
            Vector3 toTarget = targetPoint - _origin;

            if (toTarget.sqrMagnitude > 0.0001f)
                return toTarget.normalized;
        }

        return GetFallbackDirection();
    }

    /// <summary>
    /// Aim used when no enemy is in range. The current movement gives full 360
    /// degree aim while running; standing still falls back to the direction the
    /// sprite is facing.
    /// </summary>
    private Vector3 GetFallbackDirection()
    {
        if (m_rigidbody != null)
        {
            Vector3 velocity = m_rigidbody.linearVelocity;
            velocity.y = 0f;

            if (velocity.sqrMagnitude > 0.04f)
                return velocity.normalized;
        }

        if (m_movement != null)
            return m_movement.FacingDirection;

        return transform.right;
    }

    private void OnValidate()
    {
        if (m_spells == null || m_spells.Length != SpellSlotCount)
            System.Array.Resize(ref m_spells, SpellSlotCount);
    }
}
