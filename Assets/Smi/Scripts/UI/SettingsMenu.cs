using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SettingsMenu : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioMixer m_audioMixer;
    public Slider m_volumeSlider;

    [Header("UI References")]
    public TMP_Dropdown m_resolutionDropDown;
    public TMP_Dropdown m_qualityDropDown;
    public Toggle m_fullscreenToggle;

    Canvas m_canvas;
    bool bIsOpen = false;

    Resolution[] m_resolutions;

    private void Start()
    {
        SetupResolutions();
        LoadSettings();

        m_canvas = GetComponent<Canvas>();
    }

    private void SetupResolutions()
    {
        m_resolutions = Screen.resolutions;
        m_resolutionDropDown.ClearOptions();
        List<string> options = new List<string>();

        int savedResolutionIndex = PlayerPrefs.GetInt("ResolutionIndex", -1);
        int currentResolutionIndex = 0;

        for (int i = 0; i < m_resolutions.Length; i++)
        {
            string option = $"{m_resolutions[i].width} x {m_resolutions[i].height}";
            options.Add(option);

            if (savedResolutionIndex == -1)
            {
                if (m_resolutions[i].width == Screen.currentResolution.width &&
                    m_resolutions[i].height == Screen.currentResolution.height)
                {
                    currentResolutionIndex = i;
                }
            }
        }

        if (savedResolutionIndex != -1)
        {
            currentResolutionIndex = savedResolutionIndex;
        }

        m_resolutionDropDown.AddOptions(options);
        m_resolutionDropDown.value = currentResolutionIndex;
        m_resolutionDropDown.RefreshShownValue();
    }

    private void LoadSettings()
    {
        float savedVolume = PlayerPrefs.GetFloat("Volume", 0f);
        if (m_volumeSlider != null) m_volumeSlider.value = savedVolume;
        SetVolume(savedVolume);

        int savedQuality = PlayerPrefs.GetInt("QualityIndex", 2);
        if (m_qualityDropDown != null) m_qualityDropDown.value = savedQuality;
        SetQuality(savedQuality);

        bool savedFullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        if (m_fullscreenToggle != null) m_fullscreenToggle.isOn = savedFullscreen;
        SetFullscreen(savedFullscreen);

        if (m_resolutionDropDown.value < m_resolutions.Length)
        {
            SetResoltuion(m_resolutionDropDown.value);
        }
    }

    public void SetVolume(float _volume)
    {
        m_audioMixer.SetFloat("volume", _volume);
        PlayerPrefs.SetFloat("Volume", _volume);
        PlayerPrefs.Save();
    }

    public void SetQuality(int _index)
    {
        QualitySettings.SetQualityLevel(_index);
        if (m_qualityDropDown != null) m_qualityDropDown.value = _index;
        PlayerPrefs.SetInt("QualityIndex", _index);
        PlayerPrefs.Save();
    }

    public void SetFullscreen(bool _fullscreen)
    {
        Screen.fullScreen = _fullscreen;
        if (m_fullscreenToggle != null) m_fullscreenToggle.isOn = _fullscreen;
        PlayerPrefs.SetInt("Fullscreen", _fullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetResoltuion(int _index)
    {
        Resolution _resolution = m_resolutions[_index];
        Screen.SetResolution(_resolution.width, _resolution.height, Screen.fullScreen);

        PlayerPrefs.SetInt("ResolutionIndex", _index);
        PlayerPrefs.Save();
    }

    private void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (!bIsOpen)
                OpenMenu();
            else
                CloseMenu();
        }

    }

    public void OpenMenu()
    {
        bIsOpen = true;
        m_canvas.enabled = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 0f;
    }

    public void CloseMenu()
    {
        bIsOpen = false;
        m_canvas.enabled = false;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        Time.timeScale = 1f;
    }

    public void RestartGame()
    {
        CloseMenu();
        var currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    public void BackToMenu()
    {
        SceneManager.LoadScene(0);
        Destroy(MOBA_Manager.Instance.gameObject);
    }

    public void FeedbackButton()
    {
        Application.OpenURL("https://nx103418.your-storageshare.de/apps/forms/s/w4YXm3XjBDeKKWx7JyAtJoBf");
    }

    public void Mute(bool _status)
    {
        if (_status)
        {
            m_audioMixer.SetFloat("volume", -60);
            m_volumeSlider.value = -60;
            m_volumeSlider.enabled = false;
        }

        else
        {
            m_volumeSlider.enabled = true;
        }
    }
}
