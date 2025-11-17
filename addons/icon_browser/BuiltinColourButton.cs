#if TOOLS
using Godot;

[Tool]
public partial class BuiltinColourButton : Button
{
    public override void _Ready() => Pressed += CopyToClipboard;

    private void CopyToClipboard()
    {
        StyleBoxFlat style = GetThemeStylebox("normal") as StyleBoxFlat;
        DisplayServer.ClipboardSet(style.BgColor.ToHtml(false));
    }
}
#endif
