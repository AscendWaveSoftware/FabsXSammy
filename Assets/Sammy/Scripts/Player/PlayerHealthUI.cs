using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth m_playerHealth;
    [SerializeField] private Image m_healthBarImage;
    [SerializeField] private TextMeshProUGUI m_healthText;

    [Header("Health Bar Colors")]
    [SerializeField] private Color m_highHealthColor = Color.green;
    [SerializeField] private Color m_mediumHealthColor = Color.yellow;
    [SerializeField] private Color m_lowHealthColor = Color.red;

    [Header("Color Thresholds")]
    [SerializeField, Range(0f, 1f)] private float m_mediumHealthThreshold = 0.6f;
    [SerializeField, Range(0f, 1f)] private float m_lowHealthThreshold = 0.3f;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float m_fillSmoothing = 10f;

    private float m_targetFillAmount;
    private Color m_targetBarColor;

    /// <summary>Lets the HUD hand in its palette; the thresholds stay as configured.</summary>
    public void ApplyBarColors(Color _high, Color _medium, Color _low)
    {
        m_highHealthColor = _high;
        m_mediumHealthColor = _medium;
        m_lowHealthColor = _low;
    }
    private bool m_hasInitialValue;

    public Image HealthBarImage => m_healthBarImage;
    public TextMeshProUGUI HealthText => m_healthText;

    private void Awake()
    {
        if (m_playerHealth == null)
            m_playerHealth = GetComponentInParent<PlayerHealth>();
    }

    private void OnEnable()
    {
        if (m_playerHealth == null)
            return;

        m_playerHealth.OnHealthChanged += HandleHealthChanged;
    }

    private void Start()
    {
        if (m_playerHealth != null)
            HandleHealthChanged(m_playerHealth.CurrentHealth, m_playerHealth.MaxHealth);
    }

    private void Update()
    {
        if (!m_hasInitialValue || m_healthBarImage == null)
            return;

        float interpolation = 1f - Mathf.Exp(-m_fillSmoothing * Time.unscaledDeltaTime);
        m_healthBarImage.fillAmount = Mathf.Lerp(m_healthBarImage.fillAmount, m_targetFillAmount, interpolation);
        m_healthBarImage.color = Color.Lerp(m_healthBarImage.color, m_targetBarColor, interpolation);
    }

    private void OnDisable()
    {
        if (m_playerHealth == null)
            return;

        m_playerHealth.OnHealthChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(int _currentHealth, int _maxHealth)
    {
        float healthPercentage = _maxHealth <= 0 ? 0f : (float)_currentHealth / _maxHealth;

        if (m_healthBarImage != null)
        {
            m_targetFillAmount = healthPercentage;
            m_targetBarColor = GetHealthColor(healthPercentage);

            if (!m_hasInitialValue)
            {
                m_healthBarImage.fillAmount = m_targetFillAmount;
                m_healthBarImage.color = m_targetBarColor;
            }
        }

        m_hasInitialValue = true;

        if (m_healthText != null)
            m_healthText.text = $"{_currentHealth} / {_maxHealth}";
    }

    private Color GetHealthColor(float _healthPercentage)
    {
        if (_healthPercentage <= m_lowHealthThreshold)
            return m_lowHealthColor;

        if (_healthPercentage <= m_mediumHealthThreshold)
            return m_mediumHealthColor;

        return m_highHealthColor;
    }

    private void OnValidate()
    {
        m_fillSmoothing = Mathf.Max(0f, m_fillSmoothing);
    }
}
