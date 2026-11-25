using Godot;

public partial class SoundEffectsVolumeControl : VBoxContainer
{
    public override void _Ready()
    {
        var audio = GetNode<AudioManager>("/root/AudioManager");
        var slider = GetNode<HSlider>("Slider");
        var label = GetNode<Label>("Row/Value");
        TacticalTheme.ApplyLabel(GetNode<Label>("Row/Title"), TacticalTheme.Parchment, 16);
        TacticalTheme.ApplyLabel(label, TacticalTheme.BrassBright, 16);
        slider.Value = audio.SoundEffectsVolume * 100f;
        label.Text = $"{slider.Value:0}%";
        slider.ValueChanged += value =>
        {
            audio.SetSoundEffectsVolume((float)value / 100f);
            label.Text = $"{value:0}%";
        };
    }
}
