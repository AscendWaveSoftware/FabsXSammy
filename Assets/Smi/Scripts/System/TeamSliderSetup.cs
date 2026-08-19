//Code by Fabian Schmiedel

using UnityEngine;
using UnityEngine.UI;

public class TeamSliderSetup : MonoBehaviour
{
    private Slider m_slider;
    private Animation m_animation;
    private float health;

    void Awake()
    {
        m_slider = GetComponent<Slider>();
        m_animation = GetComponent<Animation>();
    }

    public void SetSliderValue(float _value)
    {
        health += _value;
        m_slider.maxValue = health;
        m_slider.value = health;

        MOBA_Manager.Instance.m_allyHealth = health;
        MOBA_Manager.Instance.m_enemyHealth = health;
    }

    public void ChangeSliderValue(float _value)
    {
        m_slider.value -= _value;
    }

    public void OnDamage(float _value)
    {
        m_slider.value = _value;
        m_animation.Play();
    }
}
