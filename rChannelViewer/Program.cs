using System;
using System.Collections;
using System.Numerics;

namespace rChannelViewer;
using Raylib_cs;
static class Program {
    static Texture2D _backgroundTexture;
    static int _channelMode = 0;
    static Texture2D[] _channels = new Texture2D[5];
    private static Image _image;
    // O <- Full image
    // 1 <- Red
    // 2 <- Green
    // 3 <- Blue
    // 4 <- Alpha
    static Camera2D _cam = new Camera2D() {
        Offset = new Vector2(0,0),
        Target = new Vector2(0,0),
        Rotation = 0,
        Zoom = 1
    };
    
    
    static void Main() {
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
        Raylib.InitWindow(800,600,"rChannelViewer");
        Raylib.SetTargetFPS(120);
        GenerateBackgroundTexture();
        LoadDefaultTexture();

        // for testing purposes I'm actually loading an image here.
        LoadNewTexture("Zygarde.jpg"); //TODO remove test image load
        
        
        
        while (!Raylib.WindowShouldClose()) {
            //Logic
            UpdateCamera();
            UpdateSelectedChannel();

            // Drawing
            Draw();
        }
    }

    private static void UpdateSelectedChannel() {
        if (Raylib.IsKeyPressed(KeyboardKey.F)) {
            _channelMode = 0;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.R)) {
            _channelMode = 1;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.G)) {
            _channelMode = 2;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.B)) {
            _channelMode = 3;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.A)) {
            _channelMode = 4;
        }
    }

    private static void UpdateCamera() {
        // Zoom
        _cam.Offset = Raylib.GetScreenCenter();
        if (_cam.Zoom < 16f && Raylib.GetMouseWheelMove() > 0) {
            _cam.Zoom *= 2;
        }
        if (Raylib.GetMouseWheelMove() < 0 && _cam.Zoom > 1/16f) {
            _cam.Zoom /= 2;
        }
        // Pan
        if (Raylib.IsMouseButtonDown(MouseButton.Left)) {
            Vector2 delta = Raylib.GetMouseDelta();
            _cam.Target = _cam.Target - delta / _cam.Zoom;
        }
    }

    private static void Draw() {
        Raylib.BeginDrawing();
        DrawBackground();
        Raylib.BeginMode2D(_cam);
        Raylib.DrawTexture(_channels[_channelMode],0,0,Color.White);
        Raylib.EndMode2D();
        DrawUI();
        Raylib.DrawFPS(10,10);
        Raylib.EndDrawing();
    }

    private static void DrawUI() {
        // Draw a Gray Rectangle that's 40 pixels high at the bottom of the screen
        Raylib.DrawRectangle(0,Raylib.GetScreenHeight()-40,Raylib.GetScreenWidth(),40,Color.DarkGray);
    }

    static void GenerateBackgroundTexture() {
        Color bgColor1 = Raylib.GetColor(0x0c1021ff);
        Color bgColor2 = Raylib.GetColor(0x192247ff);

        const int bgSize = 200;
        
        Image bgImg = Raylib.GenImageColor(bgSize, bgSize, Color.Magenta);
        for (int x = 0; x < bgSize; x++) {
            for (int y = 0; y < bgSize; y++) {
                Color thisPixelsColors = (x + y) / (bgSize/2) % 2 == 0 ? bgColor1 : bgColor2;
                Raylib.ImageDrawPixel(ref bgImg,x,y,thisPixelsColors);
            }
        }
        _backgroundTexture = Raylib.LoadTextureFromImage(bgImg);
        Raylib.UnloadImage(bgImg);
    }

    static Vector2 GetPixelUnderMouse() {
        return Raylib.GetScreenToWorld2D(Raylib.GetMousePosition(), _cam);;
    }
    
    static void DrawBackground() {
        // This function draws diagonal stripes in the background.
        float offset = (float)Raylib.GetTime() * 100 % 200;
        float screenX = Raylib.GetScreenWidth();
        float screenY = Raylib.GetScreenHeight();
        Raylib.DrawTexturePro(_backgroundTexture, new (offset, 0, screenX,screenY),new (0,0,screenX,screenY),Vector2.Zero,0,Color.White);
    }

    static void LoadNewTexture(string path) {
        Image fullTextureImage = Raylib.LoadImage(path);
        Image redChannelImage = Raylib.ImageCopy(fullTextureImage);
        Image greenChannelImage = Raylib.ImageCopy(fullTextureImage);
        Image blueChannelImage = Raylib.ImageCopy(fullTextureImage);
        Image alphaChannelImage = Raylib.ImageCopy(fullTextureImage);
        for (int x = 0; x < fullTextureImage.Width; x++) {
            for (int y = 0; y < fullTextureImage.Height; y++) {
                Color pixelColor = Raylib.GetImageColor(fullTextureImage, x, y);
                // Red channel
                Raylib.ImageDrawPixel(ref redChannelImage, x, y, new Color(pixelColor.R, pixelColor.R, pixelColor.R, (byte)255));
                // Green channel
                Raylib.ImageDrawPixel(ref greenChannelImage, x, y, new Color(pixelColor.G, pixelColor.G, pixelColor.G, (byte)255));
                // Blue channel
                Raylib.ImageDrawPixel(ref blueChannelImage, x, y, new Color(pixelColor.B, pixelColor.B, pixelColor.B, (byte)255));
                // Alpha channel
                Raylib.ImageDrawPixel(ref alphaChannelImage, x, y, new Color(pixelColor.A, pixelColor.A, pixelColor.A, (byte)255));
            }
        }
        _channels[0] = Raylib.LoadTextureFromImage(fullTextureImage);
        _channels[1] = Raylib.LoadTextureFromImage(redChannelImage);
        _channels[2] = Raylib.LoadTextureFromImage(greenChannelImage);
        _channels[3] = Raylib.LoadTextureFromImage(blueChannelImage);
        _channels[4] = Raylib.LoadTextureFromImage(alphaChannelImage);
        Raylib.UnloadImage(_image);
        _image = fullTextureImage;
        Raylib.UnloadImage(redChannelImage);
        Raylib.UnloadImage(greenChannelImage);
        Raylib.UnloadImage(blueChannelImage);
        Raylib.UnloadImage(alphaChannelImage);
        _cam.Target = new Vector2(fullTextureImage.Width/2, fullTextureImage.Height/2);
    }

    static void LoadDefaultTexture() {
        Image fauxFullTexture = Raylib.GenImageColor(100, 100, Color.Blank);
        Image fauxChannelTexture = Raylib.GenImageColor(100, 100, Color.Black);
        _image = fauxChannelTexture;
        _channels[0] = Raylib.LoadTextureFromImage(fauxFullTexture);
        _channels[1] = Raylib.LoadTextureFromImage(fauxChannelTexture);
        _channels[2] = Raylib.LoadTextureFromImage(fauxChannelTexture);
        _channels[3] = Raylib.LoadTextureFromImage(fauxChannelTexture);
        _channels[4] = Raylib.LoadTextureFromImage(fauxChannelTexture);
        Raylib.UnloadImage(fauxFullTexture);
    }
}