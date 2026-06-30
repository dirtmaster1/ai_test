using Godot;
using Godot.Collections;

public partial class Unit : Node2D
{
    private const int CellSize = 64;
    public const int MaxMovementPerTurn = 3;

    public string UnitId { get; private set; } = "";
    public string UnitName { get; private set; } = "";
    public string Team { get; private set; } = "player";
    public Vector2I GridPos { get; private set; } = Vector2I.Zero;
    public int HitPoints { get; private set; } = 10;
    public int MaxHitPoints { get; private set; } = 10;
    public int MagicPoints { get; private set; }
    public int MaxMagicPoints { get; private set; }
    public int Intelligence { get; private set; } = 5;
    public int Strength { get; private set; } = 5;
    public int Wisdom { get; private set; } = 5;
    public int Dexterity { get; private set; } = 5;
    public int Constitution { get; private set; } = 5;
    public int BaseAttackDamage { get; private set; } = 3;
    public int BaseAttackRange { get; private set; } = 1;
    public string PrimaryAbilityId { get; private set; } = "";
    public Array<string> AbilityIds { get; private set; } = new();
    public string EncounterId { get; private set; } = "";
    public int Initiative { get; private set; } = 10;
    public int WeaponAttackDamageBonus { get; private set; }
    public int WeaponAttackRangeBonus { get; private set; }
    public int ArmorClassBonus { get; private set; }
    public int ArmorAttackDamageBonus { get; private set; }
    public int ArmorAttackRangeBonus { get; private set; }
    public int BuffAttackDamageBonus { get; private set; }
    public int BuffAttackRangeBonus { get; private set; }

    public int AttackDamage => Mathf.Max(0, BaseAttackDamage + WeaponAttackDamageBonus + ArmorAttackDamageBonus + BuffAttackDamageBonus);
    public int AttackRange => Mathf.Max(1, BaseAttackRange + WeaponAttackRangeBonus + ArmorAttackRangeBonus + BuffAttackRangeBonus);
    public int ArmorClass => Mathf.Max(0, ArmorClassBonus);
    public int RemainingMovement { get; private set; } = MaxMovementPerTurn;
    public bool HasUsedAbilityThisTurn { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsActive { get; private set; }
    private readonly Dictionary<string, int> _abilityCooldownRemaining = new();

    public void Setup(Dictionary config)
    {
        UnitId = GetString(config, "id", "unit");
        UnitName = GetString(config, "name", "Unit");
        Team = GetString(config, "team", "player");
        EncounterId = GetString(config, "encounter_id", "");
        MaxHitPoints = GetInt(config, "max_hit_points", 10);
        HitPoints = GetInt(config, "hit_points", MaxHitPoints);
        MaxMagicPoints = Mathf.Max(0, GetInt(config, "max_magic_points", 0));
        MagicPoints = Mathf.Clamp(GetInt(config, "magic_points", MaxMagicPoints), 0, MaxMagicPoints);
        Intelligence = GetInt(config, "intelligence", 5);
        Strength = GetInt(config, "strength", 5);
        Wisdom = GetInt(config, "wisdom", 5);
        Dexterity = GetInt(config, "dexterity", 5);
        Constitution = GetInt(config, "constitution", 5);
        Initiative = GetInt(config, "initiative", 10);
        PrimaryAbilityId = GetString(config, "primary_ability_id", Team == "enemy" ? "melee" : "melee");
        AbilityIds = BuildAbilityIds(config, PrimaryAbilityId);
        BaseAttackDamage = GetInt(config, "base_attack_damage", Team == "player" ? 4 : 3);
        BaseAttackRange = GetInt(config, "base_attack_range", 1);
        WeaponAttackDamageBonus = GetInt(config, "weapon_attack_damage_bonus", 0);
        WeaponAttackRangeBonus = GetInt(config, "weapon_attack_range_bonus", 0);
        ArmorClassBonus = GetInt(config, "armor_class_bonus", 0);
        ArmorAttackDamageBonus = GetInt(config, "armor_attack_damage_bonus", 0);
        ArmorAttackRangeBonus = GetInt(config, "armor_attack_range_bonus", 0);
        BuffAttackDamageBonus = GetInt(config, "buff_attack_damage_bonus", 0);
        BuffAttackRangeBonus = GetInt(config, "buff_attack_range_bonus", 0);
        GridPos = GetVector2I(config, "grid_pos", Vector2I.Zero);
        ResetTurnResources();
        SyncWorldPosition();
        RefreshVisualState();
    }

    public void ResetTurnResources()
    {
        RemainingMovement = MaxMovementPerTurn;
        HasUsedAbilityThisTurn = false;

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

    public void MarkAbilityUsed(string abilityId, int cooldownTurns = 0)
    {
        HasUsedAbilityThisTurn = true;
        if (!string.IsNullOrEmpty(abilityId))
        {
            _abilityCooldownRemaining[abilityId] = Mathf.Max(0, cooldownTurns);
        }
    }

    public void SetBuffAttackDamageBonus(int bonus)
    {
        BuffAttackDamageBonus = bonus;
    }

    public void SetBuffAttackRangeBonus(int bonus)
    {
        BuffAttackRangeBonus = bonus;
    }

    public void SetWeaponBonuses(int attackDamageBonus, int attackRangeBonus)
    {
        WeaponAttackDamageBonus = attackDamageBonus;
        WeaponAttackRangeBonus = attackRangeBonus;
    }

    public void SetArmorBonuses(int armorClassBonus, int attackDamageBonus, int attackRangeBonus)
    {
        ArmorClassBonus = armorClassBonus;
        ArmorAttackDamageBonus = attackDamageBonus;
        ArmorAttackRangeBonus = attackRangeBonus;
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

    public void ApplyDamage(int amount)
    {
        if (IsDead)
        {
            return;
        }

        HitPoints = Mathf.Max(0, HitPoints - Mathf.Max(0, amount));
        if (HitPoints <= 0)
        {
            IsDead = true;
        }

        RefreshVisualState();
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
        const float radius = 22.0f;
        var fill = new Color(0.4f, 0.8f, 1.0f);
        if (Team == "enemy")
        {
            fill = new Color(0.95f, 0.4f, 0.4f);
        }

        if (IsDead)
        {
            fill = new Color(0.25f, 0.25f, 0.25f);
        }

        DrawCircle(Vector2.Zero, radius, fill);
        if (IsActive)
        {
            DrawArc(Vector2.Zero, radius + 5.0f, 0.0f, Mathf.Tau, 36, new Color(1.0f, 0.95f, 0.6f), 3.0f);
        }
    }

    private static string GetString(Dictionary dict, string key, string fallback)
    {
        return dict.ContainsKey(key) ? ((Variant)dict[key]).AsString() : fallback;
    }

    private static int GetInt(Dictionary dict, string key, int fallback)
    {
        return dict.ContainsKey(key) ? (int)((Variant)dict[key]) : fallback;
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
