using Godot;
using Godot.Collections;
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;

public partial class SaveSlotChecks : BattleController
{
    private readonly Array<string> _failures = new();
    private readonly string _directory = $"user://save-slot-test-{Guid.NewGuid():N}";
    private SaveGameSlots _slots;
    private SaveLoadMenu _menu;
    private int _savedSlot;
    private string _savedName;
    private bool _overwrite;
    private string _loadedPath;
    private SaveGameSlots _layoutSlots;
    private float _fiveCardMenuHeight;

    public override void _Ready() { }
    public override void _ExitTree() { }

    public Array<string> GetFailures() => _failures;

    public void CollectManagedWrappers()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    public void SetGameGold(int gold) =>
        ((IGamePersistenceHost)GetTree().CurrentScene.GetNode<BattleController>("Gameworld")).PartyGold = gold;

    public int GetGameGold() =>
        ((IGamePersistenceHost)GetTree().CurrentScene.GetNode<BattleController>("Gameworld")).PartyGold;

    public void RunStorageChecks()
    {
        Set("_gameData", GetNode<GameData>("/root/GameData"));
        var units = new Node2D { Name = "Units" };
        AddChild(units);
        Set("_unitsRoot", units);
        var turns = new TurnManager();
        AddChild(turns);
        Set("_turnManager", turns);
        var mainScene = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate();
        var maps = mainScene.GetNode<Node2D>("Gameworld/Maps");
        maps.GetParent().RemoveChild(maps);
        mainScene.Free();
        AddChild(maps);
        var loader = new MapLoader { MapsRootPath = "../Maps" };
        AddChild(loader);
        Set("_mapLoader", loader);
        var host = (IGamePersistenceHost)this;
        host.SpawnMapEncounter("forest-town");
        host.RestoreFlowState("exploration");
        var player = Field<Array<Unit>>("_playerUnits")[0];
        player.GrantExperience(700);
        Call("RecordLevelUpNotice", player, 1, 3, 6, 3, 3, 1);
        _slots = new SaveGameSlots(_directory);
        Check(_slots.ListManualSaves().Count == 0 && _slots.FindEmptySlot() == 1, "An empty catalog must offer slot 1.");
        for (var slot = 1; slot <= SaveGameSlots.Limit; slot++)
        {
            host.PartyGold = slot * 100;
            Check(_slots.TrySave(host, slot, $"Adventure {slot}", false, out var error), error);
        }
        var original = FileAccess.GetFileAsString(_slots.GetPath(1));
        Check(_slots.ListManualSaves().Count == 10 && _slots.FindEmptySlot() == 0,
            "Exactly ten named saves must fill the catalog.");
        Check(!_slots.TrySave(host, 11, "Eleventh", false, out var limitError) && limitError.Length > 0,
            "An eleventh save must be rejected explicitly.");
        Check(!_slots.TrySave(host, 1, "Conflict", false, out var conflictError) && conflictError.Length > 0,
            "Creating a new save must never silently overwrite an occupied slot.");
        Check(!_slots.TrySave(host, 1, "   ", true, out var nameError) && nameError.Length > 0,
            "Whitespace-only names must be rejected.");
        Check(!_slots.TrySave(host, 1, new string('x', 61), true, out _),
            "Save names must enforce the same length limit as the UI.");
        var autosave = new GamePersistence(host, $"{_directory}/autosave.json");
        Check(autosave.TryPersistSaveGame(), autosave.LastError);
        Check(original == FileAccess.GetFileAsString(_slots.GetPath(1)),
            "Automatic saving must not modify a named save.");
        var autosaveEntry = SaveGameSlots.ReadEntry(0, $"{_directory}/autosave.json", "Auto-Save");
        Check(autosaveEntry.PartyMembers.Count == Field<Array<Unit>>("_playerUnits").Count
            && autosaveEntry.PartyMembers.Count <= 5,
            "Auto-Save metadata must include the active party preview.");
        var first = _slots.ListManualSaves()[0];
        Check(first.Name == "Adventure 1" && first.Map == "Forest Town"
            && first.Timestamp != "Unknown time" && first.CanLoad, "Cards must expose name, map, and timestamp metadata.");
        var savedAt = DateTimeOffset.MinValue;
        var hasSavedAt = SaveGameSlots.TryRead(first.Path, out var data, out _)
            && DateTimeOffset.TryParse(data["saved_at_utc"].AsString(), out savedAt);
        Check(hasSavedAt && (DateTimeOffset.UtcNow - savedAt).TotalMinutes < 1,
            "A new save must record its actual UTC write time.");
        Check(hasSavedAt && first.Timestamp == savedAt.ToLocalTime()
            .ToString("MMM d, yyyy  hh:mm tt", CultureInfo.CurrentCulture),
            "Save timestamps must display in the player's local system time using a 12-hour clock.");
        var levelUpPreview = first.PartyMembers.FirstOrDefault(member => member.UnitId == player.UnitId);
        Check(first.PartyMembers.Count > 0
            && first.PartyMembers.Count == Field<Array<Unit>>("_playerUnits").Count
            && first.PartyMembers.Count <= 5 && levelUpPreview != null && levelUpPreview.Level == 4
            && levelUpPreview.Portrait != null,
            "Save metadata must contain active party levels and portraits, up to the party-size limit.");
        var savedPortrait = levelUpPreview?.Portrait;
        var hudPortrait = player.GetTurnOrderIcon();
        var portraitsMatch = savedPortrait != null && savedPortrait == hudPortrait;
        if (savedPortrait is AtlasTexture savedAtlas && hudPortrait is AtlasTexture hudAtlas)
            portraitsMatch = savedAtlas.Atlas == hudAtlas.Atlas && savedAtlas.Region == hudAtlas.Region;
        Check(portraitsMatch, "Save previews must reuse the same portrait image as the party list.");

        var persistence = new GamePersistence(host, first.Path);
        Check(persistence.TryLoadSaveGame(false), persistence.LastError);
        player = Field<Array<Unit>>("_playerUnits")[0];
        Check(host.PartyGold == 100 && player.Level == 4 && player.UnallocatedStatPoints == 3,
            "Loading slot 1 must restore that slot's world and all three pending stat points.");
        var notice = host.BuildPendingLevelUpNotices()[0];
        Check(notice["previous_level"].AsInt32() == 1 && notice["level"].AsInt32() == 4,
            "A real JSON save/load must preserve the 1-to-4 level transition.");
        Call("OnHudLevelUpStatSelected", player.UnitId, "strength");
        host.PartyGold = 777;
        var untouched = FileAccess.GetFileAsString(_slots.GetPath(2));
        Check(_slots.TrySave(host, 1, "Renamed Adventure", true, out var overwriteError), overwriteError);
        Check(untouched == FileAccess.GetFileAsString(_slots.GetPath(2)), "Overwriting one slot must not alter other slots.");
        Check(persistence.TryLoadSaveGame(false), persistence.LastError);
        player = Field<Array<Unit>>("_playerUnits")[0];
        Check(host.PartyGold == 777 && player.UnallocatedStatPoints == 2
            && _slots.ListManualSaves()[0].Name == "Renamed Adventure",
            "Overwrite must persist both the new name and partially allocated level-up state.");
        Call("OnHudLevelUpStatSelected", player.UnitId, "strength");
        Call("OnHudLevelUpStatSelected", player.UnitId, "strength");
        Check(player.UnallocatedStatPoints == 0 && !((Array<string>)Call("BuildPendingLevelUpUnitIds")).Contains(player.UnitId),
            "After loading, spending the final points must clear the portrait badge.");

        SaveGameSlots.TryRead(_slots.GetPath(8), out var legacy, out _);
        legacy.Remove("save_name");
        legacy.Remove("saved_at_utc");
        Write(_slots.GetPath(8), Json.Stringify(legacy));
        var legacyEntry = SaveGameSlots.ReadEntry(8, _slots.GetPath(8), "Legacy Save");
        Check(legacyEntry.CanLoad && legacyEntry.Name == "Legacy Save" && legacyEntry.Timestamp != "Unknown time",
            "Pre-metadata saves must remain loadable with a file-time fallback.");
        Write(_slots.GetPath(9), "{}");
        Write(_slots.GetPath(10), "{broken");
        Check(_slots.ListManualSaves().Count == 10 && !_slots.ListManualSaves()[9].CanLoad
            && !_slots.ListManualSaves()[8].CanLoad && _slots.FindEmptySlot() == 0,
            "Damaged saves must occupy their slots, expose errors, and be unavailable for loading.");
        Check(!FileAccess.FileExists(first.Path + ".tmp"), "Successful atomic saves must not leave temporary files.");
        Check(!_slots.TryDelete(0, out var autoError) && autoError.Length > 0
            && !_slots.TryDelete(-1, out var recoveryError) && recoveryError.Length > 0
            && !_slots.TryDelete(11, out _),
            "Deletion must reject autosaves, recovery checkpoints, and out-of-range slots.");
        Check(!new SaveGameSlots($"{_directory}/empty").TryDelete(1, out var missingError)
            && missingError.Length > 0, "Deleting a missing save must return an explicit error.");
    }

