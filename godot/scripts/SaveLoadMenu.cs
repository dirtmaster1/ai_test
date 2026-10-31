using Godot;
using System;

public partial class SaveLoadMenu : CanvasLayer
{
    private const float CardHeight = 112;
    private const int CardSpacing = 12;
    private const int MaxVisibleCards = 5;
    private const float PartyPortraitSize = 50.0f;
    private const float PartyPreviewCellWidth = 62.0f;
    private const float PartyPreviewSpacing = 8.0f;
    private const float CardActionSpacing = 12.0f;
    private const float DeleteButtonWidth = 76.0f;

    [Signal] public delegate void SaveSelectedEventHandler(int slot, string saveName, bool overwrite);
    [Signal] public delegate void LoadSelectedEventHandler(string path);
    [Signal] public delegate void SavesChangedEventHandler();

    private bool _saveMode;
    private bool _includeAutomatic;
    private bool _wasPaused;
    private bool _closed;
    private SceneTree _tree;
    private SaveGameSlots _slots;
    private SaveGameSlots.Entry _selected;
    private int _newSlot;
    private LineEdit _name;
    private Button _action;
    private Label _status;
    private PanelContainer _panel;
    private VBoxContainer _cards;
    private ScrollContainer _scroll;
    private bool _resizeQueued;
    private readonly System.Collections.Generic.Dictionary<int, LineEdit> _nameEditors = new();
    private ConfirmationDialog _confirmation;
    private readonly ButtonGroup _selectionGroup = new();

    public void Configure(bool saveMode, SaveGameSlots slots = null, bool includeAutomatic = true)
    {
        _saveMode = saveMode;
        _slots = slots ?? new SaveGameSlots();
        _includeAutomatic = includeAutomatic;
    }

