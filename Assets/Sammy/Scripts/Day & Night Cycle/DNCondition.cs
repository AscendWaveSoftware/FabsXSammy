using UnityEngine;
using UnityEngine.Events;

namespace DNE.Events
{
    /// <summary>
    /// Context, den Runner an Conditions/Actions durchreicht
    /// </summary>
    public struct DNContext
    {
        public int MinuteOfDay;
        public int DayIndex;
        public DayPhase CurrentDayPhase;
        public float Time01;
        public float FacingSun01;
        public Object Sender;
    }


    public abstract class DNCondition : ScriptableObject
    {
        public abstract bool Evaluate(in DNContext _ctx);
    }

    public abstract class DNAction : ScriptableObject
    {
        public abstract void Execute(in DNContext _ctx);
    }

    // Beispiel-Conditions
    [CreateAssetMenu(fileName = "Cond_Random", menuName = "Enviroment/DayNight/Conditions/Random Chance")]
    public class DNCond_RandomChance : DNCondition
    {
        [Range(0f, 1f)] public float Chance = 1f;
        public override bool Evaluate(in DNContext _ctx) => Random.value <= Chance;
    }

    // Beispiel-Actions
    [CreateAssetMenu(fileName = "Act_UnityEvent", menuName = "Enviroment/DayNight/Actions/UnityEvent")]
    public class DNAct_UnityEvent : DNAction
    {
        public UnityEvent OnExecute;
        public override void Execute(in DNContext _ctx) => OnExecute?.Invoke();
    }

    [CreateAssetMenu(fileName = "Act_ToggleGO", menuName = "Enviroment/DayNight/Actions/Toggle Gameobject")]
    public class DNAct_ToggleGameObject : DNAction
    {
        public string TargetId;
        public bool SetActive = true;
        public override void Execute(in DNContext _ctx)
        {
            var go = SceneBinding.Resolve(TargetId);
            if (go)
                go.SetActive(SetActive);

            Debug.Log("GameObject-Event Trigged");
        }
    }

    [CreateAssetMenu(fileName = "Act_PlayAudio", menuName = "Enviroment/DayNight/Actions/Play OneShot")]
    public class DNAct_PlayAudio : DNAction
    {
        public AudioSource Source;
        public AudioClip Clip;
        [Range(0f, 1f)] public float Volume = 1;
        public override void Execute(in DNContext _ctx)
        {
            if (Source && Clip)
                Source.PlayOneShot(Clip, Volume);

            Debug.Log("Audio-Event triggered!");
        }
    }
}