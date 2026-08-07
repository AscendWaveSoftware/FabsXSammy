using UnityEngine;
using UnityEngine.UI;

public class MOBA_Manager : MonoBehaviour
{
    public static MOBA_Manager Instance { get; private set; }

    public bool isGameOver = false;
    public float m_allyHealth;
    public float m_enemyHealth;
    [SerializeField] private Slider[] m_Slider;
    [SerializeField] private GameObject[] m_DestroyedTower;

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
            if (m_Slider[0] != null)
            {
                m_allyHealth += _health;
                m_Slider[0].maxValue = m_allyHealth;
                m_Slider[0].value = m_Slider[0].maxValue;
            }
        }
        else
        {
            if (m_Slider[1] != null)
            {
                m_enemyHealth += _health;
                m_Slider[1].maxValue = m_enemyHealth;
                m_Slider[1].value = m_Slider[1].maxValue;
            }
        }
    }

    public void OnDamage(float _damage, bool _isEnemy)
    {
        for (int i = 0; i < m_Slider.Length; i++)
        {
            if (m_Slider[i])
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
    }

    public void DestroyedTower(Vector3 _position, bool _isEnemy)
    {
        switch (_isEnemy)
        {
            case false:
                var destTowerAlly = Instantiate(m_DestroyedTower[0], this.transform);
                destTowerAlly.transform.position = _position;
                destTowerAlly.transform.Rotate(0, Random.Range(0, 355), 0);
                break;

            case true:
                var destTowerEnemy = Instantiate(m_DestroyedTower[1], this.transform);
                destTowerEnemy.transform.position = _position;
                break;
        }
    }
}

