using System;
using System.Numerics;
using System.Windows.Forms;
using System.IO;
using System.Reflection;

namespace rChannelViewer;

using Raylib_cs;

static class Program {
    static Texture2D _backgroundTexture;
    static int _channelMode;

    static Texture2D[] _channels = new Texture2D[5];

    private enum ColorModel {
        hex,
        rgb,
        hsv,
        floatrgb
    };

    private static ColorModel _colorModel = ColorModel.hex;

    // O <- Full image
    // 1 <- Red
    // 2 <- Green
    // 3 <- Blue
    // 4 <- Alpha
    static RenderTexture2D _magnifiedRenderTexture;
    private static bool _enableMagnifiedView = true;
    static Image _image;
    static Font _regularFont;
    static Font _monoFont;
    static Font _boldFont;

    static Camera2D _cam = new Camera2D() {
        Offset = new Vector2(0, 0),
        Target = new Vector2(0, 0),
        Rotation = 0,
        Zoom = 1
    };


    static void Main(string[] args) {
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint);
        Raylib.InitWindow(1280, 720, $"rChannelViewer v{GetBuildInfo()}");
        Image icon = Raylib.LoadImage("icon.png");
        Raylib.SetWindowIcon(icon);
        //Raylib.UnloadImage(icon);
        // Embed build info (version or build datetime) into the window title
        Raylib.SetTargetFPS(120);
        GenerateBackgroundTexture();
        LoadDefaultTexture();
        // Load Roboto Font from Windows Fonts folder
        _regularFont = Raylib.LoadFontEx("Roboto-VariableFont_wdth,wght.ttf", 30, null, 0);
        _boldFont = Raylib.LoadFontEx("Roboto-Bold.ttf", 30, null, 0);
        _monoFont = Raylib.LoadFontEx("RobotoMono-VariableFont_wght.ttf", 30, null, 0);

        _magnifiedRenderTexture = Raylib.LoadRenderTexture(8 * 15, 8 * 15);
        

        // Check if a file path is passed as an argument
        if (args.Length > 0) {
            LoadNewTexture(args[0]);
        }


