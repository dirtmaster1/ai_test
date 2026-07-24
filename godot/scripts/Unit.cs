using Godot;
using Godot.Collections;

public partial class Unit : Node2D
{
    private sealed class StatusEffectState
    {
        public string BaseStatusId = "";
        public string StatusId = "";
        public string DisplayName = "";
        public bool IsBuff;
        public int RemainingTurns;
        public int StartDelayTurns;
        public int DamagePerTurn;
        public int Stacks = 1;
        public int MaxStacks = 1;
        public string StackingMode = "refresh";
        public string Scope = "persistent";
    }

    private const int CellSize = 64;
    private const int AtlasTileSize = 64;
    private const string UnitAtlasPath = "res://assets/tilesets/units_2_64.png";
    public const int MaxMovementPerTurn = 3;
    public const int DefendDamageReductionPercent = 10;

    public string UnitId { get; private set; } = "";
    public string UnitName { get; private set; } = "";
    public string Race { get; private set; } = "human";
    public string Team { get; private set; } = "player";
    public Vector2I GridPos { get; private set; } = Vector2I.Zero;
    public int HitPoints { get; private set; } = 10;
    public int MaxHitPoints { get; private set; } = 10;
    public int MagicPoints { get; private set; }
    public int MaxMagicPoints { get; private set; }
    public int MagicPointRegenPerTurn { get; private set; }
    public int Intelligence { get; private set; } = 5;
    public int Strength { get; private set; } = 5;
    public int Wisdom { get; private set; } = 5;
    public int Dexterity { get; private set; } = 5;
    public int Constitution { get; private set; } = 5;
    public string PrimaryAbilityId { get; private set; } = "";
    public Array<string> AbilityIds { get; private set; } = new();
    public string EncounterId { get; private set; } = "";
    public int Initiative { get; private set; } = 10;
    public int Level { get; private set; } = 1;
    public int Experience { get; private set; }
    public int BaseUnarmedDamage { get; private set; } = 1;
    public int WeaponAttackDamageBonus { get; private set; }
    public int WeaponAttackRangeBonus { get; private set; }
    public int ArmorClassBonus { get; private set; }

