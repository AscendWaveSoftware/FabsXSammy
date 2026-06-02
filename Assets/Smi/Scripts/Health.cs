using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Health : MonoBehaviour, IDamageable
{
    private Slider m_healthSlider;

    public float m_damageDuration;
    public float m_currentHealth;
    public SpawnManager m_minionPool;
    public bool m_destroyed = true;

    private float m_targetHealth;
    private Coroutine m_damageCoroutine;

    private AI_Minion m_minion;
    bool isMinion => m_minion != null;


    private void OnEnable()
    {
        m_minion = GetComponent<AI_Minion>();

        StartCoroutine(MinionAlive());

        m_targetHealth = m_currentHealth;
        m_healthSlider = GetComponentInChildren<Slider>();

        StartSlider(m_targetHealth);
    }

    IEnumerator MinionAlive()
    {
        yield return new WaitForSeconds(3);
        m_destroyed = false;
    }

    public void StartSlider(float _maxValue)
    {
        m_healthSlider.maxValue = _maxValue;
        m_healthSlider.value = _maxValue;
    }

    public void UpdateSlider(float _value)
    {
        m_healthSlider.value = m_currentHealth;
    }

    public void StartLerpHealth()
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
            UpdateSlider(m_currentHealth);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        m_currentHealth = target;
        UpdateSlider(m_currentHealth);

        m_damageCoroutine = null;
    }

    public void TakeDamage(float damage)
    {
        Debug.Log(this);
        m_targetHealth -= damage;

        if (m_targetHealth <= 0)
        {
            if (isMinion)
                m_minionPool.ReturnMinion(gameObject);
            else
                Destroy(gameObject);
        }
        else if (m_damageCoroutine == null)
        {
            StartLerpHealth();
        }
    }
}