    public void OpenMenu(bool saveMode, bool empty)
    {
        if (!empty)
            SeedSortTimes();
        _savedSlot = 0;
        _loadedPath = "";
        _menu = new SaveLoadMenu();
        _menu.Configure(saveMode, empty ? new SaveGameSlots($"{_directory}/empty") : _slots, false);
        _menu.SaveSelected += (slot, name, overwrite) =>
        {
            _savedSlot = slot;
            _savedName = name;
            _overwrite = overwrite;
        };
        _menu.LoadSelected += path => _loadedPath = path;
        AddChild(_menu);
    }

    public void CheckMenu(bool saveMode, bool empty)
    {
        Check(GetTree().Paused, "The game must pause while browsing saves.");
        var action = Find<Button>("ConfirmSaveMenu");
        Check(action.Text == (saveMode ? "Save" : "Load") && action.Disabled,
            "Save/load modes must use distinct action labels and require a card selection.");
        Check(_menu.FindChildren("SaveName*", "LineEdit", true, false).Count == (saveMode ? empty ? 1 : 11 : 0),
            "Name fields must live only in save-mode cards.");
        Check(!Find<Label>("SaveMenuStatus").Visible, "The old footer helper text must not be shown.");
        if (saveMode)
        {
            var name = Find<LineEdit>("SaveName");
            Check(Find<Button>("NewSaveCard").IsAncestorOf(name) && !name.Editable,
                "The New Save name field must be inside its card and unusable until that card is selected.");
        }
        var panel = Find<PanelContainer>("SaveMenuPanel");
        Check(panel.GetGlobalRect().Position.Y >= 0
            && panel.GetGlobalRect().End.Y <= GetViewportRect().Size.Y,
            "The save menu must fit within the viewport.");
        if (!empty)
        {
            var card = Find<Button>("SaveCard_2");
            var labels = card.FindChildren("*", "Label", true, false).Cast<Label>().ToArray();
            Check(labels.Length >= 3 && labels[0].Text == "Adventure 2",
                "Save card titles must show the save name without a slot prefix.");
            var entry = _slots.ListManualSaves().Single(save => save.Slot == 2);
            Check(labels.Length >= 3 && labels[2].Text == entry.Timestamp
                && !labels[2].Text.StartsWith("Saved ", StringComparison.Ordinal),
                "Save card timestamps must appear without a Saved label.");
            var actions = card.GetNode<HBoxContainer>("CardActions");
            var preview = actions.GetNode<HBoxContainer>("PartyPreview");
            var memberViews = preview.GetChildren().OfType<VBoxContainer>().ToArray();
            Check(memberViews.Length == entry.PartyMembers.Count && memberViews.Length <= 5,
                "Each active party member must have one preview, with at most five previews per card.");
            var delete = actions.GetNode<Button>("DeleteSave_2");
            var cardBounds = card.GetGlobalRect();
            var previewBounds = preview.GetGlobalRect();
            var deleteBounds = delete.GetGlobalRect();
            Check(actions.GetChild(0) == preview && actions.GetChild(actions.GetChildCount() - 1) == delete
                && Mathf.Abs(deleteBounds.GetCenter().Y - cardBounds.GetCenter().Y) <= 1.0f
                && deleteBounds.End.X <= cardBounds.End.X - 12.0f
                && deleteBounds.Position.X >= previewBounds.End.X,
                "Delete must be at the card's far right, vertically centered, with party portraits immediately to its left.");
            if (memberViews.Length > 0)
            {
                var member = entry.PartyMembers[0];
                var level = memberViews[0].GetNode<Label>("PartyMemberLevel");
                var portrait = memberViews[0].GetNode<TextureRect>("PartyMemberPortrait");
                var matchingPortrait = portrait.Texture == member.Portrait;
                if (portrait.Texture is AtlasTexture cardAtlas && member.Portrait is AtlasTexture entryAtlas)
                    matchingPortrait = cardAtlas.Atlas == entryAtlas.Atlas && cardAtlas.Region == entryAtlas.Region;
                Check(level.Text == $"Level {member.Level}" && matchingPortrait
                    && portrait.CustomMinimumSize == new Vector2(50.0f, 50.0f)
                    && preview.MouseFilter == Control.MouseFilterEnum.Ignore
                    && portrait.MouseFilter == Control.MouseFilterEnum.Ignore
                    && string.IsNullOrEmpty(portrait.TooltipText),
                    "Party previews must show a level label above a non-interactive matching portrait.");
            }
        }
        if (empty && !saveMode)
        {
            Check(!Find<VBoxContainer>("SaveCards").GetChildren().OfType<Button>().Any(),
                "Empty load mode must show no fake loadable saves.");
            return;
        }
        if (saveMode)
        {
            var newCard = Find<Button>("NewSaveCard");
            Check(newCard.Disabled == !empty, "Creating a save must be disabled at the ten-slot limit.");
            Find<Button>(empty ? "NewSaveCard" : "SaveCard_2").EmitSignal(Button.SignalName.Pressed);
            var name = Find<LineEdit>(empty ? "SaveName" : "SaveName_2");
            Check(name.Editable && name.Visible && name.HasFocus(),
                "Selecting a save card must enable and focus its inline name editor.");
            Check(Find<Button>(empty ? "NewSaveCard" : "SaveCard_2").IsAncestorOf(name),
                "The active name editor must be contained in the selected card, not the footer.");
            name.Text = "   ";
            name.EmitSignal(LineEdit.SignalName.TextChanged, name.Text);
            Check(action.Disabled, "Blank save names must disable Save.");
            name.Text = "New Chronicle";
            name.EmitSignal(LineEdit.SignalName.TextChanged, name.Text);
            action.EmitSignal(Button.SignalName.Pressed);
            if (!empty)
            {
                Check(_savedSlot == 0, "Overwriting must require confirmation before any write.");
                var confirmation = _menu.GetChildren().OfType<ConfirmationDialog>().Single();
                confirmation.EmitSignal(ConfirmationDialog.SignalName.Canceled);
                Check(_savedSlot == 0, "Canceling overwrite must not emit a save request.");
                action.EmitSignal(Button.SignalName.Pressed);
                confirmation = _menu.GetChildren().OfType<ConfirmationDialog>().Last();
                confirmation.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
            }
            Check(_savedSlot == (empty ? 1 : 2) && _savedName == "New Chronicle" && _overwrite == !empty,
                "Saving must target the chosen slot, name, and overwrite mode.");
        }
        else
        {
            Check(!Find<VBoxContainer>("SaveCards").GetChildren().Any(node => node.Name == "NewSaveCard"),
                "Load mode must never offer new saves.");
            Check(Find<Button>("SaveCard_9").Disabled && Find<Button>("SaveCard_10").Disabled,
                "Corrupt save cards must be disabled in load mode.");
            Find<Button>("SaveCard_2").EmitSignal(Button.SignalName.Pressed);
            action.EmitSignal(Button.SignalName.Pressed);
            Check(_loadedPath == _slots.GetPath(2), "Load must submit exactly the selected save path.");
        }
        if (!empty)
        {
            var names = Find<VBoxContainer>("SaveCards").GetChildren()
                .Where(node => node.Name.ToString().StartsWith("SaveCard_", StringComparison.Ordinal))
                .Select(node => node.Name.ToString()).ToArray();
            Check(names.SequenceEqual(new[] { 2, 7, 3, 6, 4, 5, 1, 8, 9, 10 }.Select(slot => $"SaveCard_{slot}")),
                "Cards must sort by their actual save time, with modification-time fallbacks for legacy and damaged saves.");
        }
    }

