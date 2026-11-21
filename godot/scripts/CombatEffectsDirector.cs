using Godot;

public partial class CombatEffectsDirector : Node2D
{
    private const float PopupRise = 34.0f;
    private const float EffectAnimationDuration = 16.0f / 24.0f;
    private GameOptionsManager _gameOptionsManager;
    private EventBus _eventBus;
    private GameData _gameData;
    private readonly System.Collections.Generic.Dictionary<string, SpriteFrames> _effectFrames = new();

    public override void _Ready()
    {
        ZIndex = 100;
        ZAsRelative = false;
        _gameOptionsManager = GetNode<GameOptionsManager>("/root/GameOptionsManager");
        _eventBus = GetNode<EventBus>("/root/EventBus");
        _eventBus.ActionUsed += OnActionUsed;
        _gameData = GetNode<GameData>("/root/GameData");
    }

    public override void _ExitTree()
    {
        if (_eventBus != null)
        {
            _eventBus.ActionUsed -= OnActionUsed;
        }
    }

    private void OnActionUsed(Variant unitValue, Variant actionIdValue, Variant payload)
    {
        if (unitValue.VariantType != Variant.Type.Object
            || unitValue.AsGodotObject() is not Unit actor
            || !IsUsable(actor)
            || actionIdValue.VariantType != Variant.Type.String)
        {
            return;
        }

        var actionId = actionIdValue.AsString();
        if (actionId == "identify")
        {
            return;
        }

        var spell = _gameData.GetSpell(actionId);
        if (spell.Count > 0)
        {
            actor.PlayCastAnimation();
            return;
        }

        var ability = _gameData.GetAbility(actionId);
        if (ability.Count == 0 || !ability.ContainsKey("type"))
        {
            return;
        }

        var actionType = ((Variant)ability["type"]).AsString();
        if (actionType is "attack" or "ranged" or "charge" or "pin"
            or "rain_of_arrows" or "shadow_strike" or "poison_strike")
        {
            actor.PlayAttackAnimation();
        }
        else
        {
            actor.PlayCastAnimation();
        }
    }

