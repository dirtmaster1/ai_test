using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Text;

public partial class HudController : Control
{
    [Signal]
    public delegate void AbilityPressedEventHandler(string abilityId);

    [Signal]
    public delegate void EndTurnPressedEventHandler();

    [Signal]
    public delegate void EquipItemRequestedEventHandler(string itemId);

    [Signal]
    public delegate void UnequipItemRequestedEventHandler(string equippedSlotKey);

    [Signal]
    public delegate void InventoryCycleRequestedEventHandler(int delta);

    [Signal]
    public delegate void LootConfirmRequestedEventHandler(string interactionId);

    private const float GridPixelWidth = 20.0f * 64.0f;
    private const float Margin = 12.0f;
    private const float SidebarWidth = 360.0f;

    private Label _statusLabel;
    private PanelContainer _utilityPanel;
    private Label _utilityHeader;
    private Button _helpButton;
    private PanelContainer _helpPanel;
    private Label _helpHeader;
    private Label _helpBody;
    private Button _closeHelpButton;
    private PanelContainer _actionPanel;
    private PanelContainer _turnQueuePanel;
    private PanelContainer _combatLogPanel;
    private Label _activeUnitLabel;
    private Button _abilityButton1;
    private Button _abilityButton2;
    private Button _abilityButton3;
    private Button _endTurnButton;
    private Button _inventoryButton;
    private Label _selectedActionLabel;
    private Label _actionDetailsLabel;
    private Label _turnQueueHeader;
    private Label _combatLogHeader;
    private Label _turnQueueLabel;
    private ItemList _combatLog;
    private PanelContainer _inventoryPanel;
    private Label _inventoryHeader;
    private Label _inventoryUnitLabel;
    private Label _inventoryEquippedSummaryLabel;
    private ItemList _inventoryEquippedItemList;
    private ItemList _inventoryItemList;
    private Label _inventoryItemDetails;
    private Button _inventoryPrevUnitButton;
    private Button _inventoryNextUnitButton;
    private Button _equipButton;
    private Button _unequipButton;
    private Button _closeInventoryButton;
    private PanelContainer _lootPanel;
    private Label _lootHeader;
    private ItemList _lootItemList;
    private Label _lootDetailsLabel;
    private Button _confirmLootButton;
    private Button _closeLootButton;
    private readonly System.Collections.Generic.Dictionary<string, Dictionary> _lootEntriesById = new();
    private readonly System.Collections.Generic.Dictionary<string, Dictionary> _inventoryEquippedEntriesBySlot = new();
    private readonly System.Collections.Generic.HashSet<string> _equippedItemIds = new();
    private readonly System.Collections.Generic.Dictionary<string, Dictionary> _inventoryItemsById = new();
    private readonly System.Collections.Generic.Dictionary<Button, string> _abilityIdsByButton = new();
    private string _lastLogLine = "";
    private const int MaxLogEntries = 12;

    private readonly System.Collections.Generic.Dictionary<Control, Vector2> _panelOffsets = new();
    private readonly System.Collections.Generic.Dictionary<Control, Rect2> _basePanelRects = new();
    private bool _isDraggingPanel;
    private Control _dragPanel;
    private Vector2 _dragGrabOffset;