        while (!Raylib.WindowShouldClose()) {
            // Logic
            UpdateCamera();
            UpdateSelectedChannel();
            UpdateSelectedColorFormat();
            if (Raylib.IsKeyPressed(KeyboardKey.Tab)) {
                _enableMagnifiedView = !_enableMagnifiedView;
            }

            if (Raylib.IsFileDropped()) {
                LoadNewTexture(Raylib.GetDroppedFiles()[0]);
            }

            // Drawing
            Draw();
        }
    }

    // Helper: try to return assembly version or fallback to assembly file last-write timestamp
    private static string GetBuildInfo() {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var loc = asm.Location;

        var dt = File.GetLastWriteTimeUtc(loc).ToLocalTime();
        return dt.ToString("yyyy-MM-dd HH:mm");
    }


    private static void UpdateSelectedColorFormat() {
        if (Raylib.IsKeyPressed(KeyboardKey.Down)) {
            _colorModel += 1;
            if ((int)_colorModel > Enum.GetValues(typeof(ColorModel)).Length - 1) {
                _colorModel = 0;
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Up)) {
            _colorModel -= 1;
            if ((int)_colorModel < 0) {
                _colorModel = (ColorModel)(Enum.GetValues(typeof(ColorModel)).Length - 1);
            }
        }
    }

    private static void UpdateSelectedChannel() {
        if (Raylib.IsKeyPressed(KeyboardKey.F) || Raylib.IsKeyPressed(KeyboardKey.Five)) {
            _channelMode = 0;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.R) || Raylib.IsKeyPressed(KeyboardKey.One)) {
            _channelMode = 1;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.G) || Raylib.IsKeyPressed(KeyboardKey.Two)) {
            _channelMode = 2;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.B) || Raylib.IsKeyPressed(KeyboardKey.Three)) {
            _channelMode = 3;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.A) || Raylib.IsKeyPressed(KeyboardKey.Four)) {
            _channelMode = 4;
        }
    }

    private static void UpdateCamera() {
        // Zoom
        const float sqrt2 = 1.41421356f;
        _cam.Offset = Raylib.GetScreenCenter();
        if (_cam.Zoom <= 30.0f && Raylib.GetMouseWheelMove() > 0) {
            _cam.Target = GetPixelUnderMouse();
            _cam.Zoom *= sqrt2;
            // Adjust target so that the pixel under the mouse remains the same
            Vector2 newMouseWorldPos = GetPixelUnderMouse();
            Vector2 error = newMouseWorldPos - _cam.Target;
            _cam.Target -= error;
        }

        if (Raylib.GetMouseWheelMove() < 0 && _cam.Zoom > 1 / 16f) {
            _cam.Zoom /= sqrt2;
        }

        // Pan
        if (Raylib.IsMouseButtonDown(MouseButton.Left)) {
            Vector2 delta = Raylib.GetMouseDelta();
            _cam.Target -= delta / _cam.Zoom;
        }

        //Jump
        if (Raylib.IsMouseButtonPressed(MouseButton.Right)) {
            _cam.Target = GetPixelUnderMouse();
            //Raylib.SetMousePosition(Raylib.GetScreenWidth()/2, Raylib.GetScreenHeight()/2);
        }

        // Recenter
        if (Raylib.IsKeyPressed(KeyboardKey.Space)) {
            _cam.Target = new(_image.Width / 2, _image.Height / 2);
        }
    }

    private static void Draw() {
        Raylib.BeginDrawing();
        DrawBackground();
        Raylib.BeginMode2D(_cam);
        Raylib.DrawTexture(_channels[_channelMode], 0, 0, Color.White);
        Raylib.EndMode2D();
        DrawUserInterface();
        //Raylib.DrawFPS(10, 10);
        Raylib.EndDrawing();
    }

    private static void DrawUserInterface() {
        // Draw a Gray Rectangle that's 40 pixels high at the bottom of the screen
        const float barHeight = 40;
        Raylib.DrawRectangle(0, Raylib.GetScreenHeight() - 40, Raylib.GetScreenWidth(), (int)barHeight, Color.DarkGray);
        Color[] channelColors = {
            Color.DarkGray,
            Raylib.GetColor(0x880000FF),
            Raylib.GetColor(0x008800FF),
            Raylib.GetColor(0x000088FF),
            Color.Black
        };
        Raylib.DrawRectanglePro(
            new(Raylib.GetScreenWidth() / 2 - 100, Raylib.GetScreenHeight() - barHeight, 200, barHeight), Vector2.Zero, 
            0,channelColors[_channelMode]);
        var topMargin = Raylib.GetScreenHeight() - 40 + 5;
        const float infoUnit = 55;
        const float fontSize = 30;
        //Draw pixel info
        var pixelPos = GetPixelUnderMouse();
        var pixelIsInImage = pixelPos.X >= 0 && pixelPos.Y >= 0 && pixelPos.X < _image.Width &&
                             pixelPos.Y < _image.Height;
        Raylib.TraceLog(TraceLogLevel.Debug, "DrawHud");
        var pixelColor = pixelIsInImage
            ? Raylib.GetImageColor(_image, (int)pixelPos.X, (int)pixelPos.Y)
            : new Color(0, 0, 0, 0);
        DrawPixelColorInfo(pixelColor, infoUnit, topMargin, fontSize);
        if (_enableMagnifiedView) {
            DrawMagnifiedView(barHeight, pixelColor);
        }

        Raylib.DrawTextEx(
            _boldFont,
            "x" + _cam.Zoom.ToString("F2"),
            new Vector2(10, topMargin),
            fontSize, 1, Color.White);
        // Draw Pixel Coordinates in integers in the center of the UI bar
        string coordText = $"{(int)pixelPos.X} \t {(int)pixelPos.Y}";
        // Pixel Coordinates
        Raylib.DrawTextEx(
            _boldFont,
            coordText,
            new Vector2(
                Raylib.GetScreenWidth() / 2 - Raylib.MeasureTextEx(_boldFont, coordText, fontSize, 1).X / 2,
                topMargin),
            fontSize, 1, Color.White);
    }

    private static void DrawMagnifiedView(float barHeight, Color pixelColor) {
        // Draw Zoomed preview of pixel under mouse
        const float magnificationFactor = 8f;
        const int areaSize = 15; // in pixels

        Vector2 mousePixel = GetPixelUnderMouse();
        bool isPixelDark = (pixelColor.R + pixelColor.G + pixelColor.B) / 3 < 128;


        Raylib.BeginTextureMode(_magnifiedRenderTexture);
        Raylib.ClearBackground(Color.Black);
        Raylib.DrawTexturePro(_channels[0],
            new Rectangle(mousePixel.X - areaSize / 2, mousePixel.Y - areaSize / 2, areaSize, -areaSize),
            new Rectangle(0, 0, _magnifiedRenderTexture.Texture.Width, _magnifiedRenderTexture.Texture.Height),
            Vector2.Zero, 0, Color.White);
        // Draw a rectangle around the center pixel
        float pixelSize = magnificationFactor;
        Raylib.DrawRectangleLinesEx(
            new Rectangle(_magnifiedRenderTexture.Texture.Width / 2 - pixelSize / 2,
                _magnifiedRenderTexture.Texture.Height / 2 - pixelSize / 2, pixelSize, pixelSize), 1,
            isPixelDark ? Color.White : Color.Black);
        Raylib.EndTextureMode();
        Raylib.DrawTexture(_magnifiedRenderTexture.Texture,
            Raylib.GetScreenWidth() - _magnifiedRenderTexture.Texture.Width,
            Raylib.GetScreenHeight() - (int)barHeight - _magnifiedRenderTexture.Texture.Height,
            Color.White);
    }

    static string GetChannelInModel(byte x) {
        return _colorModel switch {
            ColorModel.hex => x.ToString("X2"),
            ColorModel.rgb => x.ToString(),
            ColorModel.hsv => (x / 255f * 100f).ToString("N0"), // Placeholder)
            ColorModel.floatrgb => (x / 255f).ToString("F2"),
            _ => x.ToString()
        };
    }

    private static void DrawPixelColorInfo(Color pixelColor, float infoUnit, float topMargin,
        float fontSize) {
        if (_colorModel != ColorModel.hsv) {
            Font font = _colorModel == ColorModel.hex ? _monoFont : _boldFont;
            infoUnit = _colorModel == ColorModel.hex ? Raylib.MeasureText("00",(int)fontSize) : infoUnit;
            // Alpha
            Raylib.DrawTextEx(
                font,
                GetChannelInModel(pixelColor.A),
                new Vector2(Raylib.GetScreenWidth() - infoUnit * 1, topMargin),
                fontSize, 1, Color.White);
            // Blue
            Raylib.DrawTextEx(
                font,
                GetChannelInModel(pixelColor.B),
                new Vector2(Raylib.GetScreenWidth() - infoUnit * 2, topMargin),
                fontSize, 1, Raylib.GetColor(0x9999FFFF));
            // Green
            Raylib.DrawTextEx(
                font,
                GetChannelInModel(pixelColor.G),
                new Vector2(Raylib.GetScreenWidth() - infoUnit * 3, topMargin),
                fontSize, 1, Raylib.GetColor(0x99FF99FF));
            // Red
            Raylib.DrawTextEx(
                font,
                GetChannelInModel(pixelColor.R),
                new Vector2(Raylib.GetScreenWidth() - infoUnit * 4, topMargin),
                fontSize, 1, Raylib.GetColor(0xFF9999FF));
        }
        else {
            var hsv = Raylib.ColorToHSV(pixelColor);
            // Numbers are formatted 0 to 100
            // Alpha
            Raylib.DrawTextEx(
                _regularFont,
                GetChannelInModel(pixelColor.A),
                new Vector2(Raylib.GetScreenWidth() - infoUnit * 1, topMargin),
                fontSize, 1, Color.White);
            // Value
            Raylib.DrawTextEx(
                _regularFont,
                GetChannelInModel((byte)(hsv.Z * 255f)),
                new Vector2(Raylib.GetScreenWidth() - infoUnit * 2, topMargin),
                fontSize, 1, Color.White);
            // Saturation
            Raylib.DrawTextEx(
                _regularFont,
                GetChannelInModel((byte)(hsv.Y * 255f)),
                new Vector2(Raylib.GetScreenWidth() - infoUnit * 3, topMargin),
                fontSize, 1, Color.White);
            // Hue
            Raylib.DrawTextEx(
                _regularFont,
                hsv.X.ToString("N0"),
                new Vector2(Raylib.GetScreenWidth() - infoUnit * 4, topMargin),
                fontSize, 1, hsv.Y>0?Raylib.ColorFromHSV(hsv.X, 0.5f, 1f): Color.LightGray);
        }
    }

    static void GenerateBackgroundTexture() {
        var bgColor1 = Raylib.GetColor(0x222222FF);
        var bgColor2 = Raylib.GetColor(0x111111FF);

        const int bgSize = 50;

        var bgImg = Raylib.GenImageColor(bgSize, bgSize, Color.Magenta);
        for (int x = 0; x < bgSize; x++) {
            for (int y = 0; y < bgSize; y++) {
                Color thisPixelsColors = (x + y) / (bgSize / 2) % 2 == 0 ? bgColor1 : bgColor2;
                Raylib.ImageDrawPixel(ref bgImg, x, y, thisPixelsColors);
            }
        }

        _backgroundTexture = Raylib.LoadTextureFromImage(bgImg);
        Raylib.UnloadImage(bgImg);
    }

    static Vector2 GetPixelUnderMouse() {
        return Raylib.GetScreenToWorld2D(Raylib.GetMousePosition(), _cam);
    }

    static void DrawBackground() {
        // This function draws diagonal stripes in the background.
        float offset = (float)Raylib.GetTime() * 25 % 200;
        float screenX = Raylib.GetScreenWidth();
        float screenY = Raylib.GetScreenHeight();
        Raylib.DrawTexturePro(_backgroundTexture, new(offset, 0, screenX, screenY), new(0, 0, screenX, screenY),
            Vector2.Zero, 0, Color.White);
    }

    static void LoadNewTexture(string path) {
        Image fullTextureImage = Raylib.LoadImage(path);
        if (fullTextureImage.Width == 0 || fullTextureImage.Height == 0) {
            MessageBox.Show("Failed to load image: " + path, "Error", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        Image redChannelImage = Raylib.ImageCopy(fullTextureImage);
        Image greenChannelImage = Raylib.ImageCopy(fullTextureImage);
        Image blueChannelImage = Raylib.ImageCopy(fullTextureImage);
        Image alphaChannelImage = Raylib.ImageCopy(fullTextureImage);
        for (int x = 0; x < fullTextureImage.Width; x++) {
            for (int y = 0; y < fullTextureImage.Height; y++) {
                Color pixelColor = Raylib.GetImageColor(fullTextureImage, x, y);
                // Red channel
                Raylib.ImageDrawPixel(ref redChannelImage, x, y,
                    new Color(pixelColor.R, pixelColor.R, pixelColor.R, (byte)255));
                // Green channel
                Raylib.ImageDrawPixel(ref greenChannelImage, x, y,
                    new Color(pixelColor.G, pixelColor.G, pixelColor.G, (byte)255));
                // Blue channel
                Raylib.ImageDrawPixel(ref blueChannelImage, x, y,
                    new Color(pixelColor.B, pixelColor.B, pixelColor.B, (byte)255));
                // Alpha channel
                Raylib.ImageDrawPixel(ref alphaChannelImage, x, y,
                    new Color(pixelColor.A, pixelColor.A, pixelColor.A, (byte)255));
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
        _cam.Target = new Vector2(fullTextureImage.Width / 2, fullTextureImage.Height / 2);
    }

    static void LoadDefaultTexture() {
        Image fauxFullTexture = Raylib.GenImageColor(1, 1, Color.Blank);
        Image fauxChannelTexture = Raylib.GenImageColor(1, 1, Color.Black);
        _image = fauxChannelTexture;
        _channels[0] = Raylib.LoadTextureFromImage(fauxFullTexture);
        _channels[1] = Raylib.LoadTextureFromImage(fauxChannelTexture);
        _channels[2] = Raylib.LoadTextureFromImage(fauxChannelTexture);
        _channels[3] = Raylib.LoadTextureFromImage(fauxChannelTexture);
        _channels[4] = Raylib.LoadTextureFromImage(fauxChannelTexture);
        Raylib.UnloadImage(fauxFullTexture);
    }
}