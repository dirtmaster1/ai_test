using Godot;

public static class DragPreview
{
    private static Input.MouseModeEnum _previousMouseMode;
    private static bool _mouseModeCaptured;

    public static bool IsDragging { get; private set; }

    public static Control Create(Texture2D icon)
    {
        var preview = new PanelContainer
        {
            CustomMinimumSize = new Vector2(48, 48),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = (int)RenderingServer.CanvasItemZMax
        };
        preview.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.07f, 0.06f, 0.94f),
            BorderColor = new Color(0.67f, 0.49f, 0.2f, 0.95f),
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusBottomLeft = 4
        });

        preview.AddChild(new TextureRect
        {
            Texture = icon,
            CustomMinimumSize = new Vector2(40, 40),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        return preview;
    }

    public static void BeginDrag()
    {
        IsDragging = true;
        if (_mouseModeCaptured)
        {
            return;
        }

        _previousMouseMode = Input.MouseMode;
        _mouseModeCaptured = true;
        Input.MouseMode = Input.MouseModeEnum.Hidden;
    }

    public static void EndDrag()
    {
        IsDragging = false;
        if (!_mouseModeCaptured)
        {
            return;
        }

        Input.MouseMode = _previousMouseMode;
        _mouseModeCaptured = false;
    }
}
