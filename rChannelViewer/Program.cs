using System;
using System.Numerics;
using System.Windows.Forms;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;

// dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=false 

namespace rChannelViewer;

using Raylib_cs;

public static class Program {
    static Texture2D _backgroundTexture;
    static int _channelMode;
    private static Message_Box _messageBox;
    static Texture2D[] _channels = new Texture2D[5];

    public enum ColorFormat {
        hex,
        rgb,
        hsv,
        floatrgb
    };
    public enum CoordinateFormat {
        absolute,
        relative,
        absoluteUp,
        relativeUp
    }

    private static ColorFormat _colorFormat = ColorFormat.hex;
    private static CoordinateFormat _coordinateFormat = CoordinateFormat.absolute;

    // O <- Full image
    // 1 <- Red
    // 2 <- Green
    // 3 <- Blue
    // 4 <- Alpha
    static RenderTexture2D _magnifiedRenderTexture;
    private static bool _enableMagnifiedView = true;
    private static Color _bg1 = Raylib.GetColor(0x222222FF);
    private static Color _bg2 = Raylib.GetColor(0x111111FF);

    static Image _image;

    //static Font _regularFont;
    static Font _monoFont;
    static Font _regularFont;

    static bool _helpVisible = false;

    static Camera2D _cam = new Camera2D() {
        Offset = new Vector2(0, 0),
        Target = new Vector2(0, 0),
        Rotation = 0,
        Zoom = 1
    };

    static string ColorToHex(Color color) {
        return color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
    }

    static Color HexToColor(string hex) {
        if (hex.Length != 6) return Color.Magenta;
        byte r = Convert.ToByte(hex.Substring(0, 2), 16);
        byte g = Convert.ToByte(hex.Substring(2, 2), 16);
        byte b = Convert.ToByte(hex.Substring(4, 2), 16);
        return new Color(r, g, b, (byte)255);
    }

    static void Main(string[] args) {
        // Ensure resources (fonts, config, icon.png, etc.) are loaded from the executable directory
        // even when the app is launched via "Open with" which changes the working directory to the
        // file being opened. We also normalize any incoming file argument to an absolute path so
        // it still resolves correctly after we change the current directory.
        string initialWorkingDir = Directory.GetCurrentDirectory();
        if (args.Length > 0) {
            for (int i = 0; i < args.Length; i++) {
                try {
                    if (!Path.IsPathRooted(args[i])) {
                        args[i] = Path.GetFullPath(args[i], initialWorkingDir);
                    } else {
                        args[i] = Path.GetFullPath(args[i]);
                    }
                }
                catch {
                    // If path normalization fails, leave the arg as-is (later code will handle errors)
                }
            }
        }

        // Record the executable directory and prefer explicit paths when loading resources
        string exeDir = AppContext.BaseDirectory;

        try {
            Directory.SetCurrentDirectory(exeDir);
        }
        catch {
            // If we can't change the directory for any reason, continue — relative loads may fail.
        }

        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
        Raylib.InitWindow(1280, 720, $"rChannelViewer v{GetBuildInfo()}");
        // Load icon explicitly from the exe folder
        string iconPath = Path.Combine(exeDir, "icon.png");
        if (File.Exists(iconPath)) {
            Image icon = Raylib.LoadImage(iconPath);
            Raylib.SetWindowIcon(icon);
            Raylib.UnloadImage(icon);
        }
        Raylib.SetTargetFPS(120);
        // Look for a config.json file in the executable directory
        string configPath = Path.Combine(exeDir, "config.json");
        if (File.Exists(configPath)) {
            // Load config
            try {
                string configJson = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<RcvConfig>(configJson);
                if (config != null) {
                    _enableMagnifiedView = config._enableMagnifiedView;
                    _colorFormat = config.ColorFormat;
                    _coordinateFormat = config.CoordinateFormat;
                    GenerateBackgroundTexture(HexToColor(config.bg1), HexToColor(config.bg2));
                }
            }
            catch {
                GenerateBackgroundTexture(Raylib.GetColor(0x222222FF), Raylib.GetColor(0x111111FF));
            }
        }
        else {
            GenerateBackgroundTexture(Raylib.GetColor(0x222222FF), Raylib.GetColor(0x111111FF));
        }


        LoadDefaultTexture();
        // Load fonts explicitly from the exe folder so this works regardless of current directory
        _regularFont = Raylib.LoadFontEx(Path.Combine(exeDir, "Roboto-Bold.ttf"), 30, null, 0);
        _monoFont = Raylib.LoadFontEx(Path.Combine(exeDir, "RobotoMono-VariableFont_wght.ttf"), 30, null, 0);
        _messageBox = new Message_Box(_regularFont);
        _messageBox.SetMessage(
            "Welcome to rChannelViewer!\nPress F1 for help.\nDrag & drop a file into this window to open it.", 5.0f);
        _magnifiedRenderTexture = Raylib.LoadRenderTexture(8 * 15 * 2, 8 * 15 * 2);


        // Check if a file path is passed as an argument
        if (args.Length > 0) {
            LoadNewTexture(args[0]);
        }


        while (!Raylib.WindowShouldClose()) {
            // Logic
            UpdateCamera();
            UpdateSelectedChannel();
            UpdateSelectedColorFormat();
            UpdateSelectedCoordinateFormat();
            _messageBox.Update();
            if (Raylib.IsKeyPressed(KeyboardKey.Tab)) {
                _enableMagnifiedView = !_enableMagnifiedView;
            }

            if (Raylib.IsKeyPressed(KeyboardKey.F1)) {
                _helpVisible = !_helpVisible;
            }

            if (Raylib.IsFileDropped()) {
                LoadNewTexture(Raylib.GetDroppedFiles()[0]);
            }

            // Drawing
            Draw();
        }

        // Save config on exit to the executable directory
        var saveConfig = new RcvConfig {
            _enableMagnifiedView = _enableMagnifiedView,
            ColorFormat = _colorFormat,
            CoordinateFormat = _coordinateFormat,
            bg1 = ColorToHex(_bg1),
            bg2 = ColorToHex(_bg2)
        };
        string saveJson = JsonSerializer.Serialize(saveConfig);
        File.WriteAllText(configPath, saveJson);
    }

