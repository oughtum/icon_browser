#if TOOLS
using Godot;

[Tool]
public partial class IconColourPicker : HBoxContainer
{
    [Export]
    public Button IconButton;

    [Export]
    public LineEdit IconNameInput;

    [Export]
    public SpinBox IconSizeInput;

    [Export]
    public ColorPickerButton colourPickerBtn;

    [Export]
    private Button removeBtn;

    public override void _Ready() => removeBtn.Pressed += QueueFree;
}
#endif
