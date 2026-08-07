using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// All combat sounds the player makes, split across two independent voices.
/// Melee holds the short, constant sounds; spells hold the long, rare ones. Each
/// voice only ever plays one clip, but they never cut each other, so a sword
/// swing cannot silence a spell that is still ringing out.
/// </summary>
[DisallowMultipleComponent]
public class PlayerCombatAudio : MonoBehaviour
{
    /// <summary>
    /// One audible slot. A second source takes over whatever was still running
    /// and fades it, which keeps a cut free of clicks without ever letting two
    /// clips play at full volume.
    /// </summary>
    private sealed class AudioVoice
    {
        private readonly AudioSource m_source;
        private readonly AudioSource m_cutOffSource;
        private float m_cutOffDuration;
        private float m_cutOffRemaining;
        private float m_cutOffStartVolume;

        public AudioVoice(AudioSource _source, AudioSource _cutOffSource)
        {
            m_source = _source;
            m_cutOffSource = _cutOffSource;
        }

        public void Play(AudioClip _clip, float _volume, float _pitch, float _cutOffFade)
        {
            if (_clip == null || m_source == null)
                return;

            CutOffRunningClip(_cutOffFade);

            m_source.clip = _clip;
            m_source.volume = _volume;
            m_source.pitch = _pitch;
            m_source.Play();
        }

        public void Update(float _unscaledDeltaTime)
        {
            if (m_cutOffRemaining <= 0f)
                return;

            m_cutOffRemaining -= _unscaledDeltaTime;

            if (m_cutOffRemaining <= 0f)
            {
                StopCutOffClip();
                return;
            }

            if (m_cutOffSource != null)
                m_cutOffSource.volume = m_cutOffStartVolume * (m_cutOffRemaining / m_cutOffDuration);
        }

        public void StopAll()
        {
            if (m_source != null)
            {
                m_source.Stop();
                m_source.clip = null;
            }

            StopCutOffClip();
        }

        private void CutOffRunningClip(float _cutOffFade)
        {
            if (!m_source.isPlaying)
                return;

            AudioClip runningClip = m_source.clip;

            if (_cutOffFade <= 0f || m_cutOffSource == null || runningClip == null)
            {
                m_source.Stop();
                return;
            }

            StopCutOffClip();
            m_cutOffSource.clip = runningClip;
            m_cutOffSource.pitch = m_source.pitch;
            m_cutOffSource.volume = m_source.volume;
            m_cutOffSource.timeSamples = Mathf.Clamp(m_source.timeSamples, 0, runningClip.samples - 1);
            m_cutOffSource.Play();

            m_cutOffStartVolume = m_cutOffSource.volume;
            m_cutOffDuration = _cutOffFade;
            m_cutOffRemaining = _cutOffFade;
            m_source.Stop();
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
    }

    [Header("Swing Clips")]
    [SerializeField, Tooltip("One clip per combo step. Index 0 belongs to Attack1, index 1 to Attack2, index 2 to Attack3.")]
    private AudioClip[] m_swingClips = new AudioClip[PlayerAnimationController.ComboStepCount];

    [Header("Block")]
    [SerializeField] private AudioClip m_blockClip;
    [SerializeField, Range(0f, 1f)] private float m_blockVolume = 0.85f;
    [SerializeField, Min(0f), Tooltip("Several enemies can land on the guard in the same moment. Blocks closer together than this share a single sound.")]
    private float m_minimumBlockInterval = 0.09f;

    [Header("Spells")]
    [SerializeField, Range(0f, 1f)] private float m_spellVolume = 0.9f;

    [Header("Playback")]
    [SerializeField] private AudioMixerGroup m_outputMixerGroup;
    [SerializeField, Range(0f, 1f)] private float m_volume = 0.8f;
    [SerializeField, Range(0f, 0.25f), Tooltip("Random pitch offset so repeated swings do not sound identical. Spells are left unpitched.")]
    private float m_pitchVariation = 0.05f;
    [SerializeField, Range(0f, 1f), Tooltip("0 keeps the sound fully 2D, which suits the player's own actions.")]
    private float m_spatialBlend;
    [SerializeField, Min(0f), Tooltip("Fade applied to a running clip when the next one cuts it off.")]
    private float m_cutOffFade = 0.06f;

    [Header("References")]
    [SerializeField] private PlayerAnimationController m_playerAnimation;
    [SerializeField] private PlayerHealth m_playerHealth;
    [SerializeField] private PlayerSpellCaster m_playerSpellCaster;

    private AudioVoice m_meleeVoice;
    private AudioVoice m_spellVoice;
    private float m_nextBlockSoundTime;
    private bool m_hasLoggedMissingClip;
    private bool m_hasLoggedMissingAnimationController;

    private void Awake()
    {
        if (m_playerAnimation == null)
            m_playerAnimation = GetComponent<PlayerAnimationController>();

        if (m_playerHealth == null)
            m_playerHealth = GetComponent<PlayerHealth>();

        if (m_playerSpellCaster == null)
            m_playerSpellCaster = GetComponent<PlayerSpellCaster>();

        m_meleeVoice = new AudioVoice(CreateCombatSource(), CreateCombatSource());
        m_spellVoice = new AudioVoice(CreateCombatSource(), CreateCombatSource());
    }

    private void OnEnable()
    {
        if (m_playerHealth != null)
            m_playerHealth.OnDamageBlocked += HandleDamageBlocked;

        if (m_playerSpellCaster != null)
            m_playerSpellCaster.OnSpellCast += HandleSpellCast;

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

        if (m_playerSpellCaster != null)
            m_playerSpellCaster.OnSpellCast -= HandleSpellCast;

        m_meleeVoice?.StopAll();
        m_spellVoice?.StopAll();
    }

    private void Update()
    {
        // Audio ignores Time.timeScale, so the combat hit slow motion must not
        // stretch these fades out of sync with what the player hears.
        float unscaledDeltaTime = Time.unscaledDeltaTime;

        m_meleeVoice?.Update(unscaledDeltaTime);
        m_spellVoice?.Update(unscaledDeltaTime);
    }

    private void HandleAttackSwingStarted(int _comboStep)
    {
        m_meleeVoice.Play(GetSwingClip(_comboStep), m_volume, GetVariedPitch(), m_cutOffFade);
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
        m_meleeVoice.Play(m_blockClip, m_blockVolume, GetVariedPitch(), m_cutOffFade);
    }

    private void HandleSpellCast(SpellDefinition _spell)
    {
        if (_spell == null || _spell.CastClip == null)
            return;

        // Played at a fixed pitch. A wavering pitch on a long magical sound reads
        // as a broken tape, not as variation.
        m_spellVoice.Play(_spell.CastClip, m_spellVolume, 1f, m_cutOffFade);
    }

    private float GetVariedPitch() => m_pitchVariation > 0f
        ? 1f + Random.Range(-m_pitchVariation, m_pitchVariation)
        : 1f;

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
