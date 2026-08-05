using UnityEngine;

public class PlayerUpgradeHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovementHandler m_playerMovementHandler;
    [SerializeField] private PlayerHealth m_playerHealth;
    [SerializeField] private PlayerCombat m_playerCombat;
    [SerializeField] private PlayerExperience m_playerExperience;

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
        }

        Debug.Log($"Applied upgrade: {_upgrade.UpgradeName}");
    }
}
