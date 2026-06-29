using Godot;
using Godot.Collections;
using System.Text;

public partial class HudController : Control
{
    private const float GridPixelWidth = 20.0f * 64.0f;
    private const float Margin = 12.0f;
    private const float SidebarWidth = 360.0f;

    private Label _statusLabel;
    private PanelContainer _actionPanel;
    private PanelContainer _turnQueuePanel;
    private PanelContainer _combatLogPanel;
    private Label _actionDetailsLabel;
    private Label _turnQueueLabel;
    private ItemList _combatLog;
    private string _lastLogLine = "";
    private const int MaxLogEntries = 12;

    public override void _Ready()
    {
        _statusLabel = GetNode<Label>("StatusLabel");
        _actionPanel = GetNode<PanelContainer>("ActionPanel");
        _turnQueuePanel = GetNode<PanelContainer>("TurnQueuePanel");
        _combatLogPanel = GetNode<PanelContainer>("CombatLogPanel");
        _actionDetailsLabel = GetNode<Label>("ActionPanel/ActionVBox/ActionDetails");
        _turnQueueLabel = GetNode<Label>("TurnQueuePanel/TurnQueueLabel");
        _combatLog = GetNode<ItemList>("CombatLogPanel/CombatLog");

        EnsureFullscreenLayout();
        ApplyHudLayout();
        GetViewport().SizeChanged += OnViewportSizeChanged;
    }

    public override void _ExitTree()
    {
        var viewport = GetViewport();
        if (viewport != null)
        {
            viewport.SizeChanged -= OnViewportSizeChanged;
        }
    }

    private void OnViewportSizeChanged()
    {
        EnsureFullscreenLayout();
        ApplyHudLayout();
    }

    private void EnsureFullscreenLayout()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        OffsetLeft = 0;
        OffsetTop = 0;
        OffsetRight = 0;
        OffsetBottom = 0;

        var viewport = GetViewport();
        if (viewport != null)
        {
            Size = viewport.GetVisibleRect().Size;
            Position = Vector2.Zero;
        }
    }

    private void ApplyHudLayout()
    {
        var viewport = GetViewport();
        if (viewport == null)
        {
            return;
        }

        var size = viewport.GetVisibleRect().Size;
        var sidebarLeft = Mathf.Max(GridPixelWidth + Margin, size.X - SidebarWidth - Margin);
        var sidebarRight = size.X - Margin;

        SetRect(_statusLabel, sidebarLeft, Margin, sidebarRight, 90.0f);
        SetRect(_turnQueuePanel, sidebarLeft, 98.0f, sidebarRight, 430.0f);
        SetRect(_combatLogPanel, sidebarLeft, 440.0f, sidebarRight, size.Y - 88.0f);
        SetRect(_actionPanel, sidebarLeft, size.Y - 80.0f, sidebarRight, size.Y - Margin);
    }

    private static void SetRect(Control node, float left, float top, float right, float bottom)
    {
        if (node == null)
        {
            return;
        }

        node.AnchorLeft = 0;
        node.AnchorTop = 0;
        node.AnchorRight = 0;
        node.AnchorBottom = 0;
        node.OffsetLeft = left;
        node.OffsetTop = top;
        node.OffsetRight = right;
        node.OffsetBottom = bottom;
    }

    public void SetStatusText(string text)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = text;
        }
    }

    public void SetActionDetails(string text)
    {
        if (_actionDetailsLabel != null)
        {
            _actionDetailsLabel.Text = text;
        }
    }

    public void SetTurnQueue(Array<Unit> queue, Unit activeUnit)
    {
        if (_turnQueueLabel == null)
        {
            return;
        }

        if (queue == null || queue.Count == 0)
        {
            _turnQueueLabel.Text = "Turn Queue\n-";
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Turn Queue");
        foreach (var unit in queue)
        {
            if (unit == null || unit.IsDead)
            {
                continue;
            }

            var marker = unit == activeUnit ? ">" : " ";
            builder.AppendLine($"{marker} {unit.UnitName} [{unit.Team}] {unit.HitPoints}/{unit.MaxHitPoints}");
        }

        _turnQueueLabel.Text = builder.ToString().TrimEnd();
    }

    public void AddCombatLogEntry(string text)
    {
        if (_combatLog == null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (text == _lastLogLine)
        {
            return;
        }

        _lastLogLine = text;
        _combatLog.AddItem(text);
        while (_combatLog.ItemCount > MaxLogEntries)
        {
            _combatLog.RemoveItem(0);
        }

        _combatLog.Select(_combatLog.ItemCount - 1);
        _combatLog.EnsureCurrentIsVisible();
    }
}
