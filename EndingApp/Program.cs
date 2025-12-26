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

        const int screenWidth = 400;
        const int screenHeight = 200;

        // Raylib.SetConfigFlags(ConfigFlags.UndecoratedWindow | ConfigFlags.TransparentWindow);
        Raylib.InitWindow(screenWidth, screenHeight, "EndingApp");
        Raylib.SetWindowState(ConfigFlags.VSyncHint);
        Raylib.SetTargetFPS(60);

        Rectangle endingButton = new(20, 70, 110, 60);
        Rectangle clipButton = new(145, 70, 110, 60);
        Rectangle songRequestButton = new(270, 70, 110, 60);

        EndingScene? endingScene = null;
        SongRequestScene? songRequestScene = null;

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
            else
            {
                var mousePos = Raylib.GetMousePosition();
                bool endingHovered = Raylib.CheckCollisionPointRec(mousePos, endingButton);
                bool clipHovered = Raylib.CheckCollisionPointRec(mousePos, clipButton);
                bool songRequestHovered = Raylib.CheckCollisionPointRec(
                    mousePos,
                    songRequestButton
                );

                // Handle button clicks
                if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    if (endingHovered)
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
                    else if (clipHovered)
                    {
                        // TODO: Handle Clip button click
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
            else
            {
                Raylib.ClearBackground(new Color(0, 0, 0, 0)); // Transparent background

                // Draw "Ending" button
                var endingColor = Raylib.CheckCollisionPointRec(
                    Raylib.GetMousePosition(),
                    endingButton
                )
                    ? new Color(100, 100, 100, 200)
                    : new Color(60, 60, 60, 180);
                Raylib.DrawRectangleRounded(endingButton, 0.3f, 8, endingColor);
                Raylib.DrawRectangleRoundedLines(
                    endingButton,
                    0.3f,
                    8,
                    new Color(200, 200, 200, 255)
                );

                string endingText = "Ending";
                int endingTextWidth = Raylib.MeasureText(endingText, 20);
                Raylib.DrawText(
                    endingText,
                    (int)(endingButton.X + (endingButton.Width - endingTextWidth) / 2),
                    (int)(endingButton.Y + 20),
                    20,
                    Color.White
                );

                // Draw "Clip" button
                var clipColor = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), clipButton)
                    ? new Color(100, 100, 100, 200)
                    : new Color(60, 60, 60, 180);
                Raylib.DrawRectangleRounded(clipButton, 0.3f, 8, clipColor);
                Raylib.DrawRectangleRoundedLines(
                    clipButton,
                    0.3f,
                    8,
                    new Color(200, 200, 200, 255)
                );

                string clipText = "Clip";
                int clipTextWidth = Raylib.MeasureText(clipText, 20);
                Raylib.DrawText(
                    clipText,
                    (int)(clipButton.X + (clipButton.Width - clipTextWidth) / 2),
                    (int)(clipButton.Y + 20),
                    20,
                    Color.White
                );

                // Draw "Song Request" button
                var songRequestColor = Raylib.CheckCollisionPointRec(
                    Raylib.GetMousePosition(),
                    songRequestButton
                )
                    ? new Color(100, 100, 100, 200)
                    : new Color(60, 60, 60, 180);
                Raylib.DrawRectangleRounded(songRequestButton, 0.3f, 8, songRequestColor);
                Raylib.DrawRectangleRoundedLines(
                    songRequestButton,
                    0.3f,
                    8,
                    new Color(200, 200, 200, 255)
                );

                string songRequestText = "Song Req";
                int songRequestTextWidth = Raylib.MeasureText(songRequestText, 20);
                Raylib.DrawText(
                    songRequestText,
                    (int)(
                        songRequestButton.X + (songRequestButton.Width - songRequestTextWidth) / 2
                    ),
                    (int)(songRequestButton.Y + 20),
                    20,
                    Color.White
                );
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