    public int AttackDamage => WeaponAttackDamageBonus > 0
        ? WeaponAttackDamageBonus
        : HasAbility("melee")
            ? Mathf.Max(1, BaseUnarmedDamage)
            : 0;
    public int AttackRange => Mathf.Max(1, WeaponAttackRangeBonus);
    public int ArmorClass => Mathf.Max(0, ArmorClassBonus);
    public int ExperienceToNextLevel => Mathf.Max(100, Level * 25);
    public int MovementPerTurn { get; private set; } = MaxMovementPerTurn;
    public int RemainingMovement { get; private set; } = MaxMovementPerTurn;
    public bool HasUsedAbilityThisTurn { get; private set; }
    public bool IsDefending { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsActive { get; private set; }
    private Sprite2D _sprite;
    private Texture2D _unitAtlas;
    private readonly Dictionary<string, int> _abilityCooldownRemaining = new();
    private readonly System.Collections.Generic.Dictionary<string, StatusEffectState> _statusEffects = new();
    private int _statusEffectSequence;

    public override void _Ready()
    {
        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (_sprite == null)
        {
            _sprite = new Sprite2D
            {
                Name = "Sprite2D",
                Centered = true
            };
            AddChild(_sprite);
        }

        _unitAtlas = GD.Load<Texture2D>(UnitAtlasPath);
        ConfigureSpriteRegion();
    }

    public void Setup(Dictionary config)
    {
        UnitId = GetString(config, "id", "unit");
        UnitName = GetString(config, "name", "Unit");
        Race = GetString(config, "race", ResolveRace());
        Team = GetString(config, "team", "player");
        EncounterId = GetString(config, "encounter_id", "");
        MaxHitPoints = GetInt(config, "max_hit_points", 10);
        HitPoints = GetInt(config, "hit_points", MaxHitPoints);
        MaxMagicPoints = Mathf.Max(0, GetInt(config, "max_magic_points", 0));
        MagicPoints = Mathf.Clamp(GetInt(config, "magic_points", MaxMagicPoints), 0, MaxMagicPoints);
        var defaultMagicRegen = MaxMagicPoints > 0 ? 1 : 0;
        MagicPointRegenPerTurn = Mathf.Max(0, GetInt(config, "magic_point_regen_per_turn", defaultMagicRegen));
        Intelligence = GetInt(config, "intelligence", 5);
        Strength = GetInt(config, "strength", 5);
        Wisdom = GetInt(config, "wisdom", 5);
        Dexterity = GetInt(config, "dexterity", 5);
        Constitution = GetInt(config, "constitution", 5);
        Initiative = GetInt(config, "initiative", 10);
        Level = Mathf.Max(1, GetInt(config, "level", 1));
        Experience = Mathf.Max(0, GetInt(config, "experience", 0));
        PrimaryAbilityId = GetString(config, "primary_ability_id", Team == "enemy" ? "melee" : "melee");
        AbilityIds = BuildAbilityIds(config, PrimaryAbilityId);
        BaseUnarmedDamage = Mathf.Max(1, GetInt(config, "base_unarmed_damage", 1));
        WeaponAttackDamageBonus = GetInt(config, "weapon_attack_damage_bonus", GetInt(config, "base_attack_damage", 0));
        WeaponAttackRangeBonus = GetInt(config, "weapon_attack_range_bonus", GetInt(config, "base_attack_range", 1));
        ArmorClassBonus = GetInt(config, "armor_class_bonus", 0);
        MovementPerTurn = Mathf.Max(1, GetInt(config, "movement_per_turn", MaxMovementPerTurn));
        GridPos = GetVector2I(config, "grid_pos", Vector2I.Zero);
        ResetTurnResources();
        ConfigureSpriteRegion();
        SyncWorldPosition();
        RefreshVisualState();
    }

    public void ResetTurnResources()
    {
        RemainingMovement = MovementPerTurn;
        HasUsedAbilityThisTurn = false;
        IsDefending = false;
        MagicPoints = Mathf.Clamp(MagicPoints + Mathf.Max(0, MagicPointRegenPerTurn), 0, MaxMagicPoints);

        var keys = new Array<string>();
        foreach (var pair in _abilityCooldownRemaining)
        {
            keys.Add(pair.Key);
        }

        foreach (var key in keys)
        {
            var value = _abilityCooldownRemaining[key];
            if (value <= 0)
            {
                continue;
            }

            _abilityCooldownRemaining[key] = value - 1;
        }
    }

    public bool CanMoveThisTurn()
    {
        return RemainingMovement > 0;
    }

    public bool TrySpendMovement(int amount = 1)
    {
        var spend = Mathf.Max(0, amount);
        if (RemainingMovement < spend)
        {
            return false;
        }

        RemainingMovement -= spend;
        return true;
    }

    public bool CanUseAbilityThisTurn()
    {
        return !HasUsedAbilityThisTurn;
    }

    public bool HasAbility(string abilityId)
    {
        if (string.IsNullOrEmpty(abilityId))
        {
            return false;
        }

        foreach (var id in AbilityIds)
        {
            if (id == abilityId)
            {
                return true;
            }
        }

        return false;
    }

    public int GetAbilityCooldownRemaining(string abilityId)
    {
        if (string.IsNullOrEmpty(abilityId))
        {
            return 0;
        }

        return _abilityCooldownRemaining.TryGetValue(abilityId, out var turns) ? Mathf.Max(0, turns) : 0;
    }

    public bool CanUseAbility(string abilityId)
    {
        return CanUseAbilityThisTurn() && HasAbility(abilityId) && GetAbilityCooldownRemaining(abilityId) <= 0;
    }

    public bool CanAttackTarget(Unit target, int range, Array<Unit> allUnits)
    {
        if (!IsUsableUnit(this) || !IsUsableUnit(target) || IsDead || target.IsDead)
        {
            return false;
        }

        if (Team == target.Team)
        {
            return false;
        }

        return CanUseActionAtRange(target, range, allUnits);
    }

    public bool CanHealTarget(Unit target, int range, Array<Unit> allUnits)
    {
        if (!IsUsableUnit(this) || !IsUsableUnit(target) || IsDead || target.IsDead)
        {
            return false;
        }

        if (Team != target.Team)
        {
            return false;
        }

        return CanUseActionAtRange(target, range, allUnits);
    }

    public bool CanUseActionAtRange(Unit target, int range, Array<Unit> allUnits)
    {
        if (!IsUsableUnit(this) || !IsUsableUnit(target))
        {
            return false;
        }

        return IsWithinRange(GridPos, target.GridPos, range) && HasLineOfSightTo(target, allUnits);
    }

    public bool HasLineOfSightTo(Unit target, Array<Unit> allUnits)
    {
        if (!IsUsableUnit(this) || !IsUsableUnit(target))
        {
            return false;
        }

        var points = GetLinePoints(GridPos, target.GridPos);
        for (var i = 1; i < points.Count - 1; i++)
        {
            if (IsCellBlockingLineOfSight(points[i], target, allUnits))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsWithinRange(Vector2I from, Vector2I to, int range)
    {
        return RangeDistance(from, to) <= range;
    }

    public static int RangeDistance(Vector2I a, Vector2I b)
    {
        return Mathf.Max(Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y));
    }

    public bool HasEnoughMagicPoints(int amount)
    {
        return MagicPoints >= Mathf.Max(0, amount);
    }

    public bool TrySpendMagicPoints(int amount)
    {
        var spend = Mathf.Max(0, amount);
        if (MagicPoints < spend)
        {
            return false;
        }

        MagicPoints -= spend;
        return true;
    }

    public Dictionary ApplyStatusEffect(string statusId, string displayName, bool isBuff, int durationTurns, int startDelayTurns = 0, int damagePerTurn = 0, string stackingMode = "refresh", int maxStacks = 1, int stackAmount = 1, string scope = "persistent")
    {
        var result = new Dictionary
        {
            { "applied", false },
            { "base_status_id", "" },
            { "display_name", "" },
            { "stacks", 0 },
            { "remaining_turns", 0 },
            { "start_delay_turns", 0 },
            { "stacking_mode", "refresh" },
        };

        if (string.IsNullOrEmpty(statusId))
        {
            return result;
        }

        var baseStatusId = statusId.Trim();
        if (string.IsNullOrEmpty(baseStatusId))
        {
            return result;
        }

        var normalizedMode = NormalizeStackingMode(stackingMode);
        var safeScope = string.IsNullOrEmpty(scope) ? "persistent" : scope;
        var safeDuration = Mathf.Max(1, durationTurns);
        var safeDelay = Mathf.Max(0, startDelayTurns);
        var safeDamage = Mathf.Max(0, damagePerTurn);
        var safeStackAmount = Mathf.Max(1, stackAmount);
        var safeMaxStacks = Mathf.Max(1, maxStacks);

        if (normalizedMode == "independent")
        {
            _statusEffectSequence += 1;
            var instanceId = $"{baseStatusId}#{_statusEffectSequence}";
            _statusEffects[instanceId] = new StatusEffectState
            {
                BaseStatusId = baseStatusId,
                StatusId = instanceId,
                DisplayName = string.IsNullOrEmpty(displayName) ? baseStatusId : displayName,
                IsBuff = isBuff,
                RemainingTurns = safeDuration,
                StartDelayTurns = safeDelay,
                DamagePerTurn = safeDamage,
                Stacks = safeStackAmount,
                MaxStacks = safeMaxStacks,
                StackingMode = normalizedMode,
                Scope = safeScope,
            };

            result["applied"] = true;
            result["base_status_id"] = baseStatusId;
            result["display_name"] = string.IsNullOrEmpty(displayName) ? baseStatusId : displayName;
            result["stacks"] = safeStackAmount;
            result["remaining_turns"] = safeDuration;
            result["start_delay_turns"] = safeDelay;
            result["stacking_mode"] = normalizedMode;
            return result;
        }

        var existingKey = FindStatusEffectKeyByBaseId(baseStatusId);
        if (existingKey == null)
        {
            _statusEffects[baseStatusId] = new StatusEffectState
            {
                BaseStatusId = baseStatusId,
                StatusId = baseStatusId,
                DisplayName = string.IsNullOrEmpty(displayName) ? baseStatusId : displayName,
                IsBuff = isBuff,
                RemainingTurns = safeDuration,
                StartDelayTurns = safeDelay,
                DamagePerTurn = safeDamage,
                Stacks = safeStackAmount,
                MaxStacks = safeMaxStacks,
                StackingMode = normalizedMode,
                Scope = safeScope,
            };

            result["applied"] = true;
            result["base_status_id"] = baseStatusId;
            result["display_name"] = string.IsNullOrEmpty(displayName) ? baseStatusId : displayName;
            result["stacks"] = safeStackAmount;
            result["remaining_turns"] = safeDuration;
            result["start_delay_turns"] = safeDelay;
            result["stacking_mode"] = normalizedMode;
            return result;
        }

        var effect = _statusEffects[existingKey];
        effect.DisplayName = string.IsNullOrEmpty(displayName) ? effect.DisplayName : displayName;
        effect.IsBuff = isBuff;
        effect.Scope = safeScope;
        effect.MaxStacks = Mathf.Max(effect.MaxStacks, safeMaxStacks);
        effect.StackingMode = normalizedMode;
        effect.DamagePerTurn = Mathf.Max(effect.DamagePerTurn, safeDamage);

        if (normalizedMode == "intensity")
        {
            effect.Stacks = Mathf.Clamp(effect.Stacks + safeStackAmount, 1, effect.MaxStacks);
        }
        else
        {
            effect.Stacks = Mathf.Clamp(effect.Stacks, 1, effect.MaxStacks);
        }

        effect.RemainingTurns = Mathf.Max(effect.RemainingTurns, safeDuration);
        effect.StartDelayTurns = Mathf.Min(effect.StartDelayTurns, safeDelay);
        _statusEffects[existingKey] = effect;

        result["applied"] = true;
        result["base_status_id"] = effect.BaseStatusId;
        result["display_name"] = effect.DisplayName;
        result["stacks"] = effect.Stacks;
        result["remaining_turns"] = effect.RemainingTurns;
        result["start_delay_turns"] = effect.StartDelayTurns;
        result["stacking_mode"] = effect.StackingMode;
        return result;
    }

    public int ClearStatusEffectsByScope(string scope, bool includeBuffs = true)
    {
        if (string.IsNullOrEmpty(scope) || _statusEffects.Count == 0)
        {
            return 0;
        }

        var removed = 0;
        var keys = new Array<string>();
        foreach (var key in _statusEffects.Keys)
        {
            keys.Add(key);
        }

        foreach (var key in keys)
        {
            if (!_statusEffects.TryGetValue(key, out var effect))
            {
                continue;
            }

            if (effect.Scope != scope)
            {
                continue;
            }

            if (!includeBuffs && effect.IsBuff)
            {
                continue;
            }

            _statusEffects.Remove(key);
            removed += 1;
        }

        return removed;
    }

    public Array<Dictionary> ProcessStartOfTurnStatusEffects()
    {
        var events = new Array<Dictionary>();
        if (IsDead || _statusEffects.Count == 0)
        {
            return events;
        }

        var keys = new Array<string>();
        foreach (var key in _statusEffects.Keys)
        {
            keys.Add(key);
        }

        foreach (var key in keys)
        {
            if (!_statusEffects.TryGetValue(key, out var effect))
            {
                continue;
            }

            if (effect.StartDelayTurns > 0)
            {
                effect.StartDelayTurns = Mathf.Max(0, effect.StartDelayTurns - 1);
                _statusEffects[key] = effect;
                if (effect.StartDelayTurns > 0)
                {
                    continue;
                }
            }

            var damageApplied = 0;
            if (effect.DamagePerTurn > 0 && !IsDead)
            {
                damageApplied = ApplyDamage(effect.DamagePerTurn * Mathf.Max(1, effect.Stacks));
            }

            effect.RemainingTurns = Mathf.Max(0, effect.RemainingTurns - 1);
            var turnsLeft = effect.RemainingTurns;
            var expired = turnsLeft <= 0;

            events.Add(new Dictionary
            {
                { "status_id", effect.StatusId },
                { "display_name", effect.DisplayName },
                { "is_buff", effect.IsBuff },
                { "damage", damageApplied },
                { "remaining_turns", turnsLeft },
                { "stacks", effect.Stacks },
                { "expired", expired },
            });

            if (expired)
            {
                _statusEffects.Remove(key);
            }
            else
            {
                _statusEffects[key] = effect;
            }
        }

        return events;
    }

    public Array<Dictionary> GetStatusEntriesForHud()
    {
        var entries = new Array<Dictionary>();

        if (IsDefending && !IsDead)
        {
            entries.Add(new Dictionary
            {
                { "id", "defending" },
                { "label", "Defending" },
                { "is_buff", true },
                { "remaining_turns", -1 },
                { "start_delay_turns", 0 },
            });
        }

        foreach (var effect in _statusEffects.Values)
        {
            entries.Add(new Dictionary
            {
                { "id", effect.StatusId },
                { "base_id", effect.BaseStatusId },
                { "label", effect.DisplayName },
                { "is_buff", effect.IsBuff },
                { "remaining_turns", effect.RemainingTurns },
                { "start_delay_turns", effect.StartDelayTurns },
                { "stacks", effect.Stacks },
            });
        }

        return entries;
    }

    public int GrantExperience(int amount)
    {
        var gain = Mathf.Max(0, amount);
        if (gain <= 0)
        {
            return 0;
        }

        Experience += gain;
        var levelsGained = 0;
        while (Experience >= ExperienceToNextLevel)
        {
            Experience -= ExperienceToNextLevel;
            Level += 1;
            levelsGained += 1;
            ApplyLevelUpGains();
        }

        return levelsGained;
    }

    public void MarkAbilityUsed(string abilityId, int cooldownTurns = 0)
    {
        HasUsedAbilityThisTurn = true;
        if (!string.IsNullOrEmpty(abilityId))
        {
            _abilityCooldownRemaining[abilityId] = Mathf.Max(0, cooldownTurns);
        }
    }

    public void MarkDefending()
    {
        IsDefending = true;
        RefreshVisualState();
    }

    public void SetWeaponBonuses(int attackDamageBonus, int attackRangeBonus)
    {
        WeaponAttackDamageBonus = attackDamageBonus;
        WeaponAttackRangeBonus = attackRangeBonus;
    }

    public void SetArmorBonuses(int armorClassBonus)
    {
        ArmorClassBonus = armorClassBonus;
    }

    public Dictionary BuildRuntimeSnapshot()
    {
        var cooldowns = new Dictionary();
        foreach (var pair in _abilityCooldownRemaining)
        {
            cooldowns[pair.Key] = pair.Value;
        }

        var statusEffects = new Array<Dictionary>();
        foreach (var effect in _statusEffects.Values)
        {
            statusEffects.Add(new Dictionary
            {
                { "id", effect.StatusId },
                { "base_id", effect.BaseStatusId },
                { "display_name", effect.DisplayName },
                { "is_buff", effect.IsBuff },
                { "remaining_turns", effect.RemainingTurns },
                { "start_delay_turns", effect.StartDelayTurns },
                { "damage_per_turn", effect.DamagePerTurn },
                { "stacks", effect.Stacks },
                { "max_stacks", effect.MaxStacks },
                { "stacking_mode", effect.StackingMode },
                { "scope", effect.Scope },
            });
        }

        return new Dictionary
        {
            { "unit_id", UnitId },
            { "race", Race },
            { "grid_pos", GridPos },
            { "hit_points", HitPoints },
            { "max_hit_points", MaxHitPoints },
            { "magic_points", MagicPoints },
            { "max_magic_points", MaxMagicPoints },
            { "magic_point_regen_per_turn", MagicPointRegenPerTurn },
            { "is_dead", IsDead },
            { "movement_per_turn", MovementPerTurn },
            { "remaining_movement", RemainingMovement },
            { "has_used_ability_this_turn", HasUsedAbilityThisTurn },
            { "is_defending", IsDefending },
            { "level", Level },
            { "experience", Experience },
            { "cooldowns", cooldowns },
            { "status_effects", statusEffects },
        };
    }

    public void ApplyRuntimeSnapshot(Dictionary snapshot)
    {
        if (snapshot == null || snapshot.Count == 0)
        {
            return;
        }

        MaxHitPoints = Mathf.Max(1, GetInt(snapshot, "max_hit_points", MaxHitPoints));
        HitPoints = Mathf.Clamp(GetInt(snapshot, "hit_points", HitPoints), 0, MaxHitPoints);
        Race = GetString(snapshot, "race", Race);
        MaxMagicPoints = Mathf.Max(0, GetInt(snapshot, "max_magic_points", MaxMagicPoints));
        MagicPoints = Mathf.Clamp(GetInt(snapshot, "magic_points", MagicPoints), 0, MaxMagicPoints);
        MagicPointRegenPerTurn = Mathf.Max(0, GetInt(snapshot, "magic_point_regen_per_turn", MagicPointRegenPerTurn));
        Level = Mathf.Max(1, GetInt(snapshot, "level", Level));
        Experience = Mathf.Max(0, GetInt(snapshot, "experience", Experience));
        MovementPerTurn = Mathf.Max(1, GetInt(snapshot, "movement_per_turn", MovementPerTurn));
        RemainingMovement = Mathf.Clamp(GetInt(snapshot, "remaining_movement", RemainingMovement), 0, MovementPerTurn);
        HasUsedAbilityThisTurn = GetBool(snapshot, "has_used_ability_this_turn", HasUsedAbilityThisTurn);
        IsDefending = GetBool(snapshot, "is_defending", IsDefending);
        GridPos = GetVector2I(snapshot, "grid_pos", GridPos);

        _abilityCooldownRemaining.Clear();
        var cooldowns = GetDictionary(snapshot, "cooldowns");
        foreach (var key in cooldowns.Keys)
        {
            var abilityId = ((Variant)key).AsString();
            if (string.IsNullOrEmpty(abilityId))
            {
                continue;
            }

            _abilityCooldownRemaining[abilityId] = Mathf.Max(0, (int)((Variant)cooldowns[key]));
        }

        _statusEffects.Clear();
        var statusEffects = TryGetDictionaryArray(snapshot, "status_effects");
        foreach (var entry in statusEffects)
        {
            var statusId = GetString(entry, "id", "");
            if (string.IsNullOrEmpty(statusId))
            {
                continue;
            }

            var remainingTurns = Mathf.Max(0, GetInt(entry, "remaining_turns", 0));
            if (remainingTurns <= 0)
            {
                continue;
            }

            _statusEffects[statusId] = new StatusEffectState
            {
                BaseStatusId = GetString(entry, "base_id", statusId),
                StatusId = statusId,
                DisplayName = GetString(entry, "display_name", statusId),
                IsBuff = GetBool(entry, "is_buff", false),
                RemainingTurns = remainingTurns,
                StartDelayTurns = Mathf.Max(0, GetInt(entry, "start_delay_turns", 0)),
                DamagePerTurn = Mathf.Max(0, GetInt(entry, "damage_per_turn", 0)),
                Stacks = Mathf.Max(1, GetInt(entry, "stacks", 1)),
                MaxStacks = Mathf.Max(1, GetInt(entry, "max_stacks", 1)),
                StackingMode = NormalizeStackingMode(GetString(entry, "stacking_mode", "refresh")),
                Scope = GetString(entry, "scope", "persistent"),
            };
        }

        IsDead = GetBool(snapshot, "is_dead", HitPoints <= 0) || HitPoints <= 0;
        if (IsDead)
        {
            HitPoints = 0;
        }

        SyncWorldPosition();
        RefreshVisualState();
    }

    public void SetGridPos(Vector2I nextPos)
    {
        GridPos = nextPos;
        SyncWorldPosition();
    }

    public void SetActive(bool value)
    {
        IsActive = value;
        RefreshVisualState();
    }

    public int ApplyDamage(int amount)
    {
        if (IsDead)
        {
            return 0;
        }

        var incomingDamage = Mathf.Max(0, amount);
        if (IsDefending && incomingDamage > 0)
        {
            incomingDamage = Mathf.Max(0, incomingDamage - Mathf.FloorToInt(incomingDamage * DefendDamageReductionPercent / 100.0f));
        }

        HitPoints = Mathf.Max(0, HitPoints - incomingDamage);
        if (HitPoints <= 0)
        {
            IsDead = true;
        }

        RefreshVisualState();
        return incomingDamage;
    }

    public int ApplyHealing(int amount)
    {
        if (IsDead)
        {
            return 0;
        }

        var healAmount = Mathf.Max(0, amount);
        var nextHp = Mathf.Min(MaxHitPoints, HitPoints + healAmount);
        var actualHealed = nextHp - HitPoints;
        HitPoints = nextHp;
        QueueRedraw();
        return actualHealed;
    }

    private void RefreshVisualState()
    {
        // Keep living active units on top when multiple units overlap in a cell.
        if (IsDead)
        {
            ZIndex = 0;
        }
        else if (IsActive)
        {
            ZIndex = 20;
        }
        else
        {
            ZIndex = 10;
        }

        UpdateSpriteVisuals();
        QueueRedraw();
    }

    private void SyncWorldPosition()
    {
        Position = new Vector2(
            GridPos.X * CellSize + CellSize / 2.0f,
            GridPos.Y * CellSize + CellSize / 2.0f
        );
    }

    public override void _Draw()
    {
        if (IsActive)
        {
            DrawArc(Vector2.Zero, 30.0f, 0.0f, Mathf.Tau, 36, new Color(1.0f, 0.95f, 0.6f), 3.0f);
        }

        if (IsDead)
        {
            DrawLine(new Vector2(-20.0f, -20.0f), new Vector2(20.0f, 20.0f), new Color(0.2f, 0.2f, 0.2f, 0.8f), 3.0f);
            DrawLine(new Vector2(20.0f, -20.0f), new Vector2(-20.0f, 20.0f), new Color(0.2f, 0.2f, 0.2f, 0.8f), 3.0f);
        }

        if (IsDefending && !IsDead)
        {
            DrawArc(Vector2.Zero, 24.0f, 0.0f, Mathf.Tau, 36, new Color(0.45f, 0.85f, 1.0f, 0.9f), 2.0f);
        }
    }

    private void ConfigureSpriteRegion()
    {
        if (_sprite == null || _unitAtlas == null)
        {
            return;
        }

        _sprite.Texture = _unitAtlas;
        _sprite.RegionEnabled = true;

        var atlasCell = ResolveAtlasCell();
        _sprite.RegionRect = new Rect2(
            atlasCell.X * AtlasTileSize,
            atlasCell.Y * AtlasTileSize,
            AtlasTileSize,
            AtlasTileSize
        );
    }

    private void UpdateSpriteVisuals()
    {
        if (_sprite == null)
        {
            return;
        }

        _sprite.Modulate = Team == "enemy"
            ? new Color(1.0f, 0.92f, 0.92f, 1.0f)
            : new Color(1.0f, 1.0f, 1.0f, 1.0f);

        if (IsDead)
        {
            _sprite.Modulate = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        }
    }

    private Vector2I ResolveAtlasCell()
    {
        var key = NormalizeToken(UnitId + " " + UnitName);

        if (ContainsAny(key, "goblinarcher")) return new Vector2I(4, 1);
        if (ContainsAny(key, "goblinshaman")) return new Vector2I(5, 1);
        if (ContainsAny(key, "goblinchieftain", "chieftain")) return new Vector2I(0, 2);
        if (ContainsAny(key, "skeletonwarrior")) return new Vector2I(1, 2);
        if (ContainsAny(key, "skeletonmage")) return new Vector2I(2, 2);
        if (ContainsAny(key, "ghoul")) return new Vector2I(3, 2);
        if (ContainsAny(key, "direwolf", "wolf")) return new Vector2I(0, 3);
        if (ContainsAny(key, "giantspider", "spider")) return new Vector2I(1, 3);
        if (ContainsAny(key, "zombie")) return new Vector2I(3, 2);
        if (ContainsAny(key, "necromancer")) return new Vector2I(4, 2);
        if (ContainsAny(key, "wizard")) return new Vector2I(0, 0);
        if (ContainsAny(key, "ranger")) return new Vector2I(3, 0);
        if (ContainsAny(key, "goblin")) return new Vector2I(3, 1);
        if (ContainsAny(key, "warrior")) return new Vector2I(1, 0);
        if (ContainsAny(key, "cleric")) return new Vector2I(2, 0);

        return Team == "enemy"
            ? new Vector2I(3, 1)
            : new Vector2I(1, 0);
    }

    private string ResolveRace()
    {
        var key = NormalizeToken(UnitId + " " + UnitName);

        if (ContainsAny(key, "warrior")) return "dwarf";
        if (ContainsAny(key, "ranger")) return "elf";
        if (ContainsAny(key, "goblin", "goblinarcher", "goblinshaman", "goblinchieftain", "chieftain")) return "goblin";
        if (ContainsAny(key, "skeleton", "skeletonwarrior", "skeletonmage", "zombie")) return "undead";

        return "human";
    }

    private static string NormalizeToken(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        return value
            .ToLowerInvariant()
            .Replace("-", "")
            .Replace("_", "")
            .Replace(" ", "");
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (haystack.Contains(needle))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetString(Dictionary dict, string key, string fallback)
    {
        return dict.ContainsKey(key) ? ((Variant)dict[key]).AsString() : fallback;
    }

    private static int GetInt(Dictionary dict, string key, int fallback)
    {
        return dict.ContainsKey(key) ? (int)((Variant)dict[key]) : fallback;
    }

    private static bool GetBool(Dictionary dict, string key, bool fallback)
    {
        if (!dict.ContainsKey(key))
        {
            return fallback;
        }

        var value = (Variant)dict[key];
        if (value.VariantType == Variant.Type.Bool)
        {
            return value.AsBool();
        }

        if (value.VariantType == Variant.Type.Int)
        {
            return (int)value != 0;
        }

        return fallback;
    }

    private static Dictionary GetDictionary(Dictionary dict, string key)
    {
        if (!dict.ContainsKey(key))
        {
            return new Dictionary();
        }

        var value = (Variant)dict[key];
        return value.VariantType == Variant.Type.Dictionary ? (Dictionary)value : new Dictionary();
    }

    private static Array<Dictionary> TryGetDictionaryArray(Dictionary dict, string key)
    {
        if (!dict.ContainsKey(key))
        {
            return new Array<Dictionary>();
        }

        var raw = (Variant)dict[key];
        if (raw.VariantType != Variant.Type.Array)
        {
            return new Array<Dictionary>();
        }

        var result = new Array<Dictionary>();
        foreach (var entry in (Array)raw)
        {
            var variant = (Variant)entry;
            if (variant.VariantType == Variant.Type.Dictionary)
            {
                result.Add((Dictionary)variant);
            }
        }

        return result;
    }

    private static string NormalizeStackingMode(string stackingMode)
    {
        var mode = string.IsNullOrEmpty(stackingMode)
            ? "refresh"
            : stackingMode.Trim().ToLowerInvariant();

        return mode switch
        {
            "refresh" => "refresh",
            "intensity" => "intensity",
            "independent" => "independent",
            _ => "refresh"
        };
    }

    private string FindStatusEffectKeyByBaseId(string baseStatusId)
    {
        if (string.IsNullOrEmpty(baseStatusId))
        {
            return null;
        }

        if (_statusEffects.ContainsKey(baseStatusId))
        {
            return baseStatusId;
        }

        foreach (var pair in _statusEffects)
        {
            if (pair.Value.BaseStatusId == baseStatusId)
            {
                return pair.Key;
            }
        }

        return null;
    }

    private void ApplyLevelUpGains()
    {
        MaxHitPoints += 2;
        HitPoints = Mathf.Min(MaxHitPoints, HitPoints + 2);
        MaxMagicPoints += 1;
        MagicPoints = Mathf.Min(MaxMagicPoints, MagicPoints + 1);
    }

    private bool IsCellBlockingLineOfSight(Vector2I cell, Unit target, Array<Unit> allUnits)
    {
        if (allUnits == null)
        {
            return false;
        }

        foreach (var unit in allUnits)
        {
            if (!IsUsableUnit(unit) || unit.IsDead || unit == this || unit == target)
            {
                continue;
            }

            // Allies do not block line of sight for this unit.
            if (unit.Team == Team)
            {
                continue;
            }

            if (unit.GridPos == cell)
            {
                return true;
            }
        }

        return false;
    }

    private static Array<Vector2I> GetLinePoints(Vector2I start, Vector2I end)
    {
        var points = new Array<Vector2I>();

        var x0 = start.X;
        var y0 = start.Y;
        var x1 = end.X;
        var y1 = end.Y;
        var dx = Mathf.Abs(x1 - x0);
        var dy = Mathf.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;

        while (true)
        {
            points.Add(new Vector2I(x0, y0));
            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            var e2 = err * 2;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }

        return points;
    }

    private static bool IsUsableUnit(Unit unit)
    {
        return unit != null && GodotObject.IsInstanceValid(unit) && !unit.IsQueuedForDeletion();
    }

    private static Vector2I GetVector2I(Dictionary dict, string key, Vector2I fallback)
    {
        return dict.ContainsKey(key) ? (Vector2I)((Variant)dict[key]) : fallback;
    }

    private static Array<string> BuildAbilityIds(Dictionary config, string fallbackPrimary)
    {
        var ids = TryGetStringArray(config, "ability_ids");
        if (ids.Count == 0)
        {
            ids.Add(fallbackPrimary);
        }

        var unique = new Array<string>();
        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            var exists = false;
            foreach (var existing in unique)
            {
                if (existing == id)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                unique.Add(id);
            }
        }

        return unique;
    }

    private static Array<string> TryGetStringArray(Dictionary dict, string key)
    {
        if (!dict.ContainsKey(key))
        {
            return new Array<string>();
        }

        var raw = (Variant)dict[key];
        if (raw.VariantType != Variant.Type.Array)
        {
            return new Array<string>();
        }

        var result = new Array<string>();
        foreach (var entry in (Array)raw)
        {
            var variant = (Variant)entry;
            if (variant.VariantType == Variant.Type.String)
            {
                result.Add(variant.AsString());
            }
        }

        return result;
    }
}
