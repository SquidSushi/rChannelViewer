using System;
using Raylib_cs;

namespace rChannelViewer;
[Serializable]
public class RcvConfig {

    public bool _enableMagnifiedView { get; set; }= true;
    public Program.ColorModel _colorModel { get; set; } = Program.ColorModel.hex;
    public string bg1 { get; set; } = "111111";
    public string bg2 { get; set; } = "222222";
}