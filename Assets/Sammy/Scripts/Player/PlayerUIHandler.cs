using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUIHandler : MonoBehaviour
{
    [Header("Resource UI")]
    [SerializeField] private TextMeshProUGUI m_scrapText;

    [Header("XP UI")]
    [SerializeField] private PlayerExperience m_playerExperience;
    [SerializeField] private TextMeshProUGUI m_levelText;
    [SerializeField] private TextMeshProUGUI m_xpText;
    [SerializeField] private Image m_xpBarImage;

    [Header("Gameplay Cursor")]
    [SerializeField] private bool m_hideCursorDuringGameplay = true;
    [SerializeField] private bool m_lockCursorDuringGameplay = true;
    [SerializeField, Min(0.1f)] private float m_shopSearchInterval = 1f;

    private static PlayerUIHandler s_instance;

    private readonly HashSet<Object> m_interactiveUIRequests = new HashSet<Object>();
    private readonly List<Canvas> m_shopCanvases = new List<Canvas>();
    private float m_nextShopSearchTime;
    private bool m_cursorIsVisible;

    public static void SetInteractiveUIActive(Object _requester, bool _isActive)
    {
        if (s_instance == null || _requester == null)
            return;

        if (_isActive)
            s_instance.m_interactiveUIRequests.Add(_requester);
        else
            s_instance.m_interactiveUIRequests.Remove(_requester);

        s_instance.RefreshCursorState();
    }

    private void Awake()
    {
        s_instance = this;

        if (m_playerExperience == null)
            m_playerExperience = FindAnyObjectByType<PlayerExperience>();

        FindShopCanvases();
        ApplyCursorState(!m_hideCursorDuringGameplay);

        //TODO: hier muss noch initial Gold und Scrap Text gesetzt werden
    }

    private void Update()
    {
        if (Time.unscaledTime >= m_nextShopSearchTime)
            FindShopCanvases();

        RefreshCursorState();
    }

    private void OnEnable()
    {
        PlayerResources.OnResourceChanged += HandleResourceChanged;

        if (m_playerExperience != null)
            m_playerExperience.OnExperienceChanged += HandleExperienceChanged;
    }

    private void Start()
    {
        if (m_playerExperience != null)
            HandleExperienceChanged(
                m_playerExperience.CurrentLevel,
                m_playerExperience.CurrentXP,
                m_playerExperience.XPToNextLevel
            );
    }

    private void OnDisable()
    {
        PlayerResources.OnResourceChanged -= HandleResourceChanged;

        if (m_playerExperience != null)
            m_playerExperience.OnExperienceChanged -= HandleExperienceChanged;
    }

    private void OnDestroy()
    {
        if (s_instance != this)
            return;

        m_interactiveUIRequests.Clear();
        s_instance = null;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnApplicationFocus(bool _hasFocus)
    {
        if (_hasFocus)
            RefreshCursorState(true);
    }

    private void FindShopCanvases()
    {
        m_shopCanvases.Clear();

        ScrapShop[] shops = FindObjectsByType<ScrapShop>(FindObjectsInactive.Include);
        foreach (ScrapShop shop in shops)
        {
            if (shop == null)
                continue;

            Canvas shopCanvas = shop.GetComponentInChildren<Canvas>(true);
            if (shopCanvas != null)
                m_shopCanvases.Add(shopCanvas);
        }

        m_nextShopSearchTime = Time.unscaledTime + Mathf.Max(0.1f, m_shopSearchInterval);
    }

    private void RefreshCursorState(bool _force = false)
    {
        m_interactiveUIRequests.RemoveWhere(requester => requester == null);

        bool uiRequestedCursor = m_interactiveUIRequests.Count > 0;
        bool shouldBeVisible = !m_hideCursorDuringGameplay || uiRequestedCursor || IsShopOpen();

        if (_force || shouldBeVisible != m_cursorIsVisible)
            ApplyCursorState(shouldBeVisible);
    }

    private bool IsShopOpen()
    {
        foreach (Canvas shopCanvas in m_shopCanvases)
        {
            if (shopCanvas != null && shopCanvas.enabled && shopCanvas.gameObject.activeInHierarchy)
                return true;
        }

        return false;
    }

    private void ApplyCursorState(bool _visible)
    {
        m_cursorIsVisible = _visible;
        Cursor.lockState = _visible || !m_lockCursorDuringGameplay
            ? CursorLockMode.None
            : CursorLockMode.Locked;
        Cursor.visible = _visible;
    }

    private void OnValidate()
    {
        m_shopSearchInterval = Mathf.Max(0.1f, m_shopSearchInterval);
    }

    private void HandleResourceChanged(object sender, ResourceChangedEventArgs e)
    {
        switch (e.ResourceType)
        {
            case Resources.SCRAP:
                m_scrapText.text = e.CurrentResourceAmount.ToString();
                break;
        }
    }

    private void HandleExperienceChanged(int _level, int _currentXP, int _xpToNextLevel)
    {
        if (m_levelText != null)
            m_levelText.text = $"Level {_level}";

        if (m_xpText != null)
            m_xpText.text = $"{_currentXP} / {_xpToNextLevel} XP";

        if(m_xpBarImage != null)
        {
            float xpPercentage = _xpToNextLevel <= 0 ? 0f : (float)_currentXP / _xpToNextLevel;
            m_xpBarImage.fillAmount = xpPercentage;
        }
    }
}
