using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Thin readout for <see cref="PlayerGuard"/>, sitting just above the spell bar.
/// Without it a guard break would arrive out of nowhere; with it the player can
/// see the wall running down and choose to disengage in time.
/// </summary>
[DisallowMultipleComponent]
public class PlayerGuardBarUI : MonoBehaviour
{
    private static readonly Color TrackColor = new(0.008f, 0.014f, 0.028f, 0.92f);
    private static readonly Color ReadyColor = new(0.55f, 0.78f, 1f, 1f);
    private static readonly Color StrainedColor = new(1f, 0.74f, 0.28f, 1f);
    private static readonly Color BrokenColor = new(1f, 0.32f, 0.24f, 1f);

    [Header("References")]
    [SerializeField] private PlayerGuard m_guard;
    [SerializeField] private PlayerUIHandler m_uiHandler;

    [Header("Layout")]
    [SerializeField, Min(60f)] private float m_width = 264f;
    [SerializeField, Min(3f)] private float m_height = 9f;
    [SerializeField, Tooltip("Distance above the bottom edge. Clears the spell bar that lives below it.")]
    private float m_bottomMargin = 86f;

    private RectTransform m_root;
    private Image m_fill;
    private float m_displayedFill = 1f;

    private void Awake()
    {
        if (m_guard == null)
            m_guard = GetComponent<PlayerGuard>();

        if (m_uiHandler == null)
            m_uiHandler = GetComponent<PlayerUIHandler>();
    }

    private void OnEnable()
    {
        if (m_guard != null)
        {
            m_guard.OnGuardChanged += HandleGuardChanged;
            m_guard.OnGuardBroken += HandleGuardBroken;
        }
    }

    private void OnDisable()
    {
        if (m_guard != null)
        {
            m_guard.OnGuardChanged -= HandleGuardChanged;
            m_guard.OnGuardBroken -= HandleGuardBroken;
        }
    }

    private void Start()
    {
        BuildBar();

        if (m_guard != null)
            HandleGuardChanged(m_guard.CurrentGuard, m_guard.MaximumGuard);
    }

    private void Update()
    {
        if (m_fill == null || m_guard == null)
            return;

        // Eased rather than snapped, so a single blocked hit reads as the wall
        // giving way instead of as the bar teleporting.
        m_fill.fillAmount = Mathf.MoveTowards(m_fill.fillAmount, m_displayedFill, Time.unscaledDeltaTime * 2.6f);
        m_fill.color = GetFillColor();
    }

    private Color GetFillColor()
    {
        if (m_guard.IsBroken)
            return BrokenColor;

        return m_displayedFill <= 0.35f ? StrainedColor : ReadyColor;
    }

    private void HandleGuardChanged(float _current, float _maximum)
    {
        m_displayedFill = _maximum > 0f ? Mathf.Clamp01(_current / _maximum) : 0f;
    }

    private void HandleGuardBroken()
    {
        m_displayedFill = 0f;

        if (m_fill != null)
            m_fill.fillAmount = 0f;
    }

    private void BuildBar()
    {
        if (m_root != null || m_uiHandler == null)
            return;

        RectTransform hudRoot = m_uiHandler.HudRoot;

        if (hudRoot == null)
        {
            Debug.LogWarning("Guard bar could not be built because the player HUD root is missing.", this);
            return;
        }

        GameObject trackObject = new GameObject("Guard Bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        trackObject.layer = hudRoot.gameObject.layer;
        trackObject.transform.SetParent(hudRoot, false);

        m_root = (RectTransform)trackObject.transform;
        m_root.anchorMin = new Vector2(0.5f, 0f);
        m_root.anchorMax = new Vector2(0.5f, 0f);
        m_root.pivot = new Vector2(0.5f, 0f);
        m_root.anchoredPosition = new Vector2(0f, m_bottomMargin);
        m_root.sizeDelta = new Vector2(m_width, m_height);

        Image track = trackObject.GetComponent<Image>();
        track.color = TrackColor;
        track.raycastTarget = false;

        GameObject fillObject = new GameObject("Guard Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.layer = hudRoot.gameObject.layer;
        fillObject.transform.SetParent(m_root, false);

        RectTransform fillRect = (RectTransform)fillObject.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(1f, 1f);
        fillRect.offsetMax = new Vector2(-1f, -1f);

        m_fill = fillObject.GetComponent<Image>();
        m_fill.color = ReadyColor;
        m_fill.raycastTarget = false;
        m_fill.type = Image.Type.Filled;
        m_fill.fillMethod = Image.FillMethod.Horizontal;
        m_fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        m_fill.fillAmount = m_displayedFill;
    }
}
