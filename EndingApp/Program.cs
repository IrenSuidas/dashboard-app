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
        Rectangle testButton = new(270, 70, 110, 60);

        EndingScene? endingScene = null;
        FontTestScene? fontTestScene = null;

        while (!Raylib.WindowShouldClose())
        {
            // Update
            if (endingScene?.IsActive == true)
            {
                endingScene.Update();
            }
            else if (fontTestScene?.IsActive == true)
            {
                fontTestScene.Update();
            }
            else
            {
                var mousePos = Raylib.GetMousePosition();
                bool endingHovered = Raylib.CheckCollisionPointRec(mousePos, endingButton);
                bool clipHovered = Raylib.CheckCollisionPointRec(mousePos, clipButton);
                bool testHovered = Raylib.CheckCollisionPointRec(mousePos, testButton);

                // Handle button clicks
                if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    if (endingHovered)
                    {
                        endingScene = new EndingScene(config);
                        endingScene.Start();
                    }
                    else if (clipHovered)
                    {
                        // TODO: Handle Clip button click
                    }
                    else if (testHovered)
                    {
                        fontTestScene = new FontTestScene();
                        fontTestScene.Start();
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
            else if (fontTestScene?.IsActive == true)
            {
                Raylib.ClearBackground(Color.Black);
                fontTestScene.Draw();
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

                // Draw "Test" button
                var testColor = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), testButton)
                    ? new Color(100, 100, 100, 200)
                    : new Color(60, 60, 60, 180);
                Raylib.DrawRectangleRounded(testButton, 0.3f, 8, testColor);
                Raylib.DrawRectangleRoundedLines(
                    testButton,
                    0.3f,
                    8,
                    new Color(200, 200, 200, 255)
                );

                string testText = "Test";
                int testTextWidth = Raylib.MeasureText(testText, 20);
                Raylib.DrawText(
                    testText,
                    (int)(testButton.X + (testButton.Width - testTextWidth) / 2),
                    (int)(testButton.Y + 20),
                    20,
                    Color.White
                );
            }

            Raylib.EndDrawing();
        }

        endingScene?.Cleanup();
        fontTestScene?.Cleanup();
        Raylib.CloseWindow();
    }
}
