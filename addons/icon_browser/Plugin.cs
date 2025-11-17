#if TOOLS
using Godot;

[Tool]
public partial class Plugin : EditorPlugin
{
    private Control dock;

    public override void _EnterTree()
    {
        dock = GD.Load<PackedScene>("res://addons/icon_browser/IconBrowser.tscn")
            .Instantiate<Control>();
        AddControlToDock(DockSlot.RightBl, dock);
    }

    public override void _ExitTree()
    {
        RemoveControlFromDocks(dock);
        dock.Free();
    }
}
#endif