    public void PlayAttack(Unit attacker, Unit target, string actionId, bool isMagical, int damage)
    {
        if (!IsUsable(attacker) || !IsUsable(target))
        {
            return;
        }

        attacker.FaceDirection(target.Position - attacker.Position);
        attacker.FlashFocusHighlight(isMagical ? new Color("8f7cff") : new Color("ffd27a"), 0.28f);
        var isProjectile = isMagical || attacker.DistanceToUnitAt(attacker.GridPos, target) > 1 || actionId is "ranged" or "pin";
        if (isProjectile)
        {
            var impactColor = isMagical ? new Color("b5a6ff") : new Color("ffcf73");
            PlayProjectile(
                attacker.Position,
                target.Position,
                isMagical ? new Color("a895ff") : new Color("f3d08a"),
                isMagical ? "arcane_projectile" : "arrow",
                () =>
            {
                if (!IsUsable(target))
                {
                    return;
                }

                if (isMagical) Cue(CombatEffectVisual.Mark.ArcaneHit, target.Position, new Color("99ddff"), 0.32f);
                else PlayImpact(target.Position, impactColor);
                if (actionId == "pin") Cue(CombatEffectVisual.Mark.Pin, target.Position, impactColor);
                target.FlashFocusHighlight(new Color("ff5e57"), 0.32f);
                ShowFloatingText(target.Position, damage > 0 ? $"-{damage}" : "BLOCK", damage > 0 ? new Color("ff766d") : new Color("b9c5d8"));
            });
        }
        else
        {
            PlaySlash(attacker.Position, target.Position, actionId == "shadow-strike" ? new Color("b8a2dd") : new Color("e3e0d9"));
            if (actionId == "poison-strike") Cue(CombatEffectVisual.Mark.Drops, target.Position, new Color("95e264"));
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

        caster.FaceDirection(target.Position - caster.Position);
        PlayRing(caster.Position, new Color("78e6b0"), 22.0f, 0.34f);
        PlayRing(target.Position, new Color("9dffd0"), 42.0f, 0.52f);
        PlaySpriteEffect("holy_aura", target.Position, Colors.White, 1.2f);
        target.FlashFocusHighlight(new Color("83f0b7"), 0.5f);
        ShowFloatingText(target.Position, $"+{amount}", new Color("8ff0b2"));
    }

    public void PlayHealingSpell(Unit caster, Unit target, int amount, bool greater)
    {
        if (!IsUsable(caster) || !IsUsable(target))
            return;
        var direction = (target.Position - caster.Position).Normalized();
        caster.FaceDirection(direction);
        var mint = new Color("8affe1");
        var hand = caster.Position + (direction == Vector2.Zero ? Vector2.Right : direction) * 13 + Vector2.Up * 7;
        AnimateAndFree(CreateVisual(CombatEffectVisual.EffectKind.HealGather, hand, mint), 0.20f, 1);
        var at = target.Position;
        void Pulse() => AnimateAndFree(CreateVisual(CombatEffectVisual.EffectKind.HealPulse, at, mint), 0.62f, 1);
        var sequence = CreateTween();
        sequence.TweenInterval(ScaleDuration(0.16f));
        sequence.TweenCallback(Callable.From(() =>
        {
            Pulse();
            ShowFloatingText(at, $"+{amount}", mint);
        }));
        if (greater)
        {
            sequence.TweenInterval(ScaleDuration(0.34f));
            sequence.TweenCallback(Callable.From(Pulse));
        }
    }

    public void PlayFireball(Vector2 from, Vector2 center, float cellSize, Vector2[] cells,
        Godot.Collections.Dictionary<Vector2, int> hits)
    {
        var direction = (center - from).Normalized();
        var sequence = CreateTween();
        sequence.TweenInterval(ScaleDuration(0.15f));
        sequence.TweenCallback(Callable.From(() => PlayProjectile(from + direction * 14, center,
            new Color("ff802f"), "fireball", () =>
        {
            var wave = CreateVisual(CombatEffectVisual.EffectKind.FireFront, center, Colors.White);
            wave.Cells = cells;
            wave.Radius = cellSize / 2;
            var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/vfx/combat/fire_front.gdshader") };
            wave.Material = material;
            var extent = 0f;
            foreach (var cell in cells)
                extent = Mathf.Max(extent, center.DistanceTo(cell) + cellSize * 0.71f);
            // Snapshot results, not Unit references: defeated units may already have left the tree.
            var spread = wave.CreateTween().SetParallel();
            const float speed = 560;
            spread.TweenMethod(Callable.From<float>(r => material.SetShaderParameter("front", r)),
                0f, extent + 90, ScaleDuration((extent + 90) / speed));
            foreach (var hit in hits)
                spread.TweenCallback(Callable.From(() => ShowFloatingText(hit.Key,
                    hit.Value > 0 ? $"-{hit.Value}" : "IMMUNE", new Color("ff966e"))))
                    .SetDelay(ScaleDuration(center.DistanceTo(hit.Key) / speed));
            spread.Chain().TweenCallback(Callable.From(wave.QueueFree));
        })));
    }

    public void PlayCleansing(Unit target)
    {
        if (!IsUsable(target))
        {
            return;
        }

        var color = new Color("74e6b2");
        Cue(CombatEffectVisual.Mark.Cleanse, target.Position, color);
        target.FlashFocusHighlight(color, 0.56f);
        ShowFloatingText(target.Position, "CLEANSED", new Color("a5ffd0"));
    }

    public void PlayResurrection(Unit target)
    {
        if (!IsUsable(target))
        {
            return;
        }

        var color = new Color("ffd36b");
        Cue(CombatEffectVisual.Mark.Revive, target.Position, color, 0.78f);
        target.FlashFocusHighlight(new Color("ffe79a"), 0.78f);
        ShowFloatingText(target.Position, "REVIVED", new Color("fff0b0"));
    }

    public void PlayBlessing(Vector2 center, float radius)
    {
        var color = new Color("f5d477");
        Cue(CombatEffectVisual.Mark.Sigil, center, color, 0.72f);
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

        CombatEffectVisual.Mark? mark = label switch
        {
            "ASLEEP" => CombatEffectVisual.Mark.Sleep,
            "ROOTED" => CombatEffectVisual.Mark.Bind,
            "GUARD" or "PROTECTED" => CombatEffectVisual.Mark.Shield,
            "TAUNTED" => CombatEffectVisual.Mark.Alert,
            "BLESSED" => CombatEffectVisual.Mark.Sigil,
            _ => null,
        };
        if (mark.HasValue) Cue(mark.Value, target.Position, color);
        target.FlashFocusHighlight(color, 0.5f);
        ShowFloatingText(target.Position, label, color.Lightened(0.25f));
    }

    public void PlayArea(Vector2 center, float radius, Color color, string label = "", string actionId = "")
    {
        if (actionId is "taunt" or "protection")
        {
            PlayRing(center, color, radius, 0.48f);
            return;
        }
        var effect = actionId switch
        {
            "fireball" => "fireball",
            "sleep" => "sleep",
            "web" => "web",
            "lightning-bolt" => "lightning",
            "rain-of-arrows" => "arrow",
            "teleport" => "teleport",
            "blessing" => "holy_aura",
            "protection" or "taunt" => "guard_aura",
            _ => label switch
            {
                "FIREBALL" => "fireball",
                "SLEEP" => "sleep",
                "WEB" => "web",
                "LIGHTNING" => "lightning",
                "RAIN OF ARROWS" => "arrow",
                "TELEPORT" => "teleport",
                "BLESSING" => "holy_aura",
                _ => "guard_aura",
            },
        };
        PlaySpriteEffect(effect, center, Colors.White, Mathf.Clamp(radius / 32.0f, 0.8f, 4.0f));
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

        if (label == "GUARD")
        {
            PlaySpriteEffect("guard_aura", caster.Position, Colors.White, 0.76f);
            Cue(CombatEffectVisual.Mark.Shield, caster.Position, color);
        }
        caster.FlashFocusHighlight(color, 0.6f);
        ShowFloatingText(caster.Position, label, color.Lightened(0.25f));
    }

    public void PlayDefend(Unit actor)
    {
        PlayBuff(actor, new Color("70b7ff"), "GUARD");
    }

    private void PlayProjectile(Vector2 from, Vector2 to, Color color, string effect, System.Action onImpact)
    {
        var visual = CreateVisual(CombatEffectVisual.EffectKind.Projectile, from, color);
        visual.Direction = (to - from).Normalized();
        AddAnimatedEffect(visual, effect, Colors.White, 1.0f, visual.Direction.Angle() + Mathf.Pi / 4.0f);
        visual.QueueRedraw();

        var distance = from.DistanceTo(to);
        var duration = ScaleDuration(Mathf.Clamp(distance / 720.0f, 0.16f, 0.46f));
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
        AddAnimatedEffect(visual, "impact", color, scale);
        AnimateAndFree(visual, 0.38f, 1.35f);
    }

    private void PlaySpriteEffect(string effect, Vector2 position, Color color, float scale = 1.0f)
    {
        var visual = CreateVisual(CombatEffectVisual.EffectKind.SpriteOnly, position, Colors.White);
        visual.Scale = Vector2.One * scale;
        AddAnimatedEffect(visual, effect, color);
    }

    private void AddAnimatedEffect(
        CombatEffectVisual visual,
        string effect,
        Color color,
        float scale = 1.0f,
        float rotation = 0.0f)
    {
        var frames = GetEffectFrames(effect);
        if (frames == null)
        {
            visual.QueueFree();
            return;
        }

        var sprite = new AnimatedSprite2D
        {
            SpriteFrames = frames,
            Animation = "default",
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            Modulate = color,
            Scale = Vector2.One * scale,
            Rotation = rotation,
            ZIndex = 1,
        };
        visual.AddChild(sprite);
        sprite.AnimationFinished += visual.QueueFree;
        sprite.SpeedScale = EffectAnimationDuration / ScaleDuration(EffectAnimationDuration);
        sprite.Play("default");
    }

    private SpriteFrames GetEffectFrames(string effect)
    {
        if (_effectFrames.TryGetValue(effect, out var frames))
        {
            return frames;
        }

        var path = $"res://assets/vfx/combat/{effect}.png";
        var atlas = GD.Load<Texture2D>(path);
        if (atlas == null || atlas.GetSize() != new Vector2(256, 256))
        {
            GD.PushError($"Invalid combat VFX sheet at '{path}': expected a 256x256, 4x4 sheet.");
            return null;
        }

        frames = new SpriteFrames();
        frames.Clear("default");
        frames.SetAnimationSpeed("default", 24.0f);
        frames.SetAnimationLoopMode("default", SpriteFrames.LoopMode.None);
        for (var row = 0; row < 4; row++)
        {
            for (var column = 0; column < 4; column++)
            {
                frames.AddFrame("default", new AtlasTexture
                {
                    Atlas = atlas,
                    Region = new Rect2(column * 64, row * 64, 64, 64),
                });
            }
        }

        _effectFrames.Add(effect, frames);
        return frames;
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
        var duration = ScaleDuration(0.62f);
        tween.TweenProperty(label, "position:y", label.Position.Y - PopupRise, duration);
        tween.Parallel().TweenProperty(label, "modulate:a", 0.0f, duration).SetDelay(ScaleDuration(0.22f));
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
        duration = ScaleDuration(duration);
        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Quad);
        tween.SetEase(Tween.EaseType.Out);
        tween.TweenMethod(Callable.From<float>(value => visual.Progress = value), 0.0f, 1.0f, duration);
        tween.Parallel().TweenProperty(visual, "scale", visual.Scale * endScale, duration);
        tween.TweenCallback(Callable.From(visual.QueueFree));
    }

