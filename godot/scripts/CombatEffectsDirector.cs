using Godot;

public partial class CombatEffectsDirector : Node2D
{
    private const float PopupRise = 34.0f;

    public override void _Ready()
    {
        ZIndex = 100;
        ZAsRelative = false;
    }

    public void PlayAttack(Unit attacker, Unit target, string actionId, bool isMagical, int damage)
    {
        if (!IsUsable(attacker) || !IsUsable(target))
        {
            return;
        }

        attacker.FlashFocusHighlight(isMagical ? new Color("8f7cff") : new Color("ffd27a"), 0.28f);
        var isProjectile = isMagical || Unit.RangeDistance(attacker.GridPos, target.GridPos) > 1 || actionId is "ranged" or "pin";
        if (isProjectile)
        {
            var impactColor = isMagical ? new Color("b5a6ff") : new Color("ffcf73");
            PlayProjectile(attacker.Position, target.Position, isMagical ? new Color("a895ff") : new Color("f3d08a"), () =>
            {
                if (!IsUsable(target))
                {
                    return;
                }

                PlayImpact(target.Position, impactColor);
                target.FlashFocusHighlight(new Color("ff5e57"), 0.32f);
                ShowFloatingText(target.Position, damage > 0 ? $"-{damage}" : "BLOCK", damage > 0 ? new Color("ff766d") : new Color("b9c5d8"));
            });
        }
        else
        {
            PlaySlash(attacker.Position, target.Position, actionId == "poison-strike" ? new Color("73d66f") : new Color("ffe1a3"));
            PlayImpact(target.Position, actionId == "poison-strike" ? new Color("62c95e") : new Color("ffb55e"));
            target.FlashFocusHighlight(new Color("ff5e57"), 0.32f);
            ShowFloatingText(target.Position, damage > 0 ? $"-{damage}" : "BLOCK", damage > 0 ? new Color("ff766d") : new Color("b9c5d8"));
        }
    }

    public void PlayHeal(Unit caster, Unit target, int amount)
    {
        if (!IsUsable(caster) || !IsUsable(target))
        {
            return;
        }

        PlayRing(caster.Position, new Color("78e6b0"), 22.0f, 0.34f);
        PlayRing(target.Position, new Color("9dffd0"), 42.0f, 0.52f);
        target.FlashFocusHighlight(new Color("83f0b7"), 0.5f);
        ShowFloatingText(target.Position, $"+{amount}", new Color("8ff0b2"));
    }

    public void PlayDamageResult(Unit target, int damage, Color color)
    {
        if (!IsUsable(target))
        {
            return;
        }

        PlayImpact(target.Position, color, 0.82f);
        target.FlashFocusHighlight(color, 0.32f);
        ShowFloatingText(target.Position, damage > 0 ? $"-{damage}" : "IMMUNE", damage > 0 ? color.Lightened(0.2f) : new Color("b9c5d8"));
    }

    public void PlayStatus(Unit target, Color color, string label)
    {
        if (!IsUsable(target))
        {
            return;
        }

        PlayRing(target.Position, color, 30.0f, 0.46f);
        target.FlashFocusHighlight(color, 0.5f);
        ShowFloatingText(target.Position, label, color.Lightened(0.25f));
    }

    public void PlayArea(Vector2 center, float radius, Color color, string label = "")
    {
        PlayRing(center, color, Mathf.Max(32.0f, radius), 0.58f);
        PlayImpact(center, color, 1.45f);
        if (!string.IsNullOrEmpty(label))
        {
            ShowFloatingText(center, label, color.Lightened(0.28f));
        }
    }

    public void PlayBuff(Unit caster, Color color, string label)
    {
        if (!IsUsable(caster))
        {
            return;
        }

        PlayRing(caster.Position, color, 52.0f, 0.62f);
        caster.FlashFocusHighlight(color, 0.6f);
        ShowFloatingText(caster.Position, label, color.Lightened(0.25f));
    }

    public void PlayDefend(Unit actor)
    {
        PlayBuff(actor, new Color("70b7ff"), "GUARD");
    }

