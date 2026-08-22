using UnityEngine;
using UnityEngine.UI;

public class ButtonInteractable : MonoBehaviour
{
    Button m_thisButton;
    PlayerResources m_playerResources;


    private void DisableButton()
    {
        m_thisButton = GetComponent<Button>();
        m_thisButton.interactable = false;
    }

    private void CheckButton(int _price)
    {
        m_thisButton = GetComponent<Button>();
        m_playerResources = FindAnyObjectByType<PlayerResources>();

        if (m_playerResources.CurrentScrap >= _price)
            m_thisButton.interactable = true;

    }
}
