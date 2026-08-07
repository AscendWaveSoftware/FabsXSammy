using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small HUD strip that tells the player which key fires which spell. Slots only
/// appear once the matching level up card has been taken, and dim while the
/// spell is on cooldown.
/// </summary>
[DisallowMultipleComponent]
public class PlayerSpellBarUI : MonoBehaviour
{
    private static readonly Color PanelColor = new(0.025f, 0.04f, 0.075f, 0.94f);
    private static readonly Color TrackColor = new(0.008f, 0.014f, 0.028f, 0.92f);
    private static readonly Color NameColor = new(0.94f, 0.97f, 1f, 1f);

    private sealed class SpellSlotUI
    {
        public RectTransform Root;
        public CanvasGroup Group;
        public Image Accent;
        public Image KeyBadge;
        public Image CooldownFill;
        public TextMeshProUGUI KeyText;
        public TextMeshProUGUI NameText;
    }

    [Header("References")]
    [SerializeField] private PlayerSpellCaster m_spellCaster;
    [SerializeField] private PlayerUIHandler m_uiHandler;

    [Header("Layout")]
    [SerializeField, Min(40f)] private float m_slotWidth = 132f;
    [SerializeField, Min(20f)] private float m_slotHeight = 46f;
    [SerializeField, Min(0f)] private float m_slotSpacing = 12f;
    [SerializeField, Min(0f)] private float m_bottomMargin = 26f;

    [Header("Appearance")]
    [SerializeField, Range(0.1f, 1f), Tooltip("Opacity of a slot while its spell is still on cooldown.")]
    private float m_cooldownAlpha = 0.45f;

    private SpellSlotUI[] m_slots;
    private RectTransform m_barRoot;

    private void Awake()
    {
        if (m_spellCaster == null)
            m_spellCaster = GetComponent<PlayerSpellCaster>();

        if (m_uiHandler == null)
            m_uiHandler = GetComponent<PlayerUIHandler>();
    }

    private void OnEnable()
    {
        if (m_spellCaster != null)
            m_spellCaster.OnSpellUnlocked += HandleSpellUnlocked;
    }

    private void OnDisable()
    {
        if (m_spellCaster != null)
            m_spellCaster.OnSpellUnlocked -= HandleSpellUnlocked;
    }

    private void Start()
    {
        BuildBar();
        RefreshLayout();
    }

    private void Update()
    {
        if (m_slots == null || m_spellCaster == null)
            return;

        for (int i = 0; i < m_slots.Length; i++)
        {
            SpellSlotUI slot = m_slots[i];

            if (slot == null || !slot.Root.gameObject.activeSelf)
                continue;

            SpellDefinition spell = m_spellCaster.GetSpell(i);

            if (spell == null)
                continue;

            float remaining = m_spellCaster.GetRemainingCooldown(i);
            float readiness = spell.Cooldown > 0f ? 1f - Mathf.Clamp01(remaining / spell.Cooldown) : 1f;

            slot.CooldownFill.fillAmount = readiness;
            slot.Group.alpha = readiness >= 1f ? 1f : m_cooldownAlpha;
        }
    }

    private void HandleSpellUnlocked(int _slotIndex)
    {
        if (m_slots == null)
            BuildBar();

        RefreshLayout();
    }

    private void BuildBar()
    {
        if (m_slots != null || m_uiHandler == null || m_spellCaster == null)
            return;

        RectTransform hudRoot = m_uiHandler.HudRoot;

        if (hudRoot == null)
        {
            Debug.LogWarning("Spell bar could not be built because the player HUD root is missing.", this);
            return;
        }

        GameObject barObject = new GameObject("Spell Bar", typeof(RectTransform));
        barObject.layer = hudRoot.gameObject.layer;
        barObject.transform.SetParent(hudRoot, false);

        m_barRoot = (RectTransform)barObject.transform;
        m_barRoot.anchorMin = new Vector2(0.5f, 0f);
        m_barRoot.anchorMax = new Vector2(0.5f, 0f);
        m_barRoot.pivot = new Vector2(0.5f, 0f);
        m_barRoot.anchoredPosition = new Vector2(0f, m_bottomMargin);
        m_barRoot.sizeDelta = new Vector2(0f, m_slotHeight);

        m_slots = new SpellSlotUI[PlayerSpellCaster.SpellSlotCount];

        for (int i = 0; i < m_slots.Length; i++)
            m_slots[i] = CreateSlot(i);
    }

