using Godot;
using Godot.Collections;

public partial class DialogThemeChecks : Node
{
    public Array<string> Run()
    {
        var failures = new Array<string>();
        var dialog = new ConfirmationDialog();
        AddChild(dialog);
        TacticalTheme.ApplyDialog(dialog);

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

        if (background?.GetNodeOrNull<NinePatchRect>("TacticalDialogFrame") is not NinePatchRect frame
            || frame.Texture == null
            || frame.PatchMarginLeft != 32
            || frame.PatchMarginTop != 32
            || frame.MouseFilter != Control.MouseFilterEnum.Ignore)
        {
            failures.Add("Confirmation dialog must overlay the decorative iron-and-brass nine-patch frame");
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