    private void PlayProjectile(Vector2 from, Vector2 to, Color color, System.Action onImpact)
    {
        var visual = CreateVisual(CombatEffectVisual.EffectKind.Projectile, from, color);
        visual.Direction = (to - from).Normalized();
        visual.QueueRedraw();

        var distance = from.DistanceTo(to);
        var duration = Mathf.Clamp(distance / 720.0f, 0.16f, 0.46f);
        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Quad);
        tween.SetEase(Tween.EaseType.In);
        tween.TweenProperty(visual, "position", to, duration);
        tween.Parallel().TweenMethod(Callable.From<float>(value => visual.Progress = value), 0.0f, 1.0f, duration);
        tween.TweenCallback(Callable.From(() =>
        {
            onImpact?.Invoke();
            visual.QueueFree();
        }));
    }

    private void PlaySlash(Vector2 from, Vector2 to, Color color)
    {
        var visual = CreateVisual(CombatEffectVisual.EffectKind.Slash, to, color);
        visual.Direction = (to - from).Normalized();
        visual.QueueRedraw();
        AnimateAndFree(visual, 0.24f, 1.15f);
    }

    private void PlayImpact(Vector2 position, Color color, float scale = 1.0f)
    {
        var visual = CreateVisual(CombatEffectVisual.EffectKind.Impact, position, color);
        visual.Scale = Vector2.One * scale;
        AnimateAndFree(visual, 0.38f, 1.35f);
    }

    private void PlayRing(Vector2 position, Color color, float radius, float duration)
    {
        var visual = CreateVisual(CombatEffectVisual.EffectKind.Ring, position, color);
        visual.Radius = radius;
        visual.QueueRedraw();
        AnimateAndFree(visual, duration, 1.12f);
    }

    private void ShowFloatingText(Vector2 position, string text, Color color)
    {
        var label = new Label
        {
            Text = text,
            Position = position + new Vector2(-30.0f, -42.0f),
            Size = new Vector2(60.0f, 28.0f),
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = color,
            ZIndex = 110
        };
        label.AddThemeFontSizeOverride("font_size", 20);
        label.AddThemeColorOverride("font_outline_color", new Color(0.04f, 0.04f, 0.06f, 0.95f));
        label.AddThemeConstantOverride("outline_size", 5);
        AddChild(label);

        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Quad);
        tween.SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "position:y", label.Position.Y - PopupRise, 0.62f);
        tween.Parallel().TweenProperty(label, "modulate:a", 0.0f, 0.62f).SetDelay(0.22f);
        tween.TweenCallback(Callable.From(label.QueueFree));
    }

    private CombatEffectVisual CreateVisual(CombatEffectVisual.EffectKind kind, Vector2 position, Color color)
    {
        var visual = new CombatEffectVisual
        {
            Kind = kind,
            Position = position,
            EffectColor = color,
            ZIndex = 105
        };
        AddChild(visual);
        return visual;
    }

    private void AnimateAndFree(CombatEffectVisual visual, float duration, float endScale)
    {
        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Quad);
        tween.SetEase(Tween.EaseType.Out);
        tween.TweenMethod(Callable.From<float>(value => visual.Progress = value), 0.0f, 1.0f, duration);
        tween.Parallel().TweenProperty(visual, "scale", visual.Scale * endScale, duration);
        tween.TweenCallback(Callable.From(visual.QueueFree));
    }

    private static bool IsUsable(Unit unit)
    {
        return unit != null && GodotObject.IsInstanceValid(unit) && !unit.IsQueuedForDeletion();
    }
}

public partial class CombatEffectVisual : Node2D
{
    public enum EffectKind
    {
        Projectile,
        Slash,
        Impact,
        Ring
    }

    public EffectKind Kind { get; set; }
    public Color EffectColor { get; set; } = Colors.White;
    public Vector2 Direction { get; set; } = Vector2.Right;
    public float Radius { get; set; } = 32.0f;

    private float _progress;
    public float Progress
    {
        get => _progress;
        set
        {
            _progress = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var alpha = 1.0f - Mathf.Clamp(_progress, 0.0f, 1.0f);
        var color = new Color(EffectColor, alpha);
        var pale = new Color(EffectColor.Lightened(0.5f), alpha * 0.9f);

        switch (Kind)
        {
            case EffectKind.Projectile:
                DrawLine(-Direction * 30.0f, Direction * 5.0f, new Color(EffectColor, alpha * 0.28f), 12.0f, true);
                DrawCircle(Vector2.Zero, 8.0f, color);
                DrawCircle(Vector2.Zero, 3.5f, pale);
                break;
            case EffectKind.Slash:
                var normal = new Vector2(-Direction.Y, Direction.X);
                DrawLine((-Direction * 20.0f) - (normal * 22.0f), (Direction * 20.0f) + (normal * 22.0f), color, 9.0f, true);
                DrawLine((-Direction * 16.0f) - (normal * 18.0f), (Direction * 18.0f) + (normal * 18.0f), pale, 3.0f, true);
                break;
            case EffectKind.Impact:
                var impactRadius = Mathf.Lerp(8.0f, 36.0f, _progress);
                DrawCircle(Vector2.Zero, impactRadius, new Color(EffectColor, alpha * 0.2f));
                for (var index = 0; index < 8; index++)
                {
                    var ray = Vector2.Right.Rotated(index * Mathf.Tau / 8.0f);
                    DrawLine(ray * 9.0f, ray * impactRadius, color, 4.0f, true);
                }
                break;
            case EffectKind.Ring:
                var ringRadius = Mathf.Lerp(Radius * 0.35f, Radius, _progress);
                DrawArc(Vector2.Zero, ringRadius, 0.0f, Mathf.Tau, 48, color, 5.0f, true);
                DrawArc(Vector2.Zero, ringRadius * 0.72f, 0.0f, Mathf.Tau, 48, new Color(pale, alpha * 0.55f), 2.0f, true);
                break;
        }
    }
}