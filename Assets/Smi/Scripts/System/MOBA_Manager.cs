using UnityEngine;
using UnityEngine.UI;

public class MOBA_Manager : MonoBehaviour
{
    public static MOBA_Manager Instance { get; private set; }

    public bool isGameOver = false;
    public float m_allyHealth;
    public float m_enemyHealth;
    [SerializeField] private Slider[] m_Slider;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    public void SetSlider(float _health, bool _isEnemy)
    {
        if (!_isEnemy)
        {
            m_allyHealth += _health;
            m_Slider[0].maxValue = m_allyHealth;
            m_Slider[0].value = m_Slider[0].maxValue;
        }
        else
        {
            m_enemyHealth += _health;
            m_Slider[1].maxValue = m_enemyHealth;
            m_Slider[1].value = m_Slider[1].maxValue;
        }
    }

    public void OnDamage(float _damage, bool _isEnemy)
    {
        if (!_isEnemy)
        {
            m_allyHealth -= _damage;
            m_Slider[0].value = m_allyHealth;
        }
        else
        {
            m_enemyHealth -= _damage;
            m_Slider[1].value = m_enemyHealth;
        }
    }
}

