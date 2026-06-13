using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MOBA_Health : MonoBehaviour, IDamageable
{
    public bool m_destroyed { get; private set; }
    public float m_currentHealth { get; private set; }
    public float m_damageDuration;

    [SerializeField] private float m_targetHealth;

    private Slider m_healthSlider;
    private Coroutine m_damageCoroutine;

    private IMinionPool m_minionPool;
    private GameObject m_prefab;

    private AI_Minion m_minion;
    private bool isMinion => m_minionPool != null;

    private void OnEnable()
    {
        m_destroyed = true;
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

    public void StartSlider(float maxValue)
    {
        m_healthSlider.maxValue = maxValue;
        m_healthSlider.value = maxValue;
    }

    public void UpdateSlider(float value)
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

    public void SetPool(IMinionPool pool, GameObject prefab)
    {
        m_minionPool = pool;
        m_prefab = prefab;
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
            if (m_minionPool != null)
                m_minionPool.Return(gameObject, m_prefab); // 🔥 wichtig
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