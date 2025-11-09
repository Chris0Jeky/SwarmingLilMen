using Raylib_cs;

Raylib.InitWindow(800, 450, "Raylib test");
Raylib.SetTargetFPS(60);

while (!Raylib.WindowShouldClose())
{
    Raylib.BeginDrawing();
    Raylib.ClearBackground(Color.Black);
    Raylib.DrawCircle(400, 225, 50, Color.SkyBlue);
    Raylib.DrawFPS(10, 10);
    Raylib.EndDrawing();
}
Raylib.CloseWindow();