using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUIHandler : MonoBehaviour
{
    [Header("Resource UI")]
    [SerializeField] private TextMeshProUGUI m_scrapText;

    [Header("XP UI")]
    [SerializeField] private PlayerExperience m_playerExperience;
    [SerializeField] private TextMeshProUGUI m_levelText;
    [SerializeField] private TextMeshProUGUI m_xpText;
    [SerializeField] private Image m_xpBarImage;

    private void Awake()
    {
        if (m_playerExperience == null)
            m_playerExperience = FindAnyObjectByType<PlayerExperience>();

        //TODO: hier muss noch initial Gold und Scrap Text gesetzt werden
    }

    private void OnEnable()
    {
        PlayerResources.OnResourceChanged += HandleResourceChanged;

        if (m_playerExperience != null)
            m_playerExperience.OnExperienceChanged += HandleExperienceChanged;
    }

    private void Start()
    {
        if (m_playerExperience != null)
            HandleExperienceChanged(
                m_playerExperience.CurrentLevel,
                m_playerExperience.CurrentXP,
                m_playerExperience.XPToNextLevel
            );
    }

    private void OnDisable()
    {
        PlayerResources.OnResourceChanged -= HandleResourceChanged;

        if (m_playerExperience != null)
            m_playerExperience.OnExperienceChanged -= HandleExperienceChanged;
    }

    private void HandleResourceChanged(object sender, ResourceChangedEventArgs e)
    {
        switch (e.ResourceType)
        {
            case Resources.SCRAP:
                m_scrapText.text = e.CurrentResourceAmount.ToString();
                break;
        }
    }

    private void HandleExperienceChanged(int _level, int _currentXP, int _xpToNextLevel)
    {
        if (m_levelText != null)
            m_levelText.text = $"Level {_level}";

        if (m_xpText != null)
            m_xpText.text = $"{_currentXP} / {_xpToNextLevel} XP";

        if(m_xpBarImage != null)
        {
            float xpPercentage = _xpToNextLevel <= 0 ? 0f : (float)_currentXP / _xpToNextLevel;
            m_xpBarImage.fillAmount = xpPercentage;
        }
    }
}
