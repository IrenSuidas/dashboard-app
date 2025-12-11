using Raylib_cs;

namespace EndingApp.Services;

internal static class AudioService
{
    // Reference count to support shared ownership of the audio device.
    // Each consumer should call Register() before using other methods and Unregister() when done.
    private static int s_refCount;

    public static bool IsAudioDeviceReady => Raylib.IsAudioDeviceReady();

    public static bool Register()
    {
        try
        {
            s_refCount++;
            if (!IsAudioDeviceReady)
            {
                Raylib.InitAudioDevice();
                Logger.Info("AudioService: initialized audio device");
            }
            return IsAudioDeviceReady;
        }
        catch (Exception ex)
        {
            Logger.Warn("AudioService: Register failed: {0}", ex.Message);
            return false;
        }
    }

    public static void Unregister()
    {
        try
        {
            s_refCount = Math.Max(0, s_refCount - 1);
            if (s_refCount == 0 && IsAudioDeviceReady)
            {
                try
                {
                    Raylib.CloseAudioDevice();
                    Logger.Info("AudioService: closed audio device");
                }
                catch (Exception ex)
                {
                    Logger.Warn("AudioService: failed to close audio device: {0}", ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("AudioService: Unregister failed: {0}", ex.Message);
        }
    }

    public static void ResetPlayback(Music music)
    {
        if (!IsAudioDeviceReady || music.IsNull())
            return;
        try
        {
            try
            {
                Raylib.StopMusicStream(music);
            }
            catch { }
            // Note: Seeking the music stream here can cause native crashes when the underlying
            // audio stream couldn't be initialized (miniaudio data pipeline failure). Avoid
            // calling SeekMusicStream directly until we have a reliable check that the stream
            // is available.
            Logger.Info("AudioService: Reset playback for music");
        }
        catch (Exception ex)
        {
            Logger.Warn("AudioService: ResetPlayback failed: {0}", ex.Message);
        }
    }

    public static void Play(Music music)
    {
        if (!IsAudioDeviceReady || music.IsNull())
            return;
        try
        {
            Logger.Info("AudioService: Play requested");
            Raylib.PlayMusicStream(music);
            // Ensure playback pipeline has a chance to initialize
            try
            {
                Raylib.UpdateMusicStream(music);
            }
            catch { }
            try
            {
                Raylib.SetMusicVolume(music, 1.0f);
            }
            catch { }
            float played = 0f;
            try
            {
                played = Raylib.GetMusicTimePlayed(music);
            }
            catch { }
        }
        catch (Exception ex)
        {
            Logger.Warn("AudioService: Play failed: {0}", ex.Message);
        }
    }

    public static void Stop(Music music)
    {
        if (!IsAudioDeviceReady || music.IsNull())
            return;
        try
        {
            Raylib.StopMusicStream(music);
        }
        catch (Exception ex)
        {
            Logger.Warn("AudioService: Stop failed: {0}", ex.Message);
        }
    }

    public static void Update(Music music)
    {
        if (!IsAudioDeviceReady || music.IsNull())
            return;
        try
        {
            Raylib.UpdateMusicStream(music);
            float played = 0f;
            try
            {
                played = Raylib.GetMusicTimePlayed(music);
            }
            catch { }
        }
        catch (Exception ex)
        {
            Logger.Warn("AudioService: Update failed: {0}", ex.Message);
        }
    }

    public static void SetVolume(Music music, float volume)
    {
        if (!IsAudioDeviceReady || music.IsNull())
            return;
        try
        {
            Raylib.SetMusicVolume(music, volume);
        }
        catch (Exception ex)
        {
            Logger.Warn("AudioService: SetVolume failed: {0}", ex.Message);
        }
    }

    public static float GetTimePlayed(Music music)
    {
        if (!IsAudioDeviceReady || music.IsNull())
            return 0f;
        try
        {
            return Raylib.GetMusicTimePlayed(music);
        }
        catch (Exception ex)
        {
            Logger.Warn("AudioService: GetTimePlayed failed: {0}", ex.Message);
            return 0f;
        }
    }

    public static float GetTimeLength(Music music)
    {
        if (!IsAudioDeviceReady || music.IsNull())
            return 0f;
        try
        {
            return Raylib.GetMusicTimeLength(music);
        }
        catch (Exception ex)
        {
            Logger.Warn("AudioService: GetTimeLength failed: {0}", ex.Message);
            return 0f;
        }
    }
}

internal static class MusicExtensions
{
    public static bool IsNull(this Music music)
    {
        return music.Equals(default(Music));
    }
}
