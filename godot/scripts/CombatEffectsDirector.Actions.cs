using Godot;

public partial class CombatEffectsDirector
{
    private void Cue(CombatEffectVisual.Mark mark, Vector2 at, Color color, float duration = 0.55f,
        float scale = 1)
    {
        var v = CreateVisual(CombatEffectVisual.EffectKind.ActionMark, at, color);
        v.ActionMark = mark;
        v.Scale = Vector2.One * scale;
        AnimateAndFree(v, duration, 1);
    }

    public void PlayLightning(Vector2 origin, Vector2[] path)
    {
        if (path.Length == 0) return;
        var v = CreateVisual(CombatEffectVisual.EffectKind.ActionMark, origin, new Color("91deff"));
        v.ActionMark = CombatEffectVisual.Mark.Bolt;
        v.Cells = path;
        AnimateAndFree(v, 0.45f, 1);
    }

    public void PlayField(string actionId, Vector2[] cells)
    {
        for (var i = 0; i < cells.Length; i++)
        {
            var p = cells[i];
            switch (actionId)
            {
                case "rain-of-arrows":
                    CreateTween().TweenCallback(Callable.From(() => Cue(CombatEffectVisual.Mark.ArrowFall,
                        p, new Color("ebcb93"), 0.42f))).SetDelay(ScaleDuration((i * 7 % 13) * 0.025f));
                    break;
                case "web": Cue(CombatEffectVisual.Mark.Web, p, new Color("cce5ef")); break;
                case "sleep": Cue(CombatEffectVisual.Mark.Mist, p, new Color("bd9af3")); break;
            }
        }
    }

    public void PlayTeleport(Vector2 from, Vector2 to)
    {
        void Portal(Vector2 p, bool arrival)
        {
            var v = CreateVisual(CombatEffectVisual.EffectKind.SpriteOnly, p, Colors.White);
            v.Scale = Vector2.One * (arrival ? 0.24f : 1);
            AddAnimatedEffect(v, "teleport", Colors.White);
            var tween = v.CreateTween();
            tween.TweenProperty(v, "scale", Vector2.One * (arrival ? 1 : 0.1f), ScaleDuration(0.26f));
            if (!arrival) tween.TweenCallback(Callable.From(v.QueueFree));
        }
        Portal(from, false);
        CreateTween().TweenCallback(Callable.From(() => Portal(to, true))).SetDelay(ScaleDuration(0.2f));
    }

    public void PlayChargeTrail(Vector2 from, Vector2 to, int steps, float travelSeconds)
    {
        for (var i = 0; i < steps; i++)
        {
            float t = (float)i / steps;
            var at = from.Lerp(to, t) + Vector2.Down * 20;
            CreateTween().TweenCallback(Callable.From(() => Cue(CombatEffectVisual.Mark.Dust,
                at, new Color("b6a294"), 0.28f))).SetDelay(travelSeconds * t);
        }
    }

    public void PlayShadowDeparture(Unit thief, Vector2 from)
    {
        var sprite = thief.GetNodeOrNull<Sprite2D>("Sprite2D");
        if (sprite?.Texture == null) return;
        var ghost = new Sprite2D { Texture = sprite.Texture, RegionEnabled = sprite.RegionEnabled,
            RegionRect = sprite.RegionRect, Position = from + sprite.Position, Scale = sprite.Scale,
            TextureFilter = TextureFilterEnum.Nearest, Modulate = new Color("a180d4b8") };
        AddChild(ghost);
        var fade = ghost.CreateTween();
        fade.TweenProperty(ghost, "modulate:a", 0, ScaleDuration(0.38f));
        fade.TweenCallback(Callable.From(ghost.QueueFree));
    }

    public void PlayPoisonTick(Vector2 at)
    {
        _audio?.PlaySoundEffect("poison");
        Cue(CombatEffectVisual.Mark.Drops, at, new Color("95e264"), 0.35f, scale: 0.7f);
    }

    public void PlayUtility(Vector2 at, bool disarm)
    {
        _audio?.PlaySoundEffect(disarm ? "disarm-trap" : "pick-lock");
        Cue(disarm ? CombatEffectVisual.Mark.Trap : CombatEffectVisual.Mark.Latch, at, new Color("ffe19c"));
    }
}

public partial class CombatEffectVisual
{
    public enum Mark { Bolt, ArrowFall, Web, Mist, Shield, Alert, Bind, Sleep, Sigil, Cleanse,
        Revive, Drops, Dust, Pin, ArcaneHit, Trap, Latch }
    public Mark ActionMark { get; set; }

