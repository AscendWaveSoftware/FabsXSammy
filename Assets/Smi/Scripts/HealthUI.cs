using UnityEngine;
using UnityEngine.UI;

public class HealthUI : MonoBehaviour
{
    [SerializeField] private Slider m_healthSlider;

    public void StartSlider(float _value)
    {
        m_healthSlider.maxValue = _value;
        m_healthSlider.value = _value;
    }

    public void UpdateSlider(float _value)
    {
        m_healthSlider.value = _value;

    }
}
