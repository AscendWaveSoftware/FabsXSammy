using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LevelUpCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    private static readonly Color CardColor = new Color(0.055f, 0.071f, 0.11f, 0.98f);
    private static readonly Color CardHoverColor = new Color(0.085f, 0.105f, 0.16f, 1f);
    private static readonly Color MainTextColor = new Color(0.96f, 0.97f, 1f, 1f);
    private static readonly Color SecondaryTextColor = new Color(0.72f, 0.76f, 0.84f, 1f);

    [Header("References")]
    [SerializeField] private TextMeshProUGUI m_upgradeNameText;
    [SerializeField] private TextMeshProUGUI m_descriptionText;
    [SerializeField] private Image m_iconImage;
    [SerializeField] private Button m_selectButton;

    private UpgradeDefinition m_upgrade;
    private LevelUpSelectionUI m_selectionUI;
    private RectTransform m_rectTransform;
    private Image m_cardImage;
    private Image m_accentBar;
    private Image m_iconBackdrop;
    private Image m_titleDivider;
    private Outline m_outline;
    private CanvasGroup m_canvasGroup;
    private TextMeshProUGUI m_typeText;
    private Button m_cardButton;
    private Color m_accentColor = new Color(0.45f, 0.72f, 1f, 1f);
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
            Color targetColor = m_targetScale > 1f ? CardHoverColor : CardColor;
            m_cardImage.color = Color.Lerp(m_cardImage.color, targetColor, 1f - Mathf.Exp(-12f * deltaTime));
        }
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

        if (m_iconImage != null)
        {
            m_iconImage.sprite = _upgrade.Icon;
            m_iconImage.enabled = _upgrade.Icon != null;
        }

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

    public void OnPointerEnter(PointerEventData _eventData)
    {
        SetHighlighted(true);
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        SetHighlighted(false);
    }

    public void OnSelect(BaseEventData _eventData)
    {
        SetHighlighted(true);
    }

    public void OnDeselect(BaseEventData _eventData)
    {
        SetHighlighted(false);
    }

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
            outlineColor.a = _highlighted ? 0.95f : 0.55f;
            m_outline.effectColor = outlineColor;
            m_outline.effectDistance = _highlighted ? new Vector2(3f, -3f) : new Vector2(2f, -2f);
        }
    }

    private void ConfigureVisuals()
    {
        if (m_cardImage != null)
            m_cardImage.color = CardColor;

        Shadow shadow = GetComponent<Shadow>();
        if (shadow == null)
            shadow = gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
        shadow.effectDistance = new Vector2(0f, -12f);
        shadow.useGraphicAlpha = true;

        m_outline = GetComponent<Outline>();
        if (m_outline == null)
            m_outline = gameObject.AddComponent<Outline>();
        m_outline.effectDistance = new Vector2(2f, -2f);
        m_outline.useGraphicAlpha = true;

        m_accentBar = CreateImage("Accent Bar", transform);
        SetRect(m_accentBar.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 9f), new Vector2(0.5f, 1f));
        m_accentBar.transform.SetAsFirstSibling();

        m_iconBackdrop = CreateImage("Icon Backdrop", transform);
        SetRect(m_iconBackdrop.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 95f), new Vector2(184f, 184f), new Vector2(0.5f, 0.5f));
        if (m_cardImage != null)
        {
            m_iconBackdrop.sprite = m_cardImage.sprite;
            m_iconBackdrop.type = m_cardImage.type;
        }

        m_titleDivider = CreateImage("Title Divider", transform);
        SetRect(m_titleDivider.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(94f, 3f), new Vector2(0.5f, 0.5f));

        m_typeText = CreateText("Upgrade Type", transform);
        SetRect(m_typeText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -101f), new Vector2(280f, 28f), new Vector2(0.5f, 0.5f));
        m_typeText.fontSize = 15f;
        m_typeText.fontStyle = FontStyles.Bold;
        m_typeText.characterSpacing = 4f;
        m_typeText.alignment = TextAlignmentOptions.Center;

        if (m_upgradeNameText != null)
        {
            SetRect(m_upgradeNameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(300f, 62f), new Vector2(0.5f, 0.5f));
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
            SetRect(m_descriptionText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -74f), new Vector2(286f, 130f), new Vector2(0.5f, 0.5f));
            m_descriptionText.fontSize = 22f;
            m_descriptionText.enableAutoSizing = true;
            m_descriptionText.fontSizeMin = 17f;
            m_descriptionText.fontSizeMax = 22f;
            m_descriptionText.color = SecondaryTextColor;
            m_descriptionText.alignment = TextAlignmentOptions.Center;
            m_descriptionText.raycastTarget = false;
        }

        if (m_iconImage != null)
        {
            SetRect(m_iconImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 95f), new Vector2(140f, 140f), new Vector2(0.5f, 0.5f));
            m_iconImage.preserveAspect = true;
            m_iconImage.raycastTarget = false;
            m_iconBackdrop.transform.SetSiblingIndex(m_iconImage.transform.GetSiblingIndex());
        }

        if (m_selectButton != null)
            ConfigureButton();
    }

    private void ConfigureButton()
    {
        RectTransform buttonRect = m_selectButton.transform as RectTransform;
        SetRect(buttonRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(245f, 54f), new Vector2(0.5f, 0.5f));

        TextMeshProUGUI buttonText = m_selectButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (buttonText != null)
        {
            buttonText.text = "SELECT";
            buttonText.fontSize = 20f;
            buttonText.fontStyle = FontStyles.Bold;
            buttonText.characterSpacing = 3f;
            buttonText.color = new Color(0.035f, 0.045f, 0.07f, 1f);
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.raycastTarget = false;
        }
    }

    private void ApplyTheme(UpgradeType _upgradeType)
    {
        switch (_upgradeType)
        {
            case UpgradeType.MOVESPEED:
                m_accentColor = new Color(0.2f, 0.86f, 0.9f, 1f);
                m_typeText.text = "MOBILITY";
                break;
            case UpgradeType.MAXHEALTH:
                m_accentColor = new Color(0.35f, 0.9f, 0.5f, 1f);
                m_typeText.text = "SURVIVABILITY";
                break;
            case UpgradeType.DAMAGE:
                m_accentColor = new Color(1f, 0.38f, 0.24f, 1f);
                m_typeText.text = "OFFENSE";
                break;
            case UpgradeType.ATTACKRANGE:
                m_accentColor = new Color(1f, 0.58f, 0.25f, 1f);
                m_typeText.text = "REACH";
                break;
            case UpgradeType.CRITICALCHANCE:
                m_accentColor = new Color(1f, 0.82f, 0.24f, 1f);
                m_typeText.text = "CRITICAL";
                break;
            case UpgradeType.HEALTHONHIT:
                m_accentColor = new Color(0.95f, 0.3f, 0.62f, 1f);
                m_typeText.text = "SUSTAIN";
                break;
            case UpgradeType.DAMAGEREDUCTION:
                m_accentColor = new Color(0.35f, 0.58f, 1f, 1f);
                m_typeText.text = "DEFENSE";
                break;
            case UpgradeType.HEALTHREGEN:
                m_accentColor = new Color(0.27f, 0.82f, 0.45f, 1f);
                m_typeText.text = "RECOVERY";
                break;
            case UpgradeType.EXPERIENCEGAIN:
                m_accentColor = new Color(0.68f, 0.45f, 1f, 1f);
                m_typeText.text = "GROWTH";
                break;
            default:
                m_accentColor = new Color(0.62f, 0.48f, 1f, 1f);
                m_typeText.text = "UPGRADE";
                break;
        }

        m_accentBar.color = m_accentColor;
        m_titleDivider.color = m_accentColor;
        m_typeText.color = m_accentColor;
        m_iconImage.color = m_accentColor;

        Color backdropColor = m_accentColor;
        backdropColor.a = 0.12f;
        m_iconBackdrop.color = backdropColor;

        Color outlineColor = m_accentColor;
        outlineColor.a = 0.55f;
        m_outline.effectColor = outlineColor;

        Image buttonImage = m_selectButton != null ? m_selectButton.targetGraphic as Image : null;
        if (buttonImage != null)
        {
            buttonImage.color = m_accentColor;
            ColorBlock colors = m_selectButton.colors;
            colors.normalColor = m_accentColor;
            colors.highlightedColor = Color.Lerp(m_accentColor, Color.white, 0.22f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Color.Lerp(m_accentColor, Color.black, 0.18f);
            colors.disabledColor = new Color(m_accentColor.r, m_accentColor.g, m_accentColor.b, 0.35f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            m_selectButton.colors = colors;
        }
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

    private Image CreateImage(string _name, Transform _parent)
    {
        Transform existing = _parent.Find(_name);
        if (existing != null)
            return existing.GetComponent<Image>();

        GameObject imageObject = new GameObject(_name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.layer = gameObject.layer;
        imageObject.transform.SetParent(_parent, false);
        Image image = imageObject.GetComponent<Image>();
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
