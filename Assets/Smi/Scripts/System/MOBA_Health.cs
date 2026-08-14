using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MOBA_Health : MonoBehaviour
{
    public bool m_destroyed;
    [SerializeField] protected float m_currentHealth;
    public float m_damageDuration;

    [SerializeField] private HealthData m_data;
    protected float m_targetHealth;

    protected Slider m_healthSlider;
    private Coroutine m_damageCoroutine;

    private void OnEnable()
    {
        m_targetHealth = m_data.m_targetHealth;
        m_destroyed = true;

        if (m_damageCoroutine != null)
        {
            StopCoroutine(m_damageCoroutine);
            m_damageCoroutine = null;
        }

        StartProcess();

        m_healthSlider = GetComponentInChildren<Slider>(true);
        StartSlider(m_targetHealth);

        ResetHealth();
    }

    protected virtual void StartProcess() { }

    private void StartSlider(float maxValue)
    {
        m_healthSlider.maxValue = maxValue;
        m_healthSlider.value = maxValue;
    }

    protected void UpdateSlider(float value)
    {
        if (m_healthSlider != null) m_healthSlider.value = value;
    }

    public void StartLerpHealth()
    {
        if (m_damageCoroutine == null)
            m_damageCoroutine = StartCoroutine(LerpHealth());
    }

    protected IEnumerator LerpHealth()
    {
        float elapsedTime = 0;
        float initialHealth = m_currentHealth;
        float target = m_targetHealth;

        while (elapsedTime < m_damageDuration)
        {
            m_currentHealth = Mathf.Lerp(initialHealth, target, elapsedTime / m_damageDuration);
            UpdateSlider(m_currentHealth);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        m_currentHealth = target;
        UpdateSlider(m_currentHealth);

        m_damageCoroutine = null;
    }

    private void ResetHealth()
    {
        m_currentHealth = m_targetHealth;

        if (m_healthSlider != null)
        {
            m_healthSlider.maxValue = m_currentHealth;
            m_healthSlider.value = m_currentHealth;
        }
    }

    private void OnDisable()
    {
        if (m_damageCoroutine != null)
        {
            StopCoroutine(m_damageCoroutine);
            m_damageCoroutine = null;
        }
    }
}