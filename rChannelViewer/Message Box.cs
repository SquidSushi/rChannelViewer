using System;
using System.Numerics;

namespace rChannelViewer;

using Raylib_cs;

static class RaylibHelpers {
    // This function will draw text inside a box with word wrapping and spacing
    public static void DrawTextBoxed(ref Font font, string text, Rectangle box, int fontSize, int spacing,
        Color textColor, bool wordWrap) {
        // Pre measure text
        // Split it into words and save it in a tupel list with their width
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var wordSizes = new System.Collections.Generic.List<(string word, int width)>();
        foreach (var word in words) {
            int wordWidth = (int)Raylib.MeasureTextEx(font, word, fontSize, spacing).X;
            wordSizes.Add((word, wordWidth));
        }

        int spaceWidth = (int)Raylib.MeasureTextEx(font, " ", fontSize, spacing).X;
        int remainingWidth = (int)box.Width;
        string line = "";
        foreach (var w in wordSizes) {
            if (wordWrap && w.width > remainingWidth) {
                // Draw the current line
                Raylib.DrawTextEx(font, line, new Vector2(box.X, box.Y), fontSize, spacing, textColor);
                box.Y += fontSize + spacing;
                // Start a new line
                line = w.word + " ";
                remainingWidth = (int)box.Width - w.width - spaceWidth;
            }
            else {
                line += w.word + " ";
                remainingWidth -= w.width + spaceWidth;
            }
        }

        // Draw the last line
        if (line.Length > 0) {
            Raylib.DrawTextEx(font, line, new Vector2(box.X, box.Y), fontSize, spacing, textColor);
        }
    }
}

public class Message_Box {
}