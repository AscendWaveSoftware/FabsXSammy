using System.Collections.Generic;
using UnityEngine;

public class PlayerUpgradeHandler : MonoBehaviour
{
    /// <summary>
    /// How much of a melee damage card the spells receive as well. Above one on
    /// purpose: spell damage sits on a far larger base than a sword swing, so an
    /// equal share would barely register on it.
    /// </summary>
    private const float SpellDamageShare = 1.5f;


    [Header("Spell Cards")]
    [SerializeField, Tooltip("Cards that unlock and strengthen the player's own spells. Kept here rather than in the scene pool so they stay in sync with the spell slots.")]
    private UpgradeDefinition[] m_spellUpgrades;

    /// <summary>Upgrades the player brings along on top of the scene's pool.</summary>
    public IReadOnlyList<UpgradeDefinition> SpellUpgrades =>
        m_spellUpgrades ?? System.Array.Empty<UpgradeDefinition>();

    [Header("References")]
    [SerializeField] private PlayerMovementHandler m_playerMovementHandler;
    [SerializeField] private PlayerHealth m_playerHealth;
    [SerializeField] private PlayerCombat m_playerCombat;
    [SerializeField] private PlayerExperience m_playerExperience;
    [SerializeField] private PlayerSpellCaster m_playerSpellCaster;

    /// <summary>
    /// False for cards that would do nothing, so the level up screen never
    /// offers a spell the player already owns.
    /// </summary>
    public bool IsUpgradeAvailable(UpgradeDefinition _upgrade)
    {
        if (_upgrade == null)
            return false;

        switch (_upgrade.UpgradeType)
        {
            case UpgradeType.UNLOCKSPELL:
                return m_playerSpellCaster != null && m_playerSpellCaster.CanUnlock(_upgrade.Spell);

            // A card that strengthens a spell the player cannot even cast yet
            // would be a wasted pick, so it stays out until the spell is owned.
            case UpgradeType.SPELLPOWER:
                return m_playerSpellCaster != null && m_playerSpellCaster.IsSpellUnlocked(_upgrade.Spell);

            default:
                return true;
        }
    }

    private void Awake()
    {
        if (m_playerMovementHandler == null)
            m_playerMovementHandler = GetComponent<PlayerMovementHandler>();

        if (m_playerHealth == null)
            m_playerHealth = GetComponent<PlayerHealth>();

        if (m_playerCombat == null)
            m_playerCombat = GetComponent<PlayerCombat>();

        if (m_playerExperience == null)
            m_playerExperience = GetComponent<PlayerExperience>();

        if (m_playerSpellCaster == null)
            m_playerSpellCaster = GetComponent<PlayerSpellCaster>();
    }

    public void ApplyUpgrade(UpgradeDefinition _upgrade)
    {
        if (_upgrade == null) return;

        switch (_upgrade.UpgradeType)
        {
            case UpgradeType.MOVESPEED:
                m_playerMovementHandler?.AddMoveSpeedPercentage(_upgrade.Value);
                break;
            case UpgradeType.MAXHEALTH:
                m_playerHealth?.AddMaxHealth(Mathf.RoundToInt(_upgrade.Value));
                break;
            case UpgradeType.DAMAGE:
                m_playerCombat?.AddDamage(Mathf.RoundToInt(_upgrade.Value));

                // The spells ride along, so a damage build strengthens the whole
                // kit rather than turning the spell slots into dead weight.
                m_playerSpellCaster?.AddDamageToSpells(_upgrade.Value * SpellDamageShare);
                break;
            case UpgradeType.ATTACKRANGE:
                m_playerCombat?.AddAttackRange(_upgrade.Value);
                break;
            case UpgradeType.CRITICALCHANCE:
                m_playerCombat?.AddCriticalChance(_upgrade.Value);
                break;
            case UpgradeType.HEALTHONHIT:
                m_playerCombat?.AddHealthOnHit(Mathf.RoundToInt(_upgrade.Value));
                break;
            case UpgradeType.DAMAGEREDUCTION:
                m_playerHealth?.AddDamageReduction(_upgrade.Value);
                break;
            case UpgradeType.HEALTHREGEN:
                m_playerHealth?.AddHealthRegeneration(_upgrade.Value);
                break;
            case UpgradeType.EXPERIENCEGAIN:
                m_playerExperience?.AddExperienceGain(_upgrade.Value);
                break;
            case UpgradeType.UNLOCKSPELL:
                m_playerSpellCaster?.UnlockSpell(_upgrade.Spell);
                break;
            case UpgradeType.SPELLPOWER:
                m_playerSpellCaster?.UpgradeSpell(_upgrade.Spell, _upgrade.SpellStat, _upgrade.Value);
                break;
        }

        Debug.Log($"Applied upgrade: {_upgrade.UpgradeName}");
    }
}
