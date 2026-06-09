using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelUpCardUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI m_upgradeNameText;
    [SerializeField] private TextMeshProUGUI m_descriptionText;
    [SerializeField] private Image m_iconImage;
    [SerializeField] private Button m_selectButton;

    private UpgradeDefinition m_upgrade;
    private LevelUpSelectionUI m_selectionUI;

    private void Awake()
    {
        if (m_selectButton == null)
            m_selectButton = GetComponent<Button>();

        if(m_selectButton != null)
            m_selectButton.onClick.AddListener(SelectUpgrade);
        else
            Debug.LogWarning($"{gameObject.name} has no Button assigned.");
    }

    public void Setup(UpgradeDefinition _upgrade, LevelUpSelectionUI _selectionUI)
    {
        m_upgrade = _upgrade;
        m_selectionUI = _selectionUI;

        m_upgradeNameText.text = _upgrade.UpgradeName;
        m_descriptionText.text = _upgrade.Description;

        if (m_iconImage != null)
            m_iconImage.sprite = _upgrade.Icon;
    }

    private void SelectUpgrade()
    {
        if (m_selectionUI == null || m_upgrade == null)
            return;

        m_selectionUI.SelectUpgrade(m_upgrade);
    }
}
