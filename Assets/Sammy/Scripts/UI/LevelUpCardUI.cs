using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LevelUpCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    // The frame sprite carries its own colours; the tint only brightens it on hover.
    private static readonly Color CardTint = new Color(0.86f, 0.86f, 0.86f, 1f);
    private static readonly Color CardHoverTint = Color.white;
    private static readonly Color MainTextColor = SteampunkUI.Parchment;
    private static readonly Color SecondaryTextColor = new Color(0.86f, 0.79f, 0.66f, 1f);

    [Header("References")]
    [SerializeField] private TextMeshProUGUI m_upgradeNameText;
    [SerializeField] private TextMeshProUGUI m_descriptionText;
    [SerializeField] private Image m_iconImage;
    [SerializeField] private Button m_selectButton;

    private UpgradeDefinition m_upgrade;
    private LevelUpSelectionUI m_selectionUI;
    private RectTransform m_rectTransform;
    private Image m_cardImage;
    private Image m_typePlate;
    private Image m_emblem;
    private Image m_titleDivider;
    private Outline m_outline;
    private CanvasGroup m_canvasGroup;
    private TextMeshProUGUI m_typeText;
    private Button m_cardButton;
    private Color m_accentColor = SteampunkUI.Brass;
    private float m_targetScale = 1f;
    private float m_entranceTime;
    private float m_entranceDelay;
    private bool m_isEntering;
    private bool m_isSelecting;

    private void Awake()
    {
        m_rectTransform = transform as RectTransform;
        m_cardImage = GetComponent<Image>();
        m_canvasGroup = GetComponent<CanvasGroup>();
        if (m_canvasGroup == null)
            m_canvasGroup = gameObject.AddComponent<CanvasGroup>();

        ConfigureVisuals();

        if (m_selectButton == null)
            m_selectButton = GetComponentInChildren<Button>(true);

        if (m_selectButton != null)
            m_selectButton.onClick.AddListener(SelectUpgrade);
        else
            Debug.LogWarning($"{gameObject.name} has no Button assigned.");

        m_cardButton = GetComponent<Button>();
        if (m_cardButton != null && m_cardButton != m_selectButton)
        {
            m_cardButton.interactable = true;
            m_cardButton.transition = Selectable.Transition.None;
            m_cardButton.onClick.AddListener(SelectUpgrade);
        }
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;

        if (m_isEntering)
        {
            m_entranceTime += deltaTime;
            if (m_entranceTime < m_entranceDelay)
                return;

            float progress = Mathf.Clamp01((m_entranceTime - m_entranceDelay) / 0.28f);
            float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);
            m_canvasGroup.alpha = progress;
            m_rectTransform.localScale = Vector3.one * Mathf.Lerp(0.88f, m_targetScale, easedProgress);

            if (progress >= 1f)
                m_isEntering = false;
        }
        else
        {
            float currentScale = m_rectTransform.localScale.x;
            float newScale = Mathf.Lerp(currentScale, m_targetScale, 1f - Mathf.Exp(-14f * deltaTime));
            m_rectTransform.localScale = Vector3.one * newScale;
        }

        if (m_cardImage != null)
        {
            Color targetColor = m_targetScale > 1f ? CardHoverTint : CardTint;
            m_cardImage.color = Color.Lerp(m_cardImage.color, targetColor, 1f - Mathf.Exp(-12f * deltaTime));
        }

        // The emblem turns slowly, like a cog in the machinery behind the card.
        if (m_emblem != null)
            m_emblem.rectTransform.Rotate(0f, 0f, -12f * deltaTime);
    }

    public void Setup(UpgradeDefinition _upgrade, LevelUpSelectionUI _selectionUI)
    {
        m_upgrade = _upgrade;
        m_selectionUI = _selectionUI;
        m_isSelecting = false;

        if (_upgrade == null)
            return;

        if (m_upgradeNameText != null)
            m_upgradeNameText.text = _upgrade.UpgradeName.ToUpperInvariant();

        if (m_descriptionText != null)
            m_descriptionText.text = _upgrade.Description;

        ApplyTheme(_upgrade.UpgradeType);
    }

    public void PlayEntrance(float _delay)
    {
        m_entranceDelay = Mathf.Max(0f, _delay);
        m_entranceTime = 0f;
        m_isEntering = true;
        m_targetScale = 1f;
        m_canvasGroup.alpha = 0f;
        m_rectTransform.localScale = Vector3.one * 0.88f;
    }

    public void SetInteractable(bool _interactable)
    {
        if (m_selectButton != null)
            m_selectButton.interactable = _interactable;

        if (m_cardButton != null && m_cardButton != m_selectButton)
            m_cardButton.interactable = _interactable;

        if (!_interactable)
            SetHighlighted(false);
    }

    public void OnPointerEnter(PointerEventData _eventData) => SetHighlighted(true);

    public void OnPointerExit(PointerEventData _eventData) => SetHighlighted(false);

    public void OnSelect(BaseEventData _eventData) => SetHighlighted(true);

    public void OnDeselect(BaseEventData _eventData) => SetHighlighted(false);

    private void SelectUpgrade()
    {
        if (m_isSelecting || m_selectionUI == null || m_upgrade == null || !m_selectionUI.SelectionInputEnabled)
            return;

        m_isSelecting = true;
        m_selectionUI.SelectUpgrade(m_upgrade);
    }

    private void SetHighlighted(bool _highlighted)
    {
        m_targetScale = _highlighted ? 1.045f : 1f;
        if (m_outline != null)
        {
            Color outlineColor = m_accentColor;
            outlineColor.a = _highlighted ? 0.9f : 0.3f;
            m_outline.effectColor = outlineColor;
            m_outline.effectDistance = _highlighted ? new Vector2(3f, -3f) : new Vector2(2f, -2f);
        }
    }

    private void ConfigureVisuals()
    {
        if (m_cardImage != null)
        {
            m_cardImage.sprite = SteampunkUI.Frame;
            m_cardImage.type = Image.Type.Sliced;
            m_cardImage.pixelsPerUnitMultiplier = 0.85f;
            m_cardImage.color = CardTint;
        }

        Shadow shadow = GetComponent<Shadow>();
        if (shadow == null)
            shadow = gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(0f, -12f);
        shadow.useGraphicAlpha = true;

        m_outline = GetComponent<Outline>();
        if (m_outline == null)
            m_outline = gameObject.AddComponent<Outline>();
        m_outline.effectDistance = new Vector2(2f, -2f);
        m_outline.useGraphicAlpha = true;

        // Upgrade icons are not part of the gold master; a slowly turning cog in
        // the card's colour takes over the space they used to fill.
        if (m_iconImage != null)
            m_iconImage.gameObject.SetActive(false);

        m_emblem = CreateImage("Emblem", transform, SteampunkUI.Gear);
        SetRect(m_emblem.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(236f, 236f), new Vector2(0.5f, 0.5f));
        m_emblem.transform.SetSiblingIndex(0);

        m_typePlate = CreateImage("Type Plate", transform, SteampunkUI.Plate);
        SetRect(m_typePlate.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(214f, 38f), new Vector2(0.5f, 0.5f));

        m_typeText = CreateText("Upgrade Type", transform);
        SetRect(m_typeText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(214f, 38f), new Vector2(0.5f, 0.5f));
        m_typeText.fontSize = 15f;
        m_typeText.fontStyle = FontStyles.Bold;
        m_typeText.characterSpacing = 4f;
        m_typeText.alignment = TextAlignmentOptions.Center;
        m_typeText.color = SteampunkUI.TextOnBrass;

        m_titleDivider = CreateImage("Title Divider", transform, null);
        SetRect(m_titleDivider.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -156f), new Vector2(170f, 3f), new Vector2(0.5f, 0.5f));

        foreach (float side in new[] { -1f, 1f })
        {
            Image gear = CreateImage(side < 0f ? "Divider Gear L" : "Divider Gear R", transform, SteampunkUI.Gear);
            SetRect(gear.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(side * 97f, -156f), new Vector2(20f, 20f), new Vector2(0.5f, 0.5f));
            gear.color = SteampunkUI.Brass;
        }

        if (m_upgradeNameText != null)
        {
            SetRect(m_upgradeNameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(290f, 66f), new Vector2(0.5f, 0.5f));
            m_upgradeNameText.fontSize = 32f;
            m_upgradeNameText.enableAutoSizing = true;
            m_upgradeNameText.fontSizeMin = 22f;
            m_upgradeNameText.fontSizeMax = 32f;
            m_upgradeNameText.fontStyle = FontStyles.Bold;
            m_upgradeNameText.color = MainTextColor;
            m_upgradeNameText.alignment = TextAlignmentOptions.Center;
            m_upgradeNameText.raycastTarget = false;
        }

        if (m_descriptionText != null)
        {
            SetRect(m_descriptionText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(276f, 160f), new Vector2(0.5f, 0.5f));
            m_descriptionText.fontSize = 23f;
            m_descriptionText.enableAutoSizing = true;
            m_descriptionText.fontSizeMin = 17f;
            m_descriptionText.fontSizeMax = 23f;
            m_descriptionText.color = SecondaryTextColor;
            m_descriptionText.alignment = TextAlignmentOptions.Center;
            m_descriptionText.raycastTarget = false;
        }

        if (m_selectButton != null)
            ConfigureButton();
    }

    private void ConfigureButton()
    {
        RectTransform buttonRect = m_selectButton.transform as RectTransform;
        SetRect(buttonRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(236f, 56f), new Vector2(0.5f, 0.5f));

        if (m_selectButton.targetGraphic is Image buttonImage)
        {
            buttonImage.sprite = SteampunkUI.Plate;
            buttonImage.type = Image.Type.Sliced;
            buttonImage.color = Color.white;
        }

        // The plate carries the brass; the colour block only brightens or dims it.
        ColorBlock colors = m_selectButton.colors;
        colors.normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        colors.highlightedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        m_selectButton.colors = colors;

        TextMeshProUGUI buttonText = m_selectButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (buttonText != null)
        {
            buttonText.text = "SELECT";
            buttonText.fontSize = 21f;
            buttonText.fontStyle = FontStyles.Bold;
            buttonText.characterSpacing = 4f;
            buttonText.color = SteampunkUI.TextOnBrass;
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.raycastTarget = false;
        }
    }

    private void ApplyTheme(UpgradeType _upgradeType)
    {
        // Muted, metal and mineral tones, so every category stays distinct
        // without breaking out of the brass and iron palette.
        (Color color, string label) theme = _upgradeType switch
        {
            UpgradeType.MOVESPEED => (new Color(0.4f, 0.72f, 0.66f, 1f), "MOBILITY"),
            UpgradeType.MAXHEALTH => (new Color(0.52f, 0.72f, 0.36f, 1f), "SURVIVABILITY"),
            UpgradeType.DAMAGE => (new Color(0.86f, 0.36f, 0.22f, 1f), "OFFENSE"),
            UpgradeType.ATTACKRANGE => (new Color(0.88f, 0.56f, 0.28f, 1f), "REACH"),
            UpgradeType.CRITICALCHANCE => (new Color(0.95f, 0.76f, 0.3f, 1f), "CRITICAL"),
            UpgradeType.HEALTHONHIT => (new Color(0.8f, 0.3f, 0.38f, 1f), "SUSTAIN"),
            UpgradeType.DAMAGEREDUCTION => (new Color(0.5f, 0.64f, 0.8f, 1f), "DEFENSE"),
            UpgradeType.HEALTHREGEN => (new Color(0.58f, 0.76f, 0.5f, 1f), "RECOVERY"),
            UpgradeType.EXPERIENCEGAIN => (new Color(0.68f, 0.52f, 0.86f, 1f), "GROWTH"),
            UpgradeType.UNLOCKSPELL => (new Color(0.72f, 0.5f, 1f, 1f), "NEW SPELL"),
            UpgradeType.SPELLPOWER => (new Color(0.62f, 0.46f, 0.94f, 1f), "SPELL POWER"),
            _ => (SteampunkUI.Brass, "UPGRADE")
        };

        m_accentColor = theme.color;
        m_typeText.text = theme.label;

        m_typePlate.color = Color.Lerp(Color.white, m_accentColor, 0.3f);
        m_titleDivider.color = m_accentColor;

        Color emblemColor = m_accentColor;
        emblemColor.a = 0.1f;
        m_emblem.color = emblemColor;

        Color outlineColor = m_accentColor;
        outlineColor.a = 0.3f;
        m_outline.effectColor = outlineColor;
    }

    private TextMeshProUGUI CreateText(string _name, Transform _parent)
    {
        Transform existing = _parent.Find(_name);
        if (existing != null)
            return existing.GetComponent<TextMeshProUGUI>();

        GameObject textObject = new GameObject(_name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = gameObject.layer;
        textObject.transform.SetParent(_parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = m_upgradeNameText != null ? m_upgradeNameText.font : TMP_Settings.defaultFontAsset;
        text.raycastTarget = false;
        return text;
    }

    private Image CreateImage(string _name, Transform _parent, Sprite _sprite)
    {
        Transform existing = _parent.Find(_name);
        if (existing != null)
            return existing.GetComponent<Image>();

        GameObject imageObject = new GameObject(_name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.layer = gameObject.layer;
        imageObject.transform.SetParent(_parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = _sprite;
        image.type = _sprite != null && _sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.raycastTarget = false;
        return image;
    }

    private static void SetRect(RectTransform _rect, Vector2 _anchorMin, Vector2 _anchorMax, Vector2 _position, Vector2 _size, Vector2 _pivot)
    {
        if (_rect == null)
            return;

        _rect.anchorMin = _anchorMin;
        _rect.anchorMax = _anchorMax;
        _rect.anchoredPosition = _position;
        _rect.sizeDelta = _size;
        _rect.pivot = _pivot;
    }
}