    public override void _Ready()
    {
        ZAsRelative = false;
        ZIndex = 4000;

        _statusLabel = GetNode<Label>("StatusLabel");
        _utilityPanel = GetNode<PanelContainer>("UtilityPanel");
        _utilityHeader = GetNode<Label>("UtilityPanel/UtilityVBox/UtilityHeader");
        _helpButton = GetNode<Button>("UtilityPanel/UtilityVBox/UtilityButtons/HelpButton");
        _helpPanel = GetNode<PanelContainer>("HelpPanel");
        _helpHeader = GetNode<Label>("HelpPanel/HelpVBox/HelpHeader");
        _helpBody = GetNode<Label>("HelpPanel/HelpVBox/HelpBody");
        _closeHelpButton = GetNode<Button>("HelpPanel/HelpVBox/HelpButtons/CloseHelpButton");
        _actionPanel = GetNode<PanelContainer>("ActionPanel");
        _turnQueuePanel = GetNode<PanelContainer>("TurnQueuePanel");
        _combatLogPanel = GetNode<PanelContainer>("CombatLogPanel");
        _activeUnitLabel = GetNode<Label>("ActionPanel/ActionVBox/ActiveUnitLabel");
        var actionHeader = GetNode<Label>("ActionPanel/ActionVBox/ActionHeader");
        _abilityButton1 = GetNode<Button>("ActionPanel/ActionVBox/ActionButtons/AbilityButton1");
        _abilityButton2 = GetNode<Button>("ActionPanel/ActionVBox/ActionButtons/AbilityButton2");
        _abilityButton3 = GetNode<Button>("ActionPanel/ActionVBox/ActionButtons/AbilityButton3");
        _endTurnButton = GetNode<Button>("ActionPanel/ActionVBox/ActionButtons/EndTurnButton");
        _inventoryButton = GetNode<Button>("UtilityPanel/UtilityVBox/UtilityButtons/InventoryButton");
        _selectedActionLabel = GetNode<Label>("ActionPanel/ActionVBox/SelectedActionLabel");
        _actionDetailsLabel = GetNode<Label>("ActionPanel/ActionVBox/ActionDetails");
        _turnQueueHeader = GetNode<Label>("TurnQueuePanel/TurnQueueVBox/TurnQueueHeader");
        _turnQueueLabel = GetNode<Label>("TurnQueuePanel/TurnQueueVBox/TurnQueueLabel");
        _combatLogHeader = GetNode<Label>("CombatLogPanel/CombatLogVBox/CombatLogHeader");
        _combatLog = GetNode<ItemList>("CombatLogPanel/CombatLogVBox/CombatLog");
        _inventoryPanel = GetNode<PanelContainer>("InventoryPanel");
        _inventoryHeader = GetNode<Label>("InventoryPanel/InventoryVBox/InventoryHeader");
        _inventoryUnitLabel = GetNode<Label>("InventoryPanel/InventoryVBox/InventoryUnitLabel");
        _inventoryEquippedSummaryLabel = GetNode<Label>("InventoryPanel/InventoryVBox/InventoryEquippedSummaryLabel");
        _inventoryEquippedItemList = GetNode<ItemList>("InventoryPanel/InventoryVBox/InventoryEquippedItemList");
        _inventoryItemList = GetNode<ItemList>("InventoryPanel/InventoryVBox/InventoryItemList");
        _inventoryItemDetails = GetNode<Label>("InventoryPanel/InventoryVBox/InventoryItemDetails");
        _inventoryPrevUnitButton = GetNode<Button>("InventoryPanel/InventoryVBox/InventoryCycleButtons/PrevUnitButton");
        _inventoryNextUnitButton = GetNode<Button>("InventoryPanel/InventoryVBox/InventoryCycleButtons/NextUnitButton");
        _equipButton = GetNode<Button>("InventoryPanel/InventoryVBox/InventoryButtons/EquipButton");
        _unequipButton = GetNode<Button>("InventoryPanel/InventoryVBox/InventoryButtons/UnequipButton");
        _closeInventoryButton = GetNode<Button>("InventoryPanel/InventoryVBox/InventoryButtons/CloseInventoryButton");
        _lootPanel = GetNode<PanelContainer>("LootPanel");
        _lootHeader = GetNode<Label>("LootPanel/LootVBox/LootHeader");
        _lootItemList = GetNode<ItemList>("LootPanel/LootVBox/LootItemList");
        _lootDetailsLabel = GetNode<Label>("LootPanel/LootVBox/LootDetailsLabel");
        _confirmLootButton = GetNode<Button>("LootPanel/LootVBox/LootButtons/ConfirmLootButton");
        _closeLootButton = GetNode<Button>("LootPanel/LootVBox/LootButtons/CloseLootButton");

        _inventoryPanel.MouseFilter = MouseFilterEnum.Stop;

        _abilityButton1.Pressed += OnAbilityButton1Pressed;
        _abilityButton2.Pressed += OnAbilityButton2Pressed;
        _abilityButton3.Pressed += OnAbilityButton3Pressed;
        _endTurnButton.Pressed += OnEndTurnButtonPressed;
        _inventoryButton.Pressed += OnInventoryButtonPressed;
        _helpButton.Pressed += OnHelpButtonPressed;
        _closeHelpButton.Pressed += OnCloseHelpButtonPressed;
        _inventoryPrevUnitButton.Pressed += OnInventoryPrevUnitButtonPressed;
        _inventoryNextUnitButton.Pressed += OnInventoryNextUnitButtonPressed;
        _equipButton.Pressed += OnEquipButtonPressed;
        _unequipButton.Pressed += OnUnequipButtonPressed;
        _closeInventoryButton.Pressed += OnCloseInventoryButtonPressed;
        _inventoryEquippedItemList.ItemSelected += OnInventoryEquippedItemSelected;
        _inventoryItemList.ItemSelected += OnInventoryItemSelected;
        _lootItemList.ItemSelected += OnLootItemSelected;
        _confirmLootButton.Pressed += OnConfirmLootButtonPressed;
        _closeLootButton.Pressed += OnCloseLootButtonPressed;

        RegisterDraggable(_statusLabel, _statusLabel);
        RegisterDraggable(_utilityHeader, _utilityPanel);
        RegisterDraggable(_helpHeader, _helpPanel);
        RegisterDraggable(actionHeader, _actionPanel);
        RegisterDraggable(_turnQueueHeader, _turnQueuePanel);
        RegisterDraggable(_combatLogHeader, _combatLogPanel);
        RegisterDraggable(_inventoryHeader, _inventoryPanel);
        RegisterDraggable(_lootHeader, _lootPanel);

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

        if (_abilityButton1 != null)
        {
            _abilityButton1.Pressed -= OnAbilityButton1Pressed;
        }

        if (_abilityButton2 != null)
        {
            _abilityButton2.Pressed -= OnAbilityButton2Pressed;
        }

        if (_abilityButton3 != null)
        {
            _abilityButton3.Pressed -= OnAbilityButton3Pressed;
        }

        if (_endTurnButton != null)
        {
            _endTurnButton.Pressed -= OnEndTurnButtonPressed;
        }

        if (_inventoryButton != null)
        {
            _inventoryButton.Pressed -= OnInventoryButtonPressed;
        }

        if (_helpButton != null)
        {
            _helpButton.Pressed -= OnHelpButtonPressed;
        }

        if (_closeHelpButton != null)
        {
            _closeHelpButton.Pressed -= OnCloseHelpButtonPressed;
        }

        if (_equipButton != null)
        {
            _equipButton.Pressed -= OnEquipButtonPressed;
        }

        if (_inventoryPrevUnitButton != null)
        {
            _inventoryPrevUnitButton.Pressed -= OnInventoryPrevUnitButtonPressed;
        }

        if (_inventoryNextUnitButton != null)
        {
            _inventoryNextUnitButton.Pressed -= OnInventoryNextUnitButtonPressed;
        }

        if (_unequipButton != null)
        {
            _unequipButton.Pressed -= OnUnequipButtonPressed;
        }

        if (_closeInventoryButton != null)
        {
            _closeInventoryButton.Pressed -= OnCloseInventoryButtonPressed;
        }

        if (_inventoryItemList != null)
        {
            _inventoryItemList.ItemSelected -= OnInventoryItemSelected;
        }

        if (_inventoryEquippedItemList != null)
        {
            _inventoryEquippedItemList.ItemSelected -= OnInventoryEquippedItemSelected;
        }

        if (_lootItemList != null)
        {
            _lootItemList.ItemSelected -= OnLootItemSelected;
        }

        if (_confirmLootButton != null)
        {
            _confirmLootButton.Pressed -= OnConfirmLootButtonPressed;
        }

        if (_closeLootButton != null)
        {
            _closeLootButton.Pressed -= OnCloseLootButtonPressed;
        }
    }

