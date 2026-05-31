using UnityEngine;

public sealed class TransitionRunner
{
    private float dur, t;
    System.Action<float> onStep;
    System.Action onDone;
    private bool running;

    public void Start(float _duration, System.Action<float> _step, System.Action _done = null)
    {
        dur = Mathf.Max(0.01f, _duration);
        t = 0f;
        onStep = _step;
        onDone = _done;
        running = true;
    }

    public void Tick(float _dt)
    {
        if (!running) return;
        t += _dt;
        float x = Mathf.Clamp01(t / dur);
        onStep?.Invoke(x);
        if (t >= dur)
        {
            running = false;
            onDone?.Invoke();
        }
    }
}