    private SpellSlotUI CreateSlot(int _slotIndex)
    {
        SpellDefinition spell = m_spellCaster.GetSpell(_slotIndex);
        Color accentColor = spell != null ? spell.UiColor : NameColor;

        GameObject slotObject = new GameObject($"Spell Slot {_slotIndex + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        slotObject.layer = m_barRoot.gameObject.layer;
        slotObject.transform.SetParent(m_barRoot, false);

        SpellSlotUI slot = new SpellSlotUI
        {
            Root = (RectTransform)slotObject.transform,
            Group = slotObject.GetComponent<CanvasGroup>()
        };

        Image background = slotObject.GetComponent<Image>();
        background.color = PanelColor;
        background.raycastTarget = false;
        SetRect(slot.Root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(m_slotWidth, m_slotHeight), new Vector2(0f, 0.5f));
        AddPanelEffects(background, accentColor);

        slot.Accent = CreateImage("Accent", slot.Root, accentColor);
        SetRect(slot.Accent.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(4f, 0f), new Vector2(0f, 0.5f));

        slot.KeyBadge = CreateImage("Key Badge", slot.Root, accentColor);
        SetRect(slot.KeyBadge.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 3f), new Vector2(26f, 26f), new Vector2(0f, 0.5f));

        slot.KeyText = CreateText("Key Text", slot.Root, 17f, new Color(0.03f, 0.05f, 0.09f, 1f), TextAlignmentOptions.Center);
        SetRect(slot.KeyText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 3f), new Vector2(26f, 26f), new Vector2(0f, 0.5f));

        slot.NameText = CreateText("Name Text", slot.Root, 15f, NameColor, TextAlignmentOptions.Left);
        slot.NameText.characterSpacing = 3f;
        SetRect(slot.NameText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(48f, 3f), new Vector2(78f, 22f), new Vector2(0f, 0.5f));

        Image cooldownTrack = CreateImage("Cooldown Track", slot.Root, TrackColor);
        SetRect(cooldownTrack.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 7f), new Vector2(-24f, 4f), new Vector2(0.5f, 0.5f));

        slot.CooldownFill = CreateImage("Cooldown Fill", cooldownTrack.rectTransform, accentColor);
        slot.CooldownFill.type = Image.Type.Filled;
        slot.CooldownFill.fillMethod = Image.FillMethod.Horizontal;
        slot.CooldownFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        SetStretch(slot.CooldownFill.rectTransform);

        slotObject.SetActive(false);
        return slot;
    }

    private void RefreshLayout()
    {
        if (m_slots == null || m_spellCaster == null)
            return;

        int visibleCount = 0;

        for (int i = 0; i < m_slots.Length; i++)
        {
            bool isVisible = m_spellCaster.IsSlotUnlocked(i) && m_spellCaster.GetSpell(i) != null;
            m_slots[i].Root.gameObject.SetActive(isVisible);

            if (isVisible)
                visibleCount++;
        }

        if (m_barRoot != null)
            m_barRoot.gameObject.SetActive(visibleCount > 0);

        if (visibleCount == 0)
            return;

        float totalWidth = visibleCount * m_slotWidth + (visibleCount - 1) * m_slotSpacing;
        float cursorX = -totalWidth * 0.5f;

        for (int i = 0; i < m_slots.Length; i++)
        {
            SpellSlotUI slot = m_slots[i];

            if (!slot.Root.gameObject.activeSelf)
                continue;

            SpellDefinition spell = m_spellCaster.GetSpell(i);
            slot.KeyText.text = (i + 1).ToString();
            slot.NameText.text = string.IsNullOrWhiteSpace(spell.SpellName)
                ? spell.name.ToUpperInvariant()
                : spell.SpellName.ToUpperInvariant();

            slot.Root.anchoredPosition = new Vector2(cursorX, 0f);
            cursorX += m_slotWidth + m_slotSpacing;
        }
    }

    private Image CreateImage(string _name, Transform _parent, Color _color)
    {
        GameObject imageObject = new GameObject(_name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.layer = _parent.gameObject.layer;
        imageObject.transform.SetParent(_parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.color = _color;
        image.raycastTarget = false;
        return image;
    }

    private TextMeshProUGUI CreateText(
        string _name,
        Transform _parent,
        float _fontSize,
        Color _color,
        TextAlignmentOptions _alignment)
    {
        GameObject textObject = new GameObject(_name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = _parent.gameObject.layer;
        textObject.transform.SetParent(_parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = m_uiHandler.HudFont;
        text.fontSize = _fontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = _color;
        text.alignment = _alignment;
        text.enableAutoSizing = false;
        text.raycastTarget = false;
        return text;
    }

    private static void AddPanelEffects(Image _panel, Color _accentColor)
    {
        Shadow shadow = _panel.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.52f);
        shadow.effectDistance = new Vector2(0f, -5f);
        shadow.useGraphicAlpha = true;

        Outline outline = _panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(_accentColor.r, _accentColor.g, _accentColor.b, 0.55f);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
    }

    private static void SetRect(
        RectTransform _rect,
        Vector2 _anchorMin,
        Vector2 _anchorMax,
        Vector2 _position,
        Vector2 _size,
        Vector2 _pivot)
    {
        _rect.anchorMin = _anchorMin;
        _rect.anchorMax = _anchorMax;
        _rect.anchoredPosition = _position;
        _rect.sizeDelta = _size;
        _rect.pivot = _pivot;
        _rect.localScale = Vector3.one;
    }

    private static void SetStretch(RectTransform _rect)
    {
        _rect.anchorMin = Vector2.zero;
        _rect.anchorMax = Vector2.one;
        _rect.offsetMin = Vector2.zero;
        _rect.offsetMax = Vector2.zero;
        _rect.localScale = Vector3.one;
    }
}
