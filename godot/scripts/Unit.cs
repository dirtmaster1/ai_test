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
    public bool IsDead { get; private set; }
    public bool IsActive { get; private set; }

    public void Setup(Dictionary config)
    {
        UnitId = GetString(config, "id", "unit");
        UnitName = GetString(config, "name", "Unit");
        Team = GetString(config, "team", "player");
        MaxHitPoints = GetInt(config, "max_hit_points", 10);
        HitPoints = GetInt(config, "hit_points", MaxHitPoints);
        GridPos = GetVector2I(config, "grid_pos", Vector2I.Zero);
        SyncWorldPosition();
        QueueRedraw();
    }

    public void SetGridPos(Vector2I nextPos)
    {
        GridPos = nextPos;
        SyncWorldPosition();
    }

    public void SetActive(bool value)
    {
        IsActive = value;
        QueueRedraw();
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