    private void OnAbilityButton1Pressed()
    {
        EmitAbilityPressed(_abilityButton1);
    }

    private void OnAbilityButton2Pressed()
    {
        EmitAbilityPressed(_abilityButton2);
    }

    private void OnAbilityButton3Pressed()
    {
        EmitAbilityPressed(_abilityButton3);
    }

    private void EmitAbilityPressed(Button button)
    {
        if (button == null)
        {
            return;
        }

        if (!_abilityIdsByButton.TryGetValue(button, out var abilityId) || string.IsNullOrEmpty(abilityId))
        {
            return;
        }

        EmitSignal(SignalName.AbilityPressed, abilityId);
    }

    private void OnEndTurnButtonPressed()
    {
        EmitSignal(SignalName.EndTurnPressed);
    }

    private void OnInventoryButtonPressed()
    {
        SetInventoryVisible(!_inventoryPanel.Visible);
    }

    private void OnHelpButtonPressed()
    {
        ToggleHelpVisible();
    }

    private void OnCloseHelpButtonPressed()
    {
        SetHelpVisible(false);
    }

    private void OnEquipButtonPressed()
    {
        if (_inventoryItemList == null)
        {
            return;
        }

        var selected = _inventoryItemList.GetSelectedItems();
        if (selected.Length == 0)
        {
            return;
        }

        var index = selected[0];
        var metadata = _inventoryItemList.GetItemMetadata(index);
        var itemId = metadata.VariantType == Variant.Type.String ? metadata.AsString() : "";
        if (string.IsNullOrEmpty(itemId))
        {
            return;
        }

        EmitSignal(SignalName.EquipItemRequested, itemId);
    }

