using Godot;
using Godot.Collections;

public partial class DialogThemeChecks : Node
{
    public Array<string> Run()
    {
        var failures = new Array<string>();
        var dialog = new ConfirmationDialog();
        dialog.DialogText = "Permanently delete this save? This cannot be undone.";
        AddChild(dialog);
        TacticalTheme.ApplyDialog(dialog);

        if (dialog.GetLabel().HorizontalAlignment != HorizontalAlignment.Center
            || dialog.GetLabel().VerticalAlignment != VerticalAlignment.Center)
        {
            failures.Add("Confirmation dialog message must be centered");
        }

        if (dialog.GetOkButton().GetParent() is not BoxContainer buttonContainer
            || buttonContainer.Alignment != BoxContainer.AlignmentMode.Center
            || buttonContainer.GetThemeConstant("separation") != 10)
        {
            failures.Add("Confirmation dialog actions must be centered with compact spacing");
        }

        Panel background = null;
        for (var i = 0; i < dialog.GetChildCount(true); i++)
        {
            if (dialog.GetChild(i, true) is Panel panel)
            {
                background = panel;
                break;
            }
        }

        if (background == null)
        {
            failures.Add("Confirmation dialog must expose its rendered background panel");
        }
        else if (background.GetThemeStylebox("panel") is not StyleBoxFlat panelStyle
            || panelStyle.BorderColor != TacticalTheme.Brass
            || panelStyle.BorderWidthTop != 2
            || panelStyle.BgColor != new Color(TacticalTheme.Iron.R, TacticalTheme.Iron.G, TacticalTheme.Iron.B, 1.0f))
        {
            failures.Add("Confirmation dialog background must use the shared brass frame and opaque iron fill");
        }

        if (dialog.GetThemeStylebox("embedded_border") is not StyleBoxFlat dialogBorder
            || dialogBorder.BorderColor != TacticalTheme.Brass
            || dialogBorder.BorderWidthTop != 1)
        {
            failures.Add("Confirmation dialog must use the shared flat brass border");
        }

        if (dialog.GetOkButton().GetThemeStylebox("normal") is not StyleBoxFlat buttonStyle
            || buttonStyle.BorderColor != TacticalTheme.Brass)
        {
            failures.Add("Confirmation dialog action button must retain the shared brass border");
        }

        dialog.Free();
        return failures;
    }
}
