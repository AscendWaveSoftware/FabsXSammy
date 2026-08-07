using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class LevelUpSelectionUI : MonoBehaviour
{
    private static readonly Color OverlayColor = new Color(0.012f, 0.02f, 0.045f, 0.93f);
    private static readonly Color AccentColor = new Color(0.48f, 0.72f, 1f, 1f);

    [Header("References")]
    [SerializeField] private PlayerExperience m_playerExperience;
    [SerializeField] private PlayerUpgradeHandler m_playerUpgradeHandler;
    [SerializeField] private GameObject m_panel;
    [SerializeField] private LevelUpCardUI[] m_cardUIs;

    [Header("Upgrade Pool")]
    [SerializeField] private UpgradeDefinition[] m_availableUpgrades;

    [Header("Input Safety")]
    [SerializeField, Min(0f), Tooltip("Minimum time before an upgrade can be selected after the panel opens.")]
    private float m_inputUnlockDelay = 0.45f;
    [SerializeField, Min(0f), Tooltip("Required time without another left click before selection is enabled.")]
    private float m_clickQuietTime = 0.2f;

    private int m_pendingLevelUps;
    private int m_currentLevel;
    private float m_previousTimeScale = 1f;
    private CanvasGroup m_panelCanvasGroup;
    private TextMeshProUGUI m_levelText;
    private TextMeshProUGUI m_selectionHintText;
    private bool m_isPanelFading;
    private bool m_selectionInputEnabled;
    private float m_inputUnlockTime;
    private float m_lastClickActivityTime;

    public bool SelectionInputEnabled => m_selectionInputEnabled;

    private void Awake()
    {
        if (m_playerExperience == null)
            m_playerExperience = FindAnyObjectByType<PlayerExperience>();

        if (m_playerUpgradeHandler == null)
            m_playerUpgradeHandler = FindAnyObjectByType<PlayerUpgradeHandler>();

        ConfigurePanelVisuals();

        if (m_panel != null)
            m_panel.SetActive(false);
    }

    private void Update()
    {
        if (m_isPanelFading && m_panelCanvasGroup != null)
        {
            m_panelCanvasGroup.alpha = Mathf.MoveTowards(m_panelCanvasGroup.alpha, 1f, Time.unscaledDeltaTime * 5.5f);
            if (m_panelCanvasGroup.alpha >= 1f)
                m_isPanelFading = false;
        }

        UpdateSelectionInputGuard();
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

        PlayerUIHandler.SetInteractiveUIActive(this, false);

        if (m_panel != null && m_panel.activeSelf)
            Time.timeScale = m_previousTimeScale;
    }

    public void SelectUpgrade(UpgradeDefinition _upgrade)
    {
        if (!m_selectionInputEnabled || m_playerUpgradeHandler == null || _upgrade == null)
            return;

        SetSelectionInputEnabled(false);
        m_playerUpgradeHandler.ApplyUpgrade(_upgrade);
        m_pendingLevelUps--;

        if (m_pendingLevelUps > 0)
        {
            ShowLevelUpSelection();
            return;
        }

        CloseLevelUpSelection();
    }

    private void HandleLevelUp(int _newLevel)
    {
        m_pendingLevelUps++;
        m_currentLevel = _newLevel;
        UpdateLevelText();

        if (m_panel != null && !m_panel.activeSelf)
            ShowLevelUpSelection();
    }

    private void ShowLevelUpSelection()
    {
        if (m_availableUpgrades == null || m_availableUpgrades.Length == 0)
        {
            Debug.LogWarning("No upgrades assigned.");
            return;
        }

        if (m_cardUIs == null || m_cardUIs.Length < 3)
        {
            Debug.LogWarning("At least 3 card UIs are needed.");
            return;
        }

        if (m_panel == null)
        {
            Debug.LogWarning("No level-up panel assigned.");
            return;
        }

        if (!m_panel.activeSelf)
            m_previousTimeScale = Time.timeScale;

        Time.timeScale = 0f;
        m_panel.SetActive(true);
        PlayerUIHandler.SetInteractiveUIActive(this, true);
        BeginSelectionInputGuard();

        if (m_panelCanvasGroup != null)
        {
            m_panelCanvasGroup.alpha = 0f;
            m_panelCanvasGroup.interactable = true;
            m_panelCanvasGroup.blocksRaycasts = true;
            m_isPanelFading = true;
        }

        UpdateLevelText();
        List<UpgradeDefinition> selectedUpgrades = GetRandomUpgrades(3);

        for (int i = 0; i < m_cardUIs.Length; i++)
        {
            if (i < selectedUpgrades.Count)
            {
                m_cardUIs[i].gameObject.SetActive(true);
                m_cardUIs[i].Setup(selectedUpgrades[i], this);
                m_cardUIs[i].SetInteractable(false);
                m_cardUIs[i].PlayEntrance(0.06f + i * 0.09f);
            }
            else
            {
                m_cardUIs[i].gameObject.SetActive(false);
            }
        }
    }

    private void CloseLevelUpSelection()
    {
        if (m_panel != null)
            m_panel.SetActive(false);

        SetSelectionInputEnabled(false);
        PlayerUIHandler.SetInteractiveUIActive(this, false);
        Time.timeScale = m_previousTimeScale;
    }

    private void ConfigurePanelVisuals()
    {
        if (m_panel == null)
            return;

        Image panelImage = m_panel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = OverlayColor;
            panelImage.raycastTarget = true;
        }

        m_panelCanvasGroup = m_panel.GetComponent<CanvasGroup>();
        if (m_panelCanvasGroup == null)
            m_panelCanvasGroup = m_panel.AddComponent<CanvasGroup>();

        TMP_FontAsset font = FindCardFont();

        TextMeshProUGUI title = CreateText("Level Up Title", font);
        title.text = "LEVEL UP";
        title.fontSize = 58f;
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 8f;
        title.color = Color.white;
        title.alignment = TextAlignmentOptions.Center;
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(700f, 74f));

        m_levelText = CreateText("Current Level", font);
        m_levelText.fontSize = 19f;
        m_levelText.fontStyle = FontStyles.Bold;
        m_levelText.characterSpacing = 5f;
        m_levelText.color = AccentColor;
        m_levelText.alignment = TextAlignmentOptions.Center;
        SetRect(m_levelText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(520f, 34f));

        Image titleLine = CreateImage("Level Up Accent Line");
        titleLine.color = AccentColor;
        SetRect(titleLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -157f), new Vector2(180f, 3f));

        m_selectionHintText = CreateText("Selection Hint", font);
        m_selectionHintText.text = "CHOOSE ONE UPGRADE";
        m_selectionHintText.fontSize = 16f;
        m_selectionHintText.fontStyle = FontStyles.Bold;
        m_selectionHintText.characterSpacing = 5f;
        m_selectionHintText.color = new Color(0.62f, 0.68f, 0.78f, 1f);
        m_selectionHintText.alignment = TextAlignmentOptions.Center;
        SetRect(m_selectionHintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 35f), new Vector2(500f, 30f));

        UpdateLevelText();
    }

    private void BeginSelectionInputGuard()
    {
        m_selectionInputEnabled = false;
        m_inputUnlockTime = Time.unscaledTime + Mathf.Max(0f, m_inputUnlockDelay);
        m_lastClickActivityTime = Time.unscaledTime;

        if (m_selectionHintText != null)
            m_selectionHintText.text = "RELEASE MOUSE TO CHOOSE";
    }

    private void UpdateSelectionInputGuard()
    {
        if (m_selectionInputEnabled || m_panel == null || !m_panel.activeSelf)
            return;

        bool leftMouseIsPressed = Mouse.current != null && Mouse.current.leftButton.isPressed;
        if (leftMouseIsPressed)
            m_lastClickActivityTime = Time.unscaledTime;

        bool openingDelayFinished = Time.unscaledTime >= m_inputUnlockTime;
        bool mouseWasQuiet = !leftMouseIsPressed &&
                             Time.unscaledTime - m_lastClickActivityTime >= Mathf.Max(0f, m_clickQuietTime);

        if (openingDelayFinished && mouseWasQuiet)
            SetSelectionInputEnabled(true);
    }

    private void SetSelectionInputEnabled(bool _enabled)
    {
        m_selectionInputEnabled = _enabled;

        if (m_cardUIs != null)
        {
            foreach (LevelUpCardUI card in m_cardUIs)
            {
                if (card != null && card.gameObject.activeSelf)
                    card.SetInteractable(_enabled);
            }
        }

        if (m_selectionHintText != null)
            m_selectionHintText.text = _enabled ? "CHOOSE ONE UPGRADE" : "RELEASE MOUSE TO CHOOSE";
    }

    private void UpdateLevelText()
    {
        if (m_levelText == null)
            return;

        int displayedLevel = m_currentLevel > 0
            ? m_currentLevel
            : (m_playerExperience != null ? m_playerExperience.CurrentLevel : 1);
        m_levelText.text = $"LEVEL {displayedLevel}  //  NEW POWER AVAILABLE";
    }

    private TMP_FontAsset FindCardFont()
    {
        if (m_cardUIs != null)
        {
            foreach (LevelUpCardUI card in m_cardUIs)
            {
                if (card == null)
                    continue;

                TextMeshProUGUI text = card.GetComponentInChildren<TextMeshProUGUI>(true);
                if (text != null && text.font != null)
                    return text.font;
            }
        }

        return TMP_Settings.defaultFontAsset;
    }

    private TextMeshProUGUI CreateText(string _name, TMP_FontAsset _font)
    {
        Transform existing = m_panel.transform.Find(_name);
        if (existing != null)
            return existing.GetComponent<TextMeshProUGUI>();

        GameObject textObject = new GameObject(_name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = m_panel.layer;
        textObject.transform.SetParent(m_panel.transform, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = _font;
        text.raycastTarget = false;
        return text;
    }

    private Image CreateImage(string _name)
    {
        Transform existing = m_panel.transform.Find(_name);
        if (existing != null)
            return existing.GetComponent<Image>();

        GameObject imageObject = new GameObject(_name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.layer = m_panel.layer;
        imageObject.transform.SetParent(m_panel.transform, false);
        Image image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static void SetRect(RectTransform _rect, Vector2 _anchorMin, Vector2 _anchorMax, Vector2 _position, Vector2 _size)
    {
        _rect.anchorMin = _anchorMin;
        _rect.anchorMax = _anchorMax;
        _rect.anchoredPosition = _position;
        _rect.sizeDelta = _size;
        _rect.pivot = new Vector2(0.5f, 0.5f);
    }

    /// <summary>
    /// The scene's pool plus whatever the player brings along, so spell cards do
    /// not have to be maintained in two places.
    /// </summary>
    private IEnumerable<UpgradeDefinition> EnumerateCandidateUpgrades()
    {
        if (m_availableUpgrades != null)
        {
            foreach (UpgradeDefinition upgrade in m_availableUpgrades)
                yield return upgrade;
        }

        if (m_playerUpgradeHandler == null)
            yield break;

        foreach (UpgradeDefinition upgrade in m_playerUpgradeHandler.SpellUpgrades)
            yield return upgrade;
    }

    private List<UpgradeDefinition> GetRandomUpgrades(int _amount)
    {
        Dictionary<UpgradeType, List<UpgradeDefinition>> upgradesByType = new Dictionary<UpgradeType, List<UpgradeDefinition>>();
        List<UpgradeDefinition> selectedUpgrades = new List<UpgradeDefinition>();

        foreach (UpgradeDefinition upgrade in EnumerateCandidateUpgrades())
        {
            if (upgrade == null || upgrade.UpgradeType == UpgradeType.MIN || upgrade.UpgradeType == UpgradeType.MAX)
                continue;

            // Drops cards that would have no effect, so an already unlocked
            // spell never wastes one of the three slots.
            if (m_playerUpgradeHandler != null && !m_playerUpgradeHandler.IsUpgradeAvailable(upgrade))
                continue;

            if (!upgradesByType.TryGetValue(upgrade.UpgradeType, out List<UpgradeDefinition> variants))
            {
                variants = new List<UpgradeDefinition>();
                upgradesByType.Add(upgrade.UpgradeType, variants);
            }

            variants.Add(upgrade);
        }

        List<UpgradeType> availableTypes = new List<UpgradeType>(upgradesByType.Keys);
        int amountToSelect = Mathf.Min(_amount, availableTypes.Count);

        for (int i = 0; i < amountToSelect; i++)
        {
            int typeIndex = Random.Range(0, availableTypes.Count);
            UpgradeType selectedType = availableTypes[typeIndex];
            List<UpgradeDefinition> variants = upgradesByType[selectedType];
            selectedUpgrades.Add(variants[Random.Range(0, variants.Count)]);
            availableTypes.RemoveAt(typeIndex);
        }

        return selectedUpgrades;
    }

    private void OnValidate()
    {
        m_inputUnlockDelay = Mathf.Max(0f, m_inputUnlockDelay);
        m_clickQuietTime = Mathf.Max(0f, m_clickQuietTime);
    }
}