    private void OnInventoryPrevUnitButtonPressed()
    {
        EmitSignal(SignalName.InventoryCycleRequested, -1);
    }

    private void OnInventoryNextUnitButtonPressed()
    {
        EmitSignal(SignalName.InventoryCycleRequested, 1);
    }

    private void OnUnequipButtonPressed()
    {
        if (_inventoryEquippedItemList == null)
        {
            return;
        }

        var selected = _inventoryEquippedItemList.GetSelectedItems();
        if (selected.Length == 0)
        {
            return;
        }

        var index = selected[0];
        var metadata = _inventoryEquippedItemList.GetItemMetadata(index);
        var slotKey = metadata.VariantType == Variant.Type.String ? metadata.AsString() : "";
        if (string.IsNullOrEmpty(slotKey))
        {
            return;
        }

        EmitSignal(SignalName.UnequipItemRequested, slotKey);
    }

    private void OnCloseInventoryButtonPressed()
    {
        SetInventoryVisible(false);
    }

    private void OnInventoryItemSelected(long index)
    {
        if (_inventoryItemList == null || _inventoryItemDetails == null)
        {
            return;
        }

        var metadata = _inventoryItemList.GetItemMetadata((int)index);
        var itemId = metadata.VariantType == Variant.Type.String ? metadata.AsString() : "";
        if (string.IsNullOrEmpty(itemId) || !_inventoryItemsById.TryGetValue(itemId, out var itemData))
        {
            _inventoryItemDetails.Text = _inventoryItemList.GetItemText((int)index);
            return;
        }

        _inventoryItemDetails.Text = BuildItemDetail(itemData, _equippedItemIds.Contains(itemId));
    }

    private void OnInventoryEquippedItemSelected(long index)
    {
        if (_inventoryEquippedItemList == null || _inventoryItemDetails == null)
        {
            return;
        }

        var metadata = _inventoryEquippedItemList.GetItemMetadata((int)index);
        var slotKey = metadata.VariantType == Variant.Type.String ? metadata.AsString() : "";
        if (string.IsNullOrEmpty(slotKey) || !_inventoryEquippedEntriesBySlot.TryGetValue(slotKey, out var entry))
        {
            _inventoryItemDetails.Text = _inventoryEquippedItemList.GetItemText((int)index);
            return;
        }

        var detail = GetString(entry, "detail", _inventoryEquippedItemList.GetItemText((int)index));
        _inventoryItemDetails.Text = detail;
    }

    private void OnLootItemSelected(long index)
    {
        if (_lootItemList == null || _lootDetailsLabel == null)
        {
            return;
        }

        var metadata = _lootItemList.GetItemMetadata((int)index);
        var interactionId = metadata.VariantType == Variant.Type.String ? metadata.AsString() : "";
        if (string.IsNullOrEmpty(interactionId) || !_lootEntriesById.TryGetValue(interactionId, out var entry))
        {
            _lootDetailsLabel.Text = _lootItemList.GetItemText((int)index);
            return;
        }

        _lootDetailsLabel.Text = GetString(entry, "detail", _lootItemList.GetItemText((int)index));
    }

