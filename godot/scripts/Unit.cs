using Godot;
using Godot.Collections;

public partial class Unit : Node2D
{
    private const int CellSize = 64;

    public string UnitId { get; private set; } = "";
    public string UnitName { get; private set; } = "";
    public string Team { get; private set; } = "player";
    public Vector2I GridPos { get; private set; } = Vector2I.Zero;
    public int HitPoints { get; private set; } = 10;
    public int MaxHitPoints { get; private set; } = 10;
    public int BaseAttackDamage { get; private set; } = 3;
    public int BaseAttackRange { get; private set; } = 1;
    public string PrimaryAbilityId { get; private set; } = "";
    public string EncounterId { get; private set; } = "";
    public int Initiative { get; private set; } = 10;
    public int WeaponAttackDamageBonus { get; private set; }
    public int WeaponAttackRangeBonus { get; private set; }
    public int BuffAttackDamageBonus { get; private set; }
    public int BuffAttackRangeBonus { get; private set; }

    public int AttackDamage => Mathf.Max(0, BaseAttackDamage + WeaponAttackDamageBonus + BuffAttackDamageBonus);
    public int AttackRange => Mathf.Max(1, BaseAttackRange + WeaponAttackRangeBonus + BuffAttackRangeBonus);
    public bool IsDead { get; private set; }
    public bool IsActive { get; private set; }

    public void Setup(Dictionary config)
    {
        UnitId = GetString(config, "id", "unit");
        UnitName = GetString(config, "name", "Unit");
        Team = GetString(config, "team", "player");
        EncounterId = GetString(config, "encounter_id", "");
        MaxHitPoints = GetInt(config, "max_hit_points", 10);
        HitPoints = GetInt(config, "hit_points", MaxHitPoints);
        Initiative = GetInt(config, "initiative", 10);
        PrimaryAbilityId = GetString(config, "primary_ability_id", Team == "enemy" ? "melee" : "melee");
        BaseAttackDamage = GetInt(config, "base_attack_damage", Team == "player" ? 4 : 3);
        BaseAttackRange = GetInt(config, "base_attack_range", 1);
        WeaponAttackDamageBonus = GetInt(config, "weapon_attack_damage_bonus", 0);
        WeaponAttackRangeBonus = GetInt(config, "weapon_attack_range_bonus", 0);
        BuffAttackDamageBonus = GetInt(config, "buff_attack_damage_bonus", 0);
        BuffAttackRangeBonus = GetInt(config, "buff_attack_range_bonus", 0);
        GridPos = GetVector2I(config, "grid_pos", Vector2I.Zero);
        SyncWorldPosition();
        RefreshVisualState();
    }

    public void SetBuffAttackDamageBonus(int bonus)
    {
        BuffAttackDamageBonus = bonus;
    }

    public void SetBuffAttackRangeBonus(int bonus)
    {
        BuffAttackRangeBonus = bonus;
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

    private static Vector2I GetVector2I(Dictionary dict, string key, Vector2I fallback)
    {
        return dict.ContainsKey(key) ? (Vector2I)((Variant)dict[key]) : fallback;
    }
}