    private void DrawActionMark()
    {
        var t = Progress;
        var c = new Color(EffectColor, Mathf.Clamp((1-t)*2.5f,0,1));
        var light = new Color(Colors.White, c.A);
        var up = Vector2.Up;
        var right = Vector2.Right;
        void Line(Vector2 a, Vector2 b, float width = 3) => DrawLine(a,b,c,width);
        void Cross(Vector2 p, float s)
        {
            Line(p-right*s,p+right*s,2); Line(p+up*s,p-up*s,2);
        }
        switch (ActionMark)
        {
            case Mark.Bolt:
                var start = Vector2.Zero;
                foreach (var endpoint in Cells)
                {
                    var end = endpoint - Position;
                    var side = (end-start).Normalized().Orthogonal();
                    var a = start;
                    for (int j=1;j<=4;j++)
                    {
                        var b = start.Lerp(end,j/4f) + (j==4 ? Vector2.Zero :
                            side * (j%2==0 ? -1 : 1) * (4+3*Mathf.Sin(20*t+j)));
                        Line(a,b,7); DrawLine(a,b,light,2); a=b;
                    }
                    start=end;
                }
                break;
            case Mark.ArrowFall:
            case Mark.Pin:
                var pin = ActionMark == Mark.Pin;
                var tip = pin ? new Vector2(0,20) : new Vector2(13,-39)*(1-Mathf.Min(1,t*2))+new Vector2(0,10);
                var tail = tip + new Vector2(8,-18);
                Line(tail,tip); Line(tip,tip+new Vector2(0,-6),2); Line(tip,tip+new Vector2(5,-4),2);
                Line(tail,tail+new Vector2(-2,5),5);
                break;
            case Mark.Web:
            case Mark.Bind:
                var feet = ActionMark==Mark.Bind ? new Vector2(0,18) : Vector2.Zero;
                var span = (ActionMark==Mark.Bind ? 16 : 31)*Mathf.Min(1,0.4f+t*2);
                for(int i=0;i<8;i++){
                    var ray=right.Rotated(i*Mathf.Tau/8);
                    var next=ray.Rotated(Mathf.Tau/8);
                    var axis=new Vector2(span,span*(ActionMark==Mark.Bind?0.42f:0.76f));
                    Line(feet,feet+ray*axis,2);
                    foreach(float r in new[]{0.42f,0.77f}) Line(feet+ray*axis*r,feet+next*axis*r,2);
                }
                break;
            case Mark.Mist:
                for(int i=0;i<3;i++) DrawArc(new Vector2(-12+i*12,9-6*t),8+5*t,Mathf.Pi,Mathf.Tau,10,new Color(c,c.A*0.55f),4);
                break;
            case Mark.Sleep:
                var z= new Vector2(4,-16-12*t);
                Line(z,z+right*10);Line(z+right*10,z+Vector2.Down*9);Line(z+Vector2.Down*9,z+new Vector2(10,9));
                break;
            case Mark.Shield:
                Vector2[] rim={new(-15,-14),new(0,-19),new(15,-14),new(12,4),new(0,16),new(-12,4),new(-15,-14)};
                DrawColoredPolygon(rim[..^1],new Color(c,c.A*0.26f));DrawPolyline(rim,c,3);break;
            case Mark.Alert:
                var top = up*(22+6*t);
                Line(top,top+Vector2.Down*12,5);DrawCircle(top+Vector2.Down*18,3,c);break;
            case Mark.Sigil:
                var s=up*(24-11*t);Line(s+up*10,s+right*7);Line(s+right*7,s-up*10);Line(s-up*10,s-right*7);Line(s-right*7,s+up*10);Cross(s,3);break;
            case Mark.Revive:
                for(int i=-1;i<=1;i++)DrawRect(new Rect2(i*7-2,22-63*t,4,63*t),new Color(c,c.A*(i==0?0.75f:0.35f)));
                DrawArc(up*(18+12*t),13,0,Mathf.Tau,20,c,3);break;
            case Mark.Cleanse:
                Line(new Vector2(-19,18-38*t),new Vector2(19,18-38*t));
                for(int i=0;i<6;i++) DrawRect(new Rect2(-15+i*6,13-42*t+6*Mathf.Sin(i),4,4),new Color("796385",c.A));break;
            case Mark.Drops:
            case Mark.Dust:
                for(int i=0;i<6;i++){
                    var p=new Vector2((i-2.5f)*(3+4*t),ActionMark==Mark.Dust ? -12*t : -12*Mathf.Sin(t*Mathf.Pi)+t*15+i%2*5);
                    DrawCircle(p,ActionMark==Mark.Dust?4:3,c);
                }break;
            case Mark.ArcaneHit:
                for(int i=0;i<6;i++){var ray=right.Rotated(i*Mathf.Tau/6);Line(ray*(3+12*t),ray*(10+14*t),2);}break;
            case Mark.Latch:
                DrawRect(new Rect2(-7,-2,14,10),c); DrawArc(new Vector2(4*t,-2-5*t),6,Mathf.Pi,Mathf.Tau,12,c,3);Cross(new Vector2(12,-10),4);break;
            case Mark.Trap:
                Line(new Vector2(-15,7),new Vector2(-3-8*t,-6));Line(new Vector2(15,7),new Vector2(3+8*t,-6));Cross(up*8,4);break;
        }
    }
}