    private static void UpdateSelectedCoordinateFormat() {
        if (Raylib.IsKeyPressed(KeyboardKey.Right)) {
            _coordinateFormat++;
            if ((int)_coordinateFormat > Enum.GetValues(typeof(CoordinateFormat)).Length - 1) {
                _coordinateFormat = 0;
            }
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Left)) {
            _coordinateFormat--;
            if ((int)_coordinateFormat < 0) {
                _coordinateFormat = (CoordinateFormat)(Enum.GetValues(typeof(CoordinateFormat)).Length - 1);
            }
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
            _colorFormat += 1;
            if ((int)_colorFormat > Enum.GetValues(typeof(ColorFormat)).Length - 1) {
                _colorFormat = 0;
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Up)) {
            _colorFormat -= 1;
            if ((int)_colorFormat < 0) {
                _colorFormat = (ColorFormat)(Enum.GetValues(typeof(ColorFormat)).Length - 1);
            }
        }
    }

    private static void UpdateSelectedChannel() {
        if (Raylib.IsKeyPressed(KeyboardKey.Five)) {
            _channelMode = 0;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.One)) {
            _channelMode = 1;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Two)) {
            _channelMode = 2;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Three)) {
            _channelMode = 3;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Four)) {
            _channelMode = 4;
        }
    }

    private static void UpdateCamera() {
        _cam.Offset = Raylib.GetScreenCenter();
        if (_cam.Zoom <= 30.0f && Raylib.GetMouseWheelMove() > 0) {
            _cam.Target = GetPixelUnderMouse();
            _cam.Zoom *= 2;
            // Adjust target so that the pixel under the mouse remains the same
            Vector2 newMouseWorldPos = GetPixelUnderMouse();
            Vector2 error = newMouseWorldPos - _cam.Target;
            _cam.Target -= error;
        }

        if (Raylib.GetMouseWheelMove() < 0 && _cam.Zoom > 1 / 64f) {
            _cam.Target = GetPixelUnderMouse();
            _cam.Zoom /= 2;
            // Adjust target so that the pixel under the mouse remains the same
            Vector2 newMouseWorldPos = GetPixelUnderMouse();
            Vector2 error = newMouseWorldPos - _cam.Target;
            _cam.Target -= error;
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
        // Boxed Text test
        string loremIpsum =
            "lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.";
        //RaylibHelpers.DrawTextBoxed(ref _regularFont, loremIpsum, new Rectangle(0, 0, Raylib.GetMouseX(), Raylib.GetMouseY()), 20, 2, Color.White, true);
        Raylib.EndDrawing();
    }

    private static void DrawUserInterface() {
        if (!_helpVisible) {
            // Draw a Gray Rectangle that's 40 pixels high at the bottom of the screen
            const float barHeight = 40;
            Raylib.DrawRectangle(0, Raylib.GetScreenHeight() - (int)barHeight, Raylib.GetScreenWidth(), (int)barHeight,
                Color.DarkGray);

            Color[] channelColors = {
                Color.DarkGray,
                Raylib.GetColor(0x880000FF),
                Raylib.GetColor(0x008800FF),
                Raylib.GetColor(0x000088FF),
                Color.Black
            };
            Raylib.DrawRectanglePro(
                new(Raylib.GetScreenWidth() / 2 - 150, Raylib.GetScreenHeight() - barHeight, 300, barHeight),
                Vector2.Zero,
                0, channelColors[_channelMode]);
            Raylib.DrawRectangleGradientV(0, Raylib.GetScreenHeight() - (int)barHeight, Raylib.GetScreenWidth(), 4,
                Raylib.GetColor(0x111111ff), Color.Blank);
            var textTopMargin = Raylib.GetScreenHeight() - 40 + 5;
            const float infoUnit = 55;
            const float fontSize = 30;
            //Draw pixel info
            var pixelPos = GetPixelUnderMouse();
            var pixelIsInImage = pixelPos is { X: >= 0, Y: >= 0 } && pixelPos.X < _image.Width &&
                                 pixelPos.Y < _image.Height;
            //Raylib.TraceLog(TraceLogLevel.Debug, "DrawHud");
            var pixelColor = pixelIsInImage
                ? Raylib.GetImageColor(_image, (int)pixelPos.X, (int)pixelPos.Y)
                : new Color(0, 0, 0, 0);
            DrawPixelColorInfo(pixelColor, infoUnit, textTopMargin, fontSize);

            if (Raylib.IsKeyDown(KeyboardKey.LeftAlt) || Raylib.IsKeyDown(KeyboardKey.RightAlt) && pixelIsInImage) {
                DrawMagnifiedView();
                if (Raylib.IsMouseButtonPressed(MouseButton.Left)) {
                    PickColor();
                }
            }

            string zoomText = "x";
            zoomText += (_cam.Zoom < 1) ? "1/" + (1 / _cam.Zoom) : _cam.Zoom.ToString("F2");
            Raylib.DrawTextEx(
                _regularFont, zoomText,
                new Vector2(10, textTopMargin),
                fontSize, 1, Color.White);
            // Draw Pixel Coordinates in integers in the center of the UI bar
            var coordText = CoordText(pixelPos);
            // Pixel Coordinates
            Raylib.DrawTextEx(
                _regularFont,
                coordText,
                new Vector2(
                    Raylib.GetScreenWidth() / 2 - Raylib.MeasureTextEx(_regularFont, coordText, fontSize, 1).X / 2,
                    textTopMargin),
                fontSize, 1, Color.White);
            _messageBox.Draw();
        }
        else {
            Raylib.DrawRectangle(20, 20, Raylib.GetScreenWidth() - 40, Raylib.GetScreenHeight() - 40, _bg1);
            Raylib.DrawRectangleLinesEx(
                new Rectangle(20, 20, Raylib.GetScreenWidth() - 40, Raylib.GetScreenHeight() - 40), 2, Color.White);
            string helpText = "Welcome to rChannelViewer!\n\n" +
                              "Controls:\n" +
                              "- F1: Toggle this help\n" +
                              "- Mouse Wheel: Zoom in/out\n" +
                              "- Left Mouse Button: Pan\n" +
                              "- Right Mouse Button: Jump to pixel under mouse\n" +
                              "- Space: Recenter view\n" +
                              "- 1/2/3/4/5: Switch to Red/Green/Blue/Alpha/Full channel view\n" +
                              "- Up/Down Arrow: Change color format (Hex, RGB, HSV, Float RGB)\n" +
                              "- Left/Right Arrow: Change coordinate format (WIP)\n" + //TODO implement coordinate format change
                              "- Alt: Hold for magnified view\n" +
                              "- Alt+LMB: Pick color from image in the active color format\n" + 
                              "- Esc: Terminate the app\n\n" +
                              "Drag and drop an image file onto the window to load it.\n";
            RaylibHelpers.DrawTextBoxed(ref _regularFont, helpText,
                new Rectangle(40, 40, Raylib.GetScreenWidth() - 80, Raylib.GetScreenHeight() - 80), 24, 1,
                Color.White, true);
        }
    }

    private static string CoordText(Vector2 pixelPos) {
        switch (_coordinateFormat) {
            case CoordinateFormat.absolute:
            default:
                return $"{(int)pixelPos.X} : {(int)pixelPos.Y}";
            case CoordinateFormat.relative:
                return $"{(pixelPos.X / _image.Width):G4} : {(pixelPos.Y / _image.Height):G4}";
            case CoordinateFormat.absoluteUp:
                return $"{(int)pixelPos.X} : {(int)(_image.Height - pixelPos.Y)}";
            case CoordinateFormat.relativeUp:
                return $"{(pixelPos.X / _image.Width):G5} : {((_image.Height - pixelPos.Y) / _image.Height):G5}";
        }
    }

    private static void PickColor() {
        Vector2 mousePixel = GetPixelUnderMouse();
        Color pixelColor = Raylib.GetImageColor(_image, (int)mousePixel.X, (int)mousePixel.Y);
        string pickedColorText = "";
        switch (_colorFormat) {
            case ColorFormat.hex:
            default:
                pickedColorText = $"#{pixelColor.R:X2}{pixelColor.G:X2}{pixelColor.B:X2}{pixelColor.A:X2}";
                break;
            case ColorFormat.rgb:
                pickedColorText = $"{pixelColor.R}, {pixelColor.G}, {pixelColor.B}, {pixelColor.A}";
                break;
            case ColorFormat.hsv:
                var hsv = Raylib.ColorToHSV(pixelColor);
                pickedColorText = $"{hsv.X:N0}, {(hsv.Y * 100f):N0}%, {(hsv.Z * 100f):N0}%, {pixelColor.A / 255f * 100:N2}";
                break;
            case ColorFormat.floatrgb:
                pickedColorText = $"{(pixelColor.R / 255f):F2}, {(pixelColor.G / 255f):F2}, {(pixelColor.B / 255f):F2,} {(pixelColor.A / 255f):F2}";
                break;
        }
        Raylib.SetClipboardText(pickedColorText);
        _messageBox.SetMessage($"Picked color: {pickedColorText}\n(Copied to clipboard)", 2.0f);
    }

    private static void DrawMagnifiedView() {
        // Draw Zoomed preview of pixel under mouse
        const float magnificationFactor = 16f;

        Vector2 mousePixel = GetPixelUnderMouse();
        Color pixelColor = Raylib.GetImageColor(_image, (int)mousePixel.X, (int)mousePixel.Y);
        bool isPixelDark = (pixelColor.R + pixelColor.G + pixelColor.B) / 3 < 128;


        Raylib.BeginTextureMode(_magnifiedRenderTexture);
        Raylib.ClearBackground(Color.Blank);
        Raylib.BeginBlendMode(BlendMode.Alpha);
        Raylib.DrawTexturePro(_backgroundTexture,
            new Rectangle((float)Raylib.GetTime() * 25 % 200, 0, _magnifiedRenderTexture.Texture.Width,
                _magnifiedRenderTexture.Texture.Height),
            new Rectangle(0, 0, _magnifiedRenderTexture.Texture.Width, _magnifiedRenderTexture.Texture.Height),
            Vector2.Zero, 0, Color.White);
        Camera2D magnifyCam = new Camera2D() {
            Offset = new Vector2(_magnifiedRenderTexture.Texture.Width / 2,
                _magnifiedRenderTexture.Texture.Height / 2),
            Target = new Vector2(mousePixel.X + 0.5f, mousePixel.Y + 0.5f),
            Rotation = 0,
            Zoom = magnificationFactor
        };
        Raylib.BeginMode2D(magnifyCam);

        Raylib.DrawTexturePro(_channels[0],
            new Rectangle(0, 0, _image.Width, _image.Height),
            new Rectangle(0, 0, _image.Width, _image.Height),
            Vector2.Zero, 0, Color.White);
        Raylib.EndMode2D();
        // Draw a rectangle around the center pixel
        float pixelSize = magnificationFactor;
        Raylib.DrawRectangleLinesEx(
            new Rectangle(_magnifiedRenderTexture.Texture.Width / 2 - pixelSize / 2,
                _magnifiedRenderTexture.Texture.Height / 2 - pixelSize / 2, pixelSize, pixelSize), 1,
            isPixelDark ? Color.White : Color.Black);
        Raylib.EndBlendMode();
        Raylib.EndTextureMode();
        // Draw magnified texture right on the mouse Cursor
        Vector2 mousePos = Raylib.GetMousePosition();
        Rectangle destRect = new Rectangle(mousePos.X - _magnifiedRenderTexture.Texture.Width / 2,
            mousePos.Y - _magnifiedRenderTexture.Texture.Height / 2, _magnifiedRenderTexture.Texture.Width,
            _magnifiedRenderTexture.Texture.Height);
        Raylib.DrawTexturePro(_magnifiedRenderTexture.Texture,
            new Rectangle(0, 0, _magnifiedRenderTexture.Texture.Width, -_magnifiedRenderTexture.Texture.Height),
            destRect, Vector2.Zero, 0, Color.White);
        const float borderThickness = 8;
        destRect.X -= borderThickness;
        destRect.Y -= borderThickness;
        destRect.Width += borderThickness * 2;
        destRect.Height += borderThickness * 2;
        Raylib.DrawRectangleLinesEx(destRect, borderThickness, pixelColor with { A = 255 });
    }

    static string GetChannelInModel(byte x) {
        return _colorFormat switch {
            ColorFormat.hex => x.ToString("X2"),
            ColorFormat.rgb => x.ToString(),
            ColorFormat.hsv => (x / 255f * 100f).ToString("N0"), // Placeholder)
            ColorFormat.floatrgb => (x / 255f).ToString("F2"),
            _ => x.ToString()
        };
    }

    private static void DrawPixelColorInfo(Color pixelColor, float infoUnit, float topMargin,
        float fontSize) {
        if (_colorFormat != ColorFormat.hsv) {
            Font font = _colorFormat == ColorFormat.hex ? _monoFont : _regularFont;
            infoUnit = _colorFormat == ColorFormat.hex
                ? Raylib.MeasureTextEx(_monoFont, "00", fontSize, 1).X
                : infoUnit;
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
                fontSize, 1, hsv.Y > 0 ? Raylib.ColorFromHSV(hsv.X, 0.5f, 1f) : Color.LightGray);
        }
    }

    static void GenerateBackgroundTexture(Color bgColor1, Color bgColor2) {
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
        Vector2 r = Raylib.GetScreenToWorld2D(Raylib.GetMousePosition(), _cam);
        return r with { X = (int)r.X, Y = (int)r.Y };
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
            _messageBox.SetMessage("Failed to load image: " + path, 8.0f);
            return;
        }

        int totalPixels = fullTextureImage.Width * fullTextureImage.Height;
        int processedPixels = 0;
        Image redChannelImage = Raylib.ImageCopy(fullTextureImage);
        Image greenChannelImage = Raylib.ImageCopy(fullTextureImage);
        Image blueChannelImage = Raylib.ImageCopy(fullTextureImage);
        Image alphaChannelImage = Raylib.ImageCopy(fullTextureImage);
        for (int x = 0; x < fullTextureImage.Width; x++) {
            for (int y = 0; y < fullTextureImage.Height; y++) {
                if (processedPixels % 100_000 == 99999) {
                    float progress = processedPixels / (float)totalPixels;
                    DrawLoadingScreen(progress);
                }

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
                processedPixels++;
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
        foreach (var t in _channels) {
            Raylib.SetTextureWrap(t, TextureWrap.Clamp);
        }

        _cam.Target = new Vector2(fullTextureImage.Width / 2, fullTextureImage.Height / 2);
        float zoomX = Raylib.GetScreenWidth() - 100 / (float)fullTextureImage.Width;
        float zoomY = (Raylib.GetScreenHeight() - 100) / (float)fullTextureImage.Height;
        float favoredZoom = MathF.Min(zoomX, zoomY);
        for (float z = 64; z > favoredZoom && z > 1 / 16f; z /= 2) {
            _cam.Zoom = z / 2;
        }

        _messageBox.SetMessage("Succesfully opened image.", 1.0f);
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

    static void DrawLoadingScreen(float progress) {
        Raylib.BeginDrawing();
        DrawBackground();
        const float barWidth = 400;
        const float barHeight = 30;
        float x = (Raylib.GetScreenWidth() - barWidth) / 2;
        float y = (Raylib.GetScreenHeight() - barHeight) / 2;
        Raylib.DrawRectangle((int)x - 5, (int)y - 5, (int)barWidth + 10, (int)barHeight + 10, Color.White);
        Raylib.DrawRectangle((int)x, (int)y, (int)barWidth, (int)barHeight, Color.DarkGray);
        Raylib.DrawRectangle((int)x, (int)y, (int)(barWidth * progress), (int)barHeight, Color.LightGray);
        Raylib.EndDrawing();
    }
}