    private void SeedSortTimes()
    {
        var order = new[] { 2, 7, 3, 6, 4, 5, 1 };
        for (var index = 0; index < order.Length; index++)
        {
            var path = _slots.GetPath(order[index]);
            SaveGameSlots.TryRead(path, out var data, out _);
            data["saved_at_utc"] = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero)
                .AddHours(order.Length - index).ToOffset(TimeSpan.FromHours(-7)).ToString("O");
            Write(path, Json.Stringify(data));
        }
        foreach (var (slot, date) in new[]
        {
            (8, new DateTime(2029, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            (9, new DateTime(2028, 1, 2, 0, 0, 0, DateTimeKind.Utc)),
            (10, new DateTime(2028, 1, 1, 0, 0, 0, DateTimeKind.Utc))
        })
            System.IO.File.SetLastWriteTimeUtc(ProjectSettings.GlobalizePath(_slots.GetPath(slot)), date);
    }

    public void OpenLayoutMenu(int count)
    {
        _layoutSlots ??= new SaveGameSlots($"{_directory}/layout");
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath($"{_directory}/layout"));
        for (var slot = 1; slot <= count; slot++)
            Check(_layoutSlots.TrySave(this, slot, $"Layout save {slot}", true, out var error), error);
        for (var slot = count + 1; slot <= SaveGameSlots.Limit; slot++)
        {
            if (FileAccess.FileExists(_layoutSlots.GetPath(slot)))
                DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(_layoutSlots.GetPath(slot)));
        }
        _menu = new SaveLoadMenu();
        _menu.Configure(false, _layoutSlots, false);
        AddChild(_menu);
    }

    public void CheckCompactLayout(int count)
    {
        var panel = Find<PanelContainer>("SaveMenuPanel");
        var scroll = Find<ScrollContainer>("SaveCardScroll");
        var cards = Find<VBoxContainer>("SaveCards").GetChildren().OfType<Button>().ToArray();
        Check(_menu.FindChildren("SaveCount", "", true, false).Count == 0,
            "The named-save count must be removed from the menu.");
        Check(!_menu.FindChildren("*", "Label", true, false).Cast<Label>()
            .Any(label => label.Text.Contains("chronicle", StringComparison.OrdinalIgnoreCase)),
            "The introductory chronicle text must be removed.");
        Check(panel.Size.X <= 700.0f && panel.Size.Y <= GetViewportRect().Size.Y - 48.0f,
            "The compact menu must respect its width and viewport height limits.");
        Check(panel.GetGlobalRect().GetCenter().DistanceTo(GetViewportRect().GetCenter()) <= 1.0f,
            "Content-sized save menus must remain centered.");
        Check(scroll.Size.Y <= 5 * 112 + 4 * 12 + 1,
            "The scroll viewport must never display more than five cards.");
        for (var index = 1; index < cards.Length; index++)
        {
            Check(Mathf.Abs(cards[index].GlobalPosition.X - cards[0].GlobalPosition.X) <= 1
                && Mathf.Abs(cards[index].Size.X - cards[0].Size.X) <= 1
                && cards[index].GlobalPosition.Y >= cards[index - 1].GetGlobalRect().End.Y + 11,
                "Save cards must stack vertically at equal width, never side by side.");
        }
        if (count <= 1)
            Check(panel.Size.Y < 320, "Zero or one save must produce a short menu without a large empty area.");
        if (count == 5)
        {
            _fiveCardMenuHeight = panel.Size.Y;
            if (GetViewportRect().Size.Y >= 900)
                Check(Mathf.Abs(scroll.Size.Y - (5 * 112 + 4 * 12)) <= 1,
                    "At 1080p the menu must show exactly five complete cards before scrolling.");
        }
        if (count > 5)
        {
            Check(Mathf.Abs(panel.Size.Y - _fiveCardMenuHeight) <= 1 && scroll.GetVScrollBar().Visible,
                "Adding a sixth or later card must enable scrolling without increasing menu height.");
            scroll.ScrollVertical = int.MaxValue;
        }
    }

    public void CheckScrolledLastCard()
    {
        var scroll = Find<ScrollContainer>("SaveCardScroll");
        var cards = Find<VBoxContainer>("SaveCards");
        var last = cards.GetChild<Button>(cards.GetChildCount() - 1);
        Check(scroll.ScrollVertical > 0 && scroll.GetGlobalRect().Encloses(last.GetGlobalRect()),
            "Scrolling to the bottom must make the last save card fully accessible.");
    }

    public void SelectForDeletion() => Find<Button>("SaveCard_2").EmitSignal(Button.SignalName.Pressed);

    public void CheckDeletion(bool saveMode)
    {
        var original = FileAccess.GetFileAsString(_slots.GetPath(2));
        var untouched = FileAccess.GetFileAsString(_slots.GetPath(1));
        var changed = 0;
        _menu.SavesChanged += () => changed++;
        var confirmation = _menu.GetChildren().OfType<ConfirmationDialog>().FirstOrDefault();
        Check(confirmation != null && confirmation.Title == "Delete Save",
            "Clicking the card's Delete control must open a deletion confirmation.");
        if (confirmation == null)
            return;
        Check(FileAccess.FileExists(_slots.GetPath(2)) && _savedSlot == 0 && _loadedPath == "",
            "Requesting deletion must not immediately delete, save, or load anything.");
        confirmation.EmitSignal(ConfirmationDialog.SignalName.Canceled);
        Check(FileAccess.GetFileAsString(_slots.GetPath(2)) == original && changed == 0,
            "Canceling deletion must preserve the file and catalog.");
        Find<Button>("DeleteSave_2").EmitSignal(Button.SignalName.Pressed);
        _menu.GetChildren().OfType<ConfirmationDialog>().Last().EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        Check(!FileAccess.FileExists(_slots.GetPath(2)) && _slots.ListManualSaves().Count == 9
            && Find<VBoxContainer>("SaveCards").GetChildCount() == (saveMode ? 10 : 9) && changed == 1,
            "Confirmed deletion must remove the file, update the cards, and notify the parent screen.");
        Check(Find<Button>("ConfirmSaveMenu").Disabled
            && _menu.FindChildren("SaveCard_2", "", true, false).Count == 0,
            "Deleting the selected save must clear stale selection and disable Save/Load.");
        Check(FileAccess.GetFileAsString(_slots.GetPath(1)) == untouched,
            "Deleting one named save must leave other files unchanged.");
        if (saveMode)
        {
            Check(!Find<Button>("NewSaveCard").Disabled, "Deleting from a full catalog must immediately enable New Save.");
            Find<Button>("NewSaveCard").EmitSignal(Button.SignalName.Pressed);
            var name = Find<LineEdit>("SaveName");
            name.Text = "Replacement";
            name.EmitSignal(LineEdit.SignalName.TextChanged, name.Text);
            Find<Button>("ConfirmSaveMenu").EmitSignal(Button.SignalName.Pressed);
            Check(_savedSlot == 2 && !_overwrite, "New Save must reuse the newly freed slot.");
        }
        Write(_slots.GetPath(2), original);

        var corrupt = FileAccess.GetFileAsString(_slots.GetPath(10));
        Find<Button>("DeleteSave_10").EmitSignal(Button.SignalName.Pressed);
        _menu.GetChildren().OfType<ConfirmationDialog>().Last().EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        Check(!FileAccess.FileExists(_slots.GetPath(10)), "Corrupt named saves must also be deletable.");
        Write(_slots.GetPath(10), corrupt);
    }

    public void CheckInlineDraft()
    {
        var name = Find<LineEdit>("SaveName");
        Check(name.Editable && name.HasFocus(),
            "Clicking the inactive inline field must select New Save and make the field usable.");
        name.Text = "Unfinished chronicle";
        name.EmitSignal(LineEdit.SignalName.TextChanged, name.Text);
        Find<Button>("NewSaveCard").EmitSignal(Button.SignalName.Pressed);
        Check(name.Text == "Unfinished chronicle" && name.Editable,
            "Clicking the same card again must not erase the in-card draft.");
    }

    public void CheckInlineLayout()
    {
        foreach (var node in _menu.FindChildren("SaveName*", "LineEdit", true, false))
        {
            var editor = (LineEdit)node;
            if (!editor.Visible)
                continue;
            var card = editor.GetParent();
            while (card is not Button)
                card = card.GetParent();
            Check(((Button)card).GetGlobalRect().Encloses(editor.GetGlobalRect()),
                "Inline name fields must fit completely inside their cards at every tested viewport size.");
        }
    }

    public void CloseMenu()
    {
        _menu.Close();
        Check(!GetTree().Paused, "Closing the menu must restore the previous pause state.");
    }

    public void Cleanup()
    {
        if (IsInstanceValid(_menu) && !_menu.IsQueuedForDeletion())
            _menu.Close();
        for (var slot = 1; slot <= SaveGameSlots.Limit; slot++)
        {
            var path = _slots.GetPath(slot);
            if (FileAccess.FileExists(path))
                DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
            if (_layoutSlots != null && FileAccess.FileExists(_layoutSlots.GetPath(slot)))
                DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(_layoutSlots.GetPath(slot)));
        }
        if (_layoutSlots != null)
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath($"{_directory}/layout"));
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath($"{_directory}/autosave.json"));
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(_directory));
    }

    private T Find<T>(string name) where T : Node => (T)_menu.FindChildren(name, "", true, false)[0];
    private static void Write(string path, string content)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file.StoreString(content);
    }
    private static FieldInfo PrivateField(string name) =>
        typeof(BattleController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private T Field<T>(string name) => (T)PrivateField(name).GetValue(this);
    private void Set(string name, object value) => PrivateField(name).SetValue(this, value);
    private object Call(string name, params object[] args) =>
        typeof(BattleController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(this, args);
    private void Check(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }
}
