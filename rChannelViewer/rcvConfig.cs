using System;
using Raylib_cs;

namespace rChannelViewer;
[Serializable]
public class RcvConfig {

    public bool _enableMagnifiedView { get; set; }= true;

    public Program.ColorFormat ColorFormat { get; set; } = Program.ColorFormat.hex;
    public Program.CoordinateFormat CoordinateFormat { get; set; } = Program.CoordinateFormat.absolute;
    public string bg1 { get; set; } = "111111";
    public string bg2 { get; set; } = "222222";
}