    public override void _Ready()
    {
        Name = "SaveLoadMenu";
        Layer = 100;
        ProcessMode = ProcessModeEnum.Always;
        _tree = GetTree();
        _wasPaused = _tree.Paused;
        _tree.Paused = true;

        var backdrop = new ColorRect
        {
            Name = "SaveMenuBackdrop", Color = new Color(0.01f, 0.015f, 0.02f, 0.88f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        AddChild(backdrop);
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var center = new CenterContainer();
        backdrop.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _panel = new PanelContainer { Name = "SaveMenuPanel" };
        center.AddChild(_panel);
        _panel.AddThemeStyleboxOverride("panel", TacticalTheme.CreatePanel(false, 20));
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 12);
        _panel.AddChild(content);
        var title = AddLabel(content, _saveMode ? "Save Game" : "Load Game", 32, TacticalTheme.BrassBright);
        title.Name = "SaveMenuTitle";
        TacticalTheme.ApplyHeaderLabel(title, TacticalTheme.BrassBright, 32);

        _scroll = new ScrollContainer
        {
            Name = "SaveCardScroll", SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        content.AddChild(_scroll);
        _cards = new VBoxContainer { Name = "SaveCards", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _cards.AddThemeConstantOverride("separation", CardSpacing);
        _scroll.AddChild(_cards);
        backdrop.Resized += QueueMenuResize;
        content.MinimumSizeChanged += QueueMenuResize;

        _status = AddLabel(content, "", 16, TacticalTheme.Negative);
        _status.Name = "SaveMenuStatus";
        _status.Visible = false;
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddThemeConstantOverride("separation", 12);
        content.AddChild(buttons);
        var cancel = new Button { Name = "CancelSaveMenu", Text = "Cancel", CustomMinimumSize = new Vector2(130, 44) };
        TacticalTheme.ApplyButton(cancel, false, 18);
        buttons.AddChild(cancel);
        cancel.Pressed += Close;
        _action = new Button
        {
            Name = "ConfirmSaveMenu", Text = _saveMode ? "Save" : "Load", Disabled = true,
            CustomMinimumSize = new Vector2(150, 44)
        };
        TacticalTheme.ApplyButton(_action, true, 18);
        buttons.AddChild(_action);
        _action.Pressed += Submit;
        RefreshCards();
    }

    private void QueueMenuResize()
    {
        if (_resizeQueued)
            return;
        _resizeQueued = true;
        Callable.From(ResizeMenu).CallDeferred();
    }

    private void ResizeMenu()
    {
        _resizeQueued = false;
        if (_closed || !IsInsideTree())
            return;
        var viewport = GetViewport().GetVisibleRect().Size;
        _panel.CustomMinimumSize = new Vector2(Mathf.Min(700, viewport.X - 48), 0);
        var chromeHeight = _panel.GetCombinedMinimumSize().Y - _scroll.GetCombinedMinimumSize().Y;
        var availableHeight = Mathf.Max(CardHeight, viewport.Y - 48 - chromeHeight);
        var visibleRows = Mathf.Clamp(Mathf.FloorToInt((availableHeight + CardSpacing) / (CardHeight + CardSpacing)), 1, MaxVisibleCards);
        var cardCount = _cards.GetChildCount() > 0 && _cards.GetChild(0) is Button ? _cards.GetChildCount() : 0;
        var rows = Mathf.Min(cardCount, visibleRows);
        _scroll.CustomMinimumSize = new Vector2(0, rows == 0 ? 32 : rows * CardHeight + (rows - 1) * CardSpacing);
    }

    private void RefreshCards()
    {
        _selected = null;
        _newSlot = 0;
        _name = null;
        _nameEditors.Clear();
        _status.Visible = false;
        foreach (var child in _cards.GetChildren())
        {
            _cards.RemoveChild(child);
            child.QueueFree();
        }

        var entries = _slots.ListManualSaves();
        if (_saveMode)
        {
            var emptySlot = _slots.FindEmptySlot();
            var text = AddCard("NewSaveCard", "New Save", "",
                emptySlot == 0 ? "Overwrite or delete a named save." : "",
                emptySlot == 0, () => Select(null, emptySlot));
            AddNameEditor(text, 0, "");
        }
        if (!_saveMode && _includeAutomatic)
        {
            if (FileAccess.FileExists(SaveGameSlots.AutosavePath))
                entries.Add(SaveGameSlots.ReadEntry(0, SaveGameSlots.AutosavePath, "Auto-Save") with { Name = "Auto-Save" });
        }
        entries.Sort((left, right) =>
        {
            var newestFirst = right.SavedAt.CompareTo(left.SavedAt);
            return newestFirst != 0 ? newestFirst : left.Slot.CompareTo(right.Slot);
        });
        foreach (var entry in entries)
            AddSaveCard(entry);
        if (_cards.GetChildCount() == 0)
            AddLabel(_cards, "No saved adventures yet.", 20, TacticalTheme.ParchmentMuted);
        UpdateAction();
        _scroll.ScrollVertical = 0;
        QueueMenuResize();
    }

    private void AddNameEditor(Node parent, int slot, string name)
    {
        var editor = new LineEdit
        {
            Name = slot == 0 ? "SaveName" : $"SaveName_{slot}",
            Text = name, PlaceholderText = "Name your save", MaxLength = SaveGameSlots.NameLength,
            Visible = slot == 0, Editable = false, CustomMinimumSize = new Vector2(0, 36),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore, FocusMode = Control.FocusModeEnum.None
        };
        editor.AddThemeFontOverride("font", TacticalTheme.BodyFont);
        editor.AddThemeFontSizeOverride("font_size", 18);
        editor.AddThemeColorOverride("font_color", TacticalTheme.Parchment);
        editor.AddThemeStyleboxOverride("normal", TacticalTheme.CreatePanel(true, 4));
        parent.AddChild(editor);
        _nameEditors[slot] = editor;
        editor.TextChanged += _ => UpdateAction();
    }

    private Label AddLabel(Node parent, string text, int size, Color color)
    {
        var label = new Label
        {
            Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore
        };
        TacticalTheme.ApplyLabel(label, color, size);
        parent.AddChild(label);
        return label;
    }

    private void AddSaveCard(SaveGameSlots.Entry entry)
    {
        var text = AddCard($"SaveCard_{entry.Slot}", entry.Name, entry.Map,
            entry.CanLoad ? entry.Timestamp : entry.Error,
            !_saveMode && !entry.CanLoad, () => Select(entry, 0));
        Button delete = null;
        if (entry.Slot > 0)
        {
            var header = text.GetNode<HBoxContainer>("CardHeader");
            if (_saveMode)
                AddNameEditor(header, entry.Slot, entry.Name);
            delete = new Button
            {
                Name = $"DeleteSave_{entry.Slot}", Text = "Delete", TooltipText = $"Delete \"{entry.Name}\"",
                CustomMinimumSize = new Vector2(DeleteButtonWidth, 0.0f),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                MouseFilter = Control.MouseFilterEnum.Stop,
                MouseDefaultCursorShape = Control.CursorShape.PointingHand
            };
            TacticalTheme.ApplyButton(delete, false, 14);
            delete.AddThemeColorOverride("font_color", TacticalTheme.Negative);
            delete.Pressed += () => Confirm("Delete Save", $"Permanently delete \"{entry.Name}\"? This cannot be undone.", "Delete", () =>
            {
                if (!_slots.TryDelete(entry.Slot, out var error))
                {
                    ShowError(error);
                    return;
                }
                RefreshCards();
                EmitSignal(SignalName.SavesChanged);
            });
        }

        AddCardActions(text, entry.PartyMembers, delete);
    }

    private void AddCardActions(
        VBoxContainer text,
        System.Collections.Generic.IReadOnlyList<SaveGameSlots.PartyMember> partyMembers,
        Button delete)
    {
        var memberCount = Mathf.Min(partyMembers?.Count ?? 0, 5);
        if (memberCount == 0 && delete == null)
            return;

        var previewWidth = memberCount == 0
            ? 0.0f
            : memberCount * PartyPreviewCellWidth + (memberCount - 1) * PartyPreviewSpacing;
        var reserveDeleteSpace = delete != null || memberCount > 0;
        var actionWidth = previewWidth + (reserveDeleteSpace ? DeleteButtonWidth : 0.0f)
            + (memberCount > 0 && reserveDeleteSpace ? CardActionSpacing : 0.0f);
        text.OffsetRight = -(actionWidth + 26.0f);
        var actions = new HBoxContainer
        {
            Name = "CardActions",
            CustomMinimumSize = new Vector2(actionWidth, 0.0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        actions.AddThemeConstantOverride("separation", (int)CardActionSpacing);
        actions.AnchorLeft = 1.0f;
        actions.AnchorRight = 1.0f;
        actions.AnchorTop = 0.0f;
        actions.AnchorBottom = 1.0f;
        actions.OffsetLeft = -(actionWidth + 14.0f);
        actions.OffsetRight = -14.0f;
        actions.OffsetTop = 6.0f;
        actions.OffsetBottom = -6.0f;
        text.GetParent().AddChild(actions);

        if (memberCount > 0)
        {
            var preview = new HBoxContainer
            {
                Name = "PartyPreview",
                CustomMinimumSize = new Vector2(previewWidth, 0.0f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Alignment = BoxContainer.AlignmentMode.Center
            };
            preview.AddThemeConstantOverride("separation", (int)PartyPreviewSpacing);
            actions.AddChild(preview);

            for (var index = 0; index < memberCount; index++)
            {
                var member = partyMembers[index];
                var memberPreview = new VBoxContainer
                {
                    Name = $"PartyMember_{index + 1}",
                    CustomMinimumSize = new Vector2(PartyPreviewCellWidth, 0.0f),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Alignment = BoxContainer.AlignmentMode.Center
                };
                memberPreview.AddThemeConstantOverride("separation", 2);
                preview.AddChild(memberPreview);

                var level = new Label
                {
                    Name = "PartyMemberLevel",
                    Text = $"Level {member.Level}",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                TacticalTheme.ApplyLabel(level, TacticalTheme.Parchment, 12);
                memberPreview.AddChild(level);

                var portrait = new TextureRect
                {
                    Name = "PartyMemberPortrait",
                    Texture = member.Portrait,
                    CustomMinimumSize = new Vector2(PartyPortraitSize, PartyPortraitSize),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                memberPreview.AddChild(portrait);
            }
        }

        if (delete != null)
            actions.AddChild(delete);
        else if (memberCount > 0)
            actions.AddChild(new Control
            {
                CustomMinimumSize = new Vector2(DeleteButtonWidth, 0.0f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
    }

    private VBoxContainer AddCard(string nodeName, string title, string map, string detail, bool disabled, Action select)
    {
        var button = new Button
        {
            Name = nodeName, ToggleMode = true, ButtonGroup = _selectionGroup, Disabled = disabled,
            CustomMinimumSize = new Vector2(0, CardHeight), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand, TooltipText = $"{title}\n{map}\n{detail}"
        };
        TacticalTheme.ApplyButton(button);
        var normal = TacticalTheme.CreatePanel(true, 12);
        var selected = TacticalTheme.CreatePanel(false, 12);
        selected.BgColor = TacticalTheme.IronRaised;
        selected.BorderColor = TacticalTheme.BrassBright;
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("pressed", selected);
        _cards.AddChild(button);
        var text = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        button.AddChild(text);
        text.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        text.OffsetLeft = 14;
        text.OffsetRight = -14;
        text.OffsetTop = 8;
        text.OffsetBottom = -8;
        var header = new HBoxContainer { Name = "CardHeader", MouseFilter = Control.MouseFilterEnum.Ignore };
        header.AddThemeConstantOverride("separation", 8);
        text.AddChild(header);
        var heading = AddLabel(header, title, 21, disabled ? TacticalTheme.ParchmentMuted : TacticalTheme.BrassBright);
        heading.Name = "CardTitle";
        heading.AutowrapMode = TextServer.AutowrapMode.Off;
        heading.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        if (!string.IsNullOrEmpty(map))
            AddLabel(text, map, 17, TacticalTheme.Parchment);
        if (!string.IsNullOrEmpty(detail))
            AddLabel(text, detail, 14, TacticalTheme.ParchmentMuted);
        button.Pressed += select;
        return text;
    }

    private void Select(SaveGameSlots.Entry entry, int newSlot)
    {
        _selected = entry;
        _newSlot = newSlot;
        _status.Visible = false;
        if (_saveMode)
        {
            var selectedKey = entry?.Slot ?? 0;
            foreach (var (slot, editor) in _nameEditors)
            {
                editor.Editable = slot == selectedKey;
                editor.Visible = slot == 0 || editor.Editable;
                editor.MouseFilter = editor.Editable ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
                editor.FocusMode = editor.Editable ? Control.FocusModeEnum.All : Control.FocusModeEnum.None;
                var heading = editor.GetParent().GetNodeOrNull<Label>("CardTitle");
                if (heading != null)
                    heading.Visible = !editor.Visible;
            }
            _name = _nameEditors[selectedKey];
        }
        UpdateAction();
        if (_saveMode)
            _name.GrabFocus();
    }

    private void UpdateAction()
    {
        if (_action == null)
            return;
        _action.Disabled = _saveMode
            ? _name == null || (_selected == null && _newSlot == 0) || string.IsNullOrWhiteSpace(_name.Text)
            : _selected == null || !_selected.CanLoad;
    }

    private void Submit()
    {
        if (_action.Disabled || IsInstanceValid(_confirmation))
            return;
        if (!_saveMode)
        {
            EmitSignal(SignalName.LoadSelected, _selected.Path);
            return;
        }
        if (_selected == null)
        {
            EmitSignal(SignalName.SaveSelected, _newSlot, _name.Text.Trim(), false);
            return;
        }
        Confirm("Overwrite Save", $"Replace \"{_selected.Name}\" with the current adventure?", "Save",
            () => EmitSignal(SignalName.SaveSelected, _selected.Slot, _name.Text.Trim(), true));
    }

    private void Confirm(string title, string message, string action, Action confirmed)
    {
        if (IsInstanceValid(_confirmation))
            return;
        _confirmation = new ConfirmationDialog
        {
            Title = title, DialogText = message,
            Exclusive = true, ProcessMode = ProcessModeEnum.Always
        };
        AddChild(_confirmation);
        _confirmation.GetOkButton().Text = action;
        TacticalTheme.ApplyDialog(_confirmation);
        _confirmation.Confirmed += () =>
        {
            _confirmation.Hide();
            _confirmation.QueueFree();
            _confirmation = null;
            confirmed();
        };
        _confirmation.Canceled += () =>
        {
            _confirmation.Hide();
            _confirmation.QueueFree();
            _confirmation = null;
        };
        _confirmation.PopupCentered();
    }

    public void ShowError(string error)
    {
        _status.Text = error;
        _status.Visible = true;
        _status.AddThemeColorOverride("font_color", TacticalTheme.Negative);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape } && !IsInstanceValid(_confirmation))
        {
            GetViewport().SetInputAsHandled();
            Close();
        }
    }

    public void Close()
    {
        if (_closed)
            return;
        _closed = true;
        _tree.Paused = _wasPaused;
        QueueFree();
    }

    public override void _ExitTree()
    {
        if (!_closed)
            _tree.Paused = _wasPaused;
    }
}
