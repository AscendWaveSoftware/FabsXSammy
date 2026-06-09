using UnityEngine;

public class PlayerUpgradeHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovementHandler m_playerMovementHandler;
    [SerializeField] private PlayerHealth m_playerHealth;
    [SerializeField] private PlayerCombat m_playerCombat;

    private void Awake()
    {
        if (m_playerMovementHandler == null)
            m_playerMovementHandler = GetComponent<PlayerMovementHandler>();

        if (m_playerHealth == null)
            m_playerHealth = GetComponent<PlayerHealth>();

        if (m_playerCombat == null)
            m_playerCombat = GetComponent<PlayerCombat>();
    }

    public void ApplyUpgrade(UpgradeDefinition _upgrade)
    {
        if (_upgrade == null) return;

        switch (_upgrade.UpgradeType)
        {
            case UpgradeType.MOVESPEED:
                m_playerMovementHandler.AddMoveSpeedBonus(_upgrade.Value);
                break;
            case UpgradeType.MAXHEALTH:
                m_playerHealth.AddMaxHealth(Mathf.RoundToInt(_upgrade.Value));
                break;
            case UpgradeType.DAMAGE:
                m_playerCombat.AddDamage(Mathf.RoundToInt(_upgrade.Value));
                break;
        }

        Debug.Log($"Applied upgrade: {_upgrade.UpgradeName}");
    }
}
