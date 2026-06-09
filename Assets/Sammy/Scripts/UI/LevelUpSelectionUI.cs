using System.Collections.Generic;
using UnityEngine;

public class LevelUpSelectionUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerExperience m_playerExperience;
    [SerializeField] private PlayerUpgradeHandler m_playerUpgradeHandler;
    [SerializeField] private GameObject m_panel;
    [SerializeField] private LevelUpCardUI[] m_cardUIs;

    [Header("Upgrade Pool")]
    [SerializeField] private UpgradeDefinition[] m_availableUpgrades;

    private int m_pendingLevelUps;

    private void Awake()
    {
        if (m_playerExperience == null)
            m_playerExperience = FindAnyObjectByType<PlayerExperience>();

        if(m_playerUpgradeHandler == null)
            m_playerUpgradeHandler = FindAnyObjectByType<PlayerUpgradeHandler>();

        if(m_panel != null)
            m_panel.SetActive(false);
    }

    private void OnEnable()
    {
        if (m_playerExperience != null)
            m_playerExperience.OnLevelUp += HandleLevelUp;
    }

    private void OnDisable()
    {
        if (m_playerExperience != null)
            m_playerExperience.OnLevelUp -= HandleLevelUp;
    }

    public void SelectUpgrade(UpgradeDefinition _upgrade)
    {
        m_playerUpgradeHandler.ApplyUpgrade(_upgrade);

        m_pendingLevelUps--;

        if(m_pendingLevelUps > 0)
        {
            ShowLevelUpSelection();
            return;
        }

        CloseLevelUpSelection();
    }

    private void HandleLevelUp(int _newLevel)
    {
        m_pendingLevelUps++;

        if (m_panel != null && !m_panel.activeSelf)
            ShowLevelUpSelection();
    }

    private void ShowLevelUpSelection()
    {
        if(m_availableUpgrades == null || m_availableUpgrades.Length == 0)
        {
            Debug.LogWarning("No upgrades assigned.");
            return;
        }

        if(m_cardUIs == null || m_cardUIs.Length < 3)
        {
            Debug.LogWarning("At least 3 card UIs are needed.");
            return;
        }

        Time.timeScale = 0f;

        m_panel.SetActive(true);

        List<UpgradeDefinition> selectedUpgrades = GetRandomUpgrades(3);

        for(int i = 0; i < m_cardUIs.Length; i++)
        {
            if(i < selectedUpgrades.Count)
            {
                m_cardUIs[i].gameObject.SetActive(true);
                m_cardUIs[i].Setup(selectedUpgrades[i], this);
            }
            else
            {
                m_cardUIs[i].gameObject.SetActive(false);
            }
        }
    }

    private void CloseLevelUpSelection()
    {
        m_panel.SetActive(false);
        Time.timeScale = 1f;
    }

    private List<UpgradeDefinition> GetRandomUpgrades(int _amount)
    {
        List<UpgradeDefinition> upgradePool = new List<UpgradeDefinition>(m_availableUpgrades);
        List<UpgradeDefinition> selectedUpgrades = new List<UpgradeDefinition>();

        int amountToSelect = Mathf.Min(_amount, upgradePool.Count);

        for(int i = 0; i < amountToSelect; i++)
        {
            int randomIndex = Random.Range(0, upgradePool.Count);

            selectedUpgrades.Add(upgradePool[randomIndex]);
            upgradePool.RemoveAt(randomIndex);
        }

        return selectedUpgrades;
    }
}
