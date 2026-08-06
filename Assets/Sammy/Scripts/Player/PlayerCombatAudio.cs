using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// All combat sounds the player makes. They share one voice on purpose, so a
/// swing and a block can never talk over each other.
/// </summary>
[DisallowMultipleComponent]
public class PlayerCombatAudio : MonoBehaviour
{
    [Header("Swing Clips")]
    [SerializeField, Tooltip("One clip per combo step. Index 0 belongs to Attack1, index 1 to Attack2, index 2 to Attack3.")]
    private AudioClip[] m_swingClips = new AudioClip[PlayerAnimationController.ComboStepCount];

    [Header("Block")]
    [SerializeField] private AudioClip m_blockClip;
    [SerializeField, Range(0f, 1f)] private float m_blockVolume = 0.85f;
    [SerializeField, Min(0f), Tooltip("Several enemies can land on the guard in the same moment. Blocks closer together than this share a single sound.")]
    private float m_minimumBlockInterval = 0.09f;

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
    [SerializeField] private PlayerHealth m_playerHealth;

    private AudioSource m_combatSource;
    private AudioSource m_cutOffSource;
    private float m_cutOffDuration;
    private float m_cutOffRemaining;
    private float m_cutOffStartVolume;
    private float m_nextBlockSoundTime;
    private bool m_hasLoggedMissingClip;
    private bool m_hasLoggedMissingAnimationController;

    private void Awake()
    {
        if (m_playerAnimation == null)
            m_playerAnimation = GetComponent<PlayerAnimationController>();

        if (m_playerHealth == null)
            m_playerHealth = GetComponent<PlayerHealth>();

        m_combatSource = CreateCombatSource();
        m_cutOffSource = CreateCombatSource();
    }

    private void OnEnable()
    {
        if (m_playerHealth != null)
            m_playerHealth.OnDamageBlocked += HandleDamageBlocked;

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

        if (m_playerHealth != null)
            m_playerHealth.OnDamageBlocked -= HandleDamageBlocked;

        StopCombatAudio();
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
            StopCutOffClip();
            return;
        }

        if (m_cutOffSource != null)
            m_cutOffSource.volume = m_cutOffStartVolume * (m_cutOffRemaining / m_cutOffDuration);
    }

    private void HandleAttackSwingStarted(int _comboStep)
    {
        PlayCombatClip(GetSwingClip(_comboStep), m_volume);
    }

    private void HandleDamageBlocked(int _blockedDamage, Vector3 _attackerPosition)
    {
        if (_blockedDamage <= 0 || m_blockClip == null)
            return;

        // Every enemy landing on the guard raises its own event, and several can
        // land in the same frame. Without this gate that would stack the same
        // clip on itself and turn a block into a rattle.
        if (Time.unscaledTime < m_nextBlockSoundTime)
            return;

        m_nextBlockSoundTime = Time.unscaledTime + m_minimumBlockInterval;
        PlayCombatClip(m_blockClip, m_blockVolume);
    }

    private void PlayCombatClip(AudioClip _clip, float _volume)
    {
        if (_clip == null || m_combatSource == null)
            return;

        // A combat clip outlasts the action that triggered it, so whatever is
        // still running has to give way. Exactly one is ever audible.
        CutOffRunningClip();

        m_combatSource.clip = _clip;
        m_combatSource.volume = _volume;
        m_combatSource.pitch = m_pitchVariation > 0f
            ? 1f + Random.Range(-m_pitchVariation, m_pitchVariation)
            : 1f;
        m_combatSource.Play();
    }

    private void CutOffRunningClip()
    {
        if (!m_combatSource.isPlaying)
            return;

        AudioClip runningClip = m_combatSource.clip;

        if (m_cutOffFade <= 0f || m_cutOffSource == null || runningClip == null)
        {
            m_combatSource.Stop();
            return;
        }

        // Handing the running clip over to the second source keeps the cut free
        // of clicks without ever letting two clips play at full volume.
        StopCutOffClip();
        m_cutOffSource.clip = runningClip;
        m_cutOffSource.pitch = m_combatSource.pitch;
        m_cutOffSource.volume = m_combatSource.volume;
        m_cutOffSource.timeSamples = Mathf.Clamp(m_combatSource.timeSamples, 0, runningClip.samples - 1);
        m_cutOffSource.Play();

        m_cutOffStartVolume = m_cutOffSource.volume;
        m_cutOffDuration = m_cutOffFade;
        m_cutOffRemaining = m_cutOffFade;
        m_combatSource.Stop();
    }

    private void StopCombatAudio()
    {
        if (m_combatSource != null)
        {
            m_combatSource.Stop();
            m_combatSource.clip = null;
        }

        StopCutOffClip();
    }

    private void StopCutOffClip()
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

    private AudioSource CreateCombatSource()
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
        m_minimumBlockInterval = Mathf.Max(0f, m_minimumBlockInterval);

        if (m_swingClips == null || m_swingClips.Length != PlayerAnimationController.ComboStepCount)
            System.Array.Resize(ref m_swingClips, PlayerAnimationController.ComboStepCount);
    }
}
