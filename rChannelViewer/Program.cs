namespace rChannelViewer;
using Raylib_cs;
class Program {
    static void Main() {
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
        Raylib.InitWindow(800,600,"rChannelViewer");
        Raylib.SetTargetFPS(120);
        GenerateBackgroundTexture();

        while (!Raylib.WindowShouldClose()) {
            Raylib.BeginDrawing();
            //Raylib.ClearBackground(Color.DarkGray);
            DrawBackground();
            Raylib.EndDrawing();
        }
    }

    private static void GenerateBackgroundTexture() {
        Color bgColor1 = Raylib.GetColor(0x0c1021ff);
        Color bgColor2 = Raylib.GetColor(0x192247ff);

        const int bgSize = 200;
        
        Image bgImg = Raylib.GenImageColor(bgSize, bgSize, Color.DarkBlue);
        for (int x = 0; x < bgSize; x++) {
            for (int y = 0; y < bgSize; y++) {
                Color thisPixelsColors = (x + y) / (bgSize/2) % 2 == 0 ? bgColor1 : bgColor2;
                Raylib.ImageDrawPixel(ref bgImg,x,y,thisPixelsColors);
            }
        }

        BackgroundTexture = Raylib.LoadTextureFromImage(bgImg);
    }

    private static Texture2D BackgroundTexture;
    static void DrawBackground() {
        // This function draws diagonal stripes in the background.
        float offset = (float)Raylib.GetTime() * 100 % 200;
        for (float x = 0- offset; x < Raylib.GetScreenWidth(); x += 200) {
            for (float y = 0; y < Raylib.GetScreenHeight(); y += 200) {
                Raylib.DrawTexture(BackgroundTexture,(int)x,(int)y,Color.White);
            }
        }
    }
    
}