    private float ScaleDuration(float duration)
    {
        return _gameOptionsManager.ScaleCombatDuration(duration);
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
        Ring,
        SpriteOnly,
        HealGather,
        HealPulse,
        FireFront,
        ActionMark
    }

    public EffectKind Kind { get; set; }
    public Color EffectColor { get; set; } = Colors.White;
    public Vector2 Direction { get; set; } = Vector2.Right;
    public float Radius { get; set; } = 32.0f;
    public Vector2[] Cells { get; set; }

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
            case EffectKind.ActionMark: DrawActionMark(); break;
            case EffectKind.FireFront:
                foreach (var cell in Cells)
                    DrawRect(new Rect2(cell - Position - Vector2.One * Radius, Vector2.One * Radius * 2), Colors.White);
                break;
            case EffectKind.HealGather:
            case EffectKind.HealPulse:
                var gather = Kind == EffectKind.HealGather;
                var opacity = gather ? alpha : Mathf.Clamp(alpha * 3, 0, 1);
                var mint = new Color(EffectColor, opacity);
                var white = new Color(Colors.White, opacity);
                var outline = new Color(new Color("16685e"), opacity);
                for (var i = 0; i < 7; i++)
                {
                    var angle = Mathf.Tau * i / 7;
                    var p = gather ? Vector2.Right.Rotated(angle) * (12 * alpha)
                        : new Vector2(Mathf.Cos(angle) * (26 - 5 * _progress),
                            20 - _progress * 45 + 4 * Mathf.Sin(angle));
                    DrawRect(new Rect2(p.Round(), gather ? new Vector2(3, 4) : new Vector2(5, 6)),
                        i % 2 == 0 ? mint : white);
                }
                var glint = new Vector2(0, gather ? 0 : 13 - 30 * _progress).Round();
                var arm = gather ? 2 : 10;
                var halfWidth = gather ? 1 : 3;
                var bar = new Rect2(glint - new Vector2(arm, halfWidth), new Vector2(2 * arm + 1, 2 * halfWidth + 1));
                var stem = new Rect2(glint - new Vector2(halfWidth, arm), new Vector2(bar.Size.Y, bar.Size.X));
                if (!gather)
                {
                    DrawRect(bar.Grow(2), outline);
                    DrawRect(stem.Grow(2), outline);
                }
                DrawRect(bar, mint);
                DrawRect(stem, mint);
                DrawRect(bar.Grow(-1), white);
                DrawRect(stem.Grow(-1), white);
                break;
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
            case EffectKind.SpriteOnly:
                break;
        }
    }
}