    private void OnConfirmLootButtonPressed()
    {
        if (_lootItemList == null)
        {
            return;
        }

        var selected = _lootItemList.GetSelectedItems();
        if (selected.Length == 0)
        {
            return;
        }

        var metadata = _lootItemList.GetItemMetadata(selected[0]);
        var interactionId = metadata.VariantType == Variant.Type.String ? metadata.AsString() : "";
        if (string.IsNullOrEmpty(interactionId))
        {
            return;
        }

        EmitSignal(SignalName.LootConfirmRequested, interactionId);
    }

    private void OnCloseLootButtonPressed()
    {
        SetLootPanelVisible(false);
    }

    private void OnViewportSizeChanged()
    {
        EnsureFullscreenLayout();
        ApplyHudLayout();
    }

    public override void _Input(InputEvent @event)
    {
        if (!_isDraggingPanel || _dragPanel == null)
        {
            return;
        }

        if (@event is InputEventMouseMotion motion)
        {
            UpdatePanelDragPosition(motion.GlobalPosition);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is InputEventMouseButton button && button.ButtonIndex == MouseButton.Left && !button.Pressed)
        {
            EndPanelDrag();
            GetViewport().SetInputAsHandled();
        }
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

        ApplyPanelRect(_statusLabel, new Rect2(new Vector2(sidebarLeft, Margin), new Vector2(sidebarRight - sidebarLeft, 80.0f)), size);
        ApplyPanelRect(_utilityPanel, new Rect2(new Vector2(sidebarLeft, 98.0f), new Vector2(sidebarRight - sidebarLeft, 58.0f)), size);
        ApplyPanelRect(_helpPanel, new Rect2(new Vector2(sidebarLeft, 166.0f), new Vector2(sidebarRight - sidebarLeft, 220.0f)), size);
        ApplyPanelRect(_turnQueuePanel, new Rect2(new Vector2(sidebarLeft, 166.0f), new Vector2(sidebarRight - sidebarLeft, 264.0f)), size);
        ApplyPanelRect(_combatLogPanel, new Rect2(new Vector2(sidebarLeft, 440.0f), new Vector2(sidebarRight - sidebarLeft, Mathf.Max(100.0f, size.Y - 528.0f))), size);
        ApplyPanelRect(_actionPanel, new Rect2(new Vector2(sidebarLeft, size.Y - 80.0f), new Vector2(sidebarRight - sidebarLeft, 68.0f)), size);
        ApplyPanelRect(_lootPanel, new Rect2(new Vector2(Margin, Mathf.Max(140.0f, size.Y - 286.0f)), new Vector2(420.0f, 274.0f)), size);
    }

    private void RegisterDraggable(Control handle, Control panel)
    {
        if (handle == null || panel == null)
        {
            return;
        }

        if (!_panelOffsets.ContainsKey(panel))
        {
            _panelOffsets[panel] = Vector2.Zero;
        }

        handle.MouseFilter = MouseFilterEnum.Stop;
        handle.GuiInput += (inputEvent) => OnDragHandleInput(inputEvent, panel);
    }

    private void OnDragHandleInput(InputEvent inputEvent, Control panel)
    {
        if (inputEvent is not InputEventMouseButton button || button.ButtonIndex != MouseButton.Left)
        {
            return;
        }

        if (button.Pressed)
        {
            _isDraggingPanel = true;
            _dragPanel = panel;
            _dragGrabOffset = button.GlobalPosition - panel.GlobalPosition;
            GetViewport().SetInputAsHandled();
        }
        else if (_isDraggingPanel && _dragPanel == panel)
        {
            EndPanelDrag();
            GetViewport().SetInputAsHandled();
        }
    }

    private void EndPanelDrag()
    {
        if (_dragPanel != null)
        {
            UpdatePanelOffsetFromCurrent(_dragPanel);
        }

        _dragPanel = null;
        _isDraggingPanel = false;
    }

