using System.Collections;
using UnityEngine;

public class Stats : MonoBehaviour
{
    public CombatStats _stats;
    public float m_damageDuration;

    private float m_health;

    private float m_currentHealth;
    private float m_targetHealth;
    private Coroutine m_damageCoroutine;

    HealthUI m_healthUI;

    private void Awake()
    {
        m_healthUI = GetComponent<HealthUI>();

        m_health = _stats.m_health;

        m_currentHealth = m_health;
        m_targetHealth = m_health;

        m_healthUI.StartSlider(m_health);
    }

    public void TakeDamage(GameObject _target, float _damage)
    {
        Stats targetStats = _target.GetComponent<Stats>();

        targetStats.m_targetHealth -= _damage;

        if (targetStats.m_targetHealth <= 0)
        {
            Destroy(_target.gameObject);
        }
        else if (targetStats.m_damageCoroutine == null)
        {
            targetStats.StartLerpHealth();
        }
    }

    private void StartLerpHealth()
    {
        if (m_damageCoroutine == null)
            m_damageCoroutine = StartCoroutine(LerpHealth());
    }

    private IEnumerator LerpHealth()
    {
        float elapsedTime = 0;
        float initialHealth = m_currentHealth;
        float target = m_targetHealth;

        while (elapsedTime < m_damageDuration)
        {
            m_currentHealth = Mathf.Lerp(initialHealth, target, elapsedTime / m_damageDuration);
            UpdateHealthUI();

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        m_currentHealth = target;
        UpdateHealthUI();

        m_damageCoroutine = null;
    }

    private void UpdateHealthUI()
    {
        m_healthUI.UpdateSlider(m_currentHealth);
    }
}
