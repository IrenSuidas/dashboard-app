using Raylib_cs;

namespace EndingApp;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        // Load configuration
        var config = AppConfig.Load();

        const int screenWidth = 400;
        const int screenHeight = 200;

        // Raylib.SetConfigFlags(ConfigFlags.UndecoratedWindow | ConfigFlags.TransparentWindow);
        Raylib.InitWindow(screenWidth, screenHeight, "EndingApp");
        Raylib.SetWindowState(ConfigFlags.VSyncHint);
        Raylib.SetTargetFPS(60);

        // Button properties - 3 buttons now
        Rectangle endingButton = new(20, 70, 110, 60);
        Rectangle clipButton = new(145, 70, 110, 60);

        EndingScene? endingScene = null;

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
                    long memAfterCleanup = System
                        .Diagnostics.Process.GetCurrentProcess()
                        .PrivateMemorySize64;
                    Logger.Info(
                        "Program: memory after cleanup: {0} MB",
                        memAfterCleanup / 1024 / 1024
                    );
                    endingScene = null;
                }
            }
            else
            {
                var mousePos = Raylib.GetMousePosition();
                bool endingHovered = Raylib.CheckCollisionPointRec(mousePos, endingButton);
                bool clipHovered = Raylib.CheckCollisionPointRec(mousePos, clipButton);

                // Handle button clicks
                if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    if (endingHovered)
                    {
                        long memBefore = System
                            .Diagnostics.Process.GetCurrentProcess()
                            .PrivateMemorySize64;
                        Logger.Info(
                            "Program: memory before starting EndingScene: {0} MB",
                            memBefore / 1024 / 1024
                        );
                        endingScene = new EndingScene(config);
                        endingScene.Start();
                        ResourceCache.DumpState();
                        FontCache.DumpState();
                        long memAfter = System
                            .Diagnostics.Process.GetCurrentProcess()
                            .PrivateMemorySize64;
                        Logger.Info(
                            "Program: memory after starting EndingScene: {0} MB (delta {1} MB)",
                            memAfter / 1024 / 1024,
                            (memAfter - memBefore) / 1024 / 1024
                        );
                    }
                    else if (clipHovered)
                    {
                        // TODO: Handle Clip button click
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
            }

            Raylib.EndDrawing();
        }

        endingScene?.Cleanup();
        Raylib.CloseWindow();
    }
}