    private void UpdatePanelDragPosition(Vector2 mouseGlobal)
    {
        var panel = _dragPanel;
        if (panel == null)
        {
            return;
        }

        var viewport = GetViewport();
        if (viewport == null)
        {
            return;
        }

        var viewportSize = viewport.GetVisibleRect().Size;
        var target = mouseGlobal - _dragGrabOffset;
        var maxX = Mathf.Max(0.0f, viewportSize.X - panel.Size.X);
        var maxY = Mathf.Max(0.0f, viewportSize.Y - panel.Size.Y);
        target.X = Mathf.Clamp(target.X, 0.0f, maxX);
        target.Y = Mathf.Clamp(target.Y, 0.0f, maxY);

        panel.GlobalPosition = target;
        UpdatePanelOffsetFromCurrent(panel);
    }

    private void ApplyPanelRect(Control panel, Rect2 baseRect, Vector2 viewportSize)
    {
        if (panel == null)
        {
            return;
        }

        _basePanelRects[panel] = baseRect;
        var offset = _panelOffsets.TryGetValue(panel, out var storedOffset) ? storedOffset : Vector2.Zero;
        var pos = baseRect.Position + offset;
        pos.X = Mathf.Clamp(pos.X, 0.0f, Mathf.Max(0.0f, viewportSize.X - baseRect.Size.X));
        pos.Y = Mathf.Clamp(pos.Y, 0.0f, Mathf.Max(0.0f, viewportSize.Y - baseRect.Size.Y));

        SetRect(panel, pos.X, pos.Y, pos.X + baseRect.Size.X, pos.Y + baseRect.Size.Y);
        _panelOffsets[panel] = pos - baseRect.Position;
    }

