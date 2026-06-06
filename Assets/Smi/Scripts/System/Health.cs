using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Health : MonoBehaviour, IDamageable
{

    public bool m_destroyed { get; private set; }
    public float m_currentHealth { get; private set; }
    public float m_damageDuration;

    [SerializeField] private float m_targetHealth;
    private Slider m_healthSlider;
    private Coroutine m_damageCoroutine;

    private IMinionPool m_minionPool;
    private AI_Minion m_minion;
    bool isMinion => m_minion != null;


    private void OnEnable()
    {
        m_minion = GetComponent<AI_Minion>();

        if (m_damageCoroutine != null)
        {
            StopCoroutine(m_damageCoroutine);
            m_damageCoroutine = null;
        }

        m_healthSlider = GetComponentInChildren<Slider>(true);

        ResetHealth();
        StartSlider(m_targetHealth);

        StartCoroutine(MinionAlive());
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

    public void SetPool(IMinionPool pool)
    {
        m_minionPool = pool;
    }

    public void TakeDamage(float damage)
    {
        if (!gameObject.activeInHierarchy)
            return;

        m_currentHealth -= damage;
        m_currentHealth = Mathf.Max(m_currentHealth, 0);

        UpdateSlider(m_currentHealth);

        if (m_currentHealth <= 0)
        {
            if (isMinion)
                m_minionPool.Return(gameObject);
            else
                Destroy(gameObject);
        }
    }

    private void ResetHealth()
    {
        m_currentHealth = m_targetHealth;

        if (m_healthSlider != null)
        {
            m_healthSlider.maxValue = m_currentHealth;
            m_healthSlider.value = m_currentHealth;
        }

        m_destroyed = false;
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
