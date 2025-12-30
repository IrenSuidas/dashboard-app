using EndingApp.Services;
using Raylib_cs;

namespace EndingApp.Scenes.SongRequest;

internal sealed partial class SongRequestScene
{
    private static void UpdateSongFile(string title)
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "assets", "song.txt");
            // Ensure directory exists just in case
            string? dir = Path.GetDirectoryName(path);
            if (dir != null)
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, title);
        }
        catch (Exception ex)
        {
            Logger.Error($"SongRequestScene: Failed to update song.txt - {ex.Message}");
        }
    }

    private void UpdateCurrentSongDuration(bool isRequested, float len)
    {
        if (_currentSongItem == null)
            return;

        lock (_listLock)
        {
            if (isRequested)
            {
                // We need to find the item in the list that matches the current song
                // Ideally using the index, but let's be safe and search by path if index is out of bounds or mismatch
                if (
                    _currentRequestedIndex < _requestedSongs.Count
                    && _requestedSongs[_currentRequestedIndex].Path == _currentSongItem.Path
                )
                {
                    _requestedSongs[_currentRequestedIndex] = _requestedSongs[
                        _currentRequestedIndex
                    ] with
                    {
                        Data = _requestedSongs[_currentRequestedIndex].Data with
                        {
                            Duration = (int)len,
                        },
                    };
                }
                else
                {
                    // Fallback search
                    int idx = _requestedSongs.FindIndex(x => x.Path == _currentSongItem.Path);
                    if (idx != -1)
                    {
                        _requestedSongs[idx] = _requestedSongs[idx] with
                        {
                            Data = _requestedSongs[idx].Data with { Duration = (int)len },
                        };
                    }
                }
            }
            else
            {
                if (
                    _currentRecurrentIndex < _recurrentSongs.Count
                    && _recurrentSongs[_currentRecurrentIndex].Path == _currentSongItem.Path
                )
                {
                    _recurrentSongs[_currentRecurrentIndex] = _recurrentSongs[
                        _currentRecurrentIndex
                    ] with
                    {
                        Data = _recurrentSongs[_currentRecurrentIndex].Data with
                        {
                            Duration = (int)len,
                        },
                    };
                }
                else
                {
                    int idx = _recurrentSongs.FindIndex(x => x.Path == _currentSongItem.Path);
                    if (idx != -1)
                    {
                        _recurrentSongs[idx] = _recurrentSongs[idx] with
                        {
                            Data = _recurrentSongs[idx].Data with { Duration = (int)len },
                        };
                    }
                }
            }
        }
    }

    private void StartCrossfade(PlaylistItem nextItem, bool isRequested, float seekPos = 0)
    {
        if (_isMusicLoaded)
        {
            // Save recurrent position if we are interrupting it
            if (!_isPlayingRequested && isRequested)
            {
                _recurrentSeekPosition = AudioService.GetTimePlayed(_currentMusic);
                // Find index of current item in recurrent list to be safe
                int idx = _recurrentSongs.FindIndex(x => x.Path == _currentSongItem?.Path);
                if (idx != -1)
                    _currentRecurrentIndex = idx;
            }

            _fadingMusic = _currentMusic;
            _isFadingMusicLoaded = true;
            _isCrossfading = true;
            _crossfadeTimer = 0f;
        }

        _currentSongItem = nextItem;
        _currentMusic = AudioService.Load(nextItem.Path);
        AudioService.Play(_currentMusic);
        if (seekPos > 0)
            AudioService.Seek(_currentMusic, seekPos);
        AudioService.SetVolume(_currentMusic, 0f); // Start silent

        _isMusicLoaded = true;
        _isPlayingRequested = isRequested;
        if (isRequested)
            _playedRequestedPaths.Add(nextItem.Path);

        // Check for duration update immediately for crossfading too
        float len = AudioService.GetTimeLength(_currentMusic);
        if (len > 0 && (_currentSongItem.Duration == null || _currentSongItem.Duration == 0))
        {
            _currentSongItem = _currentSongItem with
            {
                Data = _currentSongItem.Data with { Duration = (int)len },
            };
            UpdateCurrentSongDuration(isRequested, len);
            // Save to manifest
            List<Song> requested = _requestedSongs.Select(x => x.Data).ToList();
            List<Song> recurrent = _recurrentSongs.Select(x => x.Data).ToList();
            SaveManifest(requested, recurrent);
        }

        if (!_isFadingMusicLoaded) // If no previous music, just fade in? Or instant?
        {
            AudioService.SetVolume(_currentMusic, _volume);
            _isCrossfading = false;
        }

        UpdateSongFile(nextItem.Title);
    }

    private void PlayNext(PlaylistItem nextItem, bool isRequested)
    {
        if (_isMusicLoaded)
        {
            if (_currentSongItem != null)
                _history.Push(_currentSongItem);
            AudioService.Stop(_currentMusic);
            AudioService.Unload(_currentMusic);
        }

        _currentSongItem = nextItem;
        _currentMusic = AudioService.Load(nextItem.Path);
        AudioService.Play(_currentMusic);
        AudioService.SetVolume(_currentMusic, _volume);
        _isMusicLoaded = true;
        _isPlayingRequested = isRequested;
        _isPaused = false;
        if (isRequested)
            _playedRequestedPaths.Add(nextItem.Path);

        // Update duration if missing
        float len = AudioService.GetTimeLength(_currentMusic);
        if (len > 0 && (_currentSongItem.Duration == null || _currentSongItem.Duration == 0))
        {
            _currentSongItem = _currentSongItem with
            {
                Data = _currentSongItem.Data with { Duration = (int)len },
            };

            // Update in list too
            lock (_listLock)
            {
                if (isRequested)
                {
                    int idx = _requestedSongs.FindIndex(x => x.Path == nextItem.Path);
                    if (idx != -1)
                        _requestedSongs[idx] = _requestedSongs[idx] with
                        {
                            Data = _requestedSongs[idx].Data with { Duration = (int)len },
                        };
                }
                else
                {
                    int idx = _recurrentSongs.FindIndex(x => x.Path == nextItem.Path);
                    if (idx != -1)
                        _recurrentSongs[idx] = _recurrentSongs[idx] with
                        {
                            Data = _recurrentSongs[idx].Data with { Duration = (int)len },
                        };
                }
            }

            // Save to manifest
            List<Song> requested = _requestedSongs.Select(x => x.Data).ToList();
            List<Song> recurrent = _recurrentSongs.Select(x => x.Data).ToList();
            SaveManifest(requested, recurrent);
        }

        UpdateSongFile(nextItem.Title);
    }

    private void PlayPrevious()
    {
        if (!_isMusicLoaded)
            return;

        // If played more than 3 seconds, restart
        if (AudioService.GetTimePlayed(_currentMusic) > 3.0f)
        {
            AudioService.Seek(_currentMusic, 0f);
            return;
        }

        // Else go to history
        if (_history.Count > 0)
        {
            var prev = _history.Pop();
            // We don't push current to history when going back, effectively discarding it from history flow
            // But we need to stop current
            AudioService.Stop(_currentMusic);
            AudioService.Unload(_currentMusic);

            _currentSongItem = prev;
            _currentMusic = AudioService.Load(prev.Path);
            AudioService.Play(_currentMusic);
            AudioService.SetVolume(_currentMusic, _volume);
            _isMusicLoaded = true;
            _isPlayingRequested = prev.Type == "requested";
            _isPaused = false;

            // Sync indices
            lock (_listLock)
            {
                if (_isPlayingRequested)
                {
                    int index = _requestedSongs.FindIndex(x => x.Path == prev.Path);
                    if (index != -1)
                        _currentRequestedIndex = index;
                }
                else
                {
                    int index = _recurrentSongs.FindIndex(x => x.Path == prev.Path);
                    if (index != -1)
                        _currentRecurrentIndex = index;
                }
            }

            UpdateSongFile(prev.Title);
        }
    }

    private void HandleSeeking()
    {
        var mousePos = Raylib.GetMousePosition();
        float barX = 40;
        // Updated logic to match Draw.cs position
        float bottomAreaHeight = 160;
        float bottomY = _config.Ending.Height - bottomAreaHeight;
        float barY = bottomY + 100;
        float barWidth = _config.Ending.Width - 80;
        float barHeight = 10;

        // Expanded hitbox
        if (
            mousePos.X >= barX
            && mousePos.X <= barX + barWidth
            && mousePos.Y >= barY - 15
            && mousePos.Y <= barY + barHeight + 15
        )
        {
            float seekRatio = (mousePos.X - barX) / barWidth;
            seekRatio = Math.Clamp(seekRatio, 0f, 1f);
            float totalTime = AudioService.GetTimeLength(_currentMusic);
            AudioService.Seek(_currentMusic, totalTime * seekRatio);
        }
    }

    private void HandleVolume()
    {
        var mousePos = Raylib.GetMousePosition();

        // Updated logic to match Draw.cs position (Underneath controls, bottom right)
        float btnSize = 32;
        float spacing = 20;
        float rightMargin = 40;
        float bottomAreaHeight = 160;
        float bottomY = _config.Ending.Height - bottomAreaHeight;
        float controlsY = bottomY + 25;

        // Re-calculate control positions to find volume position
        float nextX = _config.Ending.Width - rightMargin - btnSize;
        float playX = nextX - spacing - btnSize;
        float prevX = playX - spacing - btnSize;

        float volWidth = (nextX + btnSize) - prevX;
        float volHeight = 8;
        float volX = prevX;
        float volY = controlsY + btnSize + 15;

        // Expanded hitbox for easier clicking
        if (
            mousePos.X >= volX - 10
            && mousePos.X <= volX + volWidth + 10
            && mousePos.Y >= volY - 10
            && mousePos.Y <= volY + volHeight + 10
        )
        {
            float volChange = 0;

            // Handle Mouse Wheel
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0)
            {
                volChange = wheel * 0.05f; // 5% change per step
            }

            // Handle Click
            if (Raylib.IsMouseButtonDown(MouseButton.Left))
            {
                float volRatio = (mousePos.X - volX) / volWidth;
                _volume = Math.Clamp(volRatio, 0f, 1f);
                volChange = 0; // Click overrides wheel
            }

            if (volChange != 0)
            {
                _volume = Math.Clamp(_volume + volChange, 0f, 1f);
            }

            if (
                _isMusicLoaded
                && !_isCrossfading
                && (volChange != 0 || Raylib.IsMouseButtonDown(MouseButton.Left))
            )
            {
                AudioService.SetVolume(_currentMusic, _volume);
            }
        }
    }

    private void HandleControls()
    {
        if (!Raylib.IsMouseButtonPressed(MouseButton.Left))
            return;

        var mousePos = Raylib.GetMousePosition();

        float btnSize = 32;
        float spacing = 20;
        float rightMargin = 40;
        float bottomAreaHeight = 160;
        float bottomY = _config.Ending.Height - bottomAreaHeight;
        float controlsY = bottomY + 25; // Updated Y

        // Calculate X positions right-aligned
        // Order: [Prev] [Play] [Next] -> Right edge
        float nextX = _config.Ending.Width - rightMargin - btnSize;
        float playX = nextX - spacing - btnSize;
        float prevX = playX - spacing - btnSize;

        // Prev [<<]
        // Note: Hitbox adjusted to be generous around the text
        Rectangle prevRect = new(prevX, controlsY, btnSize, btnSize);
        if (Raylib.CheckCollisionPointRec(mousePos, prevRect))
        {
            PlayPrevious();
            return;
        }

        // Play/Pause [> / ||]
        Rectangle playRect = new(playX, controlsY, btnSize, btnSize);
        if (Raylib.CheckCollisionPointRec(mousePos, playRect))
        {
            _isPaused = !_isPaused;
            if (_isPaused)
                AudioService.Pause(_currentMusic);
            else
                AudioService.Resume(_currentMusic);
            return;
        }

        // Next [>>]
        Rectangle nextRect = new(nextX, controlsY, btnSize, btnSize);
        if (Raylib.CheckCollisionPointRec(mousePos, nextRect))
        {
            // Trigger next logic manually
            lock (_listLock)
            {
                if (_requestedSongs.Count > _currentRequestedIndex + 1)
                {
                    _currentRequestedIndex++;
                    PlayNext(_requestedSongs[_currentRequestedIndex], true);
                }
                else if (_recurrentSongs.Count > 0)
                {
                    _currentRecurrentIndex = (_currentRecurrentIndex + 1) % _recurrentSongs.Count;
                    PlayNext(_recurrentSongs[_currentRecurrentIndex], false);
                }
            }
        }
    }

    private void ProbeNextMissingDuration()
    {
        // Don't probe while fading or loading music to avoid audio conflicts
        if (_isCrossfading || !_isMusicLoaded)
            return;

        PlaylistItem? target = null;
        bool isRequested = false;
        int index = -1;

        lock (_listLock)
        {
            // Check requested first
            for (int i = 0; i < _requestedSongs.Count; i++)
            {
                var item = _requestedSongs[i];
                if ((item.Duration == null || item.Duration == 0) && File.Exists(item.Path))
                {
                    // Don't probe currently playing song (it updates itself naturally)
                    if (_currentSongItem?.Path == item.Path)
                        continue;

                    target = item;
                    isRequested = true;
                    index = i;
                    break;
                }
            }

            // Then recurrent
            if (target == null)
            {
                for (int i = 0; i < _recurrentSongs.Count; i++)
                {
                    var item = _recurrentSongs[i];
                    if ((item.Duration == null || item.Duration == 0) && File.Exists(item.Path))
                    {
                        if (_currentSongItem?.Path == item.Path)
                            continue;

                        target = item;
                        isRequested = false;
                        index = i;
                        break;
                    }
                }
            }
        }

        if (target != null)
        {
            try
            {
                // Quickly load, get time, unload
                // Note: Raylib.LoadMusicStream must be on main thread, which we are in Update()
                var temp = AudioService.Load(target.Path);
                float len = AudioService.GetTimeLength(temp);
                AudioService.Unload(temp);

                if (len > 0)
                {
                    lock (_listLock)
                    {
                        if (isRequested)
                        {
                            if (
                                index < _requestedSongs.Count
                                && _requestedSongs[index].Path == target.Path
                            )
                            {
                                _requestedSongs[index] = _requestedSongs[index] with
                                {
                                    Data = _requestedSongs[index].Data with { Duration = (int)len },
                                };
                            }
                        }
                        else
                        {
                            if (
                                index < _recurrentSongs.Count
                                && _recurrentSongs[index].Path == target.Path
                            )
                            {
                                _recurrentSongs[index] = _recurrentSongs[index] with
                                {
                                    Data = _recurrentSongs[index].Data with { Duration = (int)len },
                                };
                            }
                        }
                    }

                    // Save periodically? Or just let the next fetch/play save it.
                    // Let's save to manifest every successful probe to be safe but maybe batching is better?
                    // For now, save immediately is safest.
                    List<Song> requested = _requestedSongs.Select(x => x.Data).ToList();
                    List<Song> recurrent = _recurrentSongs.Select(x => x.Data).ToList();
                    SaveManifest(requested, recurrent);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(
                    $"SongRequestScene: Failed to probe duration for {target.Title} - {ex.Message}"
                );
            }
        }
    }

    public void Update()
    {
        if (!IsActive)
            return;

        // Polling
        if (Raylib.GetTime() - _lastFetchTime > FetchInterval && !_isFetching)
        {
            _lastFetchTime = Raylib.GetTime();
            _ = FetchAndDownloadSongs();
        }

        // Exit check
        if (
            (
                Raylib.IsKeyDown(KeyboardKey.LeftControl)
                || Raylib.IsKeyDown(KeyboardKey.RightControl)
            ) && Raylib.IsKeyPressed(KeyboardKey.Space)
        )
        {
            Logger.Info("SongRequestScene: ctrl+space pressed - returning to main menu");
            IsActive = false;
            return;
        }

        // Probing for missing durations
        if (Raylib.GetTime() - _lastProbeTime > ProbeInterval)
        {
            _lastProbeTime = Raylib.GetTime();
            ProbeNextMissingDuration();
        }

        // Audio Update
        if (_isMusicLoaded)
            AudioService.Update(_currentMusic);
        if (_isFadingMusicLoaded)
            AudioService.Update(_fadingMusic);

        // Crossfade Logic
        if (_isCrossfading)
        {
            _crossfadeTimer += Raylib.GetFrameTime();
            float alpha = Math.Clamp(_crossfadeTimer / CrossfadeDuration, 0f, 1f);

            // Fade in current, Fade out fading
            AudioService.SetVolume(_currentMusic, alpha * _volume);
            AudioService.SetVolume(_fadingMusic, (1.0f - alpha) * _volume);

            if (alpha >= 1.0f)
            {
                _isCrossfading = false;
                AudioService.Stop(_fadingMusic);
                AudioService.Unload(_fadingMusic);
                _isFadingMusicLoaded = false;
            }
        }

        // Playback Logic
        lock (_listLock)
        {
            // Check if we need to switch from Recurrent to Requested
            if (
                !_isPlayingRequested
                && _requestedSongs.Count > _currentRequestedIndex
                && !_isCrossfading
            )
            {
                // Interrupt Recurrent
                Logger.Info("SongRequestScene: Interrupting Recurrent for Requested");
                StartCrossfade(_requestedSongs[_currentRequestedIndex], true);
                return;
            }

            // Check if current song finished
            if (
                _isMusicLoaded
                && AudioService.GetTimePlayed(_currentMusic)
                    >= AudioService.GetTimeLength(_currentMusic) - 0.1f
            ) // Tolerance
            {
                if (_isPlayingRequested)
                {
                    // Requested finished
                    _currentRequestedIndex++;
                    if (_requestedSongs.Count > _currentRequestedIndex)
                    {
                        // Play next requested (Hard cut or quick fade? Hard cut for now)
                        PlayNext(_requestedSongs[_currentRequestedIndex], true);
                    }
                    else
                    {
                        // No more requested, resume recurrent
                        if (_recurrentSongs.Count > 0)
                        {
                            Logger.Info("SongRequestScene: Resuming Recurrent");
                            // Ensure index is valid
                            if (_currentRecurrentIndex >= _recurrentSongs.Count)
                                _currentRecurrentIndex = 0;

                            var nextRecurrent = _recurrentSongs[_currentRecurrentIndex];
                            StartCrossfade(nextRecurrent, false, _recurrentSeekPosition);
                        }
                    }
                }
                else
                {
                    // Recurrent finished
                    if (_recurrentSongs.Count > 0)
                    {
                        _currentRecurrentIndex =
                            (_currentRecurrentIndex + 1) % _recurrentSongs.Count;
                        PlayNext(_recurrentSongs[_currentRecurrentIndex], false);
                    }
                }
            }

            // Initial Start
            if (!_isMusicLoaded && !_isCrossfading)
            {
                if (_requestedSongs.Count > _currentRequestedIndex)
                {
                    PlayNext(_requestedSongs[_currentRequestedIndex], true);
                }
                else if (_recurrentSongs.Count > 0)
                {
                    PlayNext(_recurrentSongs[_currentRecurrentIndex], false);
                }
            }
        }

        // Seek bar logic (only for current music)
        if (_isMusicLoaded && Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            HandleSeeking();
            HandleControls();
        }

        HandleVolume();
    }
}
