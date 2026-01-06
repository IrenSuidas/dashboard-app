using EndingApp.Scenes.ClipScene;
using EndingApp.Scenes.SongRequest;
using Raylib_cs;

namespace EndingApp;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        Logger.Info("Program: Application starting...");
        CleanupRequestedFolder();

        // Load configuration
        var config = AppConfig.Load();

        const int screenWidth = 640;
        const int screenHeight = 220;

        // Raylib.SetConfigFlags(ConfigFlags.UndecoratedWindow | ConfigFlags.TransparentWindow);
        Raylib.SetConfigFlags(ConfigFlags.AlwaysRunWindow);
        Raylib.InitWindow(screenWidth, screenHeight, "EndingApp");
        Raylib.SetWindowState(ConfigFlags.VSyncHint);
        Raylib.SetTargetFPS(60);

        var icon = Raylib.LoadImage("assets/icon.png");
        Raylib.SetWindowIcon(icon);
        Raylib.UnloadImage(icon);

        Rectangle clipButton = new(30, 80, 170, 80);
        Rectangle endingButton = new(225, 80, 170, 80);
        Rectangle songRequestButton = new(420, 80, 190, 80);

        EndingScene? endingScene = null;
        SongRequestScene? songRequestScene = null;
        ClipScene? clipScene = null;

        while (!Raylib.WindowShouldClose())
        {
            // Update
            if (endingScene?.IsActive == true)
            {
                endingScene.Update();
                // If the scene requested returning to main menu, perform cleanup here and clear the reference.
                if (endingScene != null && !endingScene.IsActive)
                {
                    Logger.Info(
                        "Program: EndingScene is no longer active; performing cleanup and releasing reference."
                    );
                    // Restore the main window state and close audio device
                    EndingScene.RestoreWindowAndAudioState();
                    // Cleanup resources
                    endingScene.Cleanup();
                    // Dump resource cache to check counts of cached resources
                    ResourceCache.DumpState();
                    FontCache.DumpState();
                    long memAfterCleanup = Diagnostics.GetPrivateMemoryMB();
                    Diagnostics.LogMemory("Program: memory after cleanup", memAfterCleanup);
                    endingScene = null;
                }
            }
            else if (songRequestScene?.IsActive == true)
            {
                songRequestScene.Update();
                if (songRequestScene != null && !songRequestScene.IsActive)
                {
                    Logger.Info(
                        "Program: SongRequestScene is no longer active; performing cleanup."
                    );
                    songRequestScene.Cleanup();
                    songRequestScene = null;
                }
            }
            else if (clipScene?.IsActive == true)
            {
                clipScene.Update();
                if (clipScene != null && !clipScene.IsActive)
                {
                    Logger.Info("Program: ClipScene is no longer active; performing cleanup.");
                    clipScene.Cleanup();
                    clipScene = null;
                }
            }
            else
            {
                var mousePos = Raylib.GetMousePosition();
                bool clipHovered = Raylib.CheckCollisionPointRec(mousePos, clipButton);
                bool endingHovered = Raylib.CheckCollisionPointRec(mousePos, endingButton);
                bool songRequestHovered = Raylib.CheckCollisionPointRec(
                    mousePos,
                    songRequestButton
                );

                // Handle button clicks
                if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    if (clipHovered)
                    {
                        Logger.Info("Program: Starting ClipScene");
                        clipScene ??= new ClipScene(config);
                        clipScene.Start();
                    }
                    else if (endingHovered)
                    {
                        long memBefore = Diagnostics.GetPrivateMemoryMB();
                        Diagnostics.LogMemory(
                            "Program: memory before starting EndingScene",
                            memBefore
                        );
                        endingScene = new EndingScene(config);
                        endingScene.Start();
                        ResourceCache.DumpState();
                        FontCache.DumpState();
                        long memAfter = Diagnostics.GetPrivateMemoryMB();
                        Diagnostics.LogMemoryDelta(
                            "Program: memory after starting EndingScene",
                            memBefore,
                            memAfter
                        );
                    }
                    else if (songRequestHovered)
                    {
                        Logger.Info("Program: Starting SongRequestScene");
                        songRequestScene ??= new SongRequestScene(config);
                        songRequestScene.Start();
                    }
                }
            }

            // Draw
            Raylib.BeginDrawing();

            if (endingScene?.IsActive == true)
            {
                Raylib.ClearBackground(Color.Black);
                endingScene.Draw();
            }
            else if (songRequestScene?.IsActive == true)
            {
                songRequestScene.Draw();
            }
            else if (clipScene?.IsActive == true)
            {
                clipScene.Draw();
            }
            else
            {
                Raylib.ClearBackground(Color.Black);

                // Draw "Clip" button
                bool clipHover = Raylib.CheckCollisionPointRec(
                    Raylib.GetMousePosition(),
                    clipButton
                );

                // Background with subtle shadow effect
                Rectangle clipShadow = new(
                    clipButton.X + 3,
                    clipButton.Y + 3,
                    clipButton.Width,
                    clipButton.Height
                );
                Raylib.DrawRectangleRounded(clipShadow, 0.2f, 10, new Color(0, 0, 0, 100));

                // Main button
                var clipBg = clipHover ? Color.White : new Color(240, 240, 240, 255);
                Raylib.DrawRectangleRounded(clipButton, 0.2f, 10, clipBg);

                // Border (draw multiple times for thickness)
                var clipBorder = clipHover
                    ? new Color(60, 60, 60, 255)
                    : new Color(150, 150, 150, 255);
                Raylib.DrawRectangleRoundedLines(clipButton, 0.2f, 10, clipBorder);
                Rectangle clipInner = new(
                    clipButton.X + 1,
                    clipButton.Y + 1,
                    clipButton.Width - 2,
                    clipButton.Height - 2
                );
                Raylib.DrawRectangleRoundedLines(clipInner, 0.2f, 10, clipBorder);

                // Text
                string clipText = "Clips";
                var clipTextColor = clipHover ? Color.Black : new Color(40, 40, 40, 255);
                int clipTextWidth = Raylib.MeasureText(clipText, 24);
                Raylib.DrawText(
                    clipText,
                    (int)(clipButton.X + (clipButton.Width - clipTextWidth) / 2),
                    (int)(clipButton.Y + (clipButton.Height - 24) / 2),
                    24,
                    clipTextColor
                );

                // Draw "Ending" button
                bool endingHover = Raylib.CheckCollisionPointRec(
                    Raylib.GetMousePosition(),
                    endingButton
                );

                // Background with subtle shadow effect
                Rectangle endingShadow = new(
                    endingButton.X + 3,
                    endingButton.Y + 3,
                    endingButton.Width,
                    endingButton.Height
                );
                Raylib.DrawRectangleRounded(endingShadow, 0.2f, 10, new Color(0, 0, 0, 100));

                // Main button
                var endingBg = endingHover ? Color.White : new Color(240, 240, 240, 255);
                Raylib.DrawRectangleRounded(endingButton, 0.2f, 10, endingBg);

                // Border (draw multiple times for thickness)
                var endingBorder = endingHover
                    ? new Color(60, 60, 60, 255)
                    : new Color(150, 150, 150, 255);
                Raylib.DrawRectangleRoundedLines(endingButton, 0.2f, 10, endingBorder);
                Rectangle endingInner = new(
                    endingButton.X + 1,
                    endingButton.Y + 1,
                    endingButton.Width - 2,
                    endingButton.Height - 2
                );
                Raylib.DrawRectangleRoundedLines(endingInner, 0.2f, 10, endingBorder);

                // Text
                string endingText = "Ending";
                var endingTextColor = endingHover ? Color.Black : new Color(40, 40, 40, 255);
                int endingTextWidth = Raylib.MeasureText(endingText, 24);
                Raylib.DrawText(
                    endingText,
                    (int)(endingButton.X + (endingButton.Width - endingTextWidth) / 2),
                    (int)(endingButton.Y + (endingButton.Height - 24) / 2),
                    24,
                    endingTextColor
                );

                // Draw "Song Request" button
                bool songRequestHover = Raylib.CheckCollisionPointRec(
                    Raylib.GetMousePosition(),
                    songRequestButton
                );

                // Background with subtle shadow effect
                Rectangle songRequestShadow = new(
                    songRequestButton.X + 3,
                    songRequestButton.Y + 3,
                    songRequestButton.Width,
                    songRequestButton.Height
                );
                Raylib.DrawRectangleRounded(songRequestShadow, 0.2f, 10, new Color(0, 0, 0, 100));

                // Main button
                var songRequestBg = songRequestHover ? Color.White : new Color(240, 240, 240, 255);
                Raylib.DrawRectangleRounded(songRequestButton, 0.2f, 10, songRequestBg);

                // Border (draw multiple times for thickness)
                var songRequestBorder = songRequestHover
                    ? new Color(60, 60, 60, 255)
                    : new Color(150, 150, 150, 255);
                Raylib.DrawRectangleRoundedLines(songRequestButton, 0.2f, 10, songRequestBorder);
                Rectangle songRequestInner = new(
                    songRequestButton.X + 1,
                    songRequestButton.Y + 1,
                    songRequestButton.Width - 2,
                    songRequestButton.Height - 2
                );
                Raylib.DrawRectangleRoundedLines(songRequestInner, 0.2f, 10, songRequestBorder);

                string songRequestText = "Song Request";
                var songRequestTextColor = songRequestHover
                    ? Color.Black
                    : new Color(40, 40, 40, 255);
                int songRequestTextWidth = Raylib.MeasureText(songRequestText, 24);
                Raylib.DrawText(
                    songRequestText,
                    (int)(
                        songRequestButton.X + (songRequestButton.Width - songRequestTextWidth) / 2
                    ),
                    (int)(songRequestButton.Y + (songRequestButton.Height - 24) / 2),
                    24,
                    songRequestTextColor
                );

                string title = "EndingApp";
                int titleSize = 32;
                int titleWidth = Raylib.MeasureText(title, titleSize);
                Raylib.DrawText(title, (screenWidth - titleWidth) / 2, 25, titleSize, Color.White);
            }

            Raylib.EndDrawing();
        }

        endingScene?.Cleanup();
        Raylib.CloseWindow();
    }

    private static void CleanupRequestedFolder()
    {
        try
        {
            string tempPath = Path.Combine(AppContext.BaseDirectory, "temp");
            string requestedPath = Path.Combine(tempPath, "requested");

            if (Directory.Exists(requestedPath))
            {
                Logger.Info($"Program: Cleaning up {requestedPath}");
                // Delete all files in the directory
                string[] files = Directory.GetFiles(requestedPath);
                foreach (string file in files)
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Program: Failed to cleanup requested folder: {ex.Message}");
        }
    }
}
