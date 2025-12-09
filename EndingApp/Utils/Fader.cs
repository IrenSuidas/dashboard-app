namespace EndingApp.Utils;

/// <summary>
/// Simple utility to perform fade-in / fade-out transitions with a float alpha [0..1].
/// </summary>
internal struct Fader
{
    private float _elapsed;
    private float _duration;

    /// <summary>Alpha value in range [0,1].</summary>
    public float Alpha { get; private set; }

    public bool Active { get; private set; }

    public FadeState State { get; private set; }

    public enum FadeState
    {
        Idle = 0,
        FadingIn,
        FadingOut,
    }

    public void Reset()
    {
        _elapsed = 0f;
        _duration = 0f;
        Alpha = 0f;
        Active = false;
        State = FadeState.Idle;
    }

    public void StartFadeIn(float duration)
    {
        _duration = Math.Max(0.0001f, duration);
        _elapsed = 0f;
        Active = true;
        State = FadeState.FadingIn;
        Alpha = 0f;
        Logger.Debug("Fader.StartFadeIn(duration={0})", _duration);
    }

    public void StartFadeOut(float duration)
    {
        _duration = Math.Max(0.0001f, duration);
        _elapsed = 0f;
        Active = true;
        State = FadeState.FadingOut;
        Alpha = 1f;
        Logger.Debug("Fader.StartFadeOut(duration={0})", _duration);
    }

    public void SetAlpha(float alpha)
    {
        Alpha = Math.Clamp(alpha, 0f, 1f);
        Active = false;
        State = FadeState.Idle;
    }

    public void Update(float dt)
    {
        if (!Active)
            return;
        _elapsed += dt;
        if (State == FadeState.FadingIn)
        {
            Alpha = Math.Clamp(_elapsed / _duration, 0f, 1f);
            if (_elapsed >= _duration)
            {
                Alpha = 1f;
                Active = false;
                State = FadeState.Idle;
            }
        }
        else if (State == FadeState.FadingOut)
        {
            Alpha = 1f - Math.Clamp(_elapsed / _duration, 0f, 1f);
            if (_elapsed >= _duration)
            {
                Alpha = 0f;
                Active = false;
                State = FadeState.Idle;
            }
        }
    }
}
