using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
public class PlayerCombatAudio : MonoBehaviour
{
    [Header("Swing Clips")]
    [SerializeField, Tooltip("One clip per combo step. Index 0 belongs to Attack1, index 1 to Attack2, index 2 to Attack3.")]
    private AudioClip[] m_swingClips = new AudioClip[PlayerAnimationController.ComboStepCount];

    [Header("Playback")]
    [SerializeField] private AudioMixerGroup m_outputMixerGroup;
    [SerializeField, Range(0f, 1f)] private float m_volume = 0.8f;
    [SerializeField, Range(0f, 0.25f), Tooltip("Random pitch offset so repeated swings do not sound identical.")]
    private float m_pitchVariation = 0.05f;
    [SerializeField, Range(0f, 1f), Tooltip("0 keeps the swing fully 2D, which suits the player's own weapon.")]
    private float m_spatialBlend;
    [SerializeField, Min(0f), Tooltip("Fade applied to a running swing when the next combo swing cuts it off.")]
    private float m_cutOffFade = 0.06f;

    [Header("References")]
    [SerializeField] private PlayerAnimationController m_playerAnimation;

    private AudioSource m_swingSource;
    private AudioSource m_cutOffSource;
    private float m_cutOffDuration;
    private float m_cutOffRemaining;
    private float m_cutOffStartVolume;
    private bool m_hasLoggedMissingClip;
    private bool m_hasLoggedMissingAnimationController;

    private void Awake()
    {
        if (m_playerAnimation == null)
            m_playerAnimation = GetComponent<PlayerAnimationController>();

        m_swingSource = CreateSwingSource();
        m_cutOffSource = CreateSwingSource();
    }

    private void OnEnable()
    {
        if (m_playerAnimation != null)
        {
            m_playerAnimation.OnAttackSwingStarted += HandleAttackSwingStarted;
            return;
        }

        if (!m_hasLoggedMissingAnimationController)
        {
            Debug.LogError("PlayerCombatAudio requires a PlayerAnimationController. Attack audio stays silent.", this);
            m_hasLoggedMissingAnimationController = true;
        }
    }

    private void OnDisable()
    {
        if (m_playerAnimation != null)
            m_playerAnimation.OnAttackSwingStarted -= HandleAttackSwingStarted;

        StopSwingAudio();
    }

    private void Update()
    {
        if (m_cutOffRemaining <= 0f)
            return;

        // Audio ignores Time.timeScale, so the combat hit slow motion must not
        // stretch this fade out of sync with what the player hears.
        m_cutOffRemaining -= Time.unscaledDeltaTime;

        if (m_cutOffRemaining <= 0f)
        {
            StopCutOffSwing();
            return;
        }

        if (m_cutOffSource != null)
            m_cutOffSource.volume = m_cutOffStartVolume * (m_cutOffRemaining / m_cutOffDuration);
    }

    private void HandleAttackSwingStarted(int _comboStep)
    {
        AudioClip swingClip = GetSwingClip(_comboStep);

        if (swingClip == null || m_swingSource == null)
            return;

        // A swing clip outlasts its animation, so the previous swing has to give
        // way before the next one starts. Exactly one swing is ever audible.
        CutOffRunningSwing();

        m_swingSource.clip = swingClip;
        m_swingSource.volume = m_volume;
        m_swingSource.pitch = m_pitchVariation > 0f
            ? 1f + Random.Range(-m_pitchVariation, m_pitchVariation)
            : 1f;
        m_swingSource.Play();
    }

    private void CutOffRunningSwing()
    {
        if (!m_swingSource.isPlaying)
            return;

        AudioClip runningClip = m_swingSource.clip;

        if (m_cutOffFade <= 0f || m_cutOffSource == null || runningClip == null)
        {
            m_swingSource.Stop();
            return;
        }

        // Handing the running swing over to the second source keeps the cut free
        // of clicks without ever letting two swings play at full volume.
        StopCutOffSwing();
        m_cutOffSource.clip = runningClip;
        m_cutOffSource.pitch = m_swingSource.pitch;
        m_cutOffSource.volume = m_swingSource.volume;
        m_cutOffSource.timeSamples = Mathf.Clamp(m_swingSource.timeSamples, 0, runningClip.samples - 1);
        m_cutOffSource.Play();

        m_cutOffStartVolume = m_cutOffSource.volume;
        m_cutOffDuration = m_cutOffFade;
        m_cutOffRemaining = m_cutOffFade;
        m_swingSource.Stop();
    }

    private void StopSwingAudio()
    {
        if (m_swingSource != null)
        {
            m_swingSource.Stop();
            m_swingSource.clip = null;
        }

        StopCutOffSwing();
    }

    private void StopCutOffSwing()
    {
        m_cutOffRemaining = 0f;
        m_cutOffDuration = 0f;

        if (m_cutOffSource == null)
            return;

        m_cutOffSource.Stop();
        m_cutOffSource.clip = null;
    }

    private AudioClip GetSwingClip(int _comboStep)
    {
        int clipIndex = _comboStep - 1;

        if (m_swingClips != null && clipIndex >= 0 && clipIndex < m_swingClips.Length &&
            m_swingClips[clipIndex] != null)
        {
            return m_swingClips[clipIndex];
        }

        if (!m_hasLoggedMissingClip)
        {
            Debug.LogWarning($"No swing audio clip assigned for attack {_comboStep}.", this);
            m_hasLoggedMissingClip = true;
        }

        return null;
    }

    private AudioSource CreateSwingSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.priority = 128;
        source.volume = m_volume;
        source.spatialBlend = m_spatialBlend;
        source.dopplerLevel = 0f;
        source.outputAudioMixerGroup = m_outputMixerGroup;
        return source;
    }

    private void OnValidate()
    {
        m_cutOffFade = Mathf.Max(0f, m_cutOffFade);

        if (m_swingClips == null || m_swingClips.Length != PlayerAnimationController.ComboStepCount)
            System.Array.Resize(ref m_swingClips, PlayerAnimationController.ComboStepCount);
    }
}
