using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyStats enemyStats;
    [SerializeField] private Image healthBar;

    [Header("Settings")]
    [SerializeField] private bool hideWhenFullHealth = true;

    private void Awake()
    {
        if (enemyStats == null)
            enemyStats = GetComponentInParent<EnemyStats>();

        if(healthBar == null)
            healthBar = GetComponentInParent<Image>();
    }

    private void OnEnable()
    {
        if (enemyStats == null)
            return;

        enemyStats.OnHealthChanged += HandleHealthChanged;
    }

    private void Start()
    {
        if (enemyStats != null)
            HandleHealthChanged(enemyStats.CurrentHealth, enemyStats.MaxHealth);
    }

    private void OnDisable()
    {
        if (enemyStats == null)
            return;

        enemyStats.OnHealthChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(int _currentHealth, int _maxHealht)
    {
        if (healthBar == null)
            return;

        float healthPercentage = _maxHealht <= 0 ? 0f : (float)_currentHealth / _maxHealht;

        healthBar.fillAmount = healthPercentage;

        if (hideWhenFullHealth)
            healthBar.gameObject.SetActive(healthPercentage < 1f && healthPercentage > 0f);
    }
}
