//Code by Fabian Schmiedel

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ButtonsAudio : MonoBehaviour
{
    [Header("Audio Clip")]
    private AudioSource audioSource;
    [SerializeField] private AudioClip hoverSound;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();

        SetupAllButtons();
    }

    public void SetupAllButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Exclude);

        foreach (Button button in buttons)
        {
            RegisterHoverEvent(button);
        }
    }

    public void OnButtonClick(AudioClip m_clip)
    {
        PlaySound(m_clip);
    }

    private void RegisterHoverEvent(Button button)
    {
        EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>() ?? button.gameObject.AddComponent<EventTrigger>();

        trigger.triggers.Clear();

        EventTrigger.Entry hoverEntry = new EventTrigger.Entry();
        hoverEntry.eventID = EventTriggerType.PointerEnter;

        hoverEntry.callback.AddListener((data) => { OnButtonHover(button); });
        trigger.triggers.Add(hoverEntry);
    }

    private void OnButtonHover(Button button)
    {
        if (button != null && button.interactable)
        {
            PlaySound(hoverSound);
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