    private void UpdatePanelOffsetFromCurrent(Control panel)
    {
        if (panel == null || !_basePanelRects.TryGetValue(panel, out var baseRect))
        {
            return;
        }

        _panelOffsets[panel] = panel.Position - baseRect.Position;
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

    public void SetSelectedAction(string actionText)
    {
        if (_selectedActionLabel != null)
        {
            _selectedActionLabel.Text = $"Selected Ability: {actionText}";
        }
    }

    public void SetActionButtonsEnabled(bool abilityEnabled, bool endTurnEnabled)
    {
        if (_abilityButton1 != null)
        {
            _abilityButton1.Disabled = !abilityEnabled;
        }

        if (_abilityButton2 != null)
        {
            _abilityButton2.Disabled = !abilityEnabled;
        }

        if (_abilityButton3 != null)
        {
            _abilityButton3.Disabled = !abilityEnabled;
        }

        if (_endTurnButton != null)
        {
            _endTurnButton.Disabled = !endTurnEnabled;
        }

        if (_inventoryButton != null)
        {
            _inventoryButton.Disabled = false;
        }
    }

    public void SetAbilityButtons(Array<Dictionary> abilities, bool canUseAnyAbility)
    {
        _abilityIdsByButton.Clear();

        var buttons = new Button[] { _abilityButton1, _abilityButton2, _abilityButton3 };
        for (var i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            if (button == null)
            {
                continue;
            }

            if (abilities == null || i >= abilities.Count)
            {
                button.Visible = false;
                button.Disabled = true;
                button.Text = "";
                button.TooltipText = "";
                continue;
            }

            var entry = abilities[i];
            var abilityId = GetString(entry, "id", "");
            var label = GetString(entry, "label", abilityId);
            var detail = GetString(entry, "detail", label);
            var cooldownRemaining = GetInt(entry, "cooldown_remaining", 0);
            var isSelected = GetInt(entry, "is_selected", 0) == 1;

            button.Visible = true;
            button.Text = cooldownRemaining > 0
                ? $"{label} (CD {cooldownRemaining})"
                : label;
            button.TooltipText = detail;
            button.Disabled = !canUseAnyAbility || cooldownRemaining > 0;
            _abilityIdsByButton[button] = abilityId;

            if (isSelected)
            {
                button.Text = $"> {button.Text}";
            }
        }
    }

    public void SetActiveUnit(Unit active)
    {
        if (_activeUnitLabel == null)
        {
            return;
        }

        if (active == null)
        {
            _activeUnitLabel.Text = "Turn: -";
            return;
        }

        _activeUnitLabel.Text =
            $"Turn: {active.UnitName} [{active.Team}] | HP {active.HitPoints}/{active.MaxHitPoints} | Move {active.RemainingMovement}/{Unit.MaxMovementPerTurn}";
    }

    public void SetInventoryItems(Array<Dictionary> items, Array<string> equippedItemIds)
    {
        if (_inventoryItemList == null)
        {
            return;
        }

        _equippedItemIds.Clear();
        if (equippedItemIds != null)
        {
            foreach (var itemId in equippedItemIds)
            {
                if (!string.IsNullOrEmpty(itemId))
                {
                    _equippedItemIds.Add(itemId);
                }
            }
        }

        _inventoryItemList.Clear();
        _inventoryItemsById.Clear();

        if (items == null)
        {
            return;
        }

        foreach (var item in items)
        {
            var id = GetString(item, "id", "");
            var name = GetString(item, "name", id);
            var type = GetString(item, "type", "item");
            _inventoryItemsById[id] = item;

            var suffix = _equippedItemIds.Contains(id) ? " (equipped)" : "";
            var line = BuildItemSummary(item) + suffix;
            _inventoryItemList.AddItem(line);
            _inventoryItemList.SetItemMetadata(_inventoryItemList.ItemCount - 1, id);
        }

        if (_inventoryItemList.ItemCount > 0)
        {
            _inventoryItemList.Select(0);
            var firstId = _inventoryItemList.GetItemMetadata(0).AsString();
            if (!string.IsNullOrEmpty(firstId) && _inventoryItemsById.TryGetValue(firstId, out var firstItem))
            {
                _inventoryItemDetails.Text = BuildItemDetail(firstItem, _equippedItemIds.Contains(firstId));
            }
            else
            {
                _inventoryItemDetails.Text = _inventoryItemList.GetItemText(0);
            }
        }
        else if (_inventoryItemDetails != null)
        {
            _inventoryItemDetails.Text = "No unequipped shared inventory items.";
        }
    }

    public void SetInventoryEquippedItems(Array<Dictionary> entries)
    {
        if (_inventoryEquippedItemList == null)
        {
            return;
        }

        _inventoryEquippedItemList.Clear();
        _inventoryEquippedEntriesBySlot.Clear();

        if (entries == null)
        {
            return;
        }

        foreach (var entry in entries)
        {
            var slotKey = GetString(entry, "slot_key", "");
            if (string.IsNullOrEmpty(slotKey))
            {
                continue;
            }

            var label = GetString(entry, "label", slotKey);
            _inventoryEquippedEntriesBySlot[slotKey] = entry;
            _inventoryEquippedItemList.AddItem(label);
            _inventoryEquippedItemList.SetItemMetadata(_inventoryEquippedItemList.ItemCount - 1, slotKey);
        }

        if (_inventoryEquippedItemList.ItemCount > 0)
        {
            _inventoryEquippedItemList.Select(0);
            OnInventoryEquippedItemSelected(0);
        }
    }

    public void SetInventoryUnitName(string unitName)
    {
        if (_inventoryUnitLabel != null)
        {
            _inventoryUnitLabel.Text = $"Unit: {unitName}";
        }
    }

    public void SetInventoryEquippedSummary(string text)
    {
        if (_inventoryEquippedSummaryLabel != null)
        {
            _inventoryEquippedSummaryLabel.Text = text;
        }
    }

    public void SetInventoryVisible(bool visible)
    {
        if (_inventoryPanel != null)
        {
            _inventoryPanel.Visible = visible;
            if (visible)
            {
                _inventoryPanel.MoveToFront();
            }
        }
    }

    public void ToggleInventoryVisible()
    {
        if (_inventoryPanel != null)
        {
            _inventoryPanel.Visible = !_inventoryPanel.Visible;
            if (_inventoryPanel.Visible)
            {
                _inventoryPanel.MoveToFront();
            }
        }
    }

    public void SetHelpVisible(bool visible)
    {
        if (_helpPanel != null)
        {
            _helpPanel.Visible = visible;
            if (visible)
            {
                _helpPanel.MoveToFront();
            }
        }
    }

    public void ToggleHelpVisible()
    {
        if (_helpPanel != null)
        {
            SetHelpVisible(!_helpPanel.Visible);
        }
    }

    public void SetHelpText(string text)
    {
        if (_helpBody != null)
        {
            _helpBody.Text = text;
        }
    }

    public bool IsInventoryVisible()
    {
        return _inventoryPanel != null && _inventoryPanel.Visible;
    }

    public void SetLootPanelVisible(bool visible)
    {
        if (_lootPanel != null)
        {
            _lootPanel.Visible = visible;
        }
    }

    public void SetLootEntries(Array<Dictionary> entries)
    {
        if (_lootItemList == null || _lootDetailsLabel == null)
        {
            return;
        }

        _lootItemList.Clear();
        _lootEntriesById.Clear();

        if (entries == null || entries.Count == 0)
        {
            _lootDetailsLabel.Text = "No nearby loot interactions.";
            SetLootPanelVisible(false);
            return;
        }

        foreach (var entry in entries)
        {
            var interactionId = GetString(entry, "id", "");
            if (string.IsNullOrEmpty(interactionId))
            {
                continue;
            }

            var label = GetString(entry, "label", interactionId);
            _lootEntriesById[interactionId] = entry;
            _lootItemList.AddItem(label);
            _lootItemList.SetItemMetadata(_lootItemList.ItemCount - 1, interactionId);
        }

        if (_lootItemList.ItemCount > 0)
        {
            _lootItemList.Select(0);
            var firstId = _lootItemList.GetItemMetadata(0).AsString();
            if (!string.IsNullOrEmpty(firstId) && _lootEntriesById.TryGetValue(firstId, out var firstEntry))
            {
                _lootDetailsLabel.Text = GetString(firstEntry, "detail", _lootItemList.GetItemText(0));
            }
            else
            {
                _lootDetailsLabel.Text = _lootItemList.GetItemText(0);
            }
        }
    }

    private static string GetString(Dictionary dict, string key, string fallback)
    {
        if (dict == null || !dict.ContainsKey(key))
        {
            return fallback;
        }

        return ((Variant)dict[key]).AsString();
    }

    private static int GetInt(Dictionary dict, string key, int fallback)
    {
        if (dict == null || !dict.ContainsKey(key))
        {
            return fallback;
        }

        return (int)((Variant)dict[key]);
    }

    private static string BuildItemSummary(Dictionary item)
    {
        var id = GetString(item, "id", "item");
        var name = GetString(item, "name", id);
        var type = GetString(item, "type", "item");

        if (type == "weapon")
        {
            var base_dmg = GetInt(item, "base_damage", 0);
            var bonus_dmg = GetInt(item, "bonus_damage", 0);
            var range = GetInt(item, "range", 0);
            return $"{name} [weapon] dmg+{base_dmg} (+{bonus_dmg}) range+{range}";
        }

        if (type == "armor")
        {
            var base_defense = GetInt(item, "base_defense", 0);
            var bonus_defense = GetInt(item, "bonus_defense", 0);
            return $"{name} [armor] def+{base_defense} (+{bonus_defense})";
        }

        return $"{name} [{type}]";
    }

    private static string BuildItemDetail(Dictionary item, bool equipped)
    {
        var id = GetString(item, "id", "item");
        var name = GetString(item, "name", id);
        var type = GetString(item, "type", "item");
        var prefix = equipped ? "(equipped) " : "";

        if (type == "weapon")
        {
            var base_dmg = GetInt(item, "base_damage", 0);
            var bonus_dmg = GetInt(item, "bonus_damage", 0);
            var range = GetInt(item, "range", 0);
            return $"{prefix}{name} - Weapon\nDamage: +{base_dmg} (+{bonus_dmg})\nRange: +{range}";
        }

        if (type == "armor")
        {
            var base_defense = GetInt(item, "base_defense", 0);
            var bonus_defense = GetInt(item, "bonus_defense", 0);
            return $"{prefix}{name} - Armor\nDefense: +{base_defense} (+{bonus_defense})";
        }

        return $"{prefix}{name} - {type}";
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
