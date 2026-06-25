using Godot;

public partial class HudController : Control
{
    private Label _statusLabel;

    public override void _Ready()
    {
        _statusLabel = GetNode<Label>("StatusLabel");
    }

    public void SetStatusText(string text)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = text;
        }
    }
}
