using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System;
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

    [Signal]
    public delegate void VendorBuyRequestedEventHandler(string itemId);

    [Signal]
    public delegate void VendorSellRequestedEventHandler(string itemId);

    [Signal]
    public delegate void SaveRequestedEventHandler();

    [Signal]
    public delegate void LoadRequestedEventHandler();

    [Signal]
    public delegate void TurnOrderUnitFocusedEventHandler(string unitId);

    [Signal]
    public delegate void PartyUnitSelectedEventHandler(string unitId);

    [Signal]
    public delegate void PartyOrderRequestedEventHandler(string sourceUnitId, string targetUnitId);

    [Signal]
    public delegate void ReserveStoreRequestedEventHandler(string partyUnitId);

    [Signal]
    public delegate void ReserveBringRequestedEventHandler(string reserveUnitId, string replacePartyUnitId);

    private const float GridPixelWidth = 20.0f * 64.0f;
    private const float Margin = 12.0f;
    private const float SidebarWidth = 430.0f;
    private const float SidebarRightInset = 24.0f;

    private PanelContainer _utilityPanel;
    private Label _utilityHeader;
    private Button _helpButton;
    private Button _saveButton;
    private Button _loadButton;
    private PanelContainer _helpPanel;
    private Label _helpHeader;
    private Label _helpBody;
    private Button _closeHelpButton;
    private PanelContainer _actionPanel;
    private PanelContainer _characterPanel;
    private Label _characterHeader;
    private Label _characterSummaryLabel;
    private Label _characterStatusLabel;
    private Button _characterPrevButton;
    private Button _characterNextButton;
    private Button _characterCloseButton;
    private PanelContainer _partyPanel;
    private Label _partyHeader;
    private VBoxContainer _partyList;
    private MarginContainer _turnOrderDisplay;
    private HBoxContainer _turnOrderIcons;
    private PanelContainer _combatBannerPanel;
    private Label _combatBannerLabel;
    private Tween _combatBannerTween;
    private PanelContainer _combatLogPanel;
    private Label _activeUnitLabel;
    private Button _abilityButton1;
    private Button _abilityButton2;
    private Button _abilityButton3;
    private Button _endTurnButton;
    private Button _inventoryButton;
    private Button _characterButton;
    private Button _reserveButton;
    private Label _combatLogHeader;
    private ItemList _combatLog;
    private Button _combatLogResizeHandle;
    private PanelContainer _inventoryPanel;
    private Label _inventoryHeader;
    private Label _inventoryUnitLabel;
    private Label _inventoryEquippedSummaryLabel;
    private Label _inventoryGoldLabel;
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
    private PanelContainer _vendorPanel;
    private Label _vendorHeader;
    private Label _vendorDialogueLabel;
    private Button _vendorTalkButton;
    private Button _vendorStoreButton;
    private TabContainer _vendorStoreTabs;
    private ItemList _vendorBuyList;
    private ItemList _vendorSellList;
    private Button _vendorBuyButton;
    private Button _vendorSellButton;
    private Label _vendorStatusLabel;
    private Button _closeVendorButton;
    private PanelContainer _reservePanel;
    private Label _reserveHeader;
    private ItemList _reserveActivePartyList;
    private ItemList _reserveRosterList;
    private Label _reserveDetailsLabel;
    private Button _storeToReserveButton;
    private Button _bringFromReserveButton;
    private Button _closeReserveButton;
    private string _lootAllInteractionId = "";
    private readonly System.Collections.Generic.Dictionary<string, Dictionary> _lootEntriesById = new();
    private readonly System.Collections.Generic.Dictionary<string, Dictionary> _vendorBuyItemsById = new();
    private readonly System.Collections.Generic.Dictionary<string, Dictionary> _vendorSellItemsById = new();
    private readonly System.Collections.Generic.Dictionary<string, Dictionary> _inventoryEquippedEntriesBySlot = new();
    private readonly System.Collections.Generic.HashSet<string> _equippedItemIds = new();
    private readonly System.Collections.Generic.Dictionary<string, Dictionary> _inventoryItemsById = new();
    private readonly System.Collections.Generic.Dictionary<string, Dictionary> _reserveActiveEntriesById = new();
    private readonly System.Collections.Generic.Dictionary<string, Dictionary> _reserveRosterEntriesById = new();
    private readonly System.Collections.Generic.Dictionary<Button, string> _abilityIdsByButton = new();
    private string _lastLogLine = "";
    private string _turnOrderSignature = "";
    private string _partyListSignature = "";
    private const int MaxLogEntries = 250;
    private const float TurnOrderIconSize = 48.0f;
    private const float CombatLogTextPadding = 16.0f;
    private const float CombatLogMaxWrapWidth = 420.0f;

    private bool _showWorldHoverTooltip;
    private string _worldHoverTitle = "";
    private string _worldHoverDetails = "";
    private Vector2 _worldHoverCursor;
    private Color _worldHoverBackground = new(0.05f, 0.05f, 0.08f, 0.86f);
    private Color _worldHoverBorder = new(0.82f, 0.86f, 0.94f, 0.95f);
    private Color _worldHoverTitleColor = new(1.0f, 0.95f, 0.8f, 1.0f);
    private Color _worldHoverDetailsColor = new(0.95f, 0.9f, 0.78f, 1.0f);

    private readonly System.Collections.Generic.Dictionary<Control, Vector2> _panelOffsets = new();
    private readonly System.Collections.Generic.Dictionary<Control, Rect2> _basePanelRects = new();
    private readonly System.Collections.Generic.Dictionary<Control, Vector2> _panelSizeOverrides = new();
    private bool _isDraggingPanel;
    private Control _dragPanel;
    private Vector2 _dragGrabOffset;
    private bool _isResizingPanel;
    private Control _resizePanel;
    private Vector2 _resizeStartMouseGlobal;
    private Vector2 _resizeStartSize;
    private const float MinResizablePanelWidth = 260.0f;
    private const float MinResizablePanelHeight = 160.0f;

    public override void _Ready()
    {
        TopLevel = true;
        ZAsRelative = false;
        ZIndex = 4000;

        _utilityPanel = GetNode<PanelContainer>("UtilityPanel");
        _utilityHeader = GetNode<Label>("UtilityPanel/UtilityVBox/UtilityHeader");
        _helpButton = GetNode<Button>("UtilityPanel/UtilityVBox/UtilityButtons/HelpButton");
        _saveButton = GetNode<Button>("UtilityPanel/UtilityVBox/UtilityButtons/SaveButton");
        _loadButton = GetNode<Button>("UtilityPanel/UtilityVBox/UtilityButtons/LoadButton");
        _helpPanel = GetNode<PanelContainer>("HelpPanel");
        _helpHeader = GetNode<Label>("HelpPanel/HelpVBox/HelpHeader");
        _helpBody = GetNode<Label>("HelpPanel/HelpVBox/HelpBody");
        _closeHelpButton = GetNode<Button>("HelpPanel/HelpVBox/HelpButtons/CloseHelpButton");
        _actionPanel = GetNode<PanelContainer>("ActionPanel");
        _characterPanel = GetNode<PanelContainer>("CharacterPanel");
        _characterHeader = GetNode<Label>("CharacterPanel/CharacterVBox/CharacterHeader");
        _characterSummaryLabel = GetNode<Label>("CharacterPanel/CharacterVBox/CharacterDetailsPanel/CharacterDetailsVBox/CharacterSummaryLabel");
        _characterStatusLabel = GetNode<Label>("CharacterPanel/CharacterVBox/CharacterDetailsPanel/CharacterDetailsVBox/CharacterStatusLabel");
        _characterPrevButton = GetNode<Button>("CharacterPanel/CharacterVBox/CharacterCycleButtons/CharacterPrevButton");
        _characterNextButton = GetNode<Button>("CharacterPanel/CharacterVBox/CharacterCycleButtons/CharacterNextButton");
        _characterCloseButton = GetNode<Button>("CharacterPanel/CharacterVBox/CharacterCycleButtons/CharacterCloseButton");
        _partyPanel = GetNode<PanelContainer>("PartyPanel");
        _partyHeader = GetNode<Label>("PartyPanel/PartyVBox/PartyHeader");
        _partyList = GetNode<VBoxContainer>("PartyPanel/PartyVBox/PartyList");
        _turnOrderDisplay = GetNode<MarginContainer>("TurnOrderDisplay");
        _turnOrderIcons = GetNode<HBoxContainer>("TurnOrderDisplay/TurnOrderCenter/TurnOrderIcons");
        _combatBannerPanel = GetNode<PanelContainer>("CombatBanner");
        _combatBannerLabel = GetNode<Label>("CombatBanner/CombatBannerLabel");
        _combatLogPanel = GetNode<PanelContainer>("CombatLogPanel");
        _activeUnitLabel = GetNode<Label>("ActionPanel/ActionVBox/ActiveUnitLabel");
        var actionHeader = GetNode<Label>("ActionPanel/ActionVBox/ActionHeader");
        _abilityButton1 = GetNode<Button>("ActionPanel/ActionVBox/ActionButtons/AbilityButton1");
        _abilityButton2 = GetNode<Button>("ActionPanel/ActionVBox/ActionButtons/AbilityButton2");
        _abilityButton3 = GetNode<Button>("ActionPanel/ActionVBox/ActionButtons/AbilityButton3");
        _endTurnButton = GetNode<Button>("ActionPanel/ActionVBox/ActionButtons/EndTurnButton");
        _abilityButton1.FocusMode = FocusModeEnum.None;
        _abilityButton2.FocusMode = FocusModeEnum.None;
        _abilityButton3.FocusMode = FocusModeEnum.None;
        _endTurnButton.FocusMode = FocusModeEnum.None;
        _inventoryButton = GetNode<Button>("UtilityPanel/UtilityVBox/UtilityButtons/InventoryButton");
        _characterButton = GetNode<Button>("UtilityPanel/UtilityVBox/UtilityButtons/CharacterButton");
        _reserveButton = GetNode<Button>("UtilityPanel/UtilityVBox/UtilityButtons/ReserveButton");
        _combatLogHeader = GetNode<Label>("CombatLogPanel/CombatLogVBox/CombatLogHeader");
        _combatLog = GetNode<ItemList>("CombatLogPanel/CombatLogVBox/CombatLog");
        _combatLogResizeHandle = GetNode<Button>("CombatLogPanel/CombatLogVBox/CombatLogResizeRow/CombatLogResizeHandle");
        _inventoryPanel = GetNode<PanelContainer>("InventoryPanel");
        _inventoryHeader = GetNode<Label>("InventoryPanel/InventoryVBox/InventoryHeader");
        _inventoryUnitLabel = GetNode<Label>("InventoryPanel/InventoryVBox/InventoryUnitLabel");
        _inventoryEquippedSummaryLabel = GetNode<Label>("InventoryPanel/InventoryVBox/InventoryEquippedSummaryLabel");
        _inventoryEquippedItemList = GetNode<ItemList>("InventoryPanel/InventoryVBox/InventoryEquippedItemList");
        _inventoryGoldLabel = GetNode<Label>("InventoryPanel/InventoryVBox/InventoryGoldLabel");
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
        _vendorPanel = GetNode<PanelContainer>("VendorPanel");
        _vendorHeader = GetNode<Label>("VendorPanel/VendorVBox/VendorHeader");
        _vendorDialogueLabel = GetNode<Label>("VendorPanel/VendorVBox/VendorDialogueLabel");
        _vendorTalkButton = GetNode<Button>("VendorPanel/VendorVBox/VendorChoiceButtons/TalkButton");
        _vendorStoreButton = GetNode<Button>("VendorPanel/VendorVBox/VendorChoiceButtons/StoreButton");
        _vendorStoreTabs = GetNode<TabContainer>("VendorPanel/VendorVBox/StoreTabs");
        _vendorBuyList = GetNode<ItemList>("VendorPanel/VendorVBox/StoreTabs/Buy/BuyList");
        _vendorSellList = GetNode<ItemList>("VendorPanel/VendorVBox/StoreTabs/Sell/SellList");
        _vendorBuyButton = GetNode<Button>("VendorPanel/VendorVBox/StoreTabs/Buy/BuyButton");
        _vendorSellButton = GetNode<Button>("VendorPanel/VendorVBox/StoreTabs/Sell/SellButton");
        _vendorStatusLabel = GetNode<Label>("VendorPanel/VendorVBox/VendorStatusLabel");
        _closeVendorButton = GetNode<Button>("VendorPanel/VendorVBox/VendorButtons/CloseVendorButton");
        _reservePanel = GetNode<PanelContainer>("ReservePanel");
        _reserveHeader = GetNode<Label>("ReservePanel/ReserveVBox/ReserveHeader");
        _reserveActivePartyList = GetNode<ItemList>("ReservePanel/ReserveVBox/ReserveActivePartyList");
        _reserveRosterList = GetNode<ItemList>("ReservePanel/ReserveVBox/ReserveRosterList");
        _reserveDetailsLabel = GetNode<Label>("ReservePanel/ReserveVBox/ReserveDetailsLabel");
        _storeToReserveButton = GetNode<Button>("ReservePanel/ReserveVBox/ReserveButtons/StoreToReserveButton");
        _bringFromReserveButton = GetNode<Button>("ReservePanel/ReserveVBox/ReserveButtons/BringFromReserveButton");
        _closeReserveButton = GetNode<Button>("ReservePanel/ReserveVBox/ReserveButtons/CloseReserveButton");
        _confirmLootButton.Text = "Loot All";

        ApplyFantasyHudStyling();

        _inventoryPanel.MouseFilter = MouseFilterEnum.Stop;
        _vendorPanel.MouseFilter = MouseFilterEnum.Stop;
        _reservePanel.MouseFilter = MouseFilterEnum.Stop;

        _abilityButton1.Pressed += OnAbilityButton1Pressed;
        _abilityButton2.Pressed += OnAbilityButton2Pressed;
        _abilityButton3.Pressed += OnAbilityButton3Pressed;
        _endTurnButton.Pressed += OnEndTurnButtonPressed;
        _inventoryButton.Pressed += OnInventoryButtonPressed;
        _characterButton.Pressed += OnCharacterButtonPressed;
        _reserveButton.Pressed += OnReserveButtonPressed;
        _helpButton.Pressed += OnHelpButtonPressed;
        _saveButton.Pressed += OnSaveButtonPressed;
        _loadButton.Pressed += OnLoadButtonPressed;
        _closeHelpButton.Pressed += OnCloseHelpButtonPressed;
        _inventoryPrevUnitButton.Pressed += OnInventoryPrevUnitButtonPressed;
        _inventoryNextUnitButton.Pressed += OnInventoryNextUnitButtonPressed;
        _characterPrevButton.Pressed += OnCharacterPrevButtonPressed;
        _characterNextButton.Pressed += OnCharacterNextButtonPressed;
        _characterCloseButton.Pressed += OnCharacterCloseButtonPressed;
        _equipButton.Pressed += OnEquipButtonPressed;
        _unequipButton.Pressed += OnUnequipButtonPressed;
        _closeInventoryButton.Pressed += OnCloseInventoryButtonPressed;
        _inventoryEquippedItemList.ItemSelected += OnInventoryEquippedItemSelected;
        _inventoryItemList.ItemSelected += OnInventoryItemSelected;
        _lootItemList.ItemSelected += OnLootItemSelected;
        _confirmLootButton.Pressed += OnConfirmLootButtonPressed;
        _closeLootButton.Pressed += OnCloseLootButtonPressed;
        _vendorTalkButton.Pressed += OnVendorTalkButtonPressed;
        _vendorStoreButton.Pressed += OnVendorStoreButtonPressed;
        _vendorBuyButton.Pressed += OnVendorBuyButtonPressed;
        _vendorSellButton.Pressed += OnVendorSellButtonPressed;
        _closeVendorButton.Pressed += OnCloseVendorButtonPressed;
        _reserveActivePartyList.ItemSelected += OnReserveActivePartySelected;
        _reserveRosterList.ItemSelected += OnReserveRosterSelected;
        _storeToReserveButton.Pressed += OnStoreToReserveButtonPressed;
        _bringFromReserveButton.Pressed += OnBringFromReserveButtonPressed;
        _closeReserveButton.Pressed += OnCloseReserveButtonPressed;

        RegisterDraggable(_utilityHeader, _utilityPanel);
        RegisterDraggable(_helpHeader, _helpPanel);
        RegisterDraggable(_characterHeader, _characterPanel);
        RegisterDraggable(actionHeader, _actionPanel);
        RegisterDraggable(_combatLogHeader, _combatLogPanel);
        RegisterDraggable(_inventoryHeader, _inventoryPanel);
        RegisterDraggable(_lootHeader, _lootPanel);
        RegisterDraggable(_vendorHeader, _vendorPanel);
        RegisterDraggable(_reserveHeader, _reservePanel);
        RegisterResizable(_combatLogResizeHandle, _combatLogPanel);

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

        if (_characterButton != null)
        {
            _characterButton.Pressed -= OnCharacterButtonPressed;
        }

        if (_reserveButton != null)
        {
            _reserveButton.Pressed -= OnReserveButtonPressed;
        }

        if (_helpButton != null)
        {
            _helpButton.Pressed -= OnHelpButtonPressed;
        }

        if (_saveButton != null)
        {
            _saveButton.Pressed -= OnSaveButtonPressed;
        }

        if (_loadButton != null)
        {
            _loadButton.Pressed -= OnLoadButtonPressed;
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

        if (_characterPrevButton != null)
        {
            _characterPrevButton.Pressed -= OnCharacterPrevButtonPressed;
        }

        if (_characterNextButton != null)
        {
            _characterNextButton.Pressed -= OnCharacterNextButtonPressed;
        }

        if (_characterCloseButton != null)
        {
            _characterCloseButton.Pressed -= OnCharacterCloseButtonPressed;
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

        if (_vendorTalkButton != null)
        {
            _vendorTalkButton.Pressed -= OnVendorTalkButtonPressed;
        }

        if (_vendorStoreButton != null)
        {
            _vendorStoreButton.Pressed -= OnVendorStoreButtonPressed;
        }

        if (_vendorBuyButton != null)
        {
            _vendorBuyButton.Pressed -= OnVendorBuyButtonPressed;
        }

        if (_vendorSellButton != null)
        {
            _vendorSellButton.Pressed -= OnVendorSellButtonPressed;
        }

        if (_closeVendorButton != null)
        {
            _closeVendorButton.Pressed -= OnCloseVendorButtonPressed;
        }

        if (_reserveActivePartyList != null)
        {
            _reserveActivePartyList.ItemSelected -= OnReserveActivePartySelected;
        }

        if (_reserveRosterList != null)
        {
            _reserveRosterList.ItemSelected -= OnReserveRosterSelected;
        }

        if (_storeToReserveButton != null)
        {
            _storeToReserveButton.Pressed -= OnStoreToReserveButtonPressed;
        }

        if (_bringFromReserveButton != null)
        {
            _bringFromReserveButton.Pressed -= OnBringFromReserveButtonPressed;
        }

        if (_closeReserveButton != null)
        {
            _closeReserveButton.Pressed -= OnCloseReserveButtonPressed;
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

    private void OnCharacterButtonPressed()
    {
        ToggleCharacterVisible();
    }

    private void OnReserveButtonPressed()
    {
        ToggleReserveVisible();
    }

    private void OnHelpButtonPressed()
    {
        ToggleHelpVisible();
    }

    private void OnSaveButtonPressed()
    {
        EmitSignal(SignalName.SaveRequested);
    }

    private void OnLoadButtonPressed()
    {
        EmitSignal(SignalName.LoadRequested);
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

    private void OnCharacterPrevButtonPressed()
    {
        EmitSignal(SignalName.InventoryCycleRequested, -1);
    }

    private void OnCharacterNextButtonPressed()
    {
        EmitSignal(SignalName.InventoryCycleRequested, 1);
    }

    private void OnCharacterCloseButtonPressed()
    {
        SetCharacterVisible(false);
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
        EmitSignal(SignalName.LootConfirmRequested, interactionId);
    }

    private void OnConfirmLootButtonPressed()
    {
        if (string.IsNullOrEmpty(_lootAllInteractionId))
        {
            return;
        }

        EmitSignal(SignalName.LootConfirmRequested, _lootAllInteractionId);
    }

    private void OnCloseLootButtonPressed()
    {
        SetLootPanelVisible(false);
    }

    private void OnVendorTalkButtonPressed()
    {
        if (_vendorDialogueLabel != null)
        {
            _vendorDialogueLabel.Text = "Hello welcome to forest town! Please save us from the evil Necromancer in the Graveyard!";
        }

        if (_vendorStoreTabs != null)
        {
            _vendorStoreTabs.Visible = false;
        }
    }

    private void OnVendorStoreButtonPressed()
    {
        if (_vendorDialogueLabel != null)
        {
            _vendorDialogueLabel.Text = "Take a look. The vendor buys and sells for gold.";
        }

        if (_vendorStoreTabs != null)
        {
            _vendorStoreTabs.Visible = true;
        }
    }

    private void OnVendorBuyButtonPressed()
    {
        EmitSelectedVendorItem(_vendorBuyList, _vendorBuyItemsById, SignalName.VendorBuyRequested);
    }

    private void OnVendorSellButtonPressed()
    {
        EmitSelectedVendorItem(_vendorSellList, _vendorSellItemsById, SignalName.VendorSellRequested);
    }

    private void OnCloseVendorButtonPressed()
    {
        SetVendorPanelVisible(false);
    }

    private void OnReserveActivePartySelected(long index)
    {
        if (_reserveActivePartyList == null || _reserveDetailsLabel == null)
        {
            return;
        }

        if (_reserveRosterList != null)
        {
            _reserveRosterList.DeselectAll();
        }

        var metadata = _reserveActivePartyList.GetItemMetadata((int)index);
        var unitId = metadata.VariantType == Variant.Type.String ? metadata.AsString() : "";
        if (!string.IsNullOrEmpty(unitId) && _reserveActiveEntriesById.TryGetValue(unitId, out var entry))
        {
            _reserveDetailsLabel.Text = GetString(entry, "detail", _reserveActivePartyList.GetItemText((int)index));
        }
    }

    private void OnReserveRosterSelected(long index)
    {
        if (_reserveRosterList == null || _reserveDetailsLabel == null)
        {
            return;
        }

        if (_reserveActivePartyList != null)
        {
            _reserveActivePartyList.DeselectAll();
        }

        var metadata = _reserveRosterList.GetItemMetadata((int)index);
        var unitId = metadata.VariantType == Variant.Type.String ? metadata.AsString() : "";
        if (!string.IsNullOrEmpty(unitId) && _reserveRosterEntriesById.TryGetValue(unitId, out var entry))
        {
            _reserveDetailsLabel.Text = GetString(entry, "detail", _reserveRosterList.GetItemText((int)index));
        }
    }

    private void OnStoreToReserveButtonPressed()
    {
        if (_reserveActivePartyList == null)
        {
            return;
        }

        var selected = _reserveActivePartyList.GetSelectedItems();
        if (selected.Length == 0)
        {
            return;
        }

        var metadata = _reserveActivePartyList.GetItemMetadata(selected[0]);
        var unitId = metadata.VariantType == Variant.Type.String ? metadata.AsString() : "";
        if (!string.IsNullOrWhiteSpace(unitId))
        {
            EmitSignal(SignalName.ReserveStoreRequested, unitId);
        }
    }

    private void OnBringFromReserveButtonPressed()
    {
        if (_reserveRosterList == null)
        {
            return;
        }

        var selectedReserve = _reserveRosterList.GetSelectedItems();
        if (selectedReserve.Length == 0)
        {
            return;
        }

        var reserveMetadata = _reserveRosterList.GetItemMetadata(selectedReserve[0]);
        var reserveUnitId = reserveMetadata.VariantType == Variant.Type.String ? reserveMetadata.AsString() : "";
        if (string.IsNullOrWhiteSpace(reserveUnitId))
        {
            return;
        }

        var replacePartyUnitId = "";
        if (_reserveActivePartyList != null)
        {
            var selectedParty = _reserveActivePartyList.GetSelectedItems();
            if (selectedParty.Length > 0)
            {
                var partyMetadata = _reserveActivePartyList.GetItemMetadata(selectedParty[0]);
                replacePartyUnitId = partyMetadata.VariantType == Variant.Type.String ? partyMetadata.AsString() : "";
            }
        }

        EmitSignal(SignalName.ReserveBringRequested, reserveUnitId, replacePartyUnitId);
    }

    private void OnCloseReserveButtonPressed()
    {
        SetReservePanelVisible(false);
    }

    private void EmitSelectedVendorItem(ItemList list, System.Collections.Generic.Dictionary<string, Dictionary> entriesById, StringName signalName)
    {
        if (list == null)
        {
            return;
        }

        var selected = list.GetSelectedItems();
        if (selected.Length == 0)
        {
            return;
        }

        var metadata = list.GetItemMetadata(selected[0]);
        var itemId = metadata.VariantType == Variant.Type.String ? metadata.AsString() : "";
        if (string.IsNullOrEmpty(itemId) || !entriesById.ContainsKey(itemId))
        {
            return;
        }

        EmitSignal(signalName, itemId);
    }

    private void OnViewportSizeChanged()
    {
        EnsureFullscreenLayout();
        ApplyHudLayout();
    }

    public override void _Input(InputEvent @event)
    {
        if (_isResizingPanel && _resizePanel != null)
        {
            if (@event is InputEventMouseMotion resizeMotion)
            {
                UpdatePanelResize(resizeMotion.GlobalPosition);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (@event is InputEventMouseButton resizeButton && resizeButton.ButtonIndex == MouseButton.Left && !resizeButton.Pressed)
            {
                EndPanelResize();
                GetViewport().SetInputAsHandled();
                return;
            }
        }

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

    public override void _Draw()
    {
        if (!_showWorldHoverTooltip)
        {
            return;
        }

        var viewport = GetViewportRect().Size;
        var font = ThemeDB.FallbackFont;
        if (font == null)
        {
            return;
        }

        var titleWidth = font.GetStringSize(_worldHoverTitle, HorizontalAlignment.Left, -1, ThemeDB.FallbackFontSize).X;
        var detailsWidth = font.GetStringSize(_worldHoverDetails, HorizontalAlignment.Left, -1, ThemeDB.FallbackFontSize).X;
        var contentWidth = Mathf.Max(titleWidth, detailsWidth);
        var panelSize = new Vector2(Mathf.Max(236.0f, contentWidth + 24.0f), _worldHoverDetails.Contains("\n") ? 92.0f : 62.0f);

        var cursor = _worldHoverCursor + new Vector2(14.0f, 14.0f);
        if (cursor.X + panelSize.X > viewport.X)
        {
            cursor.X = viewport.X - panelSize.X - 8.0f;
        }

        if (cursor.Y + panelSize.Y > viewport.Y)
        {
            cursor.Y = viewport.Y - panelSize.Y - 8.0f;
        }

        var rect = new Rect2(cursor, panelSize);
        DrawRect(rect, _worldHoverBackground, true);
        DrawRect(rect, _worldHoverBorder, false, 2.0f);
        DrawString(ThemeDB.FallbackFont, cursor + new Vector2(10.0f, 20.0f), _worldHoverTitle, HorizontalAlignment.Left, -1, ThemeDB.FallbackFontSize, _worldHoverTitleColor);
        DrawString(ThemeDB.FallbackFont, cursor + new Vector2(10.0f, 42.0f), _worldHoverDetails, HorizontalAlignment.Left, -1, ThemeDB.FallbackFontSize, _worldHoverDetailsColor);
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
        var sidebarRight = size.X - SidebarRightInset;
        var sidebarLeft = Mathf.Max(GridPixelWidth + Margin, sidebarRight - SidebarWidth);

        const float panelGap = 10.0f;
        const float utilityHeight = 20.0f;
        const float characterHeight = 218.0f;
        const float helpHeight = 220.0f;
        const float actionHeight = 68.0f;

        var utilityTop = Margin;
        var actionTop = utilityTop + utilityHeight + panelGap + 50.0f;
        var detailsTop = actionTop + actionHeight + panelGap + 30.0f;
        var combatTop = detailsTop;
        var combatHeight = 100.0f;

        ApplyPanelRect(_utilityPanel, new Rect2(new Vector2(sidebarLeft, utilityTop), new Vector2(sidebarRight - sidebarLeft, utilityHeight)), size);
        ApplyPanelRect(_actionPanel, new Rect2(new Vector2(sidebarLeft, actionTop), new Vector2(sidebarRight - sidebarLeft, actionHeight)), size);
        ApplyPanelRect(_characterPanel, new Rect2(new Vector2(sidebarLeft, detailsTop), new Vector2(sidebarRight - sidebarLeft, characterHeight)), size);
        ApplyPanelRect(_helpPanel, new Rect2(new Vector2(sidebarLeft, detailsTop), new Vector2(sidebarRight - sidebarLeft, helpHeight)), size);
        ApplyPanelRect(_combatLogPanel, new Rect2(new Vector2(sidebarLeft, combatTop), new Vector2(sidebarRight - sidebarLeft, combatHeight)), size);
        ApplyPanelRect(_lootPanel, new Rect2(new Vector2(Margin, Mathf.Max(140.0f, size.Y - 286.0f)), new Vector2(420.0f, 274.0f)), size);
        ApplyPanelRect(_vendorPanel, new Rect2(new Vector2(Mathf.Max(Margin, (size.X - 480.0f) * 0.5f), Mathf.Max(Margin, (size.Y - 440.0f) * 0.5f)), new Vector2(480.0f, 440.0f)), size);
        ApplyPanelRect(_reservePanel, new Rect2(new Vector2(Mathf.Max(Margin, (size.X - 520.0f) * 0.5f), Mathf.Max(Margin, (size.Y - 480.0f) * 0.5f)), new Vector2(520.0f, 480.0f)), size);
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

    private void RegisterResizable(Control handle, Control panel)
    {
        if (handle == null || panel == null)
        {
            return;
        }

        handle.MouseFilter = MouseFilterEnum.Stop;
        handle.GuiInput += (inputEvent) => OnResizeHandleInput(inputEvent, panel);
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

    private void OnResizeHandleInput(InputEvent inputEvent, Control panel)
    {
        if (inputEvent is not InputEventMouseButton button || button.ButtonIndex != MouseButton.Left)
        {
            return;
        }

        if (button.Pressed)
        {
            _isResizingPanel = true;
            _resizePanel = panel;
            _resizeStartMouseGlobal = button.GlobalPosition;
            _resizeStartSize = panel.Size;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_isResizingPanel && _resizePanel == panel)
        {
            EndPanelResize();
            GetViewport().SetInputAsHandled();
        }
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

    private void UpdatePanelResize(Vector2 mouseGlobal)
    {
        var panel = _resizePanel;
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
        var delta = mouseGlobal - _resizeStartMouseGlobal;
        var maxWidth = Mathf.Max(MinResizablePanelWidth, viewportSize.X - panel.Position.X);
        var maxHeight = Mathf.Max(MinResizablePanelHeight, viewportSize.Y - panel.Position.Y);
        var width = Mathf.Clamp(_resizeStartSize.X + delta.X, MinResizablePanelWidth, maxWidth);
        var height = Mathf.Clamp(_resizeStartSize.Y + delta.Y, MinResizablePanelHeight, maxHeight);

        _panelSizeOverrides[panel] = new Vector2(width, height);
        panel.Size = new Vector2(width, height);
        ApplyHudLayout();
    }

    private void EndPanelResize()
    {
        _isResizingPanel = false;
        _resizePanel = null;
    }

    private void ApplyPanelRect(Control panel, Rect2 baseRect, Vector2 viewportSize)
    {
        if (panel == null)
        {
            return;
        }

        _basePanelRects[panel] = baseRect;
        var panelSize = _panelSizeOverrides.TryGetValue(panel, out var overriddenSize)
            ? overriddenSize
            : baseRect.Size;

        var isResizablePanel = panel == _combatLogPanel;
        var minWidth = isResizablePanel ? MinResizablePanelWidth : 1.0f;
        var minHeight = isResizablePanel ? MinResizablePanelHeight : 1.0f;

        panelSize.X = Mathf.Clamp(panelSize.X, minWidth, Mathf.Max(minWidth, viewportSize.X));
        panelSize.Y = Mathf.Clamp(panelSize.Y, minHeight, Mathf.Max(minHeight, viewportSize.Y));

        var offset = _panelOffsets.TryGetValue(panel, out var storedOffset) ? storedOffset : Vector2.Zero;
        var pos = baseRect.Position + offset;
        pos.X = Mathf.Clamp(pos.X, 0.0f, Mathf.Max(0.0f, viewportSize.X - panelSize.X));
        pos.Y = Mathf.Clamp(pos.Y, 0.0f, Mathf.Max(0.0f, viewportSize.Y - panelSize.Y));

        SetRect(panel, pos.X, pos.Y, pos.X + panelSize.X, pos.Y + panelSize.Y);
        _panelOffsets[panel] = pos - baseRect.Position;
        _panelSizeOverrides[panel] = panelSize;
    }

    private void UpdatePanelOffsetFromCurrent(Control panel)
    {
        if (panel == null || !_basePanelRects.TryGetValue(panel, out var baseRect))
        {
            return;
        }

        _panelOffsets[panel] = panel.Position - baseRect.Position;
    }

    private void ApplyFantasyHudStyling()
    {
        var panelBackground = new Color(0.1f, 0.12f, 0.15f, 0.94f);
        var panelBorder = new Color(0.34f, 0.47f, 0.58f, 0.95f);
        var panelShadow = new Color(0.01f, 0.02f, 0.03f, 0.35f);
        var headerColor = new Color(0.86f, 0.93f, 0.98f, 1.0f);
        var bodyColor = new Color(0.83f, 0.88f, 0.93f, 1.0f);
        var mutedBodyColor = new Color(0.66f, 0.74f, 0.81f, 1.0f);

        var panelStyle = CreatePanelStyle(panelBackground, panelBorder, panelShadow);
        StylePanel(_utilityPanel, panelStyle);
        StylePanel(_actionPanel, panelStyle);
        StylePanel(_characterPanel, panelStyle);
        StylePanel(_partyPanel, panelStyle);
        StylePanel(_combatLogPanel, panelStyle);
        StylePanel(_inventoryPanel, panelStyle);
        StylePanel(_helpPanel, panelStyle);
        StylePanel(_lootPanel, panelStyle);
        StylePanel(_vendorPanel, panelStyle);
        StylePanel(_reservePanel, panelStyle);
        StylePanel(_combatBannerPanel, panelStyle);

        StyleHeaderLabel(_utilityHeader, headerColor);
        StyleHeaderLabel(_helpHeader, headerColor);
        StyleHeaderLabel(_characterHeader, headerColor);
        StyleHeaderLabel(_partyHeader, headerColor);
        StyleHeaderLabel(_combatLogHeader, headerColor);
        StyleHeaderLabel(_inventoryHeader, headerColor);
        StyleHeaderLabel(_lootHeader, headerColor);
        StyleHeaderLabel(_vendorHeader, headerColor);
        StyleHeaderLabel(_reserveHeader, headerColor);

        if (_combatBannerLabel != null)
        {
            _combatBannerLabel.AddThemeColorOverride("font_color", new Color(0.98f, 0.93f, 0.78f, 1.0f));
            _combatBannerLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.55f));
            _combatBannerLabel.AddThemeConstantOverride("shadow_offset_x", 1);
            _combatBannerLabel.AddThemeConstantOverride("shadow_offset_y", 1);
            _combatBannerLabel.AddThemeFontSizeOverride("font_size", 28);
        }

        StyleBodyLabel(_activeUnitLabel, bodyColor, 15);
        StyleBodyLabel(_characterSummaryLabel, bodyColor, 14);
        StyleBodyLabel(_characterStatusLabel, mutedBodyColor, 13);
        StyleBodyLabel(_helpBody, bodyColor, 14);
        StyleBodyLabel(_inventoryUnitLabel, bodyColor, 14);
        StyleBodyLabel(_inventoryEquippedSummaryLabel, mutedBodyColor, 13);
        StyleBodyLabel(_inventoryGoldLabel, bodyColor, 14);
        StyleBodyLabel(_inventoryItemDetails, mutedBodyColor, 13);
        StyleBodyLabel(_lootDetailsLabel, mutedBodyColor, 13);
        StyleBodyLabel(_vendorDialogueLabel, bodyColor, 14);
        StyleBodyLabel(_vendorStatusLabel, mutedBodyColor, 13);
        StyleBodyLabel(_reserveDetailsLabel, mutedBodyColor, 13);

        StyleButton(_inventoryButton, false);
        StyleButton(_helpButton, false);
        StyleButton(_saveButton, false);
        StyleButton(_loadButton, false);
        StyleButton(_characterButton, false);
        StyleButton(_reserveButton, false);
        StyleButton(_characterPrevButton, false);
        StyleButton(_characterNextButton, false);
        StyleButton(_characterCloseButton, false);
        StyleButton(_abilityButton1, true);
        StyleButton(_abilityButton2, true);
        StyleButton(_abilityButton3, true);
        StyleButton(_endTurnButton, true);
        StyleButton(_closeHelpButton, false);
        StyleButton(_inventoryPrevUnitButton, false);
        StyleButton(_inventoryNextUnitButton, false);
        StyleButton(_equipButton, true);
        StyleButton(_unequipButton, false);
        StyleButton(_closeInventoryButton, false);
        StyleButton(_confirmLootButton, true);
        StyleButton(_closeLootButton, false);
        StyleButton(_vendorTalkButton, true);
        StyleButton(_vendorStoreButton, true);
        StyleButton(_vendorBuyButton, true);
        StyleButton(_vendorSellButton, true);
        StyleButton(_closeVendorButton, false);
        StyleButton(_storeToReserveButton, false);
        StyleButton(_bringFromReserveButton, true);
        StyleButton(_closeReserveButton, false);

        StyleItemList(_combatLog, bodyColor, mutedBodyColor);
        StyleItemList(_inventoryEquippedItemList, bodyColor, mutedBodyColor);
        StyleItemList(_inventoryItemList, bodyColor, mutedBodyColor);
        StyleItemList(_lootItemList, bodyColor, mutedBodyColor);
        StyleItemList(_vendorBuyList, bodyColor, mutedBodyColor);
        StyleItemList(_vendorSellList, bodyColor, mutedBodyColor);
        StyleItemList(_reserveActivePartyList, bodyColor, mutedBodyColor);
        StyleItemList(_reserveRosterList, bodyColor, mutedBodyColor);

        var characterInnerPanel = GetNodeOrNull<PanelContainer>("CharacterPanel/CharacterVBox/CharacterDetailsPanel");
        if (characterInnerPanel != null)
        {
            var innerStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.11f, 0.13f, 0.95f),
                BorderColor = new Color(0.3f, 0.42f, 0.5f, 0.95f),
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderWidthLeft = 1,
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2,
                CornerRadiusBottomRight = 2,
                CornerRadiusBottomLeft = 2,
                ContentMarginTop = 6,
                ContentMarginRight = 6,
                ContentMarginBottom = 6,
                ContentMarginLeft = 6
            };
            characterInnerPanel.AddThemeStyleboxOverride("panel", innerStyle);
        }

        _worldHoverBackground = new Color(0.08f, 0.11f, 0.14f, 0.95f);
        _worldHoverBorder = panelBorder;
        _worldHoverTitleColor = headerColor;
        _worldHoverDetailsColor = bodyColor;
    }

    private static StyleBoxFlat CreatePanelStyle(Color background, Color border, Color shadow)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusBottomLeft = 4,
            ContentMarginTop = 6,
            ContentMarginRight = 8,
            ContentMarginBottom = 6,
            ContentMarginLeft = 8,
            ShadowColor = shadow,
            ShadowSize = 2,
            AntiAliasing = true
        };
    }

    private static void StylePanel(PanelContainer panel, StyleBoxFlat baseStyle)
    {
        if (panel == null)
        {
            return;
        }

        panel.AddThemeStyleboxOverride("panel", (StyleBox)baseStyle.Duplicate());
    }

    private static void StyleHeaderLabel(Label label, Color color)
    {
        if (label == null)
        {
            return;
        }

        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.45f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        label.AddThemeFontSizeOverride("font_size", 16);
    }

    private static void StyleBodyLabel(Label label, Color color, int fontSize)
    {
        if (label == null)
        {
            return;
        }

        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.35f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        label.AddThemeFontSizeOverride("font_size", fontSize);
    }

    private static void StyleBodyRichText(RichTextLabel label, Color color, Color mutedColor, int fontSize)
    {
        if (label == null)
        {
            return;
        }

        var innerPanelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.11f, 0.13f, 0.95f),
            BorderColor = new Color(0.3f, 0.42f, 0.5f, 0.95f),
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomRight = 2,
            CornerRadiusBottomLeft = 2,
            ContentMarginTop = 6,
            ContentMarginRight = 6,
            ContentMarginBottom = 6,
            ContentMarginLeft = 6
        };

        label.AddThemeStyleboxOverride("normal", innerPanelStyle);
        label.AddThemeColorOverride("default_color", color);
        label.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.35f));
        label.AddThemeColorOverride("font_outline_color", mutedColor);
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        label.AddThemeConstantOverride("line_separation", 2);
        label.AddThemeFontSizeOverride("normal_font_size", fontSize);
    }

    private static void StyleButton(Button button, bool emphasized)
    {
        if (button == null)
        {
            return;
        }

        var normalBg = emphasized ? new Color(0.17f, 0.27f, 0.34f, 0.98f) : new Color(0.14f, 0.17f, 0.21f, 0.96f);
        var hoverBg = emphasized ? new Color(0.21f, 0.33f, 0.42f, 1.0f) : new Color(0.18f, 0.22f, 0.27f, 1.0f);
        var pressedBg = emphasized ? new Color(0.13f, 0.21f, 0.27f, 1.0f) : new Color(0.11f, 0.14f, 0.17f, 1.0f);
        var border = emphasized ? new Color(0.45f, 0.66f, 0.8f, 0.95f) : new Color(0.34f, 0.47f, 0.58f, 0.9f);
        var disabledBg = new Color(0.1f, 0.12f, 0.15f, 0.82f);
        var disabledText = new Color(0.48f, 0.54f, 0.6f, 1.0f);

        button.AddThemeStyleboxOverride("normal", CreateButtonStyle(normalBg, border));
        button.AddThemeStyleboxOverride("hover", CreateButtonStyle(hoverBg, border));
        button.AddThemeStyleboxOverride("pressed", CreateButtonStyle(pressedBg, border));
        button.AddThemeStyleboxOverride("focus", CreateButtonStyle(hoverBg, new Color(0.62f, 0.82f, 0.97f, 1.0f)));
        button.AddThemeStyleboxOverride("disabled", CreateButtonStyle(disabledBg, new Color(0.26f, 0.32f, 0.38f, 0.85f)));

        button.AddThemeColorOverride("font_color", new Color(0.89f, 0.94f, 0.98f, 1.0f));
        button.AddThemeColorOverride("font_focus_color", new Color(0.95f, 0.98f, 1.0f, 1.0f));
        button.AddThemeColorOverride("font_pressed_color", new Color(0.85f, 0.92f, 0.97f, 1.0f));
        button.AddThemeColorOverride("font_hover_color", new Color(0.94f, 0.98f, 1.0f, 1.0f));
        button.AddThemeColorOverride("font_disabled_color", disabledText);
        button.AddThemeFontSizeOverride("font_size", 14);
    }

    private static StyleBoxFlat CreateButtonStyle(Color background, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusBottomLeft = 3,
            ContentMarginTop = 5,
            ContentMarginRight = 8,
            ContentMarginBottom = 5,
            ContentMarginLeft = 8,
            AntiAliasing = true
        };
    }

    private static void StyleItemList(ItemList list, Color bodyColor, Color mutedBodyColor)
    {
        if (list == null)
        {
            return;
        }

        var baseStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.11f, 0.13f, 0.95f),
            BorderColor = new Color(0.3f, 0.42f, 0.5f, 0.95f),
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomRight = 2,
            CornerRadiusBottomLeft = 2,
            ContentMarginTop = 4,
            ContentMarginRight = 4,
            ContentMarginBottom = 4,
            ContentMarginLeft = 4
        };

        list.AddThemeStyleboxOverride("panel", baseStyle);
        list.AddThemeColorOverride("font_color", bodyColor);
        list.AddThemeColorOverride("font_selected_color", new Color(0.94f, 0.98f, 1.0f, 1.0f));
        list.AddThemeColorOverride("guide_color", mutedBodyColor);
        list.AddThemeColorOverride("cursor_color", new Color(0.54f, 0.78f, 0.94f, 0.9f));
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

    public void SetCharacterSummary(string text)
    {
        if (_characterSummaryLabel != null)
        {
            _characterSummaryLabel.Text = text;
        }
    }

    public void SetCharacterStatusSummary(string text)
    {
        if (_characterStatusLabel != null)
        {
            _characterStatusLabel.Text = text;
        }
    }

    public string BuildCharacterSummary(Unit unit, string selectedAbilityName, string primaryAbilityName)
    {
        if (unit == null)
        {
            return "No active character.";
        }

        var status = unit.IsDead ? "Defeated" : "Ready";
        if (unit.IsDefending && !unit.IsDead)
        {
            status = $"Defending (-{Unit.DefendDamageReductionPercent}% damage taken)";
        }

        var encounterLabel = string.IsNullOrEmpty(unit.EncounterId) ? "Party" : unit.EncounterId;
        var experienceToNextLevel = Mathf.Max(0, unit.ExperienceToNextLevel - unit.Experience);

        return
            $"Name: {unit.UnitName}\n" +
            $"Race: {unit.Race}\n" +
            $"Team: {unit.Team}\n" +
            $"Status: {status}\n" +
            $"\n" +
            $"Level: {unit.Level}\n" +
            $"Experience: {unit.Experience}/{unit.ExperienceToNextLevel} ({experienceToNextLevel} to next level)\n" +
            $"\n" +
            $"HP: {unit.HitPoints}/{unit.MaxHitPoints}\n" +
            $"MP: {unit.MagicPoints}/{unit.MaxMagicPoints}\n" +
            $"Armor Class: {unit.ArmorClass}\n" +
            $"\n" +
            $"Strength: {unit.Strength}\n" +
            $"Dexterity: {unit.Dexterity}\n" +
            $"Constitution: {unit.Constitution}\n" +
            $"Intelligence: {unit.Intelligence}\n" +
            $"Wisdom: {unit.Wisdom}\n" +
            $"\n" +
            $"Attack Damage: {unit.AttackDamage}\n" +
            $"Attack Range: {unit.AttackRange}\n" +
                $"Initiative: {unit.EffectiveInitiative} (base {unit.Initiative})";
    }

    public string BuildCharacterStatusSummary(Unit unit)
    {
        if (unit == null)
        {
            return "Status Effects: none";
        }

        var entries = unit.GetStatusEntriesForHud();
        if (entries == null || entries.Count == 0)
        {
            return "Status Effects: none";
        }

        var builder = new StringBuilder();
        builder.Append("Status Effects:\n");
        foreach (var entry in entries)
        {
            var label = GetString(entry, "label", "Effect");
            var isBuff = GetBool(entry, "is_buff", false);
            var remainingTurns = GetInt(entry, "remaining_turns", -1);
            var startDelayTurns = GetInt(entry, "start_delay_turns", 0);
            var stacks = Mathf.Max(1, GetInt(entry, "stacks", 1));

            builder.Append("- ");
            builder.Append(isBuff ? "Buff: " : "Debuff: ");
            builder.Append(label);
            if (stacks > 1)
            {
                builder.Append($" ({stacks} stacks)");
            }

            if (startDelayTurns > 0)
            {
                builder.Append($" (starts in {startDelayTurns} turn{(startDelayTurns == 1 ? "" : "s")})");
            }
            else if (remainingTurns > 0)
            {
                builder.Append($" ({remainingTurns} turn{(remainingTurns == 1 ? "" : "s")} left)");
            }

            builder.Append("\n");
        }

        return builder.ToString().TrimEnd();
    }

    public string BuildHelpText(string flowState)
    {
        var common =
            "CONTROLS\n" +
            "- Inventory: I\n" +
            "- Reserves: R\n" +
            "- Help: H\n" +
            "- Save/Load: Utility panel buttons\n" +
            "- Inspect: hover units and interactables\n" +
            "- Character page: click a party card\n" +
            "- Cycle target: Tab / Shift+Tab or Prev/Next Member\n";

        if (flowState == "Exploration")
        {
            return common +
                "\nEXPLORATION\n" +
                "- Move party: WASD or Arrow keys\n" +
                "- Marching order: drag party cards\n" +
                "- Interact: left-click chest/loot while adjacent (range 1)\n" +
                "- Loot UI: click item to loot, or use Loot All\n" +
                "- Map transitions: step on glowing edge cells\n" +
                "- Combat starts when enemies engage your party";
        }

        if (flowState == "Combat")
        {
            return common +
                "\nCOMBAT\n" +
                "- Move: WASD/Arrow keys or click reachable cells\n" +
                "- Ability: F, then choose direction/cell\n" +
                "- End turn: Space or End Turn button\n" +
                "- Limits: one ability use and limited movement each turn\n" +
                "- Win encounter to return to exploration";
        }

        return common +
            "\nDEFEAT\n" +
            "- All party members are down\n" +
            "- Restart encounter or reload to continue";
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
            var isEnabled = GetInt(entry, "is_enabled", 0) == 1;
            var isSelected = GetInt(entry, "is_selected", 0) == 1;

            button.Visible = true;
            button.Text = cooldownRemaining > 0
                ? $"{label} (CD {cooldownRemaining})"
                : label;
            button.TooltipText = detail;
            button.Disabled = !canUseAnyAbility || !isEnabled || cooldownRemaining > 0;
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
            $"Turn: {active.UnitName} ({active.Team})\n" +
            $"HP {active.HitPoints}/{active.MaxHitPoints}  MP {active.MagicPoints}/{active.MaxMagicPoints}  Move {active.RemainingMovement}/{active.MovementPerTurn}";
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

    public void SetInventoryGold(int goldAmount)
    {
        if (_inventoryGoldLabel != null)
        {
            _inventoryGoldLabel.Text = $"Party Gold: {Mathf.Max(0, goldAmount)} gp";
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

    public void SetCharacterVisible(bool visible)
    {
        if (_characterPanel != null)
        {
            _characterPanel.Visible = visible;
            if (visible)
            {
                _characterPanel.MoveToFront();
            }
        }
    }

    public void SetPartyList(Array<Unit> party, string selectedUnitId, bool reorderEnabled)
    {
        if (_partyList == null)
        {
            return;
        }

        var signatureBuilder = new StringBuilder($"{selectedUnitId}|{reorderEnabled}");
        if (party != null)
        {
            foreach (var unit in party)
            {
                if (unit != null)
                {
                    signatureBuilder.Append('|')
                        .Append(unit.UnitId).Append(':')
                        .Append(unit.HitPoints).Append('/').Append(unit.MaxHitPoints).Append(':')
                        .Append(unit.MagicPoints).Append('/').Append(unit.MaxMagicPoints).Append(':')
                        .Append(unit.ArmorClass).Append(':')
                        .Append(unit.IsDead);
                }
            }
        }

        var signature = signatureBuilder.ToString();
        if (signature == _partyListSignature)
        {
            return;
        }

        _partyListSignature = signature;
        foreach (var child in _partyList.GetChildren())
        {
            child.QueueFree();
        }

        if (party == null)
        {
            return;
        }

        foreach (var unit in party)
        {
            if (unit != null)
            {
                _partyList.AddChild(CreatePartyCard(unit, unit.UnitId == selectedUnitId, reorderEnabled));
            }
        }
    }

    private PartyCard CreatePartyCard(Unit unit, bool selected, bool reorderEnabled)
    {
        var accent = unit.IsDead
            ? new Color(0.35f, 0.35f, 0.35f, 1.0f)
            : new Color(0.72f, 0.58f, 0.3f, 1.0f);
        var card = new PartyCard
        {
            UnitId = unit.UnitId,
            ReorderEnabled = reorderEnabled && !unit.IsDead,
            CustomMinimumSize = new Vector2(0.0f, 72.0f),
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            TooltipText = reorderEnabled
                ? $"Open {unit.UnitName}\nDrag to change marching order"
                : $"Open {unit.UnitName}"
        };
        card.AddThemeStyleboxOverride("normal", CreatePartyCardStyle(new Color(0.055f, 0.06f, 0.065f, 0.97f), accent, selected ? 2 : 1));
        card.AddThemeStyleboxOverride("hover", CreatePartyCardStyle(new Color(0.09f, 0.095f, 0.1f, 1.0f), new Color(0.88f, 0.73f, 0.4f, 1.0f), 2));
        card.AddThemeStyleboxOverride("pressed", CreatePartyCardStyle(new Color(0.035f, 0.04f, 0.045f, 1.0f), accent, 2));
        card.AddThemeStyleboxOverride("focus", CreatePartyCardStyle(new Color(0.075f, 0.08f, 0.085f, 1.0f), new Color(0.95f, 0.82f, 0.5f, 1.0f), 2));
        card.UnitSelected += unitId => EmitSignal(SignalName.PartyUnitSelected, unitId);
        card.ReorderRequested += (sourceUnitId, targetUnitId) => EmitSignal(SignalName.PartyOrderRequested, sourceUnitId, targetUnitId);

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 8);
        card.AddChild(row);
        row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 8.0f;
        row.OffsetTop = 6.0f;
        row.OffsetRight = -8.0f;
        row.OffsetBottom = -6.0f;

        var portrait = new TextureRect
        {
            CustomMinimumSize = new Vector2(48.0f, 48.0f),
            Texture = unit.GetTurnOrderIcon(),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = unit.IsDead ? new Color(0.5f, 0.5f, 0.5f, 0.65f) : Colors.White
        };
        row.AddChild(portrait);

        var details = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        details.AddThemeConstantOverride("separation", 2);
        row.AddChild(details);

        var name = new Label
        {
            Text = unit.IsDead ? $"{unit.UnitName} (Down)" : unit.UnitName,
            MouseFilter = MouseFilterEnum.Ignore
        };
        name.AddThemeColorOverride("font_color", unit.IsDead ? new Color(0.55f, 0.55f, 0.55f) : new Color(0.9f, 0.82f, 0.64f));
        name.AddThemeFontSizeOverride("font_size", 13);
        details.AddChild(name);

        details.AddChild(CreatePartyResourceRow("HP", unit.HitPoints, unit.MaxHitPoints, new Color(0.7f, 0.13f, 0.13f)));
        details.AddChild(CreatePartyResourceRow("MP", unit.MagicPoints, unit.MaxMagicPoints, new Color(0.12f, 0.28f, 0.62f)));
        details.AddChild(CreatePartyStatRow($"AC {unit.ArmorClass}"));
        return card;
    }

    private static Label CreatePartyStatRow(string text)
    {
        var label = new Label
        {
            Text = text,
            MouseFilter = MouseFilterEnum.Ignore
        };

        label.AddThemeColorOverride("font_color", new Color(0.76f, 0.73f, 0.66f));
        label.AddThemeFontSizeOverride("font_size", 10);
        return label;
    }

    private static HBoxContainer CreatePartyResourceRow(string label, int value, int maximum, Color fillColor)
    {
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 5);

        var valueLabel = new Label
        {
            Text = $"{label} {value}/{maximum}",
            CustomMinimumSize = new Vector2(62.0f, 0.0f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        valueLabel.AddThemeColorOverride("font_color", new Color(0.76f, 0.73f, 0.66f));
        valueLabel.AddThemeFontSizeOverride("font_size", 10);
        row.AddChild(valueLabel);

        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = Mathf.Max(1, maximum),
            Value = Mathf.Clamp(value, 0, Mathf.Max(1, maximum)),
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0.0f, 8.0f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        bar.AddThemeStyleboxOverride("background", CreateResourceBarStyle(new Color(0.015f, 0.018f, 0.022f, 0.9f)));
        bar.AddThemeStyleboxOverride("fill", CreateResourceBarStyle(fillColor));
        row.AddChild(bar);
        return row;
    }

    private static StyleBoxFlat CreatePartyCardStyle(Color background, Color border, int borderWidth)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusBottomLeft = 6,
            ContentMarginLeft = 7.0f,
            ContentMarginTop = 6.0f,
            ContentMarginRight = 7.0f,
            ContentMarginBottom = 6.0f
        };
    }

    private static StyleBoxFlat CreateResourceBarStyle(Color color)
    {
        return new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusBottomLeft = 3
        };
    }

    public void ToggleCharacterVisible()
    {
        if (_characterPanel != null)
        {
            SetCharacterVisible(!_characterPanel.Visible);
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

    public void ToggleReserveVisible()
    {
        if (_reservePanel != null)
        {
            SetReservePanelVisible(!_reservePanel.Visible);
        }
    }

    public void SetReservePanelVisible(bool visible)
    {
        if (_reservePanel != null)
        {
            _reservePanel.Visible = visible;
            if (visible)
            {
                _reservePanel.MoveToFront();
            }
        }
    }

    public void SetReserveEntries(Array<Dictionary> activePartyEntries, Array<Dictionary> reserveEntries)
    {
        SetReserveList(_reserveActivePartyList, _reserveActiveEntriesById, activePartyEntries, "No active party members.");
        SetReserveList(_reserveRosterList, _reserveRosterEntriesById, reserveEntries, "No reserve members.");

        if (_storeToReserveButton != null)
        {
            _storeToReserveButton.Disabled = _reserveActivePartyList == null || _reserveActivePartyList.ItemCount == 0;
        }

        if (_bringFromReserveButton != null)
        {
            _bringFromReserveButton.Disabled = _reserveRosterList == null || _reserveRosterList.ItemCount == 0;
        }

        if (_reserveDetailsLabel != null)
        {
            _reserveDetailsLabel.Text = "Select an active member to send to reserves, or select a reserve member to bring back.";
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

    public bool ShouldBlockWorldMouseInput()
    {
        if (_isDraggingPanel || _isResizingPanel)
        {
            return true;
        }

        var viewport = GetViewport();
        if (viewport == null)
        {
            return false;
        }

        var hovered = viewport.GuiGetHoveredControl();
        if (hovered == null)
        {
            return false;
        }

        // HudController itself is a fullscreen root control; only child controls should block world clicks.
        if (hovered == this)
        {
            return false;
        }

        return hovered == this || IsAncestorOf(hovered);
    }

    public void SetLootPanelVisible(bool visible)
    {
        if (_lootPanel != null)
        {
            _lootPanel.Visible = visible;
            if (visible)
            {
                _lootPanel.MoveToFront();
            }
        }
    }

    public void PositionLootPanelAboveCell(Vector2I cell, int cellSize)
    {
        if (_lootPanel == null || cellSize <= 0)
        {
            return;
        }

        var viewport = GetViewport();
        if (viewport == null)
        {
            return;
        }

        var viewportSize = viewport.GetVisibleRect().Size;
        var panelSize = _basePanelRects.TryGetValue(_lootPanel, out var baseRect)
            ? baseRect.Size
            : _lootPanel.Size;

        if (panelSize.X <= 1.0f || panelSize.Y <= 1.0f)
        {
            panelSize = new Vector2(420.0f, 274.0f);
        }

        var cellTopLeft = new Vector2(cell.X * cellSize, cell.Y * cellSize);
        var cellCenterX = cellTopLeft.X + (cellSize * 0.5f);
        var targetX = cellCenterX - (panelSize.X * 0.5f);
        var targetY = cellTopLeft.Y - panelSize.Y - 8.0f;

        var maxX = Mathf.Max(0.0f, viewportSize.X - panelSize.X);
        var maxY = Mathf.Max(0.0f, viewportSize.Y - panelSize.Y);
        var clampedX = Mathf.Clamp(targetX, 0.0f, maxX);
        var clampedY = Mathf.Clamp(targetY, 0.0f, maxY);
        SetRect(_lootPanel, clampedX, clampedY, clampedX + panelSize.X, clampedY + panelSize.Y);

        if (_basePanelRects.TryGetValue(_lootPanel, out var baseRectForOffset))
        {
            _panelOffsets[_lootPanel] = new Vector2(clampedX, clampedY) - baseRectForOffset.Position;
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
        _lootAllInteractionId = "";
        if (_lootHeader != null)
        {
            _lootHeader.Text = "Nearby Loot";
        }

        if (entries == null || entries.Count == 0)
        {
            _lootDetailsLabel.Text = "No loot available.";
            if (_confirmLootButton != null)
            {
                _confirmLootButton.Disabled = true;
            }
            SetLootPanelVisible(false);
            return;
        }

        var sourceTitle = GetString(entries[0], "source_title", "");
        if (!string.IsNullOrEmpty(sourceTitle) && _lootHeader != null)
        {
            _lootHeader.Text = sourceTitle;
        }

        _lootAllInteractionId = GetString(entries[0], "loot_all_id", "");
        if (_confirmLootButton != null)
        {
            _confirmLootButton.Disabled = string.IsNullOrEmpty(_lootAllInteractionId);
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
            _lootDetailsLabel.Text = "Click an item to loot it, or use Loot All.";
        }
    }

    public void OpenVendorPanel(string vendorName)
    {
        if (_vendorHeader != null)
        {
            _vendorHeader.Text = string.IsNullOrEmpty(vendorName) ? "Vendor" : vendorName;
        }

        if (_vendorDialogueLabel != null)
        {
            _vendorDialogueLabel.Text = "Welcome. Would you like to talk or browse the store?";
        }

        if (_vendorStoreTabs != null)
        {
            _vendorStoreTabs.Visible = false;
            _vendorStoreTabs.CurrentTab = 0;
        }

        SetVendorPanelVisible(true);
    }

    public void SetVendorPanelVisible(bool visible)
    {
        if (_vendorPanel != null)
        {
            _vendorPanel.Visible = visible;
            if (visible)
            {
                _vendorPanel.MoveToFront();
            }
        }
    }

    public void SetVendorStatus(string text)
    {
        if (_vendorStatusLabel != null)
        {
            _vendorStatusLabel.Text = text;
        }
    }

    public void SetVendorTransactionMessage(string text)
    {
        if (_vendorDialogueLabel != null && !string.IsNullOrEmpty(text))
        {
            _vendorDialogueLabel.Text = text;
        }
    }

    public void SetVendorItems(Array<Dictionary> buyItems, Array<Dictionary> sellItems)
    {
        SetVendorList(_vendorBuyList, _vendorBuyItemsById, buyItems, "Vendor has nothing left to sell.");
        SetVendorList(_vendorSellList, _vendorSellItemsById, sellItems, "No shared inventory items to sell.");

        if (_vendorBuyButton != null)
        {
            _vendorBuyButton.Disabled = _vendorBuyList == null || _vendorBuyList.ItemCount == 0;
        }

        if (_vendorSellButton != null)
        {
            _vendorSellButton.Disabled = _vendorSellList == null || _vendorSellList.ItemCount == 0;
        }
    }

    private static void SetVendorList(ItemList list, System.Collections.Generic.Dictionary<string, Dictionary> entriesById, Array<Dictionary> items, string emptyText)
    {
        if (list == null)
        {
            return;
        }

        list.Clear();
        entriesById.Clear();

        if (items == null || items.Count == 0)
        {
            list.AddItem(emptyText);
            list.SetItemDisabled(0, true);
            return;
        }

        foreach (var item in items)
        {
            var itemId = GetString(item, "id", "");
            if (string.IsNullOrEmpty(itemId))
            {
                continue;
            }

            var quantity = GetInt(item, "quantity", 1);
            var price = GetInt(item, "price", 0);
            var label = $"{Mathf.Max(1, quantity)} x {BuildItemSummary(item)} - {Mathf.Max(0, price)} gp";
            entriesById[itemId] = item;
            list.AddItem(label);
            list.SetItemMetadata(list.ItemCount - 1, itemId);
        }

        if (list.ItemCount > 0)
        {
            list.Select(0);
        }
    }

    private static void SetReserveList(ItemList list, System.Collections.Generic.Dictionary<string, Dictionary> entriesById, Array<Dictionary> items, string emptyText)
    {
        if (list == null)
        {
            return;
        }

        list.Clear();
        entriesById.Clear();

        if (items == null || items.Count == 0)
        {
            list.AddItem(emptyText);
            list.SetItemDisabled(0, true);
            return;
        }

        foreach (var item in items)
        {
            var unitId = GetString(item, "id", "");
            if (string.IsNullOrWhiteSpace(unitId))
            {
                continue;
            }

            var label = GetString(item, "label", unitId);
            entriesById[unitId] = item;
            list.AddItem(label);
            list.SetItemMetadata(list.ItemCount - 1, unitId);
        }

        if (list.ItemCount > 0)
        {
            list.Select(0);
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

    private static bool GetBool(Dictionary dict, string key, bool fallback)
    {
        if (dict == null || !dict.ContainsKey(key))
        {
            return fallback;
        }

        var value = (Variant)dict[key];
        return value.VariantType switch
        {
            Variant.Type.Bool => (bool)value,
            Variant.Type.Int => (int)value != 0,
            Variant.Type.Float => !Mathf.IsZeroApprox((float)value),
            _ => bool.TryParse(value.AsString(), out var parsed) ? parsed : fallback
        };
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
            var base_armor_class = GetInt(item, "base_armor_class", 0);
            var bonus_armor_class = GetInt(item, "bonus_armor_class", 0);
            return $"{name} [armor] armor_class+{base_armor_class} (+{bonus_armor_class})";
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
            var base_armor_class = GetInt(item, "base_armor_class", 0);
            var bonus_armor_class = GetInt(item, "bonus_armor_class", 0);
            return $"{prefix}{name} - Armor\nArmor Class: +{base_armor_class} (+{bonus_armor_class})";
        }

        return $"{prefix}{name} - {type}";
    }

    public void SetTurnOrder(Array<Unit> turnOrder, Unit activeUnit)
    {
        if (_turnOrderDisplay == null || _turnOrderIcons == null)
        {
            return;
        }

        if (turnOrder == null || turnOrder.Count == 0)
        {
            _turnOrderDisplay.Visible = false;
            _turnOrderSignature = "";
            foreach (var child in _turnOrderIcons.GetChildren())
            {
                child.QueueFree();
            }
            return;
        }

        var signatureBuilder = new StringBuilder(activeUnit?.UnitId ?? "");
        foreach (var unit in turnOrder)
        {
            if (unit != null && !unit.IsDead)
            {
                signatureBuilder.Append('|').Append(unit.UnitId);
            }
        }

        var signature = signatureBuilder.ToString();
        if (_turnOrderDisplay.Visible && signature == _turnOrderSignature)
        {
            return;
        }

        _turnOrderSignature = signature;
        foreach (var child in _turnOrderIcons.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var unit in turnOrder)
        {
            if (unit == null || unit.IsDead)
            {
                continue;
            }

            var isActive = unit == activeUnit;
            var frameColor = unit.Team == "enemy"
                ? new Color(0.82f, 0.22f, 0.2f, 1.0f)
                : new Color(0.2f, 0.68f, 0.34f, 1.0f);
            if (isActive)
            {
                frameColor = new Color(1.0f, 0.82f, 0.3f, 1.0f);
            }

            var frameStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.05f, 0.07f, 0.09f, 0.94f),
                BorderColor = frameColor,
                BorderWidthLeft = isActive ? 3 : 2,
                BorderWidthTop = isActive ? 3 : 2,
                BorderWidthRight = isActive ? 3 : 2,
                BorderWidthBottom = isActive ? 3 : 2,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomRight = 4,
                CornerRadiusBottomLeft = 4,
                ContentMarginLeft = 3.0f,
                ContentMarginTop = 3.0f,
                ContentMarginRight = 3.0f,
                ContentMarginBottom = 3.0f
            };

            var frame = new PanelContainer();
            frame.AddThemeStyleboxOverride("panel", frameStyle);

            var icon = new TextureButton
            {
                CustomMinimumSize = new Vector2(TurnOrderIconSize, TurnOrderIconSize),
                TextureNormal = unit.GetTurnOrderIcon(),
                IgnoreTextureSize = true,
                StretchMode = TextureButton.StretchModeEnum.Scale,
                TooltipText = $"{unit.UnitName} - {unit.Team}\nClick to focus",
                FocusMode = FocusModeEnum.None,
                MouseDefaultCursorShape = CursorShape.PointingHand
            };
            var unitId = unit.UnitId;
            icon.GuiInput += inputEvent => OnTurnOrderIconInput(inputEvent, unitId);
            frame.AddChild(icon);
            _turnOrderIcons.AddChild(frame);
        }

        _turnOrderDisplay.Visible = _turnOrderIcons.GetChildCount() > 0;
    }

    private void OnTurnOrderIconInput(InputEvent inputEvent, string unitId)
    {
        if (inputEvent is not InputEventMouseButton mouseButton
            || mouseButton.ButtonIndex != MouseButton.Left
            || !mouseButton.Pressed)
        {
            return;
        }

        EmitSignal(SignalName.TurnOrderUnitFocused, unitId);
        AcceptEvent();
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
        foreach (var wrappedLine in WrapCombatLogText(text))
        {
            _combatLog.AddItem(wrappedLine);
        }

        while (_combatLog.ItemCount > MaxLogEntries)
        {
            _combatLog.RemoveItem(0);
        }

        _combatLog.Select(_combatLog.ItemCount - 1);
        _combatLog.EnsureCurrentIsVisible();
    }

    public void ShowCombatBanner(string text, Color accentColor)
    {
        if (_combatBannerPanel == null || _combatBannerLabel == null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _combatBannerTween?.Kill();

        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.06f, 0.08f, 0.92f),
            BorderColor = accentColor,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusBottomLeft = 6,
            ContentMarginTop = 6,
            ContentMarginRight = 8,
            ContentMarginBottom = 6,
            ContentMarginLeft = 8,
            ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.45f),
            ShadowSize = 3
        };

        _combatBannerPanel.AddThemeStyleboxOverride("panel", panelStyle);
        _combatBannerLabel.Text = text;
        _combatBannerLabel.AddThemeColorOverride("font_color", accentColor.Lightened(0.15f));

        _combatBannerPanel.Visible = true;
        _combatBannerPanel.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        _combatBannerPanel.Scale = new Vector2(0.93f, 0.93f);
        _combatBannerPanel.PivotOffset = _combatBannerPanel.Size * 0.5f;

        _combatBannerTween = CreateTween();
        _combatBannerTween.SetTrans(Tween.TransitionType.Sine);
        _combatBannerTween.SetEase(Tween.EaseType.Out);
        _combatBannerTween.TweenProperty(_combatBannerPanel, "modulate:a", 1.0f, 0.16f);
        _combatBannerTween.Parallel().TweenProperty(_combatBannerPanel, "scale", Vector2.One, 0.16f);
        _combatBannerTween.TweenInterval(0.9f);
        _combatBannerTween.SetEase(Tween.EaseType.In);
        _combatBannerTween.TweenProperty(_combatBannerPanel, "modulate:a", 0.0f, 0.32f);
        _combatBannerTween.Parallel().TweenProperty(_combatBannerPanel, "scale", new Vector2(1.03f, 1.03f), 0.32f);
        _combatBannerTween.Finished += () =>
        {
            if (_combatBannerPanel != null)
            {
                _combatBannerPanel.Visible = false;
                _combatBannerPanel.Scale = Vector2.One;
            }
        };
    }

    public void SetWorldHoverTooltip(
        Vector2 cursor,
        string title,
        string details,
        Color background,
        Color border,
        Color titleColor,
        Color detailsColor
    )
    {
        _worldHoverCursor = cursor;
        _worldHoverTitle = string.IsNullOrEmpty(title) ? "Info" : title;
        _worldHoverDetails = string.IsNullOrEmpty(details) ? "-" : details;
        _worldHoverBackground = background;
        _worldHoverBorder = border;
        _worldHoverTitleColor = titleColor;
        _worldHoverDetailsColor = detailsColor;
        _showWorldHoverTooltip = true;
        QueueRedraw();
    }

    public void ClearWorldHoverTooltip()
    {
        if (!_showWorldHoverTooltip)
        {
            return;
        }

        _showWorldHoverTooltip = false;
        QueueRedraw();
    }

    private IEnumerable<string> WrapCombatLogText(string text)
    {
        if (_combatLog == null)
        {
            yield break;
        }

        var maxWidth = Mathf.Max(40.0f, Mathf.Min(_combatLog.Size.X - CombatLogTextPadding, CombatLogMaxWrapWidth));
        var font = ThemeDB.FallbackFont;
        var fontSize = ThemeDB.FallbackFontSize;
        if (font == null)
        {
            yield return text;
            yield break;
        }

        var normalized = text.Replace("\r", "");
        var sourceLines = normalized.Split('\n');
        for (var lineIndex = 0; lineIndex < sourceLines.Length; lineIndex++)
        {
            var source = sourceLines[lineIndex];
            if (string.IsNullOrEmpty(source))
            {
                yield return "";
                continue;
            }

            var words = source.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var current = "";
            foreach (var word in words)
            {
                var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
                if (font.GetStringSize(candidate, HorizontalAlignment.Left, -1, fontSize).X <= maxWidth)
                {
                    current = candidate;
                    continue;
                }

                if (!string.IsNullOrEmpty(current))
                {
                    yield return current;
                    current = "";
                }

                if (font.GetStringSize(word, HorizontalAlignment.Left, -1, fontSize).X <= maxWidth)
                {
                    current = word;
                    continue;
                }

                var chunk = "";
                for (var i = 0; i < word.Length; i++)
                {
                    var c = word[i];
                    var chunkCandidate = chunk + c;
                    var hasMoreCharacters = i < word.Length - 1;
                    var displayCandidate = hasMoreCharacters ? chunkCandidate + "-" : chunkCandidate;
                    if (font.GetStringSize(displayCandidate, HorizontalAlignment.Left, -1, fontSize).X <= maxWidth)
                    {
                        chunk = chunkCandidate;
                        continue;
                    }

                    if (!string.IsNullOrEmpty(chunk))
                    {
                        yield return chunk + "-";
                        chunk = c.ToString();
                        continue;
                    }

                    // Very narrow layouts can force single-character chunks.
                    yield return c.ToString();
                }

                if (!string.IsNullOrEmpty(chunk))
                {
                    current = chunk;
                }
            }

            if (!string.IsNullOrEmpty(current))
            {
                yield return current;
            }
        }
    }
}
