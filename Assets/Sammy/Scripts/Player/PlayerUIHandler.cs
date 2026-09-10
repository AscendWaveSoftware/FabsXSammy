using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUIHandler : MonoBehaviour
{
    private static readonly Color MainTextColor = SteampunkUI.Parchment;
    private static readonly Color SecondaryTextColor = SteampunkUI.ParchmentMuted;
    private static readonly Color XpColor = new Color(0.95f, 0.74f, 0.32f, 1f);
    private static readonly Color ScrapColor = new Color(0.96f, 0.76f, 0.4f, 1f);

    // Patina, amber and rust instead of pure traffic light colours, so the bar
    // still reads at a glance but belongs to the brass palette.
    private static readonly Color HealthHighColor = new Color(0.47f, 0.69f, 0.31f, 1f);
    private static readonly Color HealthMediumColor = new Color(0.9f, 0.63f, 0.2f, 1f);
    private static readonly Color HealthLowColor = new Color(0.86f, 0.23f, 0.14f, 1f);

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

    [Header("HUD Animation")]
    [SerializeField, Min(0f)] private float m_progressBarSmoothing = 10f;

    private static PlayerUIHandler s_instance;

    private readonly HashSet<Object> m_interactiveUIRequests = new HashSet<Object>();
    private readonly List<Canvas> m_shopCanvases = new List<Canvas>();
    private float m_nextShopSearchTime;
    private bool m_cursorIsVisible;
    private Canvas m_playerHudCanvas;
    private PlayerHealthUI m_playerHealthUI;
    private float m_targetXpFillAmount;
    private bool m_hasInitialXpValue;

    /// <summary>
    /// Screen space root the HUD is built into. Available from Start onwards,
    /// because the reparenting happens in Awake.
    /// </summary>
    public RectTransform HudRoot => m_playerHudCanvas != null ? m_playerHudCanvas.transform as RectTransform : null;

    /// <summary>Font the rest of the HUD uses, so additions match it.</summary>
    public TMP_FontAsset HudFont => m_levelText != null && m_levelText.font != null
        ? m_levelText.font
        : TMP_Settings.defaultFontAsset;

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

        m_playerHealthUI = GetComponent<PlayerHealthUI>();
        ConfigureScreenSpaceHud();
        FindShopCanvases();
        ApplyCursorState(!m_hideCursorDuringGameplay);

        //TODO: hier muss noch initial Gold und Scrap Text gesetzt werden
    }

    private void Update()
    {
        if (Time.unscaledTime >= m_nextShopSearchTime)
            FindShopCanvases();

        AnimateHud();
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
        m_progressBarSmoothing = Mathf.Max(0f, m_progressBarSmoothing);
    }

    private void HandleResourceChanged(object sender, ResourceChangedEventArgs e)
    {
        switch (e.ResourceType)
        {
            case Resources.SCRAP:
                if (m_scrapText != null)
                    m_scrapText.text = $"SCRAP  {e.CurrentResourceAmount}";
                break;
        }
    }

    private void HandleExperienceChanged(int _level, int _currentXP, int _xpToNextLevel)
    {
        if (m_levelText != null)
            m_levelText.text = $"<size=36%><cspace=2>LEVEL</cspace></size>\n{_level}";

        if (m_xpText != null)
            m_xpText.text = $"{_currentXP} / {_xpToNextLevel} XP";

        if(m_xpBarImage != null)
        {
            float xpPercentage = _xpToNextLevel <= 0 ? 0f : (float)_currentXP / _xpToNextLevel;
            m_targetXpFillAmount = xpPercentage;

            if (!m_hasInitialXpValue)
                m_xpBarImage.fillAmount = xpPercentage;

            m_hasInitialXpValue = true;
        }
    }

    private void AnimateHud()
    {
        if (!m_hasInitialXpValue || m_xpBarImage == null)
            return;

        float interpolation = 1f - Mathf.Exp(-m_progressBarSmoothing * Time.unscaledDeltaTime);
        m_xpBarImage.fillAmount = Mathf.Lerp(m_xpBarImage.fillAmount, m_targetXpFillAmount, interpolation);
    }

    private void ConfigureScreenSpaceHud()
    {
        m_playerHudCanvas = FindOwnedHudCanvas();
        if (m_playerHudCanvas == null)
        {
            Debug.LogWarning("Player HUD Canvas could not be found.");
            return;
        }

        Canvas screenSpaceCanvas = FindTargetScreenSpaceCanvas(m_playerHudCanvas);
        RectTransform hudRoot = m_playerHudCanvas.transform as RectTransform;

        if (screenSpaceCanvas != null)
        {
            hudRoot.SetParent(screenSpaceCanvas.transform, false);
            hudRoot.SetAsFirstSibling();
            m_playerHudCanvas.overrideSorting = false;
        }
        else
        {
            hudRoot.SetParent(null, false);
            m_playerHudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            m_playerHudCanvas.sortingOrder = 0;
        }

        m_playerHudCanvas.gameObject.name = "Player HUD";
        SetStretch(hudRoot, 0f);
        hudRoot.localScale = Vector3.one;

        CanvasScaler scaler = m_playerHudCanvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.enabled = screenSpaceCanvas == null;
        }

        GraphicRaycaster raycaster = m_playerHudCanvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            raycaster.enabled = false;

        SetLayerRecursively(m_playerHudCanvas.gameObject, screenSpaceCanvas != null ? screenSpaceCanvas.gameObject.layer : 5);
        BuildHudLayout(hudRoot);
    }

    private Canvas FindOwnedHudCanvas()
    {
        if (m_scrapText != null)
            return m_scrapText.GetComponentInParent<Canvas>();

        if (m_levelText != null)
            return m_levelText.GetComponentInParent<Canvas>();

        return null;
    }

    private static Canvas FindTargetScreenSpaceCanvas(Canvas _ownedCanvas)
    {
        Canvas fallbackCanvas = null;
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);

        foreach (Canvas canvas in canvases)
        {
            if (canvas == null || canvas == _ownedCanvas || !canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                continue;

            if (canvas.gameObject.name == "Player Canvas")
                return canvas;

            if (fallbackCanvas == null)
                fallbackCanvas = canvas;
        }

        return fallbackCanvas;
    }

    private void BuildHudLayout(RectTransform _hudRoot)
    {
        // Riveted iron plate in the lower left, with a brass cog as the level gauge.
        Image statusPanel = CreateImage("Status Panel", _hudRoot, SteampunkUI.Frame, Color.white);
        SetRect(statusPanel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(28f, 28f), new Vector2(520f, 164f), Vector2.zero);
        AddPanelEffects(statusPanel);

        Image levelGear = CreateImage("Level Gear", statusPanel.rectTransform, SteampunkUI.Gear, SteampunkUI.Brass);
        SetRect(levelGear.rectTransform, Vector2.zero, Vector2.zero, new Vector2(18f, 16f), new Vector2(132f, 132f), Vector2.zero);

        Image levelFace = CreateImage("Level Face", levelGear.rectTransform, SteampunkUI.Disc, new Color(0.16f, 0.13f, 0.1f, 1f));
        SetStretch(levelFace.rectTransform, 31f);

        // Scrap sits on a smaller plate in the upper right, marked with its own cog.
        Image scrapPanel = CreateImage("Scrap Panel", _hudRoot, SteampunkUI.Frame, Color.white);
        SetRect(scrapPanel.rectTransform, Vector2.one, Vector2.one, new Vector2(-28f, -28f), new Vector2(250f, 70f), Vector2.one);
        scrapPanel.pixelsPerUnitMultiplier = 1.4f;
        AddPanelEffects(scrapPanel);

        Image scrapGear = CreateImage("Scrap Gear", scrapPanel.rectTransform, SteampunkUI.Gear, SteampunkUI.Copper);
        SetRect(scrapGear.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(40f, 40f), new Vector2(0f, 0.5f));

        ConfigureLevelText(levelFace.rectTransform);
        ConfigureHealthSection(statusPanel.rectTransform);
        ConfigureXpSection(statusPanel.rectTransform);
        ConfigureScrapText(scrapPanel.rectTransform);
    }

    private void ConfigureLevelText(RectTransform _parent)
    {
        if (m_levelText == null)
            return;

        m_levelText.transform.SetParent(_parent, false);
        ConfigureText(m_levelText, 34f, SteampunkUI.BrassLight, FontStyles.Bold, TextAlignmentOptions.Center);
        m_levelText.lineSpacing = -28f;
        m_levelText.textWrappingMode = TextWrappingModes.NoWrap;
        SetStretch(m_levelText.rectTransform, 0f);
    }

    private void ConfigureHealthSection(RectTransform _parent)
    {
        if (m_playerHealthUI == null || m_playerHealthUI.HealthBarImage == null)
            return;

        TextMeshProUGUI healthLabel = CreateText("Health Label", _parent, "HEALTH");
        ConfigureText(healthLabel, 13f, SecondaryTextColor, FontStyles.Bold, TextAlignmentOptions.Left);
        healthLabel.characterSpacing = 3f;
        SetRect(healthLabel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(166f, 104f), new Vector2(140f, 20f), Vector2.zero);

        if (m_playerHealthUI.HealthText != null)
        {
            TextMeshProUGUI healthText = m_playerHealthUI.HealthText;
            healthText.transform.SetParent(_parent, false);
            ConfigureText(healthText, 15f, MainTextColor, FontStyles.Bold, TextAlignmentOptions.Right);
            SetRect(healthText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(316f, 104f), new Vector2(176f, 20f), Vector2.zero);
        }

        Image healthTrack = CreateImage("Health Track", _parent, SteampunkUI.Track, Color.white);
        SetRect(healthTrack.rectTransform, Vector2.zero, Vector2.zero, new Vector2(164f, 72f), new Vector2(330f, 28f), Vector2.zero);
        ConfigureFillImage(m_playerHealthUI.HealthBarImage, healthTrack.rectTransform, 3f);
        m_playerHealthUI.ApplyBarColors(HealthHighColor, HealthMediumColor, HealthLowColor);
    }

    private void ConfigureXpSection(RectTransform _parent)
    {
        if (m_xpBarImage == null)
            return;

        TextMeshProUGUI xpLabel = CreateText("XP Label", _parent, "EXPERIENCE");
        ConfigureText(xpLabel, 12f, SecondaryTextColor, FontStyles.Bold, TextAlignmentOptions.Left);
        xpLabel.characterSpacing = 3f;
        SetRect(xpLabel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(166f, 46f), new Vector2(160f, 18f), Vector2.zero);

        if (m_xpText != null)
        {
            m_xpText.transform.SetParent(_parent, false);
            ConfigureText(m_xpText, 13f, MainTextColor, FontStyles.Bold, TextAlignmentOptions.Right);
            SetRect(m_xpText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(316f, 46f), new Vector2(176f, 18f), Vector2.zero);
        }

        Image xpTrack = CreateImage("XP Track", _parent, SteampunkUI.Track, Color.white);
        SetRect(xpTrack.rectTransform, Vector2.zero, Vector2.zero, new Vector2(164f, 24f), new Vector2(330f, 18f), Vector2.zero);
        xpTrack.pixelsPerUnitMultiplier = 1.6f;
        m_xpBarImage.color = XpColor;
        ConfigureFillImage(m_xpBarImage, xpTrack.rectTransform, 3f);
    }

    private void ConfigureScrapText(RectTransform _parent)
    {
        if (m_scrapText == null)
            return;

        m_scrapText.transform.SetParent(_parent, false);
        ConfigureText(m_scrapText, 22f, ScrapColor, FontStyles.Bold, TextAlignmentOptions.Center);
        m_scrapText.characterSpacing = 2f;
        SetStretch(m_scrapText.rectTransform, 12f);
        m_scrapText.rectTransform.offsetMin = new Vector2(62f, 12f);

        PlayerResources resources = GetComponent<PlayerResources>();
        if (resources != null)
            m_scrapText.text = $"SCRAP  {resources.CurrentScrap}";
    }

    private Image CreateImage(string _name, Transform _parent, Sprite _sprite, Color _color)
    {
        Transform existing = _parent.Find(_name);
        if (existing != null)
            return existing.GetComponent<Image>();

        GameObject imageObject = new GameObject(_name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.layer = _parent.gameObject.layer;
        imageObject.transform.SetParent(_parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = _sprite;
        image.type = _sprite != null && _sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.color = _color;
        image.raycastTarget = false;
        return image;
    }

    private TextMeshProUGUI CreateText(string _name, Transform _parent, string _content)
    {
        Transform existing = _parent.Find(_name);
        if (existing != null)
            return existing.GetComponent<TextMeshProUGUI>();

        GameObject textObject = new GameObject(_name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = _parent.gameObject.layer;
        textObject.transform.SetParent(_parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = m_levelText != null ? m_levelText.font : TMP_Settings.defaultFontAsset;
        text.text = _content;
        text.raycastTarget = false;
        return text;
    }

    private static void ConfigureText(TextMeshProUGUI _text, float _fontSize, Color _color, FontStyles _style, TextAlignmentOptions _alignment)
    {
        _text.fontSize = _fontSize;
        _text.enableAutoSizing = false;
        _text.fontStyle = _style;
        _text.color = _color;
        _text.alignment = _alignment;
        _text.raycastTarget = false;
    }

    private static void ConfigureFillImage(Image _fillImage, RectTransform _track, float _padding)
    {
        _fillImage.transform.SetParent(_track, false);
        _fillImage.sprite = SteampunkUI.BarFill;
        _fillImage.type = Image.Type.Filled;
        _fillImage.fillMethod = Image.FillMethod.Horizontal;
        _fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        _fillImage.preserveAspect = false;
        _fillImage.raycastTarget = false;
        SetStretch(_fillImage.rectTransform, _padding);
    }

    private static void AddPanelEffects(Image _panel)
    {
        Shadow shadow = _panel.GetComponent<Shadow>();
        if (shadow == null)
            shadow = _panel.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(0f, -6f);
        shadow.useGraphicAlpha = true;
    }

    private static void SetRect(RectTransform _rect, Vector2 _anchorMin, Vector2 _anchorMax, Vector2 _position, Vector2 _size, Vector2 _pivot)
    {
        _rect.anchorMin = _anchorMin;
        _rect.anchorMax = _anchorMax;
        _rect.anchoredPosition = _position;
        _rect.sizeDelta = _size;
        _rect.pivot = _pivot;
        _rect.localScale = Vector3.one;
    }

    private static void SetStretch(RectTransform _rect, float _padding)
    {
        _rect.anchorMin = Vector2.zero;
        _rect.anchorMax = Vector2.one;
        _rect.offsetMin = new Vector2(_padding, _padding);
        _rect.offsetMax = new Vector2(-_padding, -_padding);
        _rect.pivot = new Vector2(0.5f, 0.5f);
        _rect.localScale = Vector3.one;
    }

    private static void SetLayerRecursively(GameObject _root, int _layer)
    {
        _root.layer = _layer;

        foreach (Transform child in _root.transform)
            SetLayerRecursively(child.gameObject, _layer);
    }
}
