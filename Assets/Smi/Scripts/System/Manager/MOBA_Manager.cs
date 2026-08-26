//Code by Fabian Schmiedel

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class MOBA_Manager : MonoBehaviour
{
    public static MOBA_Manager Instance { get; private set; }

    [Header("Health")]
    public float m_allyHealth;
    public float m_enemyHealth;

    [Header("Team Sliders")]
    [SerializeField] public TeamSliderSetup[] m_Slider;

    [Header("Slider Animations")]
    [SerializeField] private Animation[] m_Animation;

    [Header("Destroyed Tower Animation")]
    [SerializeField] private Animation m_destroyedAnimation;
    [SerializeField] private TextMeshProUGUI m_text;

    [Header("Destroyed Tower Prefab")]
    [SerializeField] private GameObject[] m_DestroyedTower;

    [SerializeField] UnityEvent m_WinEvent;
    [SerializeField] UnityEvent m_LoseEvent;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void SetSlider(float _value, bool _isEnemy)
    {
        if (!_isEnemy)
            m_Slider[0].SetSliderValue(_value);
        else
            m_Slider[1].SetSliderValue(_value);
    }

    public void OnDamage(float _damage, bool _isEnemy)
    {
        if (!_isEnemy)
        {
            m_allyHealth -= _damage;
            m_Slider[0].OnDamage(m_allyHealth);
            if (m_allyHealth <= 0)
                LoseGame();
        }
        else
        {
            m_enemyHealth -= _damage;
            m_Slider[1].OnDamage(m_enemyHealth);
            if (m_enemyHealth <= 0)
                WinGame();
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
                m_text.text = "You + 1000 Scraps";
                break;

            case true:
                var destTowerEnemy = Instantiate(m_DestroyedTower[1], this.transform);
                destTowerEnemy.transform.position = _position;
                m_text.text = "Enemy + 1000 Scraps";
                break;
        }


        m_destroyedAnimation.Play();
    }

    public void WinGame()
    {
        if (PlaytestAnalyticsManager.Instance != null)
        {
            PlaytestAnalyticsManager.Instance.RegisterVictory();
            PlaytestAnalyticsManager.Instance.EndRun();
        }
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        m_WinEvent.Invoke();
        Time.timeScale = 0;
    }

    public void LoseGame()
    {
        if (PlaytestAnalyticsManager.Instance != null)
        {
            PlaytestAnalyticsManager.Instance.RegisterVictory();
            PlaytestAnalyticsManager.Instance.EndRun();
        }
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        m_LoseEvent.Invoke();
        Time.timeScale = 0;
    }
}

