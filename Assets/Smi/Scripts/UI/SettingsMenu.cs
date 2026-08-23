//Code by Fabian Schmiedel

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
    public Slider m_masterSlider;
    public Slider m_musicSlider;
    public Slider m_sfxSlider;

    [Header("UI References")]
    public TMP_Dropdown m_resolutionDropDown;
    public TMP_Dropdown m_qualityDropDown;
    public TMP_Dropdown m_fpsDropDown;
    public TMP_Dropdown m_hzDropDown;

    public Toggle m_fullscreenToggle;
    public Toggle m_vsyncToggle;

    private Canvas m_canvas;
    private bool bIsOpen = false;
    private float m_muteVolume;

    private Resolution[] m_resolutions;

    private string m_groupName = "master";

    readonly int[] m_fpsOptions = { 30, 60, 120, 144, 165, 240, -1 };

    private void Start()
    {
        SetupResolutions();
        SetupFPS();
        SetupHZ();

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

        if (savedResolutionIndex >= 0 &&
            savedResolutionIndex < m_resolutions.Length)
        {
            currentResolutionIndex = savedResolutionIndex;
        }

        m_resolutionDropDown.AddOptions(options);

        m_resolutionDropDown.value = currentResolutionIndex;
        m_resolutionDropDown.RefreshShownValue();
    }


    public void SetResoltuion(int _index)
    {
        if (_index < 0 || _index >= m_resolutions.Length)
            return;

        Resolution resolution = m_resolutions[_index];

        RefreshRate refreshRate = resolution.refreshRateRatio;

        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreenMode, refreshRate);

        PlayerPrefs.SetInt("ResolutionIndex", _index);
        PlayerPrefs.Save();

        SetupHZ();
    }

    private void SetupFPS()
    {
        if (m_fpsDropDown == null)
            return;

        m_fpsDropDown.ClearOptions();

        List<string> options = new List<string>();

        foreach (int fps in m_fpsOptions)
        {
            if (fps == -1)
                options.Add("Unlimited");
            else
                options.Add(fps + " FPS");
        }

        m_fpsDropDown.AddOptions(options);

        int savedFPSIndex = PlayerPrefs.GetInt("FPSIndex", 1);

        if (savedFPSIndex < 0 ||
            savedFPSIndex >= m_fpsOptions.Length)
        {
            savedFPSIndex = 1;
        }

        m_fpsDropDown.value = savedFPSIndex;
        m_fpsDropDown.RefreshShownValue();
    }


    public void SetFPS(int _index)
    {
        if (_index < 0 || _index >= m_fpsOptions.Length)
            return;

        int fps = m_fpsOptions[_index];

        Application.targetFrameRate = fps;

        PlayerPrefs.SetInt("FPSIndex", _index);
        PlayerPrefs.Save();
    }

    public void SetVSync(bool _enabled)
    {
        if (_enabled)
        {
            QualitySettings.vSyncCount = 1;

            Application.targetFrameRate = -1;
        }
        else
        {
            QualitySettings.vSyncCount = 0;

            int fpsIndex = PlayerPrefs.GetInt("FPSIndex", 1);

            if (fpsIndex >= 0 &&
                fpsIndex < m_fpsOptions.Length)
            {
                Application.targetFrameRate =
                    m_fpsOptions[fpsIndex];
            }
        }

        PlayerPrefs.SetInt("VSync", _enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (m_vsyncToggle != null)
            m_vsyncToggle.isOn = _enabled;
    }

    private void SetupHZ()
    {
        if (m_hzDropDown == null || m_resolutions == null)
            return;

        m_hzDropDown.ClearOptions();

        List<int> refreshRates = new List<int>();

        int resolutionIndex = m_resolutionDropDown.value;

        if (resolutionIndex >= 0 &&
            resolutionIndex < m_resolutions.Length)
        {
            Resolution selectedResolution =
                m_resolutions[resolutionIndex];

            int selectedWidth = selectedResolution.width;
            int selectedHeight = selectedResolution.height;

            for (int i = 0; i < m_resolutions.Length; i++)
            {
                if (m_resolutions[i].width == selectedWidth &&
                    m_resolutions[i].height == selectedHeight)
                {
                    int hz = GetRefreshRate(m_resolutions[i]);

                    if (!refreshRates.Contains(hz))
                    {
                        refreshRates.Add(hz);
                    }
                }
            }
        }

        if (refreshRates.Count == 0)
        {
            refreshRates.Add(
                GetRefreshRate(Screen.currentResolution)
            );
        }

        refreshRates.Sort();

        List<string> options = new List<string>();

        foreach (int hz in refreshRates)
        {
            options.Add(hz + " Hz");
        }

        m_hzDropDown.AddOptions(options);

        int savedHZ = PlayerPrefs.GetInt("RefreshRate", -1);

        int selectedIndex = 0;

        if (savedHZ != -1)
        {
            for (int i = 0; i < refreshRates.Count; i++)
            {
                if (refreshRates[i] == savedHZ)
                {
                    selectedIndex = i;
                    break;
                }
            }
        }
        else
        {
            int currentHZ = GetRefreshRate(Screen.currentResolution);

            for (int i = 0; i < refreshRates.Count; i++)
            {
                if (refreshRates[i] == currentHZ)
                {
                    selectedIndex = i;
                    break;
                }
            }
        }

        m_hzDropDown.value = selectedIndex;
        m_hzDropDown.RefreshShownValue();
    }


    public void SetHZ(int _index)
    {
        if (m_hzDropDown == null)
            return;

        if (_index < 0 || _index >= m_hzDropDown.options.Count)
            return;

        string selectedText =
            m_hzDropDown.options[_index].text;

        string hzString =
            selectedText.Replace(" Hz", "");

        if (!int.TryParse(hzString, out int hz))
            return;

        Resolution resolution =
            m_resolutions[m_resolutionDropDown.value];

        RefreshRate refreshRate = new RefreshRate
        {
            numerator = (uint)hz,
            denominator = 1
        };

        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreenMode, refreshRate);

        PlayerPrefs.SetInt("RefreshRate", hz);
        PlayerPrefs.Save();
    }

    private int GetRefreshRate(Resolution resolution)
    {
        return Mathf.RoundToInt(
            (float)resolution.refreshRateRatio.value
        );
    }

    private void LoadSettings()
    {
        LoadSliderValue(m_masterSlider, "master");
        LoadSliderValue(m_musicSlider, "music");
        LoadSliderValue(m_sfxSlider, "sfx");

        int savedQuality = PlayerPrefs.GetInt("QualityIndex", 2);

        if (m_qualityDropDown != null)
            m_qualityDropDown.value = savedQuality;

        SetQuality(savedQuality);

        bool savedFullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;

        if (m_fullscreenToggle != null)
            m_fullscreenToggle.isOn = savedFullscreen;

        SetFullscreen(savedFullscreen);

        if (m_resolutionDropDown != null &&
            m_resolutionDropDown.value < m_resolutions.Length)
        {
            SetResoltuion(m_resolutionDropDown.value);
        }

        int savedFPSIndex = PlayerPrefs.GetInt("FPSIndex", 1);

        if (m_fpsDropDown != null)
        {
            m_fpsDropDown.value = savedFPSIndex;
            m_fpsDropDown.RefreshShownValue();
        }

        bool savedVSync =
            PlayerPrefs.GetInt("VSync", 0) == 1;

        if (m_vsyncToggle != null)
            m_vsyncToggle.isOn = savedVSync;

        SetVSync(savedVSync);


        if (!savedVSync)
        {
            SetFPS(savedFPSIndex);
        }
    }

    private void LoadSliderValue(Slider _slider, string _groupName)
    {
        float value = PlayerPrefs.GetFloat(_groupName, 0f);

        if (_slider != null)
            _slider.value = value;

        m_groupName = _groupName;
        SetVolume(value);
    }

    public void SetVolume(float _volume)
    {
        m_audioMixer.SetFloat(m_groupName, _volume);

        PlayerPrefs.SetFloat(m_groupName, _volume);

        PlayerPrefs.Save();
    }


    public void SetQuality(int _index)
    {
        QualitySettings.SetQualityLevel(
            _index
        );

        if (m_qualityDropDown != null)
            m_qualityDropDown.value = _index;

        PlayerPrefs.SetInt("QualityIndex", _index);

        PlayerPrefs.Save();
    }

    public void SetFullscreen(bool _fullscreen)
    {
        Screen.fullScreenMode = _fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        if (m_fullscreenToggle != null)
            m_fullscreenToggle.isOn = _fullscreen;

        PlayerPrefs.SetInt("Fullscreen", _fullscreen ? 1 : 0);

        PlayerPrefs.Save();
    }

    private void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame && Time.timeScale != 0)
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
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        m_canvas.enabled = true;
        PveRuntime.SetPaused(true);
    }

    public void CloseMenu()
    {
        bIsOpen = false;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        m_canvas.enabled = false;
        PveRuntime.SetPaused(false);
    }
    public void RestartGame()
    {
        CloseMenu();

        var currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    public void BackToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
        Destroy(MOBA_Manager.Instance.gameObject);
    }

    public void FeedbackButton()
    {
        Application.OpenURL("https://nx103418.your-storageshare.de/apps/forms/s/w4YXm3XjBDeKKWx7JyAtJoBf");
    }

    public void Mute(bool _status)
    {
        m_groupName = "master";

        if (_status)
        {
            m_muteVolume = PlayerPrefs.GetFloat(m_groupName, 0f);
            m_audioMixer.SetFloat(m_groupName, -60);

            m_masterSlider.value = -60;
            m_masterSlider.enabled = false;
        }
        else
        {
            m_masterSlider.enabled = true;
            m_masterSlider.value = m_muteVolume;
            SetVolume(m_muteVolume);
        }
    }

    public void SetAudioGroup(string _group)
    {
        m_groupName = _group;
    }
}