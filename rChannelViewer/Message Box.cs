using System;
using System.Numerics;

namespace rChannelViewer;

using Raylib_cs;

static class RaylibHelpers {
    // This function will draw text inside a box with word wrapping and spacing
    public static string WrapText(ref Font font, string text, float maxWidth, int fontSize, int spacing)
    {
        // Split into paragraphs by manual newlines
        var paragraphs = text.Split('\n');
        var wrappedText = new System.Text.StringBuilder();

        int spaceWidth = (int)Raylib.MeasureTextEx(font, " ", fontSize, spacing).X;

        foreach (var paragraph in paragraphs)
        {
            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int remainingWidth = (int)maxWidth;

            foreach (var word in words)
            {
                int wordWidth = (int)Raylib.MeasureTextEx(font, word, fontSize, spacing).X;

                // Check if word fits in current line
                if (wordWidth > remainingWidth)
                {
                    // Start a new line
                    wrappedText.Append('\n');
                    remainingWidth = (int)maxWidth - wordWidth - spaceWidth;
                }
                else
                {
                    if (remainingWidth < (int)maxWidth) // not first word
                        wrappedText.Append(' ');
                    remainingWidth -= wordWidth + spaceWidth;
                }

                wrappedText.Append(word);
            }

            wrappedText.Append('\n'); // preserve paragraph break
        }

        return wrappedText.ToString();
    }


    public static void DrawTextBoxed(ref Font font, string text, Rectangle box, int fontSize, int spacing,
        Color textColor, bool wordWrap)
    {
        string finalText = wordWrap ? WrapText(ref font, text, box.Width, fontSize, spacing) : text;

        // Split final text into lines (already has proper \n’s)
        var lines = finalText.Split('\n');
        float y = box.Y;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                y += fontSize + spacing;
                continue;
            }

            Raylib.DrawTextEx(font, line, new Vector2(box.X, y), fontSize, spacing, textColor);
            y += fontSize + spacing;
        }
    }


}

public class Message_Box(Font font) {
    
    bool _active = false;
    float _t = 0.0f; // How much % the box is open
    const float _openCloseTime = 0.3f; //time it takes to open/close the box
    string _currentMessage = "";
    float _remainingTime = 0.0f; //time left to show message
    Font _font = font;
    
    public void DisplayMessage(string message, float displayTime) {
        _currentMessage = message;
        _remainingTime = displayTime;
        _t = 0.0f;
        _active = true;
    }

    public void Update() {
        if (_active) {
            if (_t < 1.0f) {
                _t += Raylib.GetFrameTime() / _openCloseTime;
                if (_t > 1.0f) _t = 1.0f;
            }
            if (_remainingTime > 0.0f) {
                _remainingTime -= Raylib.GetFrameTime();
                if (_remainingTime <= 0.0f) {
                    _active = false;
                }
            }
        } else {
            if (_t > 0.0f) {
                _t -= Raylib.GetFrameTime() / _openCloseTime;
                if (_t < 0.0f) _t = 0.0f;
            }
        }
    }
    public void Draw() {
        var TextSize = Raylib.MeasureTextEx(_font, _currentMessage, 20, 2);
        float screenHeight = Raylib.GetScreenHeight();
        Rectangle boxRect = new Rectangle(0, screenHeight/2 - (TextSize.Y/2 + 10) * _t, Raylib.GetScreenWidth(), (TextSize.Y + 20) * _t);
        Raylib.DrawRectangleRec(boxRect, new Color(0,0,0, (int)(200 * _t)));
        Raylib.DrawTextEx(_font, _currentMessage, new Vector2(Raylib.GetScreenWidth()/2 - TextSize.X/2, Raylib.GetScreenHeight()/2 - TextSize.Y/2), 20, 2, Raylib.ColorAlpha(Color.White, _t));
    }
}