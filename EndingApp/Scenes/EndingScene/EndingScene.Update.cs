using EndingApp.Services;
using Raylib_cs;

namespace EndingApp;

internal sealed partial class EndingScene
{
    public void Update()
    {
        if (!IsActive)
            return;

        // Check for Ctrl+Space to exit ending scene
        if (
            (
                Raylib.IsKeyDown(KeyboardKey.LeftControl)
                || Raylib.IsKeyDown(KeyboardKey.RightControl)
            ) && Raylib.IsKeyPressed(KeyboardKey.Space)
        )
        {
            Logger.Info(
                "EndingScene: ctrl+space pressed - returning to main menu (deferred cleanup)"
            );
            IsActive = false;
            _creditsStarted = false;
            _showStartText = false;
            _startTextFader.SetAlpha(0f);
            _startTextPlayed = true;
            _startTextHasFadedOut = true;
            _showEndText = false;
            _endTextFader.SetAlpha(0f);
            _endTextHasFadedOut = true;
            _endBackgroundFader.SetAlpha(0f);
            _endBackgroundPlayed = true;
            return;
        }

        float dt = Raylib.GetFrameTime();
        _elapsedTime += dt;

        float startDelay = _config.Ending.StartDelay;
        float startTextHideTime = _config.Ending.StartTextHideTime;

        // Start music and credits after delay (only if music hasn't been started or stopped already)
        if (!_musicStarted && !_musicStopped && !_startTextPlayed && _elapsedTime >= startDelay)
        {
            AudioService.Play(_music);
            _musicStarted = true;
            _showStartText = true;
            _creditsStarted = true;
            _startTextFader.StartFadeIn(0.5f);
            _startTextPlayed = true;
            Logger.Info("EndingScene: StartText fade-in triggered at elapsed {0:F2}", _elapsedTime);
        }

        // Update the start text fader each frame if needed
        if (_showStartText)
        {
            _startTextFader.Update(dt);
            Logger.Debug(
                "EndingScene: StartTextFader state={0} active={1} alpha={2:F3}",
                _startTextFader.State,
                _startTextFader.Active,
                _startTextFader.Alpha
            );
        }

        // Hide the start text when fade out finishes
        if (_showStartText && !_startTextFader.Active && _startTextFader.Alpha <= 0f)
        {
            _showStartText = false;
            _startTextHasFadedOut = true;
            Logger.Info("EndingScene: StartText hidden after fade-out");
        }

        // Fade out start text after hide time, only once
        if (
            _showStartText
            && !_startTextHasFadedOut
            && _startTextFader.State != Fader.FadeState.FadingOut
            && _elapsedTime >= startDelay + startTextHideTime
        )
        {
            Logger.Debug(
                "EndingScene: Fade-out condition met: state={0} active={1} alpha={2:F3} elapsed={3:F3}",
                _startTextFader.State,
                _startTextFader.Active,
                _startTextFader.Alpha,
                _elapsedTime
            );
            _startTextFader.StartFadeOut(0.5f);
            _startTextHasFadedOut = true;
            Logger.Info(
                "EndingScene: StartText fade-out triggered at elapsed {0:F2}",
                _elapsedTime
            );
        }
        // (duplicate hide logic removed)

        // Only scroll credits after delay
        if (_creditsStarted)
        {
            _creditsScrollY -= _creditsScrollSpeed * dt;
        }

        // Check if credits have finished scrolling off-screen to start end-text sequence
        if (
            _creditsStarted
            && !_endTextStarted
            && _creditsHeight > 0f
            && _creditsScrollY <= -_creditsHeight
        )
        {
            _endTextStarted = true;
            _showEndText = true;
            _endTextFader.StartFadeIn(Math.Max(0.001f, _config.Ending.EndTextFadeInDuration));
            _endTextShowElapsed = 0f;
            if (!_endBackgroundActive)
            {
                _endBackgroundActive = true;
                _endBackgroundFader.StartFadeIn(
                    Math.Max(0.001f, _config.Ending.EndBackgroundFadeDuration)
                );
                _endBackgroundPlayed = true;
            }
        }

        // Activate plain end background 1s before the end text fades in so we can switch to a solid background
        if (!_endBackgroundActive && _creditsStarted && _creditsHeight > 0f)
        {
            float pxBeforeEnd = _creditsScrollSpeed * 1f;
            if (_creditsScrollY <= -_creditsHeight + pxBeforeEnd)
            {
                _endBackgroundActive = true;
                if (!_endBackgroundPlayed)
                {
                    _endBackgroundFader.StartFadeIn(
                        Math.Max(0.001f, _config.Ending.EndBackgroundFadeDuration)
                    );
                    _endBackgroundPlayed = true;
                }
            }
        }

        // Update end background fade progress if active
        if (_endBackgroundActive)
        {
            _endBackgroundFader.Update(dt);
        }

        // Update music stream if started and not stopped
        if (_musicStarted && !_musicStopped && AudioService.IsAudioDeviceReady)
        {
            AudioService.Update(_music);

            _musicPlayElapsed += dt;
            float played = AudioService.GetTimePlayed(_music);

            const float tolerance = 0.08f; // small tolerance in seconds
            bool endedByApi = played > 0 && played + tolerance >= _songDuration;
            bool endedByManual = _musicPlayElapsed + tolerance >= _songDuration;
            if (endedByApi || endedByManual)
            {
                try
                {
                    AudioService.Stop(_music);
                }
                catch { }
                _musicStopped = true;
                _musicStarted = false;
                Logger.Info(
                    "MUSIC: Stopped playback after {0:F2}s (duration {1:F2}s, api={2:F2}s)",
                    _musicPlayElapsed,
                    _songDuration,
                    played
                );
            }
        }

        // Update end text fading/visibility timers
        if (_showEndText)
        {
            _endTextFader.Update(dt);

            // If we have fully faded in, start counting the display time
            if (!_endTextFader.Active && _endTextFader.Alpha >= 1f)
            {
                _endTextShowElapsed += dt;
                if (!_endTextHasFadedOut && _endTextShowElapsed >= _config.Ending.EndTextHideTime)
                {
                    _endTextFader.StartFadeOut(
                        Math.Max(0.001f, _config.Ending.EndTextFadeOutDuration)
                    );
                    _endTextHasFadedOut = true;
                    Logger.Info(
                        "EndingScene: EndText fade-out triggered at elapsed {0:F2}",
                        _elapsedTime
                    );
                }
            }

            // When fade out completes, hide end text and start copyright
            if (!_endTextFader.Active && _endTextFader.Alpha <= 0f && !_endTextHasFadedOut)
            {
                _showEndText = false;
            }
            else if (!_endTextFader.Active && _endTextFader.Alpha <= 0f && _endTextHasFadedOut)
            {
                _showEndText = false;
                if (!string.IsNullOrEmpty(_config.Ending.CopyrightText) && !_copyrightStarted)
                {
                    _copyrightStarted = true;
                    _copyrightFadingIn = true;
                    _copyrightFadeElapsed = 0f;
                    _copyrightAlpha = 0f;
                }
            }
        }

        // Update copyright fade-in if active
        if (_copyrightFadingIn)
        {
            _copyrightFadeElapsed += dt;
            float dur = Math.Max(0.001f, _config.Ending.CopyrightFadeInDuration);
            _copyrightAlpha = Math.Clamp(_copyrightFadeElapsed / dur, 0f, 1f);
            if (_copyrightAlpha >= 1f)
            {
                _copyrightAlpha = 1f;
                _copyrightFadingIn = false;
            }
        }
